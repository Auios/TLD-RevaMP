# FSR 3.1 shader bundle

`src/TLDRevamp/Assets/Fsr3Upscaler.bundle` (embedded in `TLDRevamp.dll`, used by `Fixes/FsrNativeAa.cs`) holds
AMD FidelityFX FSR 3.1's upscaler compute shaders, compiled for the game's Unity. This folder is the Unity project
that builds it.

- `Packages/fidelityfx.fsr/`: the shaders from [FSR3Unity](https://github.com/ndepoel/FSR3Unity) release
  `ppv2-3.4.0d-fsr3.1` (MIT, Nico de Poel; the shader code inside is AMD's FidelityFX SDK, MIT). The FSR 2 shaders
  are left out. The matching C# runtime is in `src/TLDRevamp/Fsr3Unity/`.
- `Assets/Editor/BuildFsrBundle.cs`: builds `Out/fsr3upscaler.bundle` for 64-bit Windows, Direct3D 11 and 12,
  LZ4-compressed and deterministic (the same sources give the same file).

## Rebuilding

Needs Unity **2022.3.12f1**, the game's version (a bundle only loads in the Unity version it was built with), with a
Unity licence (a free Personal one works). Copy this folder somewhere outside the repository (Unity adds `Library/`,
`ProjectSettings/` and more to it), then:

```
Unity.exe -batchmode -quit -nographics -projectPath <copy> -executeMethod BuildFsrBundle.Build -logFile <copy>/build.log
```

`build.log` ends with `FSRBUNDLE built, 11 shaders: ...`. Copy `<copy>/Out/fsr3upscaler.bundle` to
`src/TLDRevamp/Assets/Fsr3Upscaler.bundle`. `tools/privacy_scan.py` unpacks it and checks the strings inside.
