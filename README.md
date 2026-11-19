# DarkSkies FactionGate

Remaps the Nuclear Option **SELECT FACTION** join screen (name + flag) for Boscali and Primeva. Host picks replacements in the F1 BepInEx ConfigurationManager; joiners with the mod receive those picks over Steam NetworkingMessages. Optional per-faction workshop skin profiles (players + AI).

**GitHub:** https://github.com/datfacesoldier95/DarkSkies-FactionGate  
**Releases:** https://github.com/datfacesoldier95/DarkSkies-FactionGate/releases/latest

## Install (one zip for host + players)

**Preferred:** install **DarkSkies FactionGate** from NOMM (same zip as the GitHub release).

**Manual:** download `DarkSkies-FactionGate-*.zip` from Releases and extract so you have:

`BepInEx/plugins/DarkSkies.FactionGate/FactionGate.dll` (+ `presets.json`, `Flags/`, `meta.json`).

Do not leave the `.zip` sitting loose in `plugins` — BepInEx only loads folders.

**Linux / Proton:** use zip **1.2.4+** (forward-slash paths). Older handout zips used Windows `\`, so Linux unzip dumps everything into one folder with broken names like `DarkSkies.FactionGate\Flags\uk.png`. After a good extract you must have:

`…/BepInEx/plugins/DarkSkies.FactionGate/FactionGate.dll`  
`…/BepInEx/plugins/DarkSkies.FactionGate/Flags/…`

Requires BepInEx + ConfigurationManager (F1).

`.\deploy.ps1` is off by default. Dev hot-copy only: `.\deploy.ps1 -ForceDev`.

## Host setup

1. Host the lobby / mission.
2. Press **F1** → **DarkSkies FactionGate** → set **BoscaliPreset** and **PrimevaPreset**.
3. Joiners with FactionGate loaded get the same presets automatically.

**F10** reloads `presets.json` + `Flags/` on the host and re-broadcasts.

## Add a faction

1. Drop `Flags/myflag.png`
2. Add to `presets.json`:
   `{ "id": "myflag", "displayName": "My Faction", "flag": "myflag.png" }`
3. Press F10 (or restart) so the F1 dropdown picks it up (new ids may need a game restart for the AcceptableValueList).

## MP notes

- Both host and joiners need FactionGate.
- Under the hood sides stay Boscali / Primeva (missions, HQ sync unchanged).
- Channel 7782 / magic `FGAT` (does not collide with SkinGate 7781).

## Flags

See `Flags/ATTRIBUTION.txt`. Nationals from flagcdn; fiction slots are original placeholders you can replace.

## Faction Skin Profiles

F1 → **Skin Profile Settings** (host only):

1. **Faction Selection** — loads that faction’s saved profile into the editor  
2. **Select Airframe** — which aircraft’s workshop list you are editing  
3. **Enable Faction Profile?** — when on, joiners on that remap are filtered  
4. **Unassigned Skins** / **Assigned Skins** — click a skin to move it for the selected airframe  
5. **Vanilla Skins Enabled?** — allow built-in liveries for that airframe  
6. **Faction Skin Summary** — vertical list of all custom assignments on this faction (`Airframe - Skin Name`)  
7. **Save Profile** — writes/overwrites that faction’s profile and broadcasts to joiners  
8. **Clear Profile** — removes all assigned custom skins from every airframe on that faction only (other factions untouched), then saves + broadcasts  

Edits in the lists are draft until **Save Profile** (Clear Profile saves immediately). Host must be subscribed to a workshop skin for it to appear under Available.

When a faction profile is enabled, **AI on that side** are also forced onto allowed skins (spawn + livery apply), not only players.
