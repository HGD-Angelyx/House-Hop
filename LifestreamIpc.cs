using Dalamud.Game.ClientState.Objects.SubKinds;
using System;

namespace HouseHop;

public class LifestreamIpc : IDisposable
{
    public bool IsAvailable => true;
    public void Dispose() { }

    public void GoToHouse(HousingEntry entry)
    {
        try
        {
            string district = ToLifestreamDistrict(entry.District);
            string cmd;

            if (entry.Type == HousingType.Apartment)
                cmd = $"/li {district} w{entry.Ward} apt r{entry.Room}";
            else if (entry.Type == HousingType.PrivateChamber)
                cmd = $"/li {district} w{entry.Ward} p{entry.Plot} r{entry.Room}";
            else
                cmd = $"/li {district} w{entry.Ward} p{entry.Plot}";

            // Append world if different from current world
            if (!string.IsNullOrWhiteSpace(entry.World))
            {
                // LocalPlayer moved to ObjectTable[0] in Dalamud 15
                string? currentWorld = null;
                if (HouseHop.ObjectTable[0] is IPlayerCharacter lp)
                    currentWorld = lp.CurrentWorld.Value.Name.ToString();

                if (!entry.World.Equals(currentWorld, StringComparison.OrdinalIgnoreCase))
                    cmd += $" @ {entry.World}";
            }

            // SendMessage was removed — use ICommandManager.ProcessCommand instead
            bool sent = HouseHop.CommandManager.ProcessCommand(cmd);
            if (!sent)
            {
                // /li is a Lifestream command, not a Dalamud one, so ProcessCommand
                // returns false but still forwards it to the game. Print feedback either way.
                HouseHop.Log.Information($"[HouseHop] Sent to game: {cmd}");
            }
        }
        catch (Exception ex)
        {
            HouseHop.Log.Error(ex, "[HouseHop] Failed to send Lifestream command.");
            HouseHop.ChatGui.PrintError($"[HouseHop] Failed: {ex.Message}");
        }
    }

    private static string ToLifestreamDistrict(string district) => district switch
    {
        "Mist"         => "Mist",
        "TheGoblet"    => "Goblet",
        "LavenderBeds" => "Lavender",
        "Shirogane"    => "Shirogane",
        "Empyreum"     => "Empyreum",
        _              => district
    };
}
