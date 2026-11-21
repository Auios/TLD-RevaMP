using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class EntityInUseSourceChecks
{
    private static readonly string[] BusyFacts =
    {
        "HitchCar", "CrashReturnTo", "Held", "Stored", "WornBy", "Driven", "DrivenAt", "PartIndex",
        "LockFrame", "LockContainer", "Locked", "Mounted", "LooseSplit", "ServerWornNow"
    };

    private static MethodDeclarationSyntax Method(string file, string name) =>
        CSharpSyntaxTree.ParseText(File.ReadAllText($"src/TLDRevamp/Net/Entities{file}.cs"))
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.ValueText == name);

    private static void Check(string file, string method, int busyCalls, string exceptions = "")
    {
        var body = Method(file, method);
        var references = body.DescendantNodes().OfType<IdentifierNameSyntax>().ToArray();
        int calls = references.Count(id => id.Identifier.ValueText == "Busy" && id.Parent is InvocationExpressionSyntax);
        var raw = references.Where(id => BusyFacts.Contains(id.Identifier.ValueText)).Select(id => id.Parent)
            .Where(node => node is not MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "BusyKind" } });
        string actual = string.Join(" | ", raw.Select(node => node.Parent.ToString()));
        if (calls != busyCalls || actual != exceptions)
            throw new Exception($"{method} should call Busy {busyCalls} times and use raw fields only for the listed exceptions. Found {calls} calls and [{actual}]");
    }

    internal static void Run()
    {
        // Read the target for the refusal message and the stale driver report for the grant log.
        Check(".Pickup", "ServerClaim", 2, "\"crash lease (returns to \" + se.CrashReturnTo | " +
            "se.Driven ? \" (owner's driver report \" + (Time.realtimeSinceStartup - se.DrivenAt).ToString(\"F1\") + \" s old)\" : \"\" | Time.realtimeSinceStartup - se.DrivenAt");
        // The partner's lease is contested only when due to this claimer. The grant sets a return target.
        Check(".CrashAuthority", "ServerCrashClaim", 2, "pe.CrashReturnTo == from | se.CrashReturnTo = se.OwnerId");
        Check(".Handoff", "ServerHandoffTick", 1, "{changed.Locked} | {changed.LockFrame} | {changed.Driven}");
        // Only whole cars qualify as rabbit anchors.
        Check(".Features", "ChooseRabbitOwner", 2, "c.PartIndex < 0");
        Check(".Features", "WornTakesBack", 2);
        Check("", "ServerTakeSilent", 2);
        // Clear leases due to the leaver, then use the hitch ID to find the tow.
        Check("", "ServerOnLeave", 2, "se.CrashReturnTo == playerId | se.CrashReturnTo = -1 | tr.HitchCar");
        Check(".Attach", "CascadeOwner", 2);
        // Parent attach looks up the tow and clears the obsolete hands/inventory state after attachment.
        Check(".Attach", "ServerAttach", 0, "se.HitchCar != 0 | se.HitchCar | se.Held || se.Stored | se.Held || se.Stored | se.Held = false | se.Stored = false");
        Console.WriteLine("Owner gates use Busy; raw field exceptions match their recorded uses");
    }
}
