using System.Collections.Generic;
using Lumina.Excel.Sheets;

namespace HousingHistory;

/// <summary>
/// Maps a placed-furniture id to a human-readable item name, with caching.
/// </summary>
/// <remarks>
/// VERIFY: <see cref="FFXIVClientStructs.FFXIV.Client.Game.HousingFurniture.Id"/> is documented as
/// "(0x20000 | Id) = HousingFurniture Row" indoors / "(0x30000 | Id)" outdoors. We first try the raw
/// id directly against the HousingFurniture sheet (correct for most indoor cases); if that misses we
/// fall back to showing the raw id so the log is still useful. If names come back as "Furnishing #N",
/// adjust the lookup key here.
/// </remarks>
public static class NameResolver
{
    private static readonly Dictionary<uint, string> Cache = new();

    public static string Resolve(uint furnitureId)
    {
        if (Cache.TryGetValue(furnitureId, out var cached))
            return cached;

        var name = Lookup(furnitureId);
        Cache[furnitureId] = name;
        return name;
    }

    private static string Lookup(uint furnitureId)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<HousingFurniture>();
        if (sheet.TryGetRow(furnitureId, out var row) && row.Item.IsValid)
        {
            var itemName = row.Item.Value.Name.ToString();
            if (!string.IsNullOrWhiteSpace(itemName))
                return itemName;
        }

        return $"Furnishing #{furnitureId}";
    }
}
