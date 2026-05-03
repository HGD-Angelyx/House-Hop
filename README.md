# HouseHop — FFXIV Dalamud Plugin

Teleport directly to **FC houses**, **private chambers**, and **apartments** using
[Lifestream](https://github.com/NightmareXIV/Lifestream)'s IPC. No more manually
navigating the housing menu.

---

## Requirements

- [XIVLauncher](https://goatcorp.github.io/) with Dalamud enabled
- **Lifestream** plugin installed and loaded (provides the teleport IPC)
- .NET 8 SDK + Visual Studio 2022 / Rider (to build from source)

---

## How housing data is collected

HouseHop **passively records** housing locations as you visit zones. It reads
the game's `HousingManager` memory structure on each framework tick. No external
API calls are made. Data is stored in your Dalamud config folder and persists
between sessions.

You can also **add entries manually** using the "Add manually" button in the UI.

Housing types supported:

| Type | How it's detected |
|---|---|
| FC House | Entering any plot interior; FC tag matched to owner |
| Private Chamber | Entering sub-rooms within an estate |
| Apartment | Entering an apartment building and visiting a room |

---

## Usage

- `/househop` — toggle the main window
- Use the search bar to filter by character or FC name
- Click **Teleport** to travel directly there via Lifestream
- **Pin** entries to keep them at the top of the list
- **Add manually** if you know the ward/plot/room already

---

## Building from source

1. Set `DALAMUD_HOME` to your Dalamud dev path:
   ```
   %APPDATA%\XIVLauncher\addon\Hooks\dev
   ```
2. Open `HouseHop.csproj` in Visual Studio 2022 or Rider.
3. Build (x64). Output lands in `bin\x64\Debug\net8.0-windows\`.
4. In XIVLauncher → Dalamud Settings → Experimental → Dev Plugin Locations,
   add that output folder.
5. Load via `/xlplugins` → Dev Tools.

---

## Notes on `HousingObserver`

The observer reads `FFXIVClientStructs.FFXIV.Client.Game.Housing.HousingManager`.
The exact field layout (`Ward`, `Plot`, `ApartmentWing`, etc.) can shift between
game patches. If teleports land in the wrong place after a patch, check the latest
`FFXIVClientStructs` NuGet for updated field names — the community updates these
quickly after each patch.

---

## License

MIT
