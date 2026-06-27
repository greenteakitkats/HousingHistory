# Housing History

A Dalamud plugin that keeps a timestamped, **read-only** log of furnishings you
**place** and **remove** inside your house — so when you forget what you just
changed (or deleted), you can check. Complementary to *Burning Down the House*;
it does not move or write anything, it only watches.

Open the log in-game with `/houselog`.

## How it works

- Every ~1s, on the framework thread, it snapshots the indoor furniture set via
  `HousingManager → GetFurnitureManager() → FurnitureMemory` and builds a multiset
  of furnishing ids.
- It diffs against the previous snapshot: a count increase = **Placed**, a
  decrease = **Removed**. A *move* doesn't change room membership, so it never
  produces false events.
- The first snapshot after you enter a house is **seeded silently** so your
  existing layout isn't dumped into the log. Leaving/changing zones resets the
  baseline.

## Status / things to verify

This compiles against **Dalamud.NET.Sdk 15.0.0** and current FFXIVClientStructs
field names (`HousingManager`, `HousingFurnitureManager._furnitureMemory`,
`HousingFurniture.Id`). Two things to confirm on first run:

1. **`FurnitureMemory` accessor name** — this is the source-generated public
   accessor for the internal `_furnitureMemory` fixed array. If the build can't
   find it, check the generated name in your ClientStructs version.
2. **Name resolution** — `HousingFurniture.Id` is documented as
   `(0x20000 | Id)` indoors. We look the raw id up in the `HousingFurniture`
   Excel sheet and fall back to `Furnishing #N` on a miss. If you see lots of
   `Furnishing #N`, adjust the key in `NameResolver.cs`.

## Building

> ⚠️ Dalamud is a **Windows** toolchain. You need the .NET SDK **and** the
> Dalamud dev libraries (which ship with a XIVLauncher/Dalamud install).

### On Windows (easiest)
1. Install the .NET 9 SDK and XIVLauncher (run the game once with Dalamud so the
   dev libraries exist at `%AppData%\XIVLauncher\addon\Hooks\dev`).
2. `dotnet build` in this folder.
3. In-game: `/xlsettings` → Experimental → add the built
   `HousingHistory.dll` under "Dev Plugin Locations", then load it from
   `/xlplugins` (Dev Tools).

### On your Mac (this machine)
You currently have **no .NET SDK installed**, and FFXIV+Dalamud only runs on Mac
via **XIV-on-Mac** (Wine). Options:
- **Recommended:** edit here, build/test on a Windows PC or VM.
- **All-Mac:** install the .NET SDK (`brew install dotnet-sdk`), set the
  `DALAMUD_HOME` env var to the `dev` lib folder inside your XIV-on-Mac Wine
  prefix, then `dotnet build`. Fiddlier, but works.

## Roadmap (later)
- Persist the log across sessions
- Track moves (item + old/new coordinates) — needs slot-identity reconciliation
- Per-item undo via BDTH-style position writing (leaves the read-only safe zone)
