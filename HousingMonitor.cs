using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace HousingHistory;

/// <summary>
/// Polls the indoor furniture set each tick and logs placements, removals, and moves
/// by diffing against the previous snapshot. Purely read-only — never writes to game memory.
/// </summary>
public sealed class HousingMonitor : IDisposable
{
    // A single placed object's state at snapshot time.
    private readonly record struct FurnitureRecord(uint Id, Vector3 Position, float Rotation, byte Stain);

    private const float PositionEpsilon = 0.01f; // yalms; ignore sub-threshold jitter
    private const float RotationEpsilon = 0.01f; // radians
    private const double MoveCoalesceSeconds = 5.0;

    private readonly Plugin plugin;
    private readonly List<HistoryEntry> entries = new();

    // Keyed by HousingFurniture.Index (a stable per-object id), so we can follow each
    // physical item's position across snapshots — and duplicate furnishings stay distinct.
    private Dictionary<int, FurnitureRecord> baseline = new();
    private bool haveBaseline;
    private DateTime lastPoll = DateTime.MinValue;

    public IReadOnlyList<HistoryEntry> Entries => entries;

    public HousingMonitor(Plugin plugin)
    {
        this.plugin = plugin;
        Plugin.Framework.Update += OnUpdate;
        Plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
    }

    public void Dispose()
    {
        Plugin.Framework.Update -= OnUpdate;
        Plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
    }

    public void Clear() => entries.Clear();

    private void OnTerritoryChanged(ushort territory)
    {
        // Changing zones invalidates the baseline; it reseeds silently on the next read.
        haveBaseline = false;
        baseline.Clear();
    }

    private void OnUpdate(IFramework framework)
    {
        var interval = Math.Max(0.25f, plugin.Configuration.PollIntervalSeconds);
        if ((DateTime.UtcNow - lastPoll).TotalSeconds < interval)
            return;
        lastPoll = DateTime.UtcNow;

        var current = ReadCurrentFurniture();
        if (current == null)
        {
            // Not in a readable house — drop baseline so re-entry reseeds.
            haveBaseline = false;
            return;
        }

        if (!haveBaseline)
        {
            // First snapshot after entering: seed silently, do NOT log the existing layout.
            baseline = current;
            haveBaseline = true;

            // Sanity check for first compile/run: confirms the housing reads resolved. See /xllog.
            Plugin.Log.Information($"Baseline seeded: {current.Count} item(s).");
            return;
        }

        DiffAndLog(baseline, current);
        baseline = current;
    }

    private void DiffAndLog(Dictionary<int, FurnitureRecord> oldSet, Dictionary<int, FurnitureRecord> newSet)
    {
        foreach (var (index, now) in newSet)
        {
            if (!oldSet.TryGetValue(index, out var before))
            {
                LogPlaced(index, now);
            }
            else if (before.Id != now.Id)
            {
                // The game reused this object slot for a different furnishing.
                LogRemoved(index, before);
                LogPlaced(index, now);
            }
            else if (HasMoved(before, now))
            {
                LogMoved(index, before, now);
            }
        }

        foreach (var (index, before) in oldSet)
        {
            if (!newSet.ContainsKey(index))
                LogRemoved(index, before);
        }
    }

    private static bool HasMoved(FurnitureRecord a, FurnitureRecord b)
        => Vector3.DistanceSquared(a.Position, b.Position) > PositionEpsilon * PositionEpsilon
        || MathF.Abs(a.Rotation - b.Rotation) > RotationEpsilon;

    private void LogPlaced(int index, FurnitureRecord r)
        => AddEntry(new HistoryEntry(DateTime.Now, HistoryAction.Placed, index, r.Id,
            NameResolver.Resolve(r.Id), r.Position, r.Rotation, null, 0f,
            Plugin.ClientState.TerritoryType));

    private void LogRemoved(int index, FurnitureRecord r)
        => AddEntry(new HistoryEntry(DateTime.Now, HistoryAction.Removed, index, r.Id,
            NameResolver.Resolve(r.Id), r.Position, r.Rotation, null, 0f,
            Plugin.ClientState.TerritoryType));

    private void LogMoved(int index, FurnitureRecord before, FurnitureRecord now)
    {
        // Coalesce a drag (many small updates) into a single row: keep the original
        // "from", just refresh the "to". Makes "move it back" show the net change.
        if (entries.Count > 0)
        {
            var top = entries[0];
            if (top.Action == HistoryAction.Moved
                && top.ObjectIndex == index
                && (DateTime.Now - top.Time).TotalSeconds < MoveCoalesceSeconds)
            {
                entries[0] = top with { Time = DateTime.Now, Position = now.Position, Rotation = now.Rotation };
                return;
            }
        }

        AddEntry(new HistoryEntry(DateTime.Now, HistoryAction.Moved, index, now.Id,
            NameResolver.Resolve(now.Id), now.Position, now.Rotation,
            before.Position, before.Rotation, Plugin.ClientState.TerritoryType));
    }

    private void AddEntry(HistoryEntry entry)
    {
        entries.Insert(0, entry);

        var max = Math.Max(10, plugin.Configuration.MaxEntries);
        if (entries.Count > max)
            entries.RemoveRange(max, entries.Count - max);

        Plugin.Log.Debug($"{entry.Action}: {entry.ItemName} (#{entry.FurnitureId}) @ {entry.Position}");
    }

    /// <summary>
    /// Reads the current indoor furniture as a map of object-index -> state.
    /// Returns null when there's nothing readable (not inside a house).
    /// </summary>
    private unsafe Dictionary<int, FurnitureRecord>? ReadCurrentFurniture()
    {
        var manager = HousingManager.Instance();
        if (manager == null)
            return null;

        // v1 scope: indoor only. Outdoor/ward reads cover many plots and behave differently.
        if (!manager->IsInside())
            return null;

        var furnitureManager = manager->GetFurnitureManager();
        if (furnitureManager == null)
            return null;

        var map = new Dictionary<int, FurnitureRecord>();

        // FurnitureMemory is the generated accessor for `_furnitureMemory` (length 1462).
        // Index 1461 is the temporary/preview object while dragging, so skip the last slot.
        var span = furnitureManager->FurnitureMemory;
        for (var i = 0; i < span.Length - 1; i++)
        {
            ref var f = ref span[i];
            if (f.Id == 0)
                continue; // empty slot

            var pos = new Vector3(f.Position.X, f.Position.Y, f.Position.Z);
            map[f.Index] = new FurnitureRecord(f.Id, pos, f.Rotation, f.Stain);
        }

        return map;
    }
}
