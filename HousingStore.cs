using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HouseHop;

public enum HousingType { FcHouse, PrivateChamber, Apartment }

[Serializable]
public class HousingEntry
{
    public string OwnerName  { get; set; } = string.Empty;
    public string District   { get; set; } = string.Empty;
    public int    Ward       { get; set; }
    public int    Plot       { get; set; }
    public int    Room       { get; set; }
    public bool   IsApartment { get; set; }
    public HousingType Type  { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    public string FriendlyLocation =>
        Type == HousingType.Apartment
            ? $"{DistrictDisplay} · Apt. Bldg {Plot} · Room {Room}"
            : Room > 0
                ? $"{DistrictDisplay} · Ward {Ward} · Plot {Plot} · Room {Room}"
                : $"{DistrictDisplay} · Ward {Ward} · Plot {Plot}";

    private string DistrictDisplay => District switch
    {
        "Mist"         => "Mist",
        "TheGoblet"    => "The Goblet",
        "LavenderBeds" => "Lavender Beds",
        "Shirogane"    => "Shirogane",
        "Empyreum"     => "Empyreum",
        _              => District
    };
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public List<HousingEntry> KnownHousing { get; set; } = new();
    public HashSet<string> Pinned { get; set; } = new();

    [NonSerialized] private IDalamudPluginInterface? _pi;
    public void Initialize(IDalamudPluginInterface pi) => _pi = pi;
    public void Save() => _pi?.SavePluginConfig(this);
}

public class HousingStore
{
    private readonly Configuration _config;
    public HousingStore(Configuration config) => _config = config;

    public IReadOnlyList<HousingEntry> All => _config.KnownHousing;

    public List<HousingEntry> Search(string query) =>
        string.IsNullOrWhiteSpace(query)
            ? _config.KnownHousing.ToList()
            : _config.KnownHousing
                     .Where(e => e.OwnerName.Contains(query, StringComparison.OrdinalIgnoreCase))
                     .ToList();

    public void Upsert(HousingEntry entry)
    {
        var existing = _config.KnownHousing.FirstOrDefault(e =>
            e.OwnerName.Equals(entry.OwnerName, StringComparison.OrdinalIgnoreCase)
            && e.District == entry.District
            && e.Ward == entry.Ward
            && e.Plot == entry.Plot
            && e.Room == entry.Room);

        if (existing != null)
            existing.LastSeen = DateTime.UtcNow;
        else
            _config.KnownHousing.Add(entry);

        _config.Save();
    }

    public void Remove(HousingEntry entry)
    {
        _config.KnownHousing.Remove(entry);
        _config.Save();
    }

    public bool IsPinned(string ownerName) => _config.Pinned.Contains(ownerName);

    public void TogglePin(string ownerName)
    {
        if (!_config.Pinned.Remove(ownerName))
            _config.Pinned.Add(ownerName);
        _config.Save();
    }
}
