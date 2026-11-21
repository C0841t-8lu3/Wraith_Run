You are a senior Godot 4 .NET (C#) engine and graphics engineer. Inspect the existing codebase and resolve these critical visual, architectural, and UI bugs shown in recent playtest screenshots.

After completing the fixes, verify with `dotnet build` that the project compiles with 0 errors and 0 warnings.

================================================================================
PRIORITY 0: SPLASH SCREEN REMOVAL & POLISHED LOADING SCREEN
================================================================================
1. Splash Screen:
   - In `project.godot`, disable Godot's boot splash image (`application/boot_splash/show_image=false` or `splash_screen/disable_godot_boot_splash=true`).
2. Functional Loading Screen (`scenes/ui/LoadingScreen.tscn` / `LoadingScreen.cs`):
   - Replace any low-res static backdrop with a clean, dark tactical loading screen.
   - Implement an asynchronous scene loader using `ResourceLoader.LoadThreadedRequest()` and `ResourceLoader.LoadThreadedGetStatus()`.
   - Include a smooth, glowing progress bar and a rotating tactical hint/instruction carousel (displaying keybinds, game mode rules, and operator class tips).

================================================================================
BUG 1: FIX FIRST-PERSON VIEWMODEL, ARM RIGGING & WEAPON SOCKETS
================================================================================
In screenshots, the player's first-person arms are violently twisted 90°–180° backward into the camera view, blocking the screen when holding weapons like the Sledge Hammer and Bazooka.

Inspect `scripts/player/WeaponAttachment.cs`, `scripts/player/WeaponSwitcher.cs`, and `scenes/entities/Player.tscn`:
1. Fix Bone Transforms & Socket Offsets:
   - Reset the armature rest pose and bone roll rotations for `Armature/Skeleton3D` or the viewmodel hand sockets.
   - Ensure the right hand and left hand sockets hold each weapon at a natural forward orientation pointing toward the reticle.
2. Per-Weapon Viewmodel Alignment:
   - **Sledge Hammer**: Held in ready two-handed grip at lower right/chest; arms resting in a natural tactical stance pointing forward.
   - **Bazooka**: Rested over the right shoulder extending forward toward the crosshair; prevent the operator's shoulder/sleeve from angling across and clipping through the center camera view.
   - **Rifles & Pistols (AK-47, M4, Glock 19, etc.)**: Positioned at lower-right hip-fire view, aligning directly to camera center when aiming down sights (ADS).
3. Camera Near Clipping Plane:
   - Set `Camera3D.Near` to `0.05` to prevent weapon and hand geometry from clipping into the near-plane frustum.

================================================================================
BUG 2: COMPLETE PURGE OF EXPIRED & DELETED FEATURES
================================================================================
The mode dropdown menu currently contains deleted legacy modes. Completely remove all traces of:
   - `Storm Respawn`
   - `Clan Chase`
   - `Cursed Deathmatch`
   - Redundant duplicate `2v2 Duo Buggy` (this is unified under `Pure Chase`)
   - Any leftover claimable garage mechanics (`Garage.tscn`), eating/drinking scripts, or old weapon files outside `scripts/data/WeaponRegistry.tres`.

Strictly limit the game and all dropdowns to the **4 APPROVED GAME MODES**:
   1. **Team Deathmatch** (Albert Team vs. Einstein Team)
   2. **Deathmatch** (Classic Free-For-All)
   3. **Convoy Assault** (Up to 9v9, 7-round series, 10 km extraction corridor)
   4. **Pure Chase** (2v2 Duo Buggy 4–5 minute checkpoint race with Cursed Land Events)

Update the `GameMode` enum in all mode managers, `LanMenu.cs`, `HubTablet.cs`, and UI dropdowns so only these 4 modes exist.

================================================================================
BUG 3: UI & UX POLISH (TABLET, OPERATOR GRID & IN-MATCH HUD)
================================================================================
1. Custom Game Lobby (`scripts/ui/HubTablet.cs` / `scenes/ui/CustomLobbyTab.tscn`):
   - Replace the stacked, clipping drop-down buttons with the proper 3-column tactical layout:
     * **Left Column (Host Configuration)**:
       - Header banner with `START MATCH` button.
       - Clean `OptionButton` dropdowns for Mode (4 modes only) and Map (`Warehouse`, `Ashfields`).
       - Duration pill buttons (`3m`, `5m`, `8m`, `13m`).
       - Max Players input, Player Marker toggle (`No` / `Yes`), Enable Bots toggle (`No` / `Yes`), and Bots Difficulty slider (`1`–`5`).
     * **Center Column (Albert Team vs. Einstein Team / Attackers vs. Defenders)**:
       - Stacked team containers with live player count headers (e.g., `1/3`).
       - Player calling card banners with avatar icon, rank badge, and host crown.
       - A 3-dot context menu (`⋮`) visible ONLY to the Host with functional actions: **Switch Team** and **Kick Player**.
     * **Right Column (Social & Invites)**:
       - Discovered player invite list with `Invite to lobby` buttons (used for Pure Chase 2-player crews) and a lobby chat box with a `Send message...` text bar at the bottom.
2. Rainbow Six Siege Operator Selection Grid (`scenes/ui/SiegeOperatorSelect.tscn`):
   - Replace the generic dark button list with large tactical operator cards arranged in 3 distinct rows:
     * **DRIVERS**: `Apex`, `Shift`, `Ghost`, `Tread`
     * **SHOOTERS**: `Viper`, `Rage`, `Spark`, `Flash`
     * **MEDICS**: `Pulse`, `Patch`, `Stim`, `Anchor`
   - Show role usage pips (`[ 0 / 4 ROUNDS ]`) and lock out classes once a player reaches 4 rounds.
   - On the right half of the screen, display the selected operator's 3D preview, passive perk description, and default weapon loadout.
3. In-Match Combined HUD Cleanup (`scenes/ui/CombinedHUD.tscn`):
   - Remove the bottom-left green health bar and delete all debug reminder text (`ESC tablet ENTER chat E weapon wall`).
   - Retain the clean, corner-anchored tactical HUD:
     * **Top-Left**: Mode shield icon, countdown timer, stacked Blue (friendly) vs. Red (enemy) score bars.
     * **Top-Right**: Active weapon silhouette, stacked mag/reserve ammo counters, secondary/melee slot badge.
     * **Center**: Clean 4-tick reticle, floating style bonus XP awards (`+XP HIGHLIGHT`), and bottom-center red kill streak pill (`5th KILL`).
     * **Zero Minimap**: Ensure no 2D radar or minimap renders on foot or in vehicles.

Run `dotnet build` to confirm zero compilation errors, launch a test instance to verify viewmodel arm positioning and menu navigation, and report the updated files.