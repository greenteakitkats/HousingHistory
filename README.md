# Housing History

A Dalamud plugin that keeps a timestamped, **read-only** log of furnishings you
**place**, **remove**, and **move** inside your house — with coordinates — so
when you forget what you just changed (or move something and want to put it
back), you can check. Complementary to *Burning Down the House*; it does not
move or write anything, it only watches.

Open the log in-game with `/houselog`.

## How it works

- Every ~1s, on the framework thread, it snapshots the indoor furniture set via
  `HousingManager → GetFurnitureManager() → FurnitureMemory`, keyed by each
  object's stable `HousingFurniture.Index`. Per item it records id, position,
  rotation, and stain.
- It diffs against the previous snapshot:
  - index appears → **Placed** (logs coordinates)
  - index gone → **Removed** (logs last-known coordinates)
  - same index, position/rotation changed beyond a small epsilon → **Moved**
    (logs old → new). Copy the old coordinates back into BDTH to undo a move.
  - same index, different furnishing id → slot reuse → Removed + Placed
- Dragging fires many tiny updates; successive moves of the same object within
  5s **coalesce** into one row that keeps the original "from" and updates the
  "to", so you see the net move.
- The first snapshot after you enter a house is **seeded silently** so your
  existing layout isn't dumped into the log. Leaving/changing zones resets it.

## Status / things to verify

This compiles against **Dalamud.NET.Sdk 15.0.0** and current FFXIVClientStructs
field names (`HousingManager`, `HousingFurnitureManager._furnitureMemory`,
`HousingFurniture.{Id,Position,Rotation,Index,Stain}`). Confirm on first run:

1. **`FurnitureMemory` accessor name** — the source-generated accessor for the
   internal `_furnitureMemory` fixed array. If the build can't find it, check
   the generated name in your ClientStructs version.
2. **Name resolution** — `HousingFurniture.Id` is documented as `(0x20000 | Id)`
   indoors. We look the raw id up in the `HousingFurniture` Excel sheet and fall
   back to `Furnishing #N` on a miss. If you see lots of `Furnishing #N`, adjust
   the key in `NameResolver.cs`.
3. **`Index` stability** — move tracking assumes `HousingFurniture.Index` is a
   stable per-object id within a session. If moves show up as Removed+Placed
   pairs, the assumption is wrong on your version and the key needs revisiting.
4. **Rotation units** — displayed as degrees assuming `Rotation` is radians. If
   the angle looks wrong, adjust `Format()` in `MainWindow.cs`.

## Building

> ⚠️ Dalamud is a **Windows** toolchain. You need the .NET SDK **and** the
> Dalamud dev libraries (which ship with a XIVLauncher/Dalamud install).

### On Windows (easiest)
1. Install the .NET 9 SDK and XIVLauncher (run the game once with Dalamud so the
   dev libraries exist at `%AppData%\XIVLauncher\addon\Hooks\dev`).
2. `dotnet build` in this folder.
3. In-game: `/xlsettings` → Experimental → add the built `HousingHistory.dll`
   under "Dev Plugin Locations", then load it from `/xlplugins` (Dev Tools).

### On your Mac
No .NET SDK installed, and FFXIV+Dalamud only runs on Mac via **XIV-on-Mac**
(Wine). Options:
- **Recommended:** edit here, build/test on a Windows PC or VM.
- **All-Mac:** install the .NET SDK (`brew install dotnet-sdk`), set the
  `DALAMUD_HOME` env var to the `dev` lib folder inside your XIV-on-Mac Wine
  prefix, then `dotnet build`. Fiddlier, but works.

## Roadmap (later)
- Track **stain/dye** changes (data already captured — just add a `Redyed` action)
- Persist the log across sessions
- Per-item **undo** via BDTH-style position writing (leaves the read-only safe zone)
- Outdoor / ward support
