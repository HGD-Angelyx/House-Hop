using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Text.RegularExpressions;

namespace HouseHop;

/// <summary>
/// Hooks the Estate Teleportation popup (addon "HousingSignBoard") that appears
/// when you click a friend's estate in the Social list.
///
/// When the popup opens, we read the player name, estate type, and address text,
/// parse them into a HousingEntry, and show a toast notification offering to save it.
/// </summary>
public class EstateSaver : IDisposable
{
    private readonly HouseHop _plugin;
    private readonly IAddonLifecycle _addonLifecycle;

    // Pending entry waiting for user confirmation via the in-game toast/notification
    private HousingEntry? _pending;

    public EstateSaver(HouseHop plugin, IAddonLifecycle addonLifecycle)
    {
        _plugin        = plugin;
        _addonLifecycle = addonLifecycle;
        _addonLifecycle.RegisterListener(AddonEvent.PostSetup, "HousingSignBoard", OnHousingSignBoard);
    }

    public void Dispose()
    {
        _addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "HousingSignBoard", OnHousingSignBoard);
    }

    private unsafe void OnHousingSignBoard(AddonEvent eventType, AddonArgs args)
    {
        try
        {
            var addon = (AtkUnitBase*)args.Addon;
            if (addon == null) return;

            // Node layout of HousingSignBoard:
            // [3] = Player name
            // [4] = Estate type label ("Apartments", "Private Chamber", "Free Company Estate")
            // [6] = Address text  e.g. "Kobai Goten Wing 1 Room #46, 5th Ward, Shirogane"
            string playerName  = GetNodeText(addon, 3);
            string estateType  = GetNodeText(addon, 4);
            string addressText = GetNodeText(addon, 6);

            if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(addressText))
                return;

            // Parse the address string into structured data
            var entry = ParseAddress(playerName, estateType, addressText);
            if (entry == null)
            {
                HouseHop.Log.Warning($"[HouseHop] Could not parse estate address: '{addressText}'");
                return;
            }

            // Get the friend's home world from ClientState context
            // (we're on their world when this popup shows, so current territory world = their world)
            if (HouseHop.ObjectTable[0] is Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter lp)
                entry.World = lp.CurrentWorld.Value.Name.ToString();

            // Check if already saved
            bool exists = _plugin.Store.All.Exists(e =>
                e.OwnerName.Equals(entry.OwnerName, StringComparison.OrdinalIgnoreCase)
                && e.District == entry.District
                && e.Ward == entry.Ward
                && e.Plot == entry.Plot
                && e.Room == entry.Room);

            if (exists)
            {
                HouseHop.Log.Debug($"[HouseHop] Already saved: {entry.OwnerName}");
                return;
            }

            _pending = entry;

            // Show a chat message offering to save — user types /househop save to confirm
            HouseHop.ChatGui.Print(
                $"[HouseHop] Detected estate: {entry.OwnerName} — {entry.FriendlyLocation}. " +
                $"Type /househop save to save it.");

            HouseHop.Log.Information($"[HouseHop] Pending save: {entry.OwnerName} @ {entry.FriendlyLocation}");
        }
        catch (Exception ex)
        {
            HouseHop.Log.Error(ex, "[HouseHop] EstateSaver error");
        }
    }

    /// <summary>
    /// Called by the /househop save command to confirm saving the pending entry.
    /// </summary>
    public void ConfirmSave()
    {
        if (_pending == null)
        {
            HouseHop.ChatGui.Print("[HouseHop] No pending estate to save. Open a friend's estate first.");
            return;
        }

        _plugin.Store.Upsert(_pending);
        HouseHop.ChatGui.Print($"[HouseHop] Saved: {_pending.OwnerName} — {_pending.FriendlyLocation}");
        _pending = null;
    }

    // ── Address parser ────────────────────────────────────────────────────────

    /// <summary>
    /// Parses FFXIV's estate address string format into a HousingEntry.
    ///
    /// Known formats:
    ///   Apartment:        "Kobai Goten Wing 1 Room #46, 5th Ward, Shirogane"
    ///   FC/Private estate: "Plot 12, 4th Ward, The Goblet"
    ///   Private chamber:  "Room 3, Plot 12, 4th Ward, The Goblet"
    /// </summary>
    private static HousingEntry? ParseAddress(string playerName, string estateType, string address)
    {
        var entry = new HousingEntry
        {
            OwnerName = playerName,
            LastSeen  = DateTime.UtcNow
        };

        // Determine type
        entry.Type = estateType.ToLowerInvariant() switch
        {
            var s when s.Contains("apartment")       => HousingType.Apartment,
            var s when s.Contains("private")         => HousingType.PrivateChamber,
            var s when s.Contains("free company")    => HousingType.FcHouse,
            _                                        => HousingType.FcHouse
        };
        entry.IsApartment = entry.Type == HousingType.Apartment;

        // Extract district
        entry.District = ExtractDistrict(address);
        if (entry.District == null) return null;

        // Extract ward number  e.g. "5th Ward" -> 5
        var wardMatch = Regex.Match(address, @"(\d+)\w*\s+Ward", RegexOptions.IgnoreCase);
        if (!wardMatch.Success) return null;
        entry.Ward = int.Parse(wardMatch.Groups[1].Value);

        if (entry.Type == HousingType.Apartment)
        {
            // "Kobai Goten Wing 1 Room #46"  ->  plot=1, room=46
            var aptMatch = Regex.Match(address, @"Wing\s+(\d+)\s+Room\s+#?(\d+)", RegexOptions.IgnoreCase);
            if (!aptMatch.Success) return null;
            entry.Plot = int.Parse(aptMatch.Groups[1].Value);
            entry.Room = int.Parse(aptMatch.Groups[2].Value);
        }
        else if (entry.Type == HousingType.PrivateChamber)
        {
            // "Room 3, Plot 12, 4th Ward, The Goblet"
            var chamberMatch = Regex.Match(address, @"Room\s+(\d+),\s*Plot\s+(\d+)", RegexOptions.IgnoreCase);
            if (!chamberMatch.Success) return null;
            entry.Room = int.Parse(chamberMatch.Groups[1].Value);
            entry.Plot = int.Parse(chamberMatch.Groups[2].Value);
        }
        else
        {
            // "Plot 12, 4th Ward, The Goblet"
            var plotMatch = Regex.Match(address, @"Plot\s+(\d+)", RegexOptions.IgnoreCase);
            if (!plotMatch.Success) return null;
            entry.Plot = int.Parse(plotMatch.Groups[1].Value);
        }

        return entry;
    }

    private static string? ExtractDistrict(string address) => address switch
    {
        var s when s.Contains("Mist",          StringComparison.OrdinalIgnoreCase) => "Mist",
        var s when s.Contains("Goblet",         StringComparison.OrdinalIgnoreCase) => "TheGoblet",
        var s when s.Contains("Lavender",       StringComparison.OrdinalIgnoreCase) => "LavenderBeds",
        var s when s.Contains("Shirogane",      StringComparison.OrdinalIgnoreCase) => "Shirogane",
        var s when s.Contains("Empyreum",       StringComparison.OrdinalIgnoreCase) => "Empyreum",
        // Japanese district names for Shirogane apartment buildings
        var s when s.Contains("Kobai",          StringComparison.OrdinalIgnoreCase) => "Shirogane",
        var s when s.Contains("Topmast",        StringComparison.OrdinalIgnoreCase) => "Mist",
        var s when s.Contains("Sultana",        StringComparison.OrdinalIgnoreCase) => "TheGoblet",
        var s when s.Contains("Lily Hills",     StringComparison.OrdinalIgnoreCase) => "LavenderBeds",
        var s when s.Contains("Ingleside",      StringComparison.OrdinalIgnoreCase) => "Empyreum",
        _ => null
    };

    // ── Node reader ───────────────────────────────────────────────────────────

    private static unsafe string GetNodeText(AtkUnitBase* addon, uint nodeId)
    {
        var node = addon->GetNodeById(nodeId);
        if (node == null) return string.Empty;

        var textNode = node->GetAsAtkTextNode();
        if (textNode == null) return string.Empty;

        return textNode->NodeText.ToString() ?? string.Empty;
    }
}
