using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Reads source and metadata only. Loading game code into the runtime would run the wrong framework and can invoke Unity.
static class GameLookupsTest
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    static readonly string[] Projects = { "TLDRevamp", "TLDRevamp.Dev", "TLDRevamp.Preloader", "TLDLoaderShim" };
    static int failures, lookups, patches, dynamic, unavailable, optional, injected;
    static readonly HashSet<string> Optional = File.ReadLines("tools/unit/GameLookupsOptional.tsv").Where(l => l.Length > 0 && !l.StartsWith("#")).Select(l => string.Join(".", l.Split('\t').Take(2))).ToHashSet();
    static MetadataLoadContext context;
    static readonly List<Assembly> assemblies = new();
    static readonly Dictionary<string, int> kinds = new();
    static string Tokens(SyntaxNode node) => string.Concat(node.DescendantTokens().Select(t => t.Text));
    static string Site(SyntaxNode node) => node.SyntaxTree.FilePath.Replace('\\', '/') + ":" + (node.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
    static void Fail(string message) { failures++; Console.WriteLine("FAIL " + message); }
    static bool Source(string p) => !p.Replace('\\', '/').Contains("/obj/") && !p.Replace('\\', '/').Contains("/bin/");
    static bool Registered(InvocationExpressionSyntax call) => call.Expression is MemberAccessExpressionSyntax m && m.Expression.ToString() == "GameLookups";
    static string Name(InvocationExpressionSyntax call) => call.Expression switch { MemberAccessExpressionSyntax m => m.Name.Identifier.Text, MemberBindingExpressionSyntax m => m.Name.Identifier.Text, SimpleNameSyntax m => m.Identifier.Text, _ => "" };
    static bool Raw(InvocationExpressionSyntax call)
    {
        string name = Name(call);
        if (call.ArgumentList.Arguments.Count == 0) return false; // StackFrame.GetMethod has no name argument.
        return new[] { "GetField", "GetMethod", "GetProperty", "GetType" }.Contains(name)
            || (new[] { "Field", "Method", "Property", "PropertyGetter", "PropertySetter", "Constructor", "Inner", "TypeByName", "FieldRefAccess", "StaticFieldRefAccess" }.Contains(name) && !Registered(call));
    }
    static Dictionary<string, string> Allowlist()
    {
        var result = new Dictionary<string, string>();
        foreach (string line in File.ReadLines("tools/unit/GameLookupsAllowlist.tsv"))
        {
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var columns = line.Split('\t');
            if (columns.Length != 3 || string.IsNullOrWhiteSpace(columns[2])) { Fail("allowlist entry needs a reason: " + line); continue; }
            string key = columns[0] + "|" + Tokens(SyntaxFactory.ParseExpression(columns[1]));
            if (!result.TryAdd(key, columns[2])) Fail("duplicate allowlist entry: " + key);
        }
        return result;
    }
    static void Guard(SyntaxTree[] trees)
    {
        var allowed = Allowlist();
        var used = new HashSet<string>();
        int raw = 0, registered = 0, runtimeSites = 0;
        foreach (var tree in trees)
        {
            if (tree.FilePath.EndsWith("/GameLookups.cs")) continue;
            foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (Raw(call))
                {
                    string path = tree.FilePath.Replace('\\', '/');
                    string key = path + "|" + Tokens(call);
                    raw++;
                    if (!allowed.ContainsKey(key)) Fail("unregistered lookup at " + Site(call) + ": " + call);
                    else used.Add(key);
                }
                if (!Registered(call)) continue;
                if (new[] { "Json", "Report", "PatchApplied" }.Contains(Name(call))) continue;
                if (Name(call).StartsWith("Runtime"))
                {
                    string key = tree.FilePath + "|" + Tokens(call);
                    runtimeSites++;
                    if (!allowed.ContainsKey(key)) Fail("unlisted dynamic lookup: " + Site(call));
                    else used.Add(key);
                    continue;
                }
                registered++;
                if (Name(call) == "OptionalField" && call.ArgumentList.Arguments[0].Expression is TypeOfExpressionSyntax owner && call.ArgumentList.Arguments[1].Expression is LiteralExpressionSyntax member && !Optional.Contains(owner.Type + "." + member.Token.ValueText))
                    Fail("undocumented optional lookup: " + Site(call));
                var args = call.ArgumentList.Arguments;
                if (args.Count < 2 || args[0].Expression is not TypeOfExpressionSyntax) Fail("registry lookup needs typeof(T): " + Site(call));
                if (Name(call) != "Constructor" && args.Count >= 2 && args[1].Expression is not LiteralExpressionSyntax
                    && !(args[1].Expression is InvocationExpressionSyntax n && n.Expression.ToString() == "nameof"))
                    Fail("registry member needs a literal or nameof: " + Site(call));
            }
        }
        foreach (var key in allowed.Keys.Except(used)) Fail("stale allowlist entry: " + key);
        Console.WriteLine($"Source guard: {registered} registered sites, {runtimeSites} dynamic sites, {raw} allowlisted calls ({allowed.Count} entries)");
    }
    static Type Find(string name)
    {
        foreach (var a in assemblies) { var t = a.GetType(name, false); if (t != null) return t; }
        return null;
    }
    static string MetadataName(INamedTypeSymbol symbol) => symbol.ContainingType != null
        ? MetadataName(symbol.ContainingType) + "+" + symbol.MetadataName
        : (symbol.ContainingNamespace.IsGlobalNamespace ? "" : symbol.ContainingNamespace.ToDisplayString() + ".") + symbol.MetadataName;
    static Type Type(ITypeSymbol symbol)
    {
        if (symbol is IArrayTypeSymbol array) { var e = Type(array.ElementType); return array.Rank == 1 ? e?.MakeArrayType() : e?.MakeArrayType(array.Rank); }
        if (symbol is not INamedTypeSymbol named) return null;
        var type = Find(MetadataName(named.OriginalDefinition));
        if (type != null && named.IsGenericType && !named.IsUnboundGenericType) type = type.MakeGenericType(named.TypeArguments.Select(Type).ToArray());
        return type;
    }
    static object Value(ExpressionSyntax expression, SemanticModel model)
    {
        var value = model.GetConstantValue(expression); if (value.HasValue) return value.Value;
        var symbol = model.GetSymbolInfo(expression).Symbol;
        if (symbol is ILocalSymbol or IFieldSymbol)
        {
            var declaration = symbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() as VariableDeclaratorSyntax;
            if (declaration?.Initializer != null) return Value(declaration.Initializer.Value, model.Compilation.GetSemanticModel(declaration.SyntaxTree));
        }
        throw new InvalidOperationException("not a constant: " + expression);
    }
    static Type TypeExpression(ExpressionSyntax expression, SemanticModel model)
    {
        if (expression is TypeOfExpressionSyntax t) return Type(model.GetTypeInfo(t.Type).Type) ?? throw new InvalidOperationException("type unavailable: " + t.Type);
        if (expression is InvocationExpressionSyntax call && call.Expression is MemberAccessExpressionSyntax m && m.Name.Identifier.Text == "MakeByRefType") return TypeExpression(m.Expression, model).MakeByRefType();
        throw new InvalidOperationException("unsupported parameter type: " + expression);
    }
    static Type[] Parameters(ExpressionSyntax expression, SemanticModel model)
    {
        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.NullLiteralExpression)) return null;
        if (expression.ToString().EndsWith("Type.EmptyTypes")) return System.Type.EmptyTypes;
        var init = expression switch { ImplicitArrayCreationExpressionSyntax a => a.Initializer, ArrayCreationExpressionSyntax a => a.Initializer, _ => null };
        if (init != null) return init.Expressions.Select(e => TypeExpression(e, model)).ToArray();
        if (expression is ArrayCreationExpressionSyntax array && array.Type.RankSpecifiers.Single().Sizes.Single().ToString() == "0") return System.Type.EmptyTypes;
        throw new InvalidOperationException("unsupported parameter list: " + expression);
    }
    static MemberInfo Resolve(Type type, string kind, string name, Type[] parameters = null, BindingFlags? reflection = null)
    {
        if (type == null) return null;
        for (var t = type; t != null; t = reflection.HasValue ? null : t.BaseType)
        {
            var flags = reflection ?? All;
            if (kind is "Field" or "FieldValue" or "FieldRefAccess" or "ReflectionField" or "NamedField" or "SaveIdField" or "OptionalField") return t.GetField(name, flags) ?? (reflection.HasValue ? null : Resolve(t.BaseType, kind, name));
            if (kind == "Inner") { var inner = t.GetNestedType(name, flags); if (inner != null) return inner; continue; }
            if (kind is "Property" or "ReflectionProperty" or "PropertyGetter" or "PropertySetter")
            {
                var property = t.GetProperty(name, flags);
                if (property == null) continue;
                return kind == "PropertyGetter" ? property.GetGetMethod(true) : kind == "PropertySetter" ? property.GetSetMethod(true) : property;
            }
            IEnumerable<MethodBase> methods = kind == "Constructor" ? t.GetConstructors(flags) : t.GetMethods(flags).Where(m => m.Name == name);
            foreach (var method in methods)
                if (parameters == null || method.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters)) return method;
            if (kind == "Constructor") return null;
        }
        return null;
    }
    static void Missing(string target, bool isOptional)
    {
        if (isOptional) { optional++; Console.WriteLine("MISS optional probe: " + target); }
        else Fail("missing " + target);
    }
    static void Lookups(SyntaxTree[] trees, CSharpCompilation compilation)
    {
        foreach (var tree in trees)
        {
            if (tree.FilePath.EndsWith("/GameLookups.cs")) continue;
            var model = compilation.GetSemanticModel(tree);
            foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!Registered(call)) continue;
                string kind = Name(call);
                if (new[] { "Json", "Report", "PatchApplied" }.Contains(kind)) continue;
                if (kind.StartsWith("Runtime")) { Console.WriteLine("DYNAMIC lookup, not checked: " + Site(call) + " " + call); continue; }
                string projectKind = tree.FilePath.Split('/')[1] + " " + kind;
                kinds[projectKind] = kinds.GetValueOrDefault(projectKind) + 1;
                try
                {
                    var args = call.ArgumentList.Arguments;
                    var type = TypeExpression(args[0].Expression, model);
                    string name = kind == "Constructor" ? ".ctor" : (string)Value(args[1].Expression, model);
                    Type[] parameters = null;
                    BindingFlags? flags = null;
                    if (kind is "AssemblyType" or "TypeByName") { lookups++; if (Find(name) == null) Missing(name + " at " + Site(call), false); continue; }
                    if (kind is "NamedField" or "NamedMethod")
                    {
                        int split = name.LastIndexOf(':'); type = Find(name.Substring(0, split)); name = name.Substring(split + 1);
                        if (type == null)
                        {
                            string descriptor = (string)Value(args[1].Expression, model);
                            if (descriptor.StartsWith("UnityEngine.Rendering.PostProcessing.", StringComparison.Ordinal) && !File.Exists("lib/Unity.Postprocessing.Runtime.dll"))
                            { unavailable++; Console.WriteLine("SKIP missing lib/Unity.Postprocessing.Runtime.dll: " + descriptor + " at " + Site(call)); }
                            else Missing(descriptor + " at " + Site(call), false);
                            continue;
                        }
                        kind = kind == "NamedField" ? "ReflectionField" : "ReflectionMethod";
                        flags = args.Count > 3 ? (BindingFlags)Convert.ToInt32(Value(args[3].Expression, model)) : BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
                    }
                    else if (kind.StartsWith("Reflection")) flags = args.Count > 2 && args[2].NameColon?.Name.Identifier.Text != "runtimeType" ? (BindingFlags)Convert.ToInt32(Value(args[2].Expression, model)) : BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
                    else if (kind == "Methods") { kind = "Method"; flags = (BindingFlags)Convert.ToInt32(Value(args[2].Expression, model)); }
                    else if (kind == "Method" && args.Count > 2) parameters = Parameters(args[2].Expression, model);
                    else if (kind == "Constructor" && args.Count > 1) parameters = Parameters(args[1].Expression, model);
                    if (Name(call) == "ReflectionMethod" && args.Count > 3) parameters = Parameters(args[3].Expression, model);
                    if (kind == "GenericMethod")
                    {
                        lookups++;
                        int count = Convert.ToInt32(Value(args[2].Expression, model)); var last = TypeExpression(args[3].Expression, model);
                        if (!type.GetMethods().Any(m => m.Name == name && m.IsGenericMethodDefinition && m.GetParameters().Length == count && m.GetParameters()[count - 1].ParameterType == last)) Missing(type.FullName + "." + name + " generic method at " + Site(call), false);
                        continue;
                    }
                    if (kind == "SaveIdField")
                    {
                        foreach (var row in type.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.FieldType).Where(t => t.IsGenericType && t.GetGenericTypeDefinition().FullName == "System.Collections.Generic.List`1").Select(t => t.GenericTypeArguments[0]).Distinct())
                        {
                            lookups++;
                            if (Resolve(row, "ReflectionField", name, reflection: BindingFlags.Public | BindingFlags.Instance) == null) Missing(row.FullName + "." + name + " at " + Site(call), false);
                        }
                        continue;
                    }
                    lookups++;
                    var resolved = Resolve(type, kind, name, parameters, flags);
                    if (kind == "FieldRefAccess" && resolved is FieldInfo referenceField && call.Expression is MemberAccessExpressionSyntax access && access.Name is GenericNameSyntax generic)
                    {
                        var owner = Type(model.GetTypeInfo(generic.TypeArgumentList.Arguments[0]).Type);
                        var value = Type(model.GetTypeInfo(generic.TypeArgumentList.Arguments[1]).Type);
                        if (owner != type || value == null || (referenceField.FieldType.IsValueType ? referenceField.FieldType != value : !value.IsAssignableFrom(referenceField.FieldType)))
                            Fail("field reference type mismatch: " + Site(call));
                    }
                    if (kind == "OptionalField" && !Optional.Contains(type.FullName + "." + name)) Fail("optional probe has no documented reason: " + Site(call));
                    if (resolved == null && kind == "ReflectionField" && type.FullName == "mainscript" && Injected(name, trees))
                    { injected++; Console.WriteLine("MISS unpatched lib: mainscript." + name + " (injected by LoaderAliases.InstanceAliases) at " + Site(call)); continue; }
                    if (resolved == null) Missing(type.FullName + "." + name + (parameters == null ? "" : "(" + string.Join(",", parameters.Select(t => t.FullName)) + ")") + " (" + kind + ") at " + Site(call), kind == "OptionalField");
                }
                catch (Exception e) { Fail("lookup at " + Site(call) + ": " + e.Message); }
            }
        }
    }
    static bool Injected(string name, SyntaxTree[] trees)
    {
        var source = trees.Single(t => t.FilePath == "src/TLDRevamp.Preloader/LoaderAliases.cs");
        var field = source.GetRoot().DescendantNodes().OfType<VariableDeclaratorSyntax>().Single(v => v.Identifier.Text == "InstanceAliases");
        var rows = field.Initializer.Value.DescendantNodes().OfType<ImplicitArrayCreationExpressionSyntax>();
        return rows.Any(r => r.Initializer.Expressions.OfType<LiteralExpressionSyntax>().Select(l => l.Token.ValueText).Take(2).SequenceEqual(new[] { "mainscript", name }));
    }
    sealed class Target
    {
        internal Type Type;
        internal string Name;
        internal int Kind;
        internal Type[] Arguments;
        internal Target Copy() => (Target)MemberwiseClone();
        internal void Merge(IEnumerable<CustomAttributeData> attributes)
        {
            foreach (var a in attributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch"))
            {
                var parameters = a.Constructor.GetParameters();
                for (int i = 0; i < parameters.Length; i++)
                {
                    object value = a.ConstructorArguments[i].Value;
                    switch (parameters[i].Name)
                    {
                        case "declaringType": Type = (Type)value; break;
                        case "methodName": Name = (string)value; break;
                        case "methodType": Kind = Convert.ToInt32(value); break;
                        case "argumentTypes": Arguments = ((IEnumerable<CustomAttributeTypedArgument>)value).Select(v => (Type)v.Value).ToArray(); break;
                        case "argumentVariations":
                            var variations = ((IEnumerable<CustomAttributeTypedArgument>)value).Select(v => Convert.ToInt32(v.Value)).ToArray();
                            if (Arguments == null || Arguments.Length != variations.Length) throw new InvalidOperationException("argument variations without argument types");
                            for (int n = 0; n < variations.Length; n++) if (variations[n] is 1 or 2) Arguments[n] = Arguments[n].MakeByRefType(); else if (variations[n] == 3) Arguments[n] = Arguments[n].MakePointerType();
                            break;
                        default: throw new InvalidOperationException("unhandled HarmonyPatch argument " + parameters[i].Name);
                    }
                }
            }
        }
    }
    static string DefinitionName(MetadataReader metadata, TypeDefinitionHandle handle)
    {
        var definition = metadata.GetTypeDefinition(handle);
        var parent = definition.GetDeclaringType();
        string name = metadata.GetString(definition.Name);
        if (!parent.IsNil) return DefinitionName(metadata, parent) + "+" + name;
        string ns = metadata.GetString(definition.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }
    static bool IsPatchMethod(MethodInfo method) => new[] { "Prefix", "Postfix", "Transpiler", "Finalizer" }.Contains(method.Name)
        || method.GetCustomAttributesData().Any(a => new[] { "HarmonyLib.HarmonyPrefix", "HarmonyLib.HarmonyPostfix", "HarmonyLib.HarmonyTranspiler", "HarmonyLib.HarmonyFinalizer" }.Contains(a.AttributeType.FullName));
    static void Patches()
    {
        foreach (string project in new[] { "TLDRevamp", "TLDRevamp.Dev" })
        {
            string path = $"src/{project}/bin/Release/netstandard2.1/{project}.dll";
            if (!File.Exists(path)) { Fail("build " + project + " before the offline patch check"); continue; }
            var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(path));
            using var stream = File.OpenRead(path); using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            foreach (var handle in metadata.TypeDefinitions)
            {
                var type = assembly.GetType(DefinitionName(metadata, handle));
                if (type == null) continue;
                var attributes = type.GetCustomAttributesData();
                var methods = type.GetMethods(All);
                if (!attributes.Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch") && !methods.Any(m => m.GetCustomAttributesData().Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch"))) continue;
                if (methods.Any(m => new[] { "TargetMethod", "TargetMethods", "Prepare" }.Contains(m.Name) || m.GetCustomAttributesData().Any(a => new[] { "HarmonyLib.HarmonyTargetMethod", "HarmonyLib.HarmonyTargetMethods", "HarmonyLib.HarmonyPrepare" }.Contains(a.AttributeType.FullName))))
                { dynamic++; Console.WriteLine("DYNAMIC, not checked: " + type.FullName); continue; }
                try
                {
                    var common = new Target(); common.Merge(attributes);
                    var targets = new List<(Target, string)>();
                    var signatures = new HashSet<string>();
                    foreach (var method in methods)
                    {
                        var ma = method.GetCustomAttributesData();
                        if (!IsPatchMethod(method)) continue;
                        var target = common.Copy(); target.Merge(ma);
                        string signature = target.Type?.FullName + "|" + target.Name + "|" + target.Kind + "|" + (target.Arguments == null ? "*" : string.Join(",", target.Arguments.Select(t => t.FullName)));
                        if (signatures.Add(signature)) targets.Add((target, type.FullName + "." + method.Name));
                    }
                    if (targets.Count == 0) { Console.WriteLine("MARKER, no patch methods: " + type.FullName); continue; }
                    foreach (var (t, owner) in targets)
                    {
                        string kind = t.Kind switch { 0 => "Method", 1 => "PropertyGetter", 2 => "PropertySetter", 3 => "Constructor", 4 => "StaticConstructor", _ => throw new InvalidOperationException("unsupported MethodType " + t.Kind) };
                        patches++;
                        var found = kind == "StaticConstructor" ? t.Type?.TypeInitializer : Resolve(t.Type, kind, t.Name, t.Arguments);
                        if (found == null) Missing((t.Type?.FullName ?? "<no declaring type>") + "." + (t.Name ?? kind) + " (patch " + owner + ")", false);
                    }
                }
                catch (Exception e) { Fail("patch " + type.FullName + ": " + e.Message); }
            }
        }
    }
    class Parent { private int Hidden; }
    class Fixture : Parent { private void Real(int value) { } private int Value { get; set; } }
    [HarmonyLib.HarmonyPatch(typeof(Fixture))]
    class PatchFixture
    {
        [HarmonyLib.HarmonyPatch("Real", new[] { typeof(int) })] internal static void Valid() { }
        [HarmonyLib.HarmonyPatch("Real", new[] { typeof(string) })] internal static void WrongParameters() { }
        [HarmonyLib.HarmonyPatch("Wrong", new[] { typeof(int) })] internal static void WrongName() { }
        [HarmonyLib.HarmonyPatch("Value", HarmonyLib.MethodType.Getter)] internal static void Getter() { }
        [HarmonyLib.HarmonyPatch("Value", HarmonyLib.MethodType.Setter)] internal static void Setter() { }
    }
    static void SelfTest()
    {
        // The same resolver is used with runtime fixture types here and metadata types above.
        if (Resolve(typeof(Fixture), "Method", "WrongName") != null) Fail("self-test accepted a wrong name");
        if (Resolve(typeof(Fixture), "Method", "Real", new[] { typeof(string) }) != null) Fail("self-test accepted a wrong parameter list");
        if (Resolve(typeof(Fixture), "Method", "Real", new[] { typeof(int) }) == null || Resolve(typeof(Fixture), "Field", "Hidden") == null) Fail("self-test rejected private or inherited members");
        foreach (var method in typeof(PatchFixture).GetMethods(All))
        {
            var target = new Target(); target.Merge(typeof(PatchFixture).GetCustomAttributesData()); target.Merge(method.GetCustomAttributesData());
            string kind = target.Kind == 1 ? "PropertyGetter" : target.Kind == 2 ? "PropertySetter" : "Method";
            bool resolved = Resolve(target.Type, kind, target.Name, target.Arguments) != null;
            bool expected = method.Name is "Valid" or "Getter" or "Setter";
            if (resolved != expected) Fail("combined patch attributes: " + method.Name);
        }
        Console.WriteLine("Self-test: wrong name and wrong parameters reported missing; private and inherited members resolve");
        var bad = CSharpSyntaxTree.ParseText("class C { void M() { AccessTools.Field(typeof(Game), \"bad\"); typeof(Game).GetMethod(\"bad\"); Traverse.Create(g).Field(\"bad\"); } }");
        if (bad.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Count(Raw) != 3) Fail("source guard self-test");
    }
    static void RegistryTest()
    {
        const string path = "C:/private/build/src/TLDRevamp/Fixture.cs";
        TLDRevamp.GameLookups.Field(typeof(Fixture), "Missing", file: path, line: 10);
        TLDRevamp.GameLookups.Field(typeof(Fixture), "Missing", file: path, line: 10);
        TLDRevamp.GameLookups.Method(typeof(Fixture), "Real", new[] { typeof(int) }, file: path, line: 11);
        TLDRevamp.GameLookups.PatchApplied();
        TLDRevamp.Plugin.PatchFailures = 1; TLDRevamp.Plugin.FailedPatches.Add("ExamplePatch");
        var messages = new List<string>(); TLDRevamp.GameLookups.Report(messages.Add);
        if (messages.Count != 1 || !messages[0].StartsWith("Game lookups: 1 resolved, 1 missing; patches: 1 applied, 1 failed") || !messages[0].Contains("Patch failed: ExamplePatch")) Fail("registry startup report or deduplication");
        TLDRevamp.GameLookups.Field(typeof(Fixture), "Later", file: path, line: 12);
        TLDRevamp.GameLookups.Field(typeof(Fixture), "Later", file: path, line: 12);
        if (messages.Count != 2 || !messages[1].Contains("src/TLDRevamp/Fixture.cs:12")) Fail("registry late miss should log once");
        using var json = System.Text.Json.JsonDocument.Parse(TLDRevamp.GameLookups.Json());
        if (json.RootElement.GetProperty("resolved").GetInt32() != 1 || json.RootElement.GetProperty("missing").GetArrayLength() != 2 || json.RootElement.GetProperty("patchFailures").GetArrayLength() != 1) Fail("registry JSON totals");
        if (TLDRevamp.GameLookups.Json().Contains("private/build")) Fail("registry leaks the caller's parent path");
        try { TLDRevamp.GameLookups.FieldRefAccess<Fixture, int>(typeof(Fixture), "MissingRef", file: path, line: 13); }
        catch (MissingFieldException) { }
        if (messages.Count != 3 || !messages[2].Contains("MissingRef")) Fail("field reference miss must be recorded before Harmony throws");
        TLDRevamp.GameLookups.Field(typeof(Fixture), "Later", file: path, line: 12);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) TLDRevamp.GameLookups.Field(typeof(Fixture), "Later", file: path, line: 12);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        if (allocated > 1024 || messages.Count != 3) Fail("warm field lookup allocated or recorded again: " + allocated);
        Console.WriteLine("Registry: startup block, late misses, deduplication, JSON, caller privacy, throwing field reference PASS");
    }
    static int Main()
    {
        SelfTest();
        RegistryTest();
        var trees = Projects.SelectMany(p => Directory.GetFiles("src/" + p, "*.cs", SearchOption.AllDirectories)).Where(Source)
            .Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), new CSharpParseOptions(preprocessorSymbols: new[] { "TLD_UNITY" }), path: p.Replace('\\', '/'))).ToArray();
        Guard(trees);
        if (!File.Exists("lib/Assembly-CSharp.dll")) Console.WriteLine("SKIP: no lib/Assembly-CSharp.dll");
        else
        {
            var runtime = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator);
            var libraries = Directory.GetFiles("lib", "*.dll");
            var own = new[] { "TLDRevamp", "TLDRevamp.Multiplayer", "TLDRevamp.Dev" }.Select(p => $"src/{p}/bin/Release/netstandard2.1/{p}.dll").Where(File.Exists).ToArray();
            var paths = runtime.Concat(libraries).Concat(own).Select(Path.GetFullPath).Distinct().ToArray();
            using var loadContext = new MetadataLoadContext(new PathAssemblyResolver(paths)); context = loadContext;
            foreach (var path in paths) assemblies.Add(context.LoadFromAssemblyPath(path));
            var extra = Directory.GetFiles("src/TLDRevamp.Multiplayer", "*.cs", SearchOption.AllDirectories).Where(Source).Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(p), path: p));
            var compilation = CSharpCompilation.Create("LookupSource", trees.Concat(extra), runtime.Concat(libraries).Select(p => MetadataReference.CreateFromFile(p)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            Lookups(trees, compilation);
            Patches();
        }
        foreach (var kind in kinds.OrderBy(p => p.Key)) Console.WriteLine(kind.Key + ": " + kind.Value);
        Console.WriteLine($"Game lookups: {lookups} checked, {patches} patch targets checked, {dynamic} dynamic; {optional} missing optional probes, {injected} injected members absent from unpatched lib, {unavailable} unavailable dependency sites; {failures} failures");
        return failures == 0 ? 0 : 1;
    }
}
