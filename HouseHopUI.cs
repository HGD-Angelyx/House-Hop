using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace HouseHop;

public class HouseHopUI : IDisposable
{
    private readonly HouseHop _plugin;
    private readonly WindowSystem _windowSystem = new("HouseHop");
    private readonly MainWindow _mainWindow;
    private readonly SettingsWindow _settingsWindow;

    public bool Visible
    {
        get => _mainWindow.IsOpen;
        set => _mainWindow.IsOpen = value;
    }

    public HouseHopUI(HouseHop plugin)
    {
        _plugin = plugin;
        _mainWindow     = new MainWindow(plugin);
        _settingsWindow = new SettingsWindow(plugin);
        _windowSystem.AddWindow(_mainWindow);
        _windowSystem.AddWindow(_settingsWindow);
    }

    public void Dispose() => _windowSystem.RemoveAllWindows();
    public void Draw()    => _windowSystem.Draw();
    public void OpenSettings() => _settingsWindow.IsOpen = true;
}

// ── Main Window ───────────────────────────────────────────────────────────────

public class MainWindow : Window, IDisposable
{
    private readonly HouseHop _plugin;
    private string _searchInput = string.Empty;

    private static readonly Vector4 Gold  = new(0.78f, 0.66f, 0.43f, 1f);
    private static readonly Vector4 Green = new(0.23f, 0.55f, 0.20f, 1f);

    public MainWindow(HouseHop plugin)
        : base("HouseHop##main", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 320),
            MaximumSize = new Vector2(640, 720)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawHeader();
        ImGui.Separator();
        DrawSearchBar();
        ImGui.Spacing();
        DrawTabs();
        ImGui.Separator();
        DrawFooter();
    }

    private void DrawHeader()
    {
        ImGui.TextColored(Gold, "HouseHop");
        ImGui.SameLine();
        ImGui.TextDisabled("— housing teleporter via Lifestream");
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 60);
        if (ImGui.SmallButton("Settings"))
            _plugin.UI.OpenSettings();
    }

    private void DrawSearchBar()
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.InputTextWithHint("##search", "Search name, world, or FC...", ref _searchInput, 64);
    }

    private void DrawTabs()
    {
        if (ImGui.BeginTabBar("##housingtabs"))
        {
            if (ImGui.BeginTabItem("FC House##t"))  { DrawEntryList(HousingType.FcHouse);        ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Private##t"))   { DrawEntryList(HousingType.PrivateChamber); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Apartment##t")) { DrawEntryList(HousingType.Apartment);      ImGui.EndTabItem(); }
            ImGui.EndTabBar();
        }
    }

    private void DrawEntryList(HousingType type)
    {
        var entries = _plugin.Store.Search(_searchInput)
                             .Where(e => e.Type == type)
                             .OrderByDescending(e => _plugin.Store.IsPinned(e.OwnerName))
                             .ThenByDescending(e => e.LastSeen)
                             .ToList();

        if (entries.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled(string.IsNullOrWhiteSpace(_searchInput)
                ? "No entries yet — use 'Add manually' below to add one."
                : "No results match your search.");
            return;
        }

        ImGui.BeginChild("##entrylist", new Vector2(0, -ImGui.GetFrameHeightWithSpacing() - 4), false);
        foreach (var entry in entries)
            DrawEntry(entry);
        ImGui.EndChild();
    }

    private void DrawEntry(HousingEntry entry)
    {
        ImGui.PushID(entry.OwnerName + entry.District + entry.Ward + entry.Plot + entry.Room);

        bool pinned = _plugin.Store.IsPinned(entry.OwnerName);
        if (pinned) { ImGui.TextColored(Gold, "★"); ImGui.SameLine(0, 4); }

        // Name
        ImGui.TextColored(Gold, entry.OwnerName);
        ImGui.SameLine(0, 6);
        // Location + world
        ImGui.TextDisabled(entry.FriendlyLocation);

        // Buttons right-aligned
        float avail = ImGui.GetContentRegionAvail().X;
        ImGui.SameLine(avail - 148);

        if (ImGui.SmallButton("Teleport##tp"))
            _plugin.Lifestream.GoToHouse(entry);

        ImGui.SameLine(0, 6);
        if (ImGui.SmallButton(pinned ? "Unpin##p" : "Pin##p"))
            _plugin.Store.TogglePin(entry.OwnerName);

        ImGui.SameLine(0, 6);
        if (ImGui.SmallButton("X##rm"))
            _plugin.Store.Remove(entry);

        ImGui.TextDisabled($"  Last seen: {FormatAge(entry.LastSeen)}");
        ImGui.Separator();
        ImGui.PopID();
    }

    private void DrawFooter()
    {
        ImGui.TextDisabled("Use 'Add manually' to record an address, then Teleport to go there via Lifestream.");
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 100);
        if (ImGui.SmallButton("Add manually##add"))
            ImGui.OpenPopup("##manualentry");
        DrawManualAddPopup();
    }

    // ── Manual add popup ──────────────────────────────────────────────────────

    private string _manName     = string.Empty;
    private string _manWorld    = string.Empty;
    private int    _manDistrict = 0;
    private int    _manWard     = 1;
    private int    _manPlot     = 1;
    private int    _manRoom     = 0;
    private int    _manType     = 0;

    private static readonly string[] Districts     = { "Mist", "TheGoblet", "LavenderBeds", "Shirogane", "Empyreum" };
    private static readonly string[] DistrictLabels = { "Mist", "The Goblet", "Lavender Beds", "Shirogane", "Empyreum" };
    private static readonly string[] Types         = { "FC House", "Private Chamber", "Apartment" };

    private void DrawManualAddPopup()
    {
        if (!ImGui.BeginPopup("##manualentry")) return;

        ImGui.TextColored(Gold, "Add housing entry");
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(180);
        ImGui.InputTextWithHint("##mname", "Owner or FC name *", ref _manName, 64);

        ImGui.SetNextItemWidth(140);
        ImGui.InputTextWithHint("##mworld", "World (e.g. Ragnarok) *", ref _manWorld, 32);

        ImGui.SetNextItemWidth(150);
        ImGui.Combo("District##mc", ref _manDistrict, DistrictLabels, DistrictLabels.Length);

        ImGui.SetNextItemWidth(80);
        ImGui.InputInt("Ward##mw", ref _manWard);
        _manWard = Math.Clamp(_manWard, 1, 30);

        ImGui.SetNextItemWidth(80);
        ImGui.InputInt("Plot##mp", ref _manPlot);
        _manPlot = Math.Clamp(_manPlot, 1, 60);

        ImGui.SetNextItemWidth(80);
        ImGui.InputInt("Room (0 = main)##mr", ref _manRoom);
        _manRoom = Math.Max(0, _manRoom);

        ImGui.SetNextItemWidth(150);
        ImGui.Combo("Type##mt", ref _manType, Types, Types.Length);

        ImGui.Spacing();

        bool canAdd = !string.IsNullOrWhiteSpace(_manName) && !string.IsNullOrWhiteSpace(_manWorld);
        if (!canAdd) ImGui.BeginDisabled();

        if (ImGui.Button("Add##madd"))
        {
            _plugin.Store.Upsert(new HousingEntry
            {
                OwnerName   = _manName.Trim(),
                World       = _manWorld.Trim(),
                District    = Districts[_manDistrict],
                Ward        = _manWard,
                Plot        = _manPlot,
                Room        = _manRoom,
                IsApartment = _manType == 2,
                Type        = (HousingType)_manType,
                LastSeen    = DateTime.UtcNow
            });
            _manName  = string.Empty;
            _manWorld = string.Empty;
            ImGui.CloseCurrentPopup();
        }

        if (!canAdd) ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button("Cancel##mcancel")) ImGui.CloseCurrentPopup();

        if (!canAdd)
        {
            ImGui.Spacing();
            ImGui.TextDisabled("* Name and World are required.");
        }

        ImGui.EndPopup();
    }

    private static string FormatAge(DateTime lastSeen)
    {
        var d = DateTime.UtcNow - lastSeen;
        if (d.TotalMinutes < 1) return "just now";
        if (d.TotalHours   < 1) return $"{(int)d.TotalMinutes}m ago";
        if (d.TotalDays    < 1) return $"{(int)d.TotalHours}h ago";
        return $"{(int)d.TotalDays}d ago";
    }
}

// ── Settings Window ───────────────────────────────────────────────────────────

public class SettingsWindow : Window, IDisposable
{
    private readonly HouseHop _plugin;

    public SettingsWindow(HouseHop plugin)
        : base("HouseHop Settings##settings", ImGuiWindowFlags.AlwaysAutoResize)
        => _plugin = plugin;

    public void Dispose() { }

    public override void Draw()
    {
        var gold = new Vector4(0.78f, 0.66f, 0.43f, 1f);

        ImGui.TextWrapped("HouseHop teleports you to saved housing addresses via Lifestream's /li command.");
        ImGui.Spacing();
        ImGui.TextWrapped("Make sure Lifestream is installed and loaded, then add entries using 'Add manually' in the main window.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(gold, "Lifestream command:");
        ImGui.SameLine();
        ImGui.TextDisabled("/li <district> w<ward> p<plot> [r<room>] [@ <world>]");

        ImGui.Spacing();
        ImGui.TextDisabled($"Known entries: {_plugin.Store.All.Count}");
        ImGui.Spacing();

        if (ImGui.Button("Clear all entries##clr"))
            foreach (var e in _plugin.Store.All.ToList())
                _plugin.Store.Remove(e);

        ImGui.Spacing();
        if (ImGui.Button("Close##cls")) IsOpen = false;
    }
}
