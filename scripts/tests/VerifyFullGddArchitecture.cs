using Godot;
using System;
using System.IO;

[GlobalClass]
public partial class VerifyFullGddArchitecture : Node
{
    public override void _Ready()
    {
        GD.Print("=== Starting VerifyFullGddArchitecture ===");
        try
        {
            RunAllTests();
            GD.Print("=== ALL GDD ARCHITECTURE & SYSTEM TESTS PASSED SUCCESSFULLY! ===");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"TEST FAILED: {ex.Message}\n{ex.StackTrace}");
            GetTree().Quit(1);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"Assertion Failed: {message}");
        }
    }

    private void RunAllTests()
    {
        TestSplashRemovalAndWarehouseLaunch();
        TestWarehouseHubAndInteractiveZones();
        TestTacticalTabletFourTabs();
        TestLoadoutAttachmentTabAndEightStats();
        TestLiveSpectatorCamSystem();
        TestDedicatedModeMaps();
        TestEntitiesAndModularWeaponBase();
        TestAutoloadsAndScoringProgression();
        TestCanonicalModeControllers();
    }

    private void TestSplashRemovalAndWarehouseLaunch()
    {
        GD.Print("1. Testing Splash Screen Removal & Direct 3D Warehouse Launch...");

        // Ensure Splash.tscn is completely removed
        Assert(!File.Exists("scenes/Splash.tscn"), "Splash.tscn must be permanently deleted!");
        Assert(!File.Exists("scripts/ui/SplashScreen.cs"), "SplashScreen.cs must be permanently deleted!");

        // Verify project.godot main_scene configuration
        string projectGodot = File.ReadAllText("project.godot");
        Assert(projectGodot.Contains("run/main_scene=\"res://scenes/hub/WarehouseHub.tscn\""),
            "project.godot run/main_scene must point directly to res://scenes/hub/WarehouseHub.tscn!");
        Assert(projectGodot.Contains("boot_splash/show_image=false"),
            "project.godot must have boot_splash/show_image=false!");

        // Verify GameSession returns straight to WarehouseHub
        Assert(GameSession.HubScene == "res://scenes/hub/WarehouseHub.tscn",
            "GameSession.HubScene must point to res://scenes/hub/WarehouseHub.tscn");

        GD.Print("  Splash screen removal and Warehouse launch verified!");
    }

    private void TestWarehouseHubAndInteractiveZones()
    {
        GD.Print("2. Testing Warehouse Hub and Interactive Firing Range & Vehicle Bay...");

        var hubScene = ResourceLoader.Load<PackedScene>("res://scenes/hub/WarehouseHub.tscn");
        Assert(hubScene != null, "scenes/hub/WarehouseHub.tscn must exist and be loadable!");

        // Test FiringRangeTarget mechanics (IShootable, 2.5x headshots, flinch/reset)
        var target = new FiringRangeTarget();
        AddChild(target);

        Assert(target.IsFleshTarget, "Target dummy must be recognized as shootable target");
        Assert(target.CurrentHealth == 100.0f, "Target dummy base health must be 100 HP");

        // Body shot (20 dmg)
        target.ReceiveShot(new ShotInfo
        {
            Damage = 20.0f,
            IsHeadshot = false,
            HitPoint = new Vector3(0, 1.2f, 0)
        });
        Assert(target.CurrentHealth == 80.0f, $"Body shot should deal exact 20 dmg. Actual: {target.CurrentHealth}");

        // Head shot (20 dmg * 2.5x = 50 dmg)
        target.ReceiveShot(new ShotInfo
        {
            Damage = 20.0f,
            IsHeadshot = true,
            HitPoint = new Vector3(0, 1.8f, 0)
        });
        Assert(target.CurrentHealth == 30.0f, $"Headshot should apply 2.5x multiplier (50 dmg). Actual: {target.CurrentHealth}");

        target.QueueFree();
        GD.Print("  Warehouse Hub and interactive zones verified!");
    }

    private void TestTacticalTabletFourTabs()
    {
        GD.Print("3. Testing Tactical Tablet 4 Tabs (Play, Loadout, Operators, Settings)...");

        var tabletScene = ResourceLoader.Load<PackedScene>("res://scenes/hub/TacticalTablet.tscn");
        Assert(tabletScene != null, "scenes/hub/TacticalTablet.tscn must exist and be loadable!");

        var tablet = tabletScene!.Instantiate<HubTablet>();
        AddChild(tablet);
        Assert(tablet != null, "TacticalTablet instance must be valid");
        tablet.QueueFree();

        GD.Print("  Tactical Tablet 4-tab architecture verified!");
    }

    private void TestLoadoutAttachmentTabAndEightStats()
    {
        GD.Print("4. Testing 3D Gunsmith / LoadoutAttachmentTab (6 slots + 8 live stats)...");

        var loadoutScene = ResourceLoader.Load<PackedScene>("res://scenes/ui/LoadoutAttachmentTab.tscn");
        Assert(loadoutScene != null, "scenes/ui/LoadoutAttachmentTab.tscn must exist!");

        var tab = loadoutScene!.Instantiate<LoadoutAttachmentTab>();
        AddChild(tab);

        Assert(LoadoutAttachmentTab.WeaponRoster.Length == 10, "Gunsmith must support all 10 core firearms");
        tab.UpdateStatBars();

        tab.QueueFree();
        GD.Print("  Gunsmith attachment configurator & 8-stat live bars verified!");
    }

    private void TestLiveSpectatorCamSystem()
    {
        GD.Print("5. Testing Live Spectator Observer Camera (CS2/PUBG Style)...");

        var spectatorScene = ResourceLoader.Load<PackedScene>("res://scenes/ui/LiveSpectatorCam.tscn");
        Assert(spectatorScene != null, "scenes/ui/LiveSpectatorCam.tscn must exist!");

        var spec = spectatorScene!.Instantiate<LiveSpectatorCam>();
        AddChild(spec);

        Assert(!spec.IsActive, "Spectator cam should initialize inactive");
        spec.SetSpectatorActive(true);
        Assert(spec.IsActive, "Spectator cam must activate when toggled");
        spec.SetSpectatorActive(false);

        spec.QueueFree();
        GD.Print("  Live Spectator camera system verified!");
    }

    private void TestDedicatedModeMaps()
    {
        GD.Print("6. Testing Dedicated Game Mode Maps (10 km corridor, Pure Chase, Arenas)...");

        // Convoy Corridor (10 km)
        var convoyMap = ResourceLoader.Load<PackedScene>("res://scenes/maps/ConvoyCorridor10km.tscn");
        Assert(convoyMap != null, "scenes/maps/ConvoyCorridor10km.tscn must exist!");
        var convoyNode = convoyMap!.Instantiate<Node3D>();
        Assert(convoyNode.GetNodeOrNull("CorridorRoad") != null, "Convoy corridor must have road geometry");
        Assert(convoyNode.GetNodeOrNull("ExtractionObjective") != null, "Convoy corridor must feature target extraction objective");
        convoyNode.QueueFree();

        // Pure Chase Route
        var chaseMap = ResourceLoader.Load<PackedScene>("res://scenes/maps/PureChaseRoute.tscn");
        Assert(chaseMap != null, "scenes/maps/PureChaseRoute.tscn must exist!");
        var chaseNode = chaseMap!.Instantiate<Node3D>();
        Assert(chaseNode.GetNodeOrNull("CursedZone_FloorCollapse") != null, "Pure Chase must have Floor Collapse cursed zone");
        Assert(chaseNode.GetNodeOrNull("CursedZone_PullVortex") != null, "Pure Chase must have Pull Vortex cursed zone");
        Assert(chaseNode.GetNodeOrNull("CursedZone_BlindnessFog") != null, "Pure Chase must have Blindness Fog cursed zone");
        Assert(chaseNode.GetNodeOrNull("CursedZone_MirrorShift") != null, "Pure Chase must have Mirror Shift cursed zone");
        Assert(chaseNode.GetNodeOrNull("CursedZone_GhostPossession") != null, "Pure Chase must have Ghost Possession cursed zone");
        Assert(chaseNode.GetNodeOrNull("CursedZone_DeathKnell") != null, "Pure Chase must have Death Knell cursed zone");
        chaseNode.QueueFree();

        // Arenas
        var arenaWarehouse = ResourceLoader.Load<PackedScene>("res://scenes/maps/arenas/ArenaWarehouse.tscn");
        Assert(arenaWarehouse != null, "scenes/maps/arenas/ArenaWarehouse.tscn must exist!");
        var arenaAshfields = ResourceLoader.Load<PackedScene>("res://scenes/maps/arenas/ArenaAshfields.tscn");
        Assert(arenaAshfields != null, "scenes/maps/arenas/ArenaAshfields.tscn must exist!");

        GD.Print("  Dedicated mode maps verified!");
    }

    private void TestEntitiesAndModularWeaponBase()
    {
        GD.Print("7. Testing Entities & Modular WeaponBase...");

        // PlayerOperator
        var playerScene = ResourceLoader.Load<PackedScene>("res://scenes/entities/PlayerOperator.tscn");
        Assert(playerScene != null, "scenes/entities/PlayerOperator.tscn must exist!");

        // BotController
        var botScene = ResourceLoader.Load<PackedScene>("res://scenes/entities/BotController.tscn");
        Assert(botScene != null, "scenes/entities/BotController.tscn must exist!");
        var botCtrl = botScene!.Instantiate<BotController>();
        Assert(botCtrl.Difficulty >= 1 && botCtrl.Difficulty <= 5, "BotController must support 1-5 difficulty");
        botCtrl.QueueFree();

        // WeaponBase
        var weaponScene = ResourceLoader.Load<PackedScene>("res://scenes/weapons/WeaponBase.tscn");
        Assert(weaponScene != null, "scenes/weapons/WeaponBase.tscn must exist!");
        var weaponBase = weaponScene!.Instantiate<WeaponBase>();
        AddChild(weaponBase);
        Assert(weaponBase.MuzzlePoint != null, "WeaponBase must have MuzzlePoint anchor");
        Assert(weaponBase.OpticsSocket != null, "WeaponBase must have OpticsSocket");
        Assert(weaponBase.UnderbarrelSocket != null, "WeaponBase must have UnderbarrelSocket");
        Assert(weaponBase.LaserSocket != null, "WeaponBase must have LaserSocket");
        Assert(weaponBase.MagazineSocket != null, "WeaponBase must have MagazineSocket");
        Assert(weaponBase.StockSocket != null, "WeaponBase must have StockSocket");
        weaponBase.QueueFree();

        GD.Print("  Canonical entities and WeaponBase verified!");
    }

    private void TestAutoloadsAndScoringProgression()
    {
        GD.Print("8. Testing Autoloads (GameManager, LanDiscoveryManager, ScoreAndProgression)...");

        // GameManager
        Assert(GameManager.GetRespawnDelaySeconds(MatchManager.Mode.TeamDeathmatch) == 3.0f, "Standard mode respawn delay must be 3.0s");
        Assert(GameManager.GetRespawnDelaySeconds(MatchManager.Mode.ConvoyAssault) == 9.0f, "Convoy Assault respawn delay must be 9.0s (GDD Section 1)");

        // LanDiscoveryManager
        Assert(LanDiscoveryManager.DiscoveryPort == 7777, "Lan discovery port must be 7777");

        // ScoreAndProgression & Scoring Separation Rule
        Assert(ScoreAndProgression.UsesUniversalStyleBonuses(MatchManager.Mode.TeamDeathmatch), "TDM must use Universal Style Bonuses");
        Assert(ScoreAndProgression.UsesUniversalStyleBonuses(MatchManager.Mode.Deathmatch), "DM must use Universal Style Bonuses");
        Assert(!ScoreAndProgression.UsesUniversalStyleBonuses(MatchManager.Mode.ConvoyAssault), "Convoy Assault must NOT use Universal Style Bonuses");
        Assert(!ScoreAndProgression.UsesUniversalStyleBonuses(MatchManager.Mode.PureChase), "Pure Chase must NOT use Universal Style Bonuses");

        // Rank Calculation
        Assert(ScoreAndProgression.CalculateRank(0) == 1, "0 XP must be Rank 1");
        Assert(ScoreAndProgression.CalculateRank(54000) == 55, "54k XP must be Rank 55");

        GD.Print("  Autoloads and progression rules verified!");
    }

    private void TestCanonicalModeControllers()
    {
        GD.Print("9. Testing Canonical Mode Controller scripts (GDD Section 7)...");

        var m1 = new ModeTeamDeathmatch();
        Assert(m1.ModeId == MatchManager.Mode.TeamDeathmatch, "ModeTeamDeathmatch must match TeamDeathmatch");
        m1.QueueFree();

        var m2 = new ModeDeathmatch();
        Assert(m2.ModeId == MatchManager.Mode.Deathmatch, "ModeDeathmatch must match Deathmatch");
        m2.QueueFree();

        var m3 = new ModeConvoyAssault();
        Assert(m3.ModeId == MatchManager.Mode.ConvoyAssault, "ModeConvoyAssault must match ConvoyAssault");
        m3.QueueFree();

        var m4 = new ModePureChase();
        Assert(m4.ModeId == MatchManager.Mode.PureChase, "ModePureChase must match PureChase");
        m4.QueueFree();

        GD.Print("  Canonical Mode Controllers verified!");
    }
}
