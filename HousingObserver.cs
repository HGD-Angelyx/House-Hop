using System;
using FFXIVClientStructs.FFXIV.Client.Game.Housing;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace HouseHop;

/// <summary>
/// Reads HousingManager each framework tick to passively record housing locations.
/// Only fires when the local player is inside a housing territory.
/// </summary>
public static class HousingObserver
{
    private static ushort _lastTerritory = 0;

    // Housing territory IDs for all five districts (outdoor + indoor)
    private static readonly ushort[] HousingTerritories =
    {
        339, 340, 341, 342, 343,       // Mist
        345, 346, 347, 348, 349,       // The Goblet
        357, 358, 359, 360, 361,       // Lavender Beds
        649, 650, 651, 652, 653,       // Shirogane
        980, 981, 982, 983, 984,       // Empyreum
        886, 909, 912, 935, 1145       // Apartment lobbies
    };

    public static unsafe void Observe(HousingStore store)
    {
        ushort territory = HouseHop.ClientState.TerritoryType;
        if (!IsHousingTerritory(territory)) return;
        if (territory == _lastTerritory) return;
        _lastTerritory = territory;

        var hm = HousingManager.Instance();
        if (hm == null) return;

        string? district = TerritoryToDistrict(territory);
        if (district == null) return;

        try
        {
            // Read ward/plot from HousingManager
            int ward = hm->GetCurrentWard() + 1;
            int plot = hm->GetCurrentPlot() + 1;

            if (ward <= 0 || plot <= 0) return;

            string ownerName = GetLocalName();

            // Apartment detection: plot 0–255 in HousingManager maps differently
            bool isApartment = hm->IsInside && plot > 100;

            var entry = new HousingEntry
            {
                OwnerName   = ownerName,
                District    = district,
                Ward        = ward,
                Plot        = isApartment ? (plot - 100) : plot,
                Room        = isApartment ? hm->GetCurrentRoom() + 1 : 0,
                IsApartment = isApartment,
                Type        = isApartment ? HousingType.Apartment : HousingType.FcHouse,
                LastSeen    = DateTime.UtcNow
            };

            store.Upsert(entry);
            HouseHop.Log.Debug($"[HouseHop] Recorded: {entry.OwnerName} @ {entry.FriendlyLocation}");
        }
        catch (Exception ex)
        {
            HouseHop.Log.Verbose(ex, "[HouseHop] HousingObserver read error (non-fatal)");
        }
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
        339 or 340 or 341 or 342 or 343 => "Mist",
        345 or 346 or 347 or 348 or 349 => "TheGoblet",
        357 or 358 or 359 or 360 or 361 => "LavenderBeds",
        649 or 650 or 651 or 652 or 653 => "Shirogane",
        980 or 981 or 982 or 983 or 984 => "Empyreum",
        886 or 909 or 912 or 935 or 1145 => "Mist", // apartment lobbies, district resolved later
        _ => null
    };
}
