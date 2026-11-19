# Test mods

| Mod | What it is for | Uses the API |
| --- | --- | --- |
| [ApiProbe](ApiProbe/README.md) | A small test mod for state records, the far store and session settings (`tools/modapi2.py`; not shipped) | yes |

## The example mods

The example mods that show how other BepInEx mods work with TLD RevaMP's multiplayer (Head Unit, TV Browser, Car
Trailer, Trailer Hitches and Road End) are in [TLD-Mods](https://github.com/TLD-RevaMP/TLD-Mods), each in its own
folder under `mods/`, built and released there. How the multiplayer mod API works, its limits, and what a mod gets
without it: [docs/MODDING-MULTIPLAYER.md](../../docs/MODDING-MULTIPLAYER.md).

The test tools build and deploy them when a test names them: `TLD_TEST_MODS=CarTrailer,HeadUnit,TrailerHitches,TvBrowser,RoadEnd`
builds them from a TLD-Mods checkout (`TLD_MODS_DIR`, by default `TLD-Mods` next to this repository) against this
tree's `TLDRevamp.Multiplayer` (see [tools/README.md](../../tools/README.md)). The matching scripts under `tools/`
exercise the mods in the game.

## Building ApiProbe

Same requirements as RevaMP itself (.NET SDK 8+, the game with BepInEx 5.4.23, `tools/libs.sh` run once so `lib/`
has the reference assemblies). Then, from the repository root:

```sh
dotnet build src/Mods/ApiProbe/ApiProbe.csproj -c Release
```
