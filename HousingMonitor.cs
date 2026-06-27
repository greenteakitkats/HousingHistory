using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace HousingHistory;

/// <summary>
/// Polls the indoor furniture set each tick and logs additions/removals by diffing
/// against the previous snapshot. Purely read-only — never writes to game memory.
/// </summary>
public sealed class HousingMonitor : IDisposable
{
    private readonly Plugin plugin;
    private readonly List<HistoryEntry> entries = new();

    // itemId -> count, so duplicate furnishings (two identical chairs) diff correctly.
    private Dictionary<uint, int> baseline = new();
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
        // Changing zones invalidates the baseline; it will reseed silently on the next read.
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
            // Not in a readable house (e.g. left the building) — drop baseline so re-entry reseeds.
            haveBaseline = false;
            return;
        }

        if (!haveBaseline)
        {
            // First snapshot after entering: seed silently, do NOT log the existing layout.
            baseline = current;
            haveBaseline = true;

            // Sanity check for first compile/run: confirms GetFurnitureManager()/FurnitureMemory
            // resolved and we can actually read the room. Shows up in /xllog.
            var total = 0;
            foreach (var n in current.Values) total += n;
            Plugin.Log.Information(
                $"Baseline seeded: {current.Count} furnishing type(s), {total} item(s).");
            return;
        }

        DiffAndLog(baseline, current);
        baseline = current;
    }

    private void DiffAndLog(Dictionary<uint, int> oldSet, Dictionary<uint, int> newSet)
    {
        // Count increased -> placed.
        foreach (var (id, newCount) in newSet)
        {
            oldSet.TryGetValue(id, out var oldCount);
            for (var i = 0; i < newCount - oldCount; i++)
                Add(HistoryAction.Placed, id);
        }

        // Count decreased -> removed.
        foreach (var (id, oldCount) in oldSet)
        {
            newSet.TryGetValue(id, out var newCount);
            for (var i = 0; i < oldCount - newCount; i++)
                Add(HistoryAction.Removed, id);
        }
    }

    private void Add(HistoryAction action, uint id)
    {
        entries.Insert(0, new HistoryEntry(
            DateTime.Now,
            action,
            id,
            NameResolver.Resolve(id),
            Plugin.ClientState.TerritoryType));

        var max = Math.Max(10, plugin.Configuration.MaxEntries);
        if (entries.Count > max)
            entries.RemoveRange(max, entries.Count - max);

        Plugin.Log.Debug($"{action}: {NameResolver.Resolve(id)} (#{id})");
    }

    /// <summary>
    /// Reads the current indoor furniture set as a multiset of furnishing ids.
    /// Returns null when there's nothing readable (not inside a house).
    /// </summary>
    private unsafe Dictionary<uint, int>? ReadCurrentFurniture()
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

        var counts = new Dictionary<uint, int>();

        // FurnitureMemory is the generated accessor for the `_furnitureMemory` fixed array (length 1462).
        // Index 1461 is the temporary/preview object while dragging, so we skip the last slot.
        var span = furnitureManager->FurnitureMemory;
        for (var i = 0; i < span.Length - 1; i++)
        {
            var id = span[i].Id;
            if (id == 0)
                continue; // empty slot

            counts[id] = counts.GetValueOrDefault(id) + 1;
        }

        return counts;
    }
}
