using System;
using System.IO;
using System.Reflection;
using Godot;

public partial class VerifyMedicPerksAndPolish : Node
{
    private int _passCount = 0;

    public override void _Ready()
    {
        GD.Print("=== Starting VerifyMedicPerksAndPolish ===");

        try
        {
            TestStep1CleanAshfieldsAndAttachmentUid();
            TestStep2MedicPerks();
            TestStep3UnlimitedSidearmReserveAmmo();
            TestStep4TabletAndPauseMenuEqualKey();
            TestStep5StrictFirstPersonInVehicles();
            TestStep6CanonicalHubPath();

            GD.Print($"=== ALL MEDIC PERKS & POLISH TESTS PASSED SUCCESSFULLY! ({_passCount} assertions) ===");
            GetTree().Quit(0);
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TEST FAILED] {ex.Message}\n{ex.StackTrace}");
            GetTree().Quit(1);
        }
    }

    private void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"Assertion failed: {message}");
        }
        _passCount++;
    }

    private void TestStep1CleanAshfieldsAndAttachmentUid()
    {
        GD.Print("1. Testing Step 1: Clean Ashfields and WeaponAttachment UID...");

        string[] scenes = {
            "res://scenes/Player.tscn",
            "res://scenes/entities/Player.tscn",
            "res://scenes/entities/PlayerOperator.tscn"
        };

        foreach (var scenePath in scenes)
        {
            string globalPath = ProjectSettings.GlobalizePath(scenePath);
            Assert(File.Exists(globalPath), $"Scene file {globalPath} must exist.");
            string content = File.ReadAllText(globalPath);
            Assert(content.Contains("uid=\"uid://bu3l2dbphxlj2\""),
                $"Scene {scenePath} must reference WeaponAttachment with uid://bu3l2dbphxlj2.");
            Assert(content.Contains("NodePath(\"Health:CurrentShield\")"),
                $"Scene {scenePath} must include Health:CurrentShield in replication config.");
        }
        GD.Print("  Step 1 verified!");
    }

    private void TestStep2MedicPerks()
    {
        GD.Print("2. Testing Step 2: Medic Perks (Pulse, Anchor, Stim)...");

        // Test Pulse perk: Rapid Triage
        var pulseNode = new CharacterBody3D { Name = "PulsePlayer" };
        var pulseLoadout = new CharacterLoadout { Name = "CharacterLoadout", CharacterId = "medic_pulse" };
        pulseNode.AddChild(pulseLoadout);
        var pulseHealth = new Health { Name = "Health" };
        pulseNode.AddChild(pulseHealth);

        Assert(Mathf.IsEqualApprox(pulseHealth.EffectiveReviveHoldSeconds, Health.ReviveHoldSeconds * 0.5f),
            $"medic_pulse EffectiveReviveHoldSeconds should be {Health.ReviveHoldSeconds * 0.5f}, got {pulseHealth.EffectiveReviveHoldSeconds}");

        // Without pulse
        pulseLoadout.CharacterId = "shooter_spark";
        Assert(Mathf.IsEqualApprox(pulseHealth.EffectiveReviveHoldSeconds, Health.ReviveHoldSeconds),
            $"Standard EffectiveReviveHoldSeconds should be {Health.ReviveHoldSeconds}, got {pulseHealth.EffectiveReviveHoldSeconds}");

        // Test Anchor perk: Guardian Shell
        var anchorNode = new CharacterBody3D { Name = "AnchorPlayer" };
        AddChild(anchorNode);
        anchorNode.Position = Vector3.Zero;
        var anchorLoadout = new CharacterLoadout { Name = "CharacterLoadout", CharacterId = "medic_anchor" };
        anchorNode.AddChild(anchorLoadout);
        var anchorHealth = new Health { Name = "Health" };
        anchorNode.AddChild(anchorHealth);

        // Not near teammate/vehicle -> shield is 0
        anchorHealth._PhysicsProcess(0.016);
        Assert(anchorHealth.CurrentShield == 0.0f, $"Out of proximity Anchor shield should be 0, got {anchorHealth.CurrentShield}");

        // Spawn a vehicle within 5m
        var vehicle = new Node3D { Name = "TestVehicle" };
        AddChild(vehicle);
        vehicle.Position = new Vector3(3.0f, 0, 0);
        vehicle.AddToGroup("gameplay_vehicles");

        Assert(anchorHealth.IsNearTeammateOrVehicle(), "Should be near vehicle within 5m");
        anchorHealth._PhysicsProcess(0.016);
        Assert(anchorHealth.CurrentShield == 20.0f, $"Near vehicle Anchor shield should charge to 20 HP, got {anchorHealth.CurrentShield}");

        // Move vehicle away (> 5m) -> shield drops to 0
        vehicle.Position = new Vector3(10.0f, 0, 0);
        Assert(!anchorHealth.IsNearTeammateOrVehicle(), "Should not be near vehicle when at 10m");
        anchorHealth.CurrentShield = 20.0f;
        anchorHealth._PhysicsProcess(0.016);
        Assert(anchorHealth.CurrentShield == 0.0f, "Shield should drop to 0 when out of proximity");

        // Move vehicle back near -> charges again
        vehicle.Position = new Vector3(2.0f, 0, 0);
        anchorHealth._PhysicsProcess(0.016);
        Assert(anchorHealth.CurrentShield == 20.0f, "Shield should recharge when back in proximity");

        // Spawn attacker for legal damage claims
        var attacker = new CharacterBody3D { Name = "Attacker" };
        attacker.SetMeta(PlayerIdentity.BotMeta, 99);
        AddChild(attacker);
        var atkSwitcher = new DummyWeaponSwitcher { Name = "WeaponSwitcher" };
        var testWeapon = new WeaponData { WeaponName = "TestGun", BodyDamage = 15.0f };
        typeof(WeaponSwitcher).GetField("_currentWeapon", BindingFlags.NonPublic | BindingFlags.Instance)?
            .SetValue(atkSwitcher, testWeapon);
        attacker.AddChild(atkSwitcher);

        // Take damage: 15 damage absorbed by shield, 5 shield remaining, health intact
        float initialHealth = anchorHealth.CurrentHealth;
        anchorHealth.ReceiveDamage(15.0f, 99, 0, KillStyleBonus.DefaultBaseKillPoints);
        Assert(anchorHealth.CurrentShield == 5.0f, $"Shield should have 5 HP remaining after 15 dmg, got {anchorHealth.CurrentShield}");
        Assert(anchorHealth.CurrentHealth == initialHealth, $"Health should be untouched, got {anchorHealth.CurrentHealth}");

        // Take 10 damage: 5 absorbed by shield, 5 bleeds through to health
        testWeapon.BodyDamage = 10.0f;
        anchorHealth.ReceiveDamage(10.0f, 99, 0, KillStyleBonus.DefaultBaseKillPoints);
        Assert(anchorHealth.CurrentShield == 0.0f, $"Shield should be depleted to 0, got {anchorHealth.CurrentShield}");
        Assert(anchorHealth.CurrentHealth == initialHealth - 5.0f, $"Health should be reduced by 5, got {anchorHealth.CurrentHealth}");

        // Test combat recharge delay (ShieldRechargeDelaySeconds = 5.0s)
        anchorHealth._PhysicsProcess(2.0); // 2s elapsed
        Assert(anchorHealth.CurrentShield == 0.0f, "Shield should not recharge before delay elapses");
        anchorHealth._PhysicsProcess(3.1); // > 5s elapsed
        Assert(anchorHealth.CurrentShield == 20.0f, "Shield should recharge after delay elapses");

        // Downed anchor medic should have 0 shield and not recharge
        anchorHealth.CurrentHealth = 15.0f; // in downed band (<= 20 HP)
        Assert(anchorHealth.IsDowned, "anchorHealth should be in downed band");
        anchorHealth._PhysicsProcess(10.0); // let 10s pass while near vehicle
        Assert(anchorHealth.CurrentShield == 0.0f, "Shield must remain 0 while downed even in proximity to vehicle");

        // Revive anchor medic with teammate - shield must NOT immediately pop to 20; 5s combat delay enforced
        var teammateMedic = new CharacterBody3D { Name = "TeammateMedic" };
        teammateMedic.SetMeta(PlayerIdentity.BotMeta, 88);
        teammateMedic.Position = new Vector3(1.0f, 0, 0);
        AddChild(teammateMedic);
        var teammateHealth = new Health { Name = "Health" };
        teammateMedic.AddChild(teammateHealth);

        anchorNode.SetMeta(PlayerIdentity.BotMeta, 77);
        MatchManager.Instance?.SetPeerTeam(77, 0);
        MatchManager.Instance?.SetPeerTeam(88, 0);

        anchorHealth.ReceiveRevive(PlayerIdentity.IdOf(teammateMedic), isStim: false);
        Assert(!anchorHealth.IsDowned, "anchorHealth should no longer be downed after revive");
        Assert(anchorHealth.CurrentShield == 0.0f, "Shield must be 0 immediately upon revive");
        anchorHealth._PhysicsProcess(2.0); // 2s elapsed
        Assert(anchorHealth.CurrentShield == 0.0f, "Shield must not recharge before 5s combat delay elapses after revive");
        anchorHealth._PhysicsProcess(3.1); // > 5s elapsed
        Assert(anchorHealth.CurrentShield == 20.0f, "Shield must recharge after 5s combat delay elapses post-revive");
        teammateMedic.QueueFree();

        // Downed player weapon interaction lockout
        anchorHealth.CurrentHealth = 15.0f;
        var downedWs = new WeaponSwitcher();
        downedWs.SetProcess(false);
        downedWs.SetPhysicsProcess(false);
        typeof(WeaponSwitcher).GetField("_health", BindingFlags.NonPublic | BindingFlags.Instance)?
            .SetValue(downedWs, anchorHealth);
        typeof(WeaponSwitcher).GetField("_player", BindingFlags.NonPublic | BindingFlags.Instance)?
            .SetValue(downedWs, anchorNode);
        Assert(!downedWs.TryEquip(0), "Downed player must not be able to select weapons");
        downedWs.Free();

        // Dead / downed player cannot enter vehicle
        var gameVeh = new GameVehicle();
        AddChild(gameVeh);
        gameVeh.GlobalPosition = anchorNode.GlobalPosition;
        Assert(!gameVeh.TryEnter((int)PlayerIdentity.IdOf(anchorNode)), "Downed player must not be able to enter vehicle");
        anchorHealth.CurrentHealth = 0.0f;
        Assert(anchorHealth.IsDead, "anchorHealth must be dead");
        Assert(!gameVeh.TryEnter((int)PlayerIdentity.IdOf(anchorNode)), "Dead player must not be able to enter vehicle");
        gameVeh.QueueFree();

        attacker.QueueFree();

        // Test Stim perk: Adrenaline Boost
        var stimMedic = new CharacterBody3D { Name = "StimMedic" };
        AddChild(stimMedic);
        stimMedic.Position = Vector3.Zero;
        stimMedic.SetMeta(PlayerIdentity.BotMeta, 2);
        var stimLoadout = new CharacterLoadout { Name = "CharacterLoadout", CharacterId = "medic_stim" };
        stimMedic.AddChild(stimLoadout);
        var stimHealth = new Health { Name = "Health" };
        stimMedic.AddChild(stimHealth);

        var downedPatient = new CharacterBody3D { Name = "DownedPatient" };
        AddChild(downedPatient);
        downedPatient.Position = new Vector3(1.0f, 0, 0);
        downedPatient.SetMeta(PlayerIdentity.BotMeta, 3);
        var patientHealth = new Health { Name = "Health" };
        downedPatient.AddChild(patientHealth);
        patientHealth.CurrentHealth = 15.0f; // in downed band

        stimHealth.ApplyAdrenalineBoost(5.0f);
        Assert(stimHealth.IsAdrenalineActive, "Medic should have active adrenaline boost");
        Assert(stimHealth.AdrenalineTimeRemaining <= 5.0f && stimHealth.AdrenalineTimeRemaining > 4.9f,
            $"Adrenaline time should be ~5s, got {stimHealth.AdrenalineTimeRemaining}");

        MatchManager.Instance?.SetPeerTeam(2, 0);
        MatchManager.Instance?.SetPeerTeam(3, 0);

        patientHealth.ReceiveRevive(PlayerIdentity.IdOf(stimMedic), isStim: true);
        Assert(patientHealth.IsAdrenalineActive, "Revived teammate should have active adrenaline boost when revived by stim");

        // Verify PlayerMovement and WeaponSwitcher adrenaline multipliers
        Assert(PlayerMovement.AdrenalineSpeedMultiplier == 1.20f, "PlayerMovement.AdrenalineSpeedMultiplier must be 1.20f");
        Assert(WeaponSwitcher.AdrenalineReloadSpeedMultiplier == 1.20f, "WeaponSwitcher.AdrenalineReloadSpeedMultiplier must be 1.20f");

        var testWs = new WeaponSwitcher();
        testWs.SetProcess(false);
        testWs.SetPhysicsProcess(false);
        typeof(WeaponSwitcher).GetField("_health", BindingFlags.NonPublic | BindingFlags.Instance)?
            .SetValue(testWs, stimHealth);
        float reloadMult = testWs.GetEffectiveReloadSpeedMultiplier();
        Assert(Mathf.IsEqualApprox(reloadMult, 1.20f),
            $"Effective reload speed multiplier should include 1.20x adrenaline boost, got {reloadMult}");

        // Cleanup
        anchorNode.QueueFree();
        vehicle.QueueFree();
        stimMedic.QueueFree();
        downedPatient.QueueFree();
        testWs.Free();
        pulseNode.Free();

        GD.Print("  Step 2 verified!");
    }

    private void TestStep3UnlimitedSidearmReserveAmmo()
    {
        GD.Print("3. Testing Step 3: Unlimited Sidearm Reserve Ammo...");

        var glock = new WeaponData { WeaponName = "Glock 19", WeaponClass = "Pistol", MagazineSize = 17, MaxReserveAmmo = 68 };
        var fn502 = new WeaponData { WeaponName = "FN 502", WeaponClass = "Pistol", MagazineSize = 15, MaxReserveAmmo = 60 };
        var deagle = new WeaponData { WeaponName = "Desert Eagle", WeaponClass = "Sidearm", MagazineSize = 7, MaxReserveAmmo = 28 };
        var ak47 = new WeaponData { WeaponName = "AK-47", WeaponClass = "Assault Rifle", MagazineSize = 30, MaxReserveAmmo = 120 };
        var shotgun = new WeaponData { WeaponName = "Shotgun", WeaponClass = "Shotgun", MagazineSize = 8, MaxReserveAmmo = 32 };

        Assert(WeaponSwitcher.IsSidearm(glock), "Glock 19 must be recognized as sidearm");
        Assert(WeaponSwitcher.IsSidearm(fn502), "FN 502 must be recognized as sidearm");
        Assert(WeaponSwitcher.IsSidearm(deagle), "Desert Eagle must be recognized as sidearm");
        Assert(!WeaponSwitcher.IsSidearm(ak47), "AK-47 must NOT be recognized as sidearm");
        Assert(!WeaponSwitcher.IsSidearm(shotgun), "Shotgun must NOT be recognized as sidearm");

        // Test FinishReload for sidearm vs primary via reflection
        var ws = new WeaponSwitcher();
        ws.SetProcess(false);
        ws.SetPhysicsProcess(false);

        var curWeaponField = typeof(WeaponSwitcher).GetField("_currentWeapon", BindingFlags.NonPublic | BindingFlags.Instance);
        var curAmmoField = typeof(WeaponSwitcher).GetField("_currentAmmo", BindingFlags.NonPublic | BindingFlags.Instance);
        var curReserveField = typeof(WeaponSwitcher).GetField("_currentReserve", BindingFlags.NonPublic | BindingFlags.Instance);
        var finishReloadMethod = typeof(WeaponSwitcher).GetMethod("FinishReload", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert(curWeaponField != null && curAmmoField != null && curReserveField != null && finishReloadMethod != null,
            "WeaponSwitcher reflection fields and methods must exist");

        // 1. Sidearm reload with 0 reserve ammo
        curWeaponField!.SetValue(ws, glock);
        curAmmoField!.SetValue(ws, 0);
        curReserveField!.SetValue(ws, 0);

        finishReloadMethod!.Invoke(ws, null);

        int glockAmmo = (int)curAmmoField.GetValue(ws)!;
        int glockReserve = (int)curReserveField.GetValue(ws)!;
        Assert(glockAmmo == glock.MagazineSize, $"Sidearm with 0 reserve should reload full mag ({glock.MagazineSize}), got {glockAmmo}");
        Assert(glockReserve == 0, $"Sidearm reserve should remain untouched at 0, got {glockReserve}");

        // 2. Primary weapon reload with 10 reserve ammo and 30 mag size
        curWeaponField.SetValue(ws, ak47);
        curAmmoField.SetValue(ws, 0);
        curReserveField.SetValue(ws, 10);

        finishReloadMethod.Invoke(ws, null);

        int akAmmo = (int)curAmmoField.GetValue(ws)!;
        int akReserve = (int)curReserveField.GetValue(ws)!;
        Assert(akAmmo == 10, $"Primary weapon should only draw available reserve (10), got {akAmmo}");
        Assert(akReserve == 0, $"Primary weapon reserve should be decremented to 0, got {akReserve}");

        ws.Free();
        GD.Print("  Step 3 verified!");
    }

    private void TestStep4TabletAndPauseMenuEqualKey()
    {
        GD.Print("4. Testing Step 4: HubTablet TypingOnScreen and PauseMenu Equal Key...");

        var tablet = new HubTablet();
        bool typing = tablet.TypingOnScreen();
        Assert(!typing, "TypingOnScreen should return false when no input box has focus");
        tablet.Free();

        // Verify HubTablet.cs contains Key.Equal check
        string tabletSource = File.ReadAllText(ProjectSettings.GlobalizePath("res://scripts/ui/HubTablet.cs"));
        Assert(tabletSource.Contains("Key.Equal"), "HubTablet.cs must handle Key.Equal");
        Assert(tabletSource.Contains("public bool TypingOnScreen()"), "HubTablet.TypingOnScreen must be public");

        // Test runtime viewport focus in TypingOnScreen
        var lineEdit = new LineEdit { Name = "TestLineEdit" };
        AddChild(lineEdit);
        lineEdit.GrabFocus();
        var tabInstance = new HubTablet();
        AddChild(tabInstance);
        Assert(tabInstance.TypingOnScreen(), "TypingOnScreen must return true when LineEdit on viewport has focus");
        lineEdit.ReleaseFocus();
        Assert(!tabInstance.TypingOnScreen(), "TypingOnScreen must return false after focus released");
        tabInstance.QueueFree();
        lineEdit.QueueFree();

        GD.Print("  Step 4 verified!");
    }

    private void TestStep5StrictFirstPersonInVehicles()
    {
        GD.Print("5. Testing Step 5: Strict First-Person in Vehicles...");

        // Verify GameVehicle.cs
        string vehicleSource = File.ReadAllText(ProjectSettings.GlobalizePath("res://scripts/vehicles/GameVehicle.cs"));
        Assert(!vehicleSource.Contains("Key.C"), "GameVehicle.cs must not poll Key.C");
        Assert(!vehicleSource.Contains("_cameraKeyWasDown"), "GameVehicle.cs must not contain _cameraKeyWasDown");
        Assert(vehicleSource.Contains("DRIVER  •  E EXIT"), "GameVehicle.cs prompt must be 'DRIVER  •  E EXIT'");
        Assert(vehicleSource.Contains("player.SetVehicleCameraView(false, this);"),
            "ToggleDriverCamera must enforce first-person view");

        // Verify TouchControls.cs
        string touchSource = File.ReadAllText(ProjectSettings.GlobalizePath("res://scripts/player/TouchControls.cs"));
        Assert(!touchSource.Contains("CAM ◉"), "TouchControls.cs must not contain 'CAM ◉' vehicle button");
        Assert(touchSource.Contains("100f"), "TouchControls.cs exit button must be anchored at 100f");

        // Verify PlayerMovement.cs
        string movementSource = File.ReadAllText(ProjectSettings.GlobalizePath("res://scripts/player/PlayerMovement.cs"));
        Assert(movementSource.Contains("IsThirdPersonVehicleView = false;"),
            "PlayerMovement.cs must enforce IsThirdPersonVehicleView = false");

        GD.Print("  Step 5 verified!");
    }

    private void TestStep6CanonicalHubPath()
    {
        GD.Print("6. Testing Step 6: Canonical Hub Path...");
        Assert(HubController.HubScenePath == "res://scenes/hub/WarehouseHub.tscn",
            $"HubController.HubScenePath must be 'res://scenes/hub/WarehouseHub.tscn', got '{HubController.HubScenePath}'");

        string hubSource = File.ReadAllText(ProjectSettings.GlobalizePath("res://scripts/world/HubController.cs"));
        Assert(hubSource.Contains("\"res://scenes/hub/WarehouseHub.tscn\""),
            "HubController.cs must set HubScenePath to WarehouseHub.tscn");

        GD.Print("  Step 6 verified!");
    }
}

public partial class DummyWeaponSwitcher : WeaponSwitcher
{
    public override void _Ready() { }
    public override void _Process(double delta) { }
    public override void _PhysicsProcess(double delta) { }
}
