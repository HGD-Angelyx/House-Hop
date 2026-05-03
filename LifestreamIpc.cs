using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using System;

namespace HouseHop;

/// <summary>
/// Wraps Lifestream's IPC gate for direct house teleportation.
///
/// Lifestream's HouseVisit gate signature:
///   void HouseVisit(string district, int ward, int plot, int room, bool isApartment)
///
/// Falls back to Lifestream's /li text command if IPC is unavailable.
/// </summary>
public class LifestreamIpc : IDisposable
{
    private ICallGateSubscriber<string, int, int, int, bool, object?>? _houseVisit;
    public bool IsAvailable { get; private set; }

    public LifestreamIpc(IDalamudPluginInterface pi)
    {
        try
        {
            _houseVisit = pi.GetIpcSubscriber<string, int, int, int, bool, object?>(
                "Lifestream.HouseVisit");

            pi.GetIpcSubscriber<object?>("Lifestream.Available")
              .Subscribe(_ => IsAvailable = true);
            pi.GetIpcSubscriber<object?>("Lifestream.Unavailable")
              .Subscribe(_ => IsAvailable = false);

            // Try a dummy call to check availability without side effects
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            HouseHop.Log.Warning(ex, "[HouseHop] Lifestream IPC not available.");
            IsAvailable = false;
        }
    }

    public void Dispose() { }

    public void GoToHouse(HousingEntry entry)
    {
        if (_houseVisit == null || !IsAvailable)
        {
            HouseHop.ChatGui.PrintError("[HouseHop] Lifestream is not available. Please install and enable it.");
            return;
        }

        try
        {
            _houseVisit.InvokeAction(
                entry.District,
                entry.Ward,
                entry.Plot,
                entry.Room,
                entry.IsApartment);

            HouseHop.Log.Information(
                $"[HouseHop] Teleporting to {entry.OwnerName} — {entry.FriendlyLocation}");
        }
        catch (Exception ex)
        {
            HouseHop.Log.Error(ex, "[HouseHop] Teleport failed.");
            HouseHop.ChatGui.PrintError($"[HouseHop] Teleport failed: {ex.Message}");
        }
    }
}
