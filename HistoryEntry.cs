using System;
using System.Numerics;

namespace HousingHistory;

public enum HistoryAction
{
    Placed,
    Removed,
    Moved,
}

/// <summary>One row in the edit-history log.</summary>
public readonly record struct HistoryEntry(
    DateTime Time,
    HistoryAction Action,
    int ObjectIndex,
    uint FurnitureId,
    string ItemName,
    Vector3 Position,        // Placed/Removed: the item's location. Moved: the NEW location.
    float Rotation,          // radians. Moved: the NEW rotation.
    Vector3? FromPosition,   // Moved only: the previous location.
    float FromRotation,      // Moved only: the previous rotation (radians).
    ushort TerritoryId);
