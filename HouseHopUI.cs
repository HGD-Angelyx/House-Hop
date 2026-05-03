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
    private static readonly Vector4 Red   = new(0.70f, 0.25f, 0.25f, 1f);

    public MainWindow(HouseHop plugin)
        : base("HouseHop##main", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(380, 320),
            MaximumSize = new Vector2(580, 720)
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
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 110);

        if (_plugin.Lifestream.IsAvailable)
            ImGui.TextColored(Green, "Lifestream: OK");
        else
            ImGui.TextColored(Red, "Lifestream: offline");

        ImGui.SameLine();
        if (ImGui.SmallButton("Settings"))
            _plugin.UI.OpenSettings();
    }

    private void DrawSearchBar()
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        ImGui.InputTextWithHint("##search", "Search character or FC name...", ref _searchInput, 64);
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
                ? $"No {TypeLabel(type)} entries yet — visit housing zones to collect data."
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

        ImGui.TextColored(Gold, entry.OwnerName);
        ImGui.SameLine();
        ImGui.TextDisabled($"  {entry.FriendlyLocation}");

        float rightX = ImGui.GetContentRegionAvail().X;
        ImGui.SameLine(rightX - 130);

        bool canTp = _plugin.Lifestream.IsAvailable;
        if (!canTp) ImGui.BeginDisabled();
        if (ImGui.SmallButton("Teleport##" + entry.OwnerName + entry.Room))
            _plugin.Lifestream.GoToHouse(entry);
        if (!canTp) ImGui.EndDisabled();

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
        ImGui.TextDisabled("Housing data is collected passively when you visit zones.");
        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 100);
        if (ImGui.SmallButton("Add manually##add"))
            ImGui.OpenPopup("##manualentry");
        DrawManualAddPopup();
    }

    // ── Manual add popup ──────────────────────────────────────────────────────

    private string _manName     = string.Empty;
    private int    _manDistrict = 0;
    private int    _manWard     = 1;
    private int    _manPlot     = 1;
    private int    _manRoom     = 0;
    private int    _manType     = 0;

    private static readonly string[] Districts = { "Mist", "TheGoblet", "LavenderBeds", "Shirogane", "Empyreum" };
    private static readonly string[] Types     = { "FC House", "Private Chamber", "Apartment" };

    private void DrawManualAddPopup()
    {
        if (!ImGui.BeginPopup("##manualentry")) return;

        ImGui.TextColored(Gold, "Add entry manually");
        ImGui.Separator();

        ImGui.SetNextItemWidth(180);
        ImGui.InputTextWithHint("##mname", "Owner / FC name", ref _manName, 64);

        ImGui.SetNextItemWidth(140);
        ImGui.Combo("District##mc", ref _manDistrict, Districts, Districts.Length);

        ImGui.SetNextItemWidth(80);
        ImGui.InputInt("Ward##mw", ref _manWard);
        _manWard = Math.Clamp(_manWard, 1, 30);

        ImGui.SetNextItemWidth(80);
        ImGui.InputInt("Plot##mp", ref _manPlot);
        _manPlot = Math.Clamp(_manPlot, 1, 60);

        ImGui.SetNextItemWidth(80);
        ImGui.InputInt("Room (0 = main)##mr", ref _manRoom);
        _manRoom = Math.Max(0, _manRoom);

        ImGui.SetNextItemWidth(140);
        ImGui.Combo("Type##mt", ref _manType, Types, Types.Length);

        ImGui.Spacing();
        if (ImGui.Button("Add##madd") && !string.IsNullOrWhiteSpace(_manName))
        {
            _plugin.Store.Upsert(new HousingEntry
            {
                OwnerName   = _manName.Trim(),
                District    = Districts[_manDistrict],
                Ward        = _manWard,
                Plot        = _manPlot,
                Room        = _manRoom,
                IsApartment = _manType == 2,
                Type        = (HousingType)_manType,
                LastSeen    = DateTime.UtcNow
            });
            _manName = string.Empty;
            ImGui.CloseCurrentPopup();
        }
        ImGui.SameLine();
        if (ImGui.Button("Cancel##mcancel")) ImGui.CloseCurrentPopup();

        ImGui.EndPopup();
    }

    private static string TypeLabel(HousingType t) => t switch
    {
        HousingType.FcHouse        => "FC house",
        HousingType.PrivateChamber => "private chamber",
        HousingType.Apartment      => "apartment",
        _                          => "housing"
    };

    private static string FormatAge(DateTime lastSeen)
    {
        var delta = DateTime.UtcNow - lastSeen;
        if (delta.TotalMinutes < 1) return "just now";
        if (delta.TotalHours   < 1) return $"{(int)delta.TotalMinutes}m ago";
        if (delta.TotalDays    < 1) return $"{(int)delta.TotalHours}h ago";
        return $"{(int)delta.TotalDays}d ago";
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
        ImGui.TextDisabled("HouseHop passively collects housing data as you visit zones.");
        ImGui.Spacing();
        ImGui.TextWrapped("To teleport, Lifestream must be installed and loaded.");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.78f, 0.66f, 0.43f, 1f), "Lifestream IPC status:");
        ImGui.SameLine();
        if (_plugin.Lifestream.IsAvailable)
            ImGui.TextColored(new Vector4(0.23f, 0.55f, 0.20f, 1f), "Connected");
        else
            ImGui.TextColored(new Vector4(0.70f, 0.25f, 0.25f, 1f), "Not found");

        ImGui.Spacing();
        ImGui.TextDisabled($"Known housing entries: {_plugin.Store.All.Count}");
        ImGui.Spacing();

        if (ImGui.Button("Clear all entries##clr"))
            foreach (var e in _plugin.Store.All.ToList())
                _plugin.Store.Remove(e);

        ImGui.Spacing();
        if (ImGui.Button("Close##cls")) IsOpen = false;
    }
}
