using System;

namespace HousingHistory;

public enum HistoryAction
{
    Placed,
    Removed,
}

/// <summary>One row in the edit-history log.</summary>
public readonly record struct HistoryEntry(
    DateTime Time,
    HistoryAction Action,
    uint FurnitureId,
    string ItemName,
    ushort TerritoryId);
