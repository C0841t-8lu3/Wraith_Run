# AGENTS.md — Wraith Run Project Instructions

## 1. Engine & Technical Constraints
- **Engine**: Godot 4.7.2 .NET build (C# with .NET 8.0, `Wraith Run.sln`, `Wraith Run.csproj`).
- **C# Conventions**:
  - All Godot node scripts MUST be `public partial class` matching their script filename.
  - Use PascalCase Godot overrides with `double delta`: `_Ready()`, `_PhysicsProcess(double delta)`.
  - Use `[Export]` for Inspector properties and `[Signal]` with `EventHandler` naming delegates.
  - Use `GD.Print()` / `GD.PrintErr()` (never `Console.WriteLine()`).
- **CRITICAL FILE SAFETY (DO NOT TOUCH OR OVERWRITE)**:
  - Audio pipeline: `scripts/autoloads/Audio.cs`, `scripts/autoloads/SoundBank.cs`, `SoundManager.cs`, `scripts/player/PlayerAudio.cs`, `scripts/ui/UiSounds.cs`, and `default_bus_layout.tres`.
  - Active Weapon Registry: Always edit `scripts/data/WeaponRegistry.tres` (loaded by `scripts/player/WeaponSwitcher.cs` and `scripts/player/WeaponAttachment.cs`). NEVER touch or recreate `data/weapons/`.
  - In-Game Tablet: Implement the UI inside `scripts/ui/HubTablet.cs`.
  - Network: Implement LAN discovery and lobby management inside `scripts/network/LanMenu.cs`.

---

## 2. Implementation Execution Plan (Execute In Order)

### PASS 1: Weapon Registry, Attachments & Handling (Combat Core)
1. **Roster Configuration (`scripts/data/WeaponRegistry.tres`)**:
   - Baseline: 100 HP player health, 2.5x headshot multiplier.
   - **AK-47**: 34 body / 85 head dmg (3 body / 2 head to kill), 600 RPM auto, 30 mag, 2.3s reload. Recoil: 3.5° vert, ±2.0° horiz, 180ms recovery.
   - **M4**: 30 body / 75 head dmg (4 body / 2 head to kill), 800 RPM auto, 30 mag, 2.0s reload. Recoil: 2.0° vert, ±1.0° horiz, 120ms recovery.
   - **P90**: 22 body / 55 head dmg (5 body / 2 head to kill), 900 RPM auto, 50 mag, 2.2s reload. Recoil: 1.2° vert, ±0.8° horiz, 90ms recovery.
   - **Kriss Vector**: 24 body / 60 head dmg (5 body / 2 head to kill), 1100 RPM auto, 25 mag, 2.0s reload. Recoil: 1.0° vert, ±0.6° horiz, 80ms recovery.
   - **Glock 19**: 35 body / 83 head dmg (3 body / 2 head to kill), 300 RPM semi, 15 mag, 1.5s reload. Recoil: 1.5° vert, ±1.0° horiz, 150ms recovery.
   - **FN 502**: 28 body / 70 head dmg (4 body / 2 head to kill), 300 RPM semi, 15 mag, 1.5s reload. Recoil: 1.5° vert, ±1.0° horiz, 150ms recovery.
   - **Desert Eagle**: 45 body / 100+ head dmg (3 body / 1 head to kill), 180 RPM semi, 7 mag, 2.0s reload. Recoil: 5.0° vert, ±3.0° horiz, 350ms recovery.
   - **Shotgun**: 70 close dmg with distance falloff (1–2 close to kill), 60 RPM pump, 6 shells, 0.5s/shell reload. Recoil: 6.0° vert, ±2.5° horiz, 400ms recovery.
   - **Sledge Hammer**: Instant 1-hit kill within melee ray/shape cast range.
   - **Bazooka**: 1-round capacity, 3.0s reload, 8.0° kick, 1000ms recovery. Deals 20 direct damage to players (5+ hits to kill on foot); destroys vehicles outright (1-hit kill).
2. **Attachment System (`scripts/player/WeaponAttachment.cs`)**:
   - 8-stat modifiers: ADS Speed, Switch Speed, Reload Speed, Vertical Recoil, Horizontal Recoil, Bullet Spread, Visual Kick, Ammo Capacity.
   - Canted backup optic: implement an input action to tilt the weapon 45° between primary optic and offset iron sight/dot.

---

### PASS 2: Warehouse Hub, Diegetic Tablet & Hotspot Networking
1. **Warehouse Hub**:
   - Spawn player directly in first-person inside `scenes/hub/WarehouseHub.tscn` (Physical Weapon Wall, Firing Range, Vehicle Bay).
   - Equipping from the Weapon Wall immediately updates active loadout.
2. **Tactical Tablet (`scripts/ui/HubTablet.cs`)**:
   - Pulled out via Menu/Tab key without freezing the game world.
   - 4 Tabs: `PLAY`, `LOADOUT`, `OPERATORS`, `SETTINGS`.
   - **Play Tab (3-Column Layout)**[cite: 44]:
     - *Left*: Start Match button, Mode dropdown (TDM, DM, Convoy Assault, Pure Chase), Map dropdown, Duration buttons (`3m`, `5m`, `8m`, `13m`), Max Players numeric input, Player Marker toggle (`No`/`Yes`), Enable Bots toggle (`No`/`Yes`), Bots Difficulty (`1`–`5`)[cite: 44].
     - *Center*: Stacked team rosters (`Albert Team` vs. `Einstein Team` or Attackers vs. Defenders)[cite: 44]. Host card displays a crown icon[cite: 44]. Only the Host sees the 3-dot context menu (`⋮`) on player cards to trigger **Switch Team** or **Kick Player**[cite: 44].
     - *Right*: `Invite Friends` panel and lobby chat[cite: 44].
3. **Hotspot Networking (`scripts/network/LanMenu.cs`)**:
   - Automatic UDP broadcast/listener on port 7777.
   - Devices connected to the Host's hotspot or Wi-Fi automatically detect the active room and list it on their tablet without entering manual IP addresses or 4-digit codes.
   - In *Pure Chase*, players can invite each other via the friend panel to form 2-player crews.

---

### PASS 3: Combined Zero-Minimap HUD & Respawn Screen
1. **HUD Layout (`scenes/ui/CombinedHUD.tscn`)**[cite: 45]:
   - **Top-Left**: Tablet menu button (`=`), Mode shield icon, countdown timer (`2:15`), stacked blue (own team) and red (enemy team) score bars[cite: 45].
   - **Top-Right**: Active weapon silhouette, stacked ammo counter (large mag ammo / small reserve ammo), secondary/melee slot icon with slot number[cite: 45].
   - **Center**: 4-tick reticle, floating style bonus text (`+XP   HIGHLIGHT` placement), bottom kill streak pill (`5th KILL`)[cite: 45].
   - **Zero Minimap**: NO on-foot minimap and NO 2D vehicle minimap.
2. **Vehicle Cockpit Integration**:
   - Keep top-left and top-right HUD elements visible.
   - Render vehicle telemetry (speedometer, RPM, 100 HP bar, tire status, boost) directly onto the physical 3D dashboard inside the car model.
3. **Respawn Screen**:
   - Display only a high-tension `"RESPAWNING..."` screen with NO numeric countdown.
   - Respawn timers: 3 seconds (TDM, DM, Pure Chase), 9 seconds (Convoy Assault).

---

### PASS 4: Vehicles & 12-Operator Rotation System
1. **Vehicle Damage Controller (`scripts/combat/VehicleDamageController.cs`)**:
   - Baseline: 100 vehicle HP.
   - Small arms fire to tires / spike strip: brings vehicle to 0 HP temporary halt for a recoverable repair window.
   - Bazooka direct hit: immediate destruction, leaving a persistent wreck for 30 seconds.
   - NO in-vehicle seat swapping.
   - Vehicles: **STV** (8-seat armored SUV with gun ports) and **Raider Buggy** (2-seat scrap buggy with roof turret).
2. **12 Operators & Perks**:
   - **Drivers**:
     - *Apex*: Reinforced Chassis (-20% collision & small-arms tire damage).
     - *Shift*: Nitrous Overdrive (+25% boost duration & faster cooldown).
     - *Ghost*: Slipstream (Reduces vehicle lock-on tracking duration).
     - *Tread*: Iron Rubber (Maintains traction when tires are damaged).
   - **Shooters**:
     - *Viper*: Precision Stabilizer (-15% recoil while firing from moving vehicle).
     - *Rage*: Hollow-Point Feed (+10% damage to vehicle armor).
     - *Spark*: Quick Draw (+20% weapon swap and reload speed).
     - *Flash*: Overclocked Mag (+15% AR and SMG magazine capacity).
   - **Medics**:
     - *Pulse*: Rapid Triage (50% faster teammate revival).
     - *Patch*: Field Mechanic (Can repair vehicle HP on foot without garages).
     - *Stim*: Adrenaline Boost (Healing grants 5s move/reload speed buff).
     - *Anchor*: Guardian Shell (+20 HP shield when near teammates/vehicles).
3. **Selection & Role Lock (`scenes/ui/SiegeOperatorSelect.tscn`)**:
   - *Rainbow Six Siege*-style operator grid before each round.
   - Players cannot pick the same operator two rounds in a row.
   - Hard cap: 4 rounds maximum in any single role class (Drivers, Shooters, Medics) across the match. Once capped, that entire class locks out.

---

### PASS 5: 4 Authentic Game Modes & Progression
1. **Team Deathmatch (`ModeTeamDeathmatch.cs`)**:
   - Team Albert vs. Team Einstein. Host controls team assignments.
   - Uses Universal Kill-Style Bonuses: Multi-Kill (+200), 360 No-Scope (+175), Headshot (+150), Hip-Fire (+125), Verge of Death (+120), Assist (+25 min).
2. **Deathmatch (`ModeDeathmatch.cs`)**:
   - Free-For-All with default settings, no storms or environmental events. Uses Universal Kill-Style Bonuses.
3. **Convoy Assault (`ModeConvoyAssault.cs`)**:
   - 9v9 maximum (up to 9 operators in one STV convoy).
   - 7 rounds total; first team to reach 4 round wins wins the match.
   - Teams swap sides (Attackers vs. Defenders) every round.
   - 10 km border-to-target tactical extraction corridor.
   - Objectives: Kill (+150), Driver Kill (+200–250), Spike Strip hit (+275), 1km advance (+100 team), Reach Checkpoint (+200), Destroy STV (+500 team), Round Win (+500).
4. **Pure Chase (`ModePureChase.cs`)**:
   - 2v2 Duo Buggy checkpoint combat race (4–5 minutes long).
   - Cursed Land Events active along the route: Floor Collapse, Pull Vortex, Blindness Fog, Mirror Shift, Ghost Possession, Death Knell.
   - Objectives: Buggy damage per 25% HP (+200), Driver Kill (+250), Shooter Kill (+200).
5. **Progression & Match End**:
   - Award XP on kills, bonuses, and wins to level up numeric Rank (`1–55+`) and emblems[cite: 44, 45].
   - Award `CP` currency to unlock attachments and calling card banners[cite: 44].
   - When a match ends, return all connected players directly to the 3D Warehouse Hub.
