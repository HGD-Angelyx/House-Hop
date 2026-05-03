using Dalamud.Game.ClientState.Objects.SubKinds;
using System;
using System.Linq;

namespace HouseHop;

public static class HousingObserver
{
    private static ushort _lastTerritory = 0;

    private static readonly ushort[] HousingTerritories =
    {
        339, 340, 341, 342, 343,
        345, 346, 347, 348, 349,
        357, 358, 359, 360, 361,
        649, 650, 651, 652, 653,
        980, 981, 982, 983, 984,
        886, 909, 912, 935, 1145
    };

    public static void Observe(HousingStore store)
    {
        ushort territory = (ushort)HouseHop.ClientState.TerritoryType;
        if (!IsHousingTerritory(territory)) return;
        if (territory == _lastTerritory) return;
        _lastTerritory = territory;

        string? district = TerritoryToDistrict(territory);
        if (district == null) return;

        string ownerName = GetLocalName();

        bool alreadyKnown = store.All.Any(e =>
            e.OwnerName.Equals(ownerName, StringComparison.OrdinalIgnoreCase)
            && e.District == district
            && e.Ward > 0);

        if (!alreadyKnown)
            HouseHop.Log.Information(
                $"[HouseHop] Entered {district} as {ownerName} — use 'Add manually' to record ward/plot.");
    }

    private static string GetLocalName()
    {
        if (HouseHop.ObjectTable[0] is IPlayerCharacter lp)
        {
            string fc = lp.CompanyTag.TextValue;
            return string.IsNullOrWhiteSpace(fc) ? lp.Name.TextValue : fc;
        }
        return "Unknown";
    }

    private static bool IsHousingTerritory(ushort id)
    {
        foreach (var t in HousingTerritories)
            if (t == id) return true;
        return false;
    }

    private static string? TerritoryToDistrict(ushort t) => t switch
    {
        339 or 340 or 341 or 342 or 343  => "Mist",
        345 or 346 or 347 or 348 or 349  => "TheGoblet",
        357 or 358 or 359 or 360 or 361  => "LavenderBeds",
        649 or 650 or 651 or 652 or 653  => "Shirogane",
        980 or 981 or 982 or 983 or 984  => "Empyreum",
        886 or 909 or 912 or 935 or 1145 => "Mist",
        _ => null
    };
}
