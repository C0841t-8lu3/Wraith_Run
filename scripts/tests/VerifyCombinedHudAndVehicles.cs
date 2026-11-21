using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class VerifyCombinedHudAndVehicles : Node
{
	public override void _Ready()
	{
		GD.Print("=== Starting VerifyCombinedHudAndVehicles ===");
		try
		{
			RunAllTests();
			GD.Print("=== ALL COMBINED HUD, VEHICLE & OPERATOR TESTS PASSED! ===");
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

	private static void AssertApprox(float actual, float expected, string message, float tolerance = 0.001f)
	{
		if (Mathf.Abs(actual - expected) > tolerance)
		{
			throw new Exception($"Assertion Failed: {message}. Expected: {expected}, Actual: {actual}");
		}
	}

	private void RunAllTests()
	{
		TestKillStyleBonusTable();
		TestRespawnDelays();
		TestVehicleDamageController();
		TestCockpitDashboard3D();
		TestGameVehicleEntities();
		Test12OperatorRosterAndPerks();
		TestOperatorRotationManagerRoleLock();
		TestSiegeOperatorSelectScene();
		TestCombinedFpsHud();
	}

	private void TestKillStyleBonusTable()
	{
		GD.Print("Testing KillStyleBonus table values against GDD Section 5.1...");
		Assert(KillStyleBonus.HeadshotBonus == 150, $"HeadshotBonus should be 150, got {KillStyleBonus.HeadshotBonus}");
		Assert(KillStyleBonus.HipFireBonus == 125, $"HipFireBonus should be 125, got {KillStyleBonus.HipFireBonus}");
		Assert(KillStyleBonus.NoScope360Bonus == 175, $"NoScope360Bonus should be 175, got {KillStyleBonus.NoScope360Bonus}");
		Assert(KillStyleBonus.VergeOfDeathBonus == 120, $"VergeOfDeathBonus should be 120, got {KillStyleBonus.VergeOfDeathBonus}");
		Assert(KillStyleBonus.MultiKillBonus == 200, $"MultiKillBonus should be 200, got {KillStyleBonus.MultiKillBonus}");
		Assert(KillStyleBonus.AssistBonusMinimum == 25, $"AssistBonusMinimum should be 25, got {KillStyleBonus.AssistBonusMinimum}");
	}

	private void TestRespawnDelays()
	{
		GD.Print("Testing respawn delays in Health (3s standard vs 9s Convoy Assault)...");
		AssertApprox(Health.StandardRespawnDelaySeconds, 3.0f, "StandardRespawnDelaySeconds must be 3.0s");
		AssertApprox(Health.ConvoyAssaultRespawnDelaySeconds, 9.0f, "ConvoyAssaultRespawnDelaySeconds must be 9.0s");

		var healthNode = new Health();

		if (MatchManager.Instance != null)
		{
			MatchManager.Instance.GameMode = MatchManager.Mode.TeamDeathmatch;
			AssertApprox(healthNode.EffectiveRespawnDelay, 3.0f, "EffectiveRespawnDelay for TDM must be 3.0s");

			MatchManager.Instance.GameMode = MatchManager.Mode.FreeForAll;
			AssertApprox(healthNode.EffectiveRespawnDelay, 3.0f, "EffectiveRespawnDelay for FFA must be 3.0s");

			MatchManager.Instance.GameMode = MatchManager.Mode.ConvoyAssault;
			AssertApprox(healthNode.EffectiveRespawnDelay, 9.0f, "EffectiveRespawnDelay for Convoy Assault must be 9.0s");
		}
		else
		{
			AssertApprox(healthNode.EffectiveRespawnDelay, 3.0f, "EffectiveRespawnDelay fallback must be 3.0s");
		}

		healthNode.Free();
	}

	private void TestVehicleDamageController()
	{
		GD.Print("Testing 100-HP Vehicle Damage Controller (recoverable disable vs Bazooka instant wreck)...");
		var vdc = new VehicleDamageController();
		AddChild(vdc);

		// Initial conditions
		AssertApprox(vdc.MaxHealth, 100.0f, "MaxHealth should be 100.0f");
		AssertApprox(vdc.CurrentHealth, 100.0f, "CurrentHealth should start at 100.0f");
		Assert(vdc.CurrentState == VehicleDamageController.DamageState.Operational, "Starting state should be Operational");
		Assert(vdc.IsOperational, "IsOperational should be true");
		Assert(vdc.CanDrive, "CanDrive should be true initially");
		Assert(vdc.FlatTireCount == 0, "FlatTireCount should start at 0");

		// 1. Small arms partial damage
		vdc.ApplyDamage(40.0f, isHeavy: false, hitTire: false);
		AssertApprox(vdc.CurrentHealth, 60.0f, "Health should be 60.0f after 40 damage");
		Assert(vdc.IsOperational, "Vehicle should still be Operational at 60 HP");
		Assert(vdc.CanDrive, "Vehicle can still drive at 60 HP");

		// 2. Small arms reduces HP to 0 -> Recoverable Disable
		vdc.ApplyDamage(60.0f, isHeavy: false, hitTire: false);
		AssertApprox(vdc.CurrentHealth, 0.0f, "Health should be 0.0f");
		Assert(vdc.CurrentState == VehicleDamageController.DamageState.Disabled, "Vehicle must transition to Disabled at 0 HP");
		Assert(vdc.IsDisabled, "IsDisabled should be true");
		Assert(!vdc.CanDrive, "CanDrive must be false when Disabled");
		Assert(vdc.CanRepair, "CanRepair must be true when Disabled");
		AssertApprox(vdc.RepairWindowRemaining, VehicleDamageController.DefaultRepairWindowSeconds, "Repair window should start at 20s");

		// 3. Repair restores vehicle from Disabled to Operational
		vdc.Repair(35.0f);
		AssertApprox(vdc.CurrentHealth, 35.0f, "Health should be 35.0f after repair");
		Assert(vdc.CurrentState == VehicleDamageController.DamageState.Operational, "Vehicle must return to Operational after repair");
		Assert(vdc.IsOperational, "IsOperational should be true after repair");
		Assert(vdc.CanDrive, "CanDrive must be restored after repair");

		// 4. Tire blowout and Spike Strip
		vdc.BlowTire(1);
		Assert(vdc.FlatTireCount == 1, "FlatTireCount should be 1 after blowing tire 1");
		Assert(!vdc.TiresIntact[1], "Tire 1 should not be intact");
		Assert(vdc.TiresIntact[0], "Tire 0 should still be intact");

		vdc.HitSpikeStrip();
		Assert(vdc.FlatTireCount == 4, "HitSpikeStrip must blow out all 4 tires");
		Assert(vdc.IsDisabled, "HitSpikeStrip must disable the vehicle");
		AssertApprox(vdc.CurrentHealth, 0.0f, "HitSpikeStrip must set health to 0 HP");

		// Repair restores tires and operational state
		vdc.Repair(50.0f);
		Assert(vdc.FlatTireCount == 0, "Repairing from disabled state must restore all tires");
		Assert(vdc.IsOperational, "Vehicle must be operational again");

		// 5. Bazooka / Heavy Weapon Instant Wreck
		vdc.Reset();
		AssertApprox(vdc.CurrentHealth, 100.0f, "Health should be reset to 100.0f");
		Assert(vdc.IsOperational, "Vehicle should be operational after reset");

		// Heavy weapon direct hit (Bazooka)
		vdc.ApplyDamage(100.0f, isHeavy: true, hitTire: false);
		Assert(vdc.CurrentState == VehicleDamageController.DamageState.Wrecked, "Heavy weapon must instantly wreck vehicle");
		Assert(vdc.IsWrecked, "IsWrecked must be true");
		Assert(!vdc.CanDrive, "Wrecked vehicle cannot drive");
		Assert(!vdc.CanRepair, "Wrecked vehicle has NO repair path (CanRepair must be false)");
		AssertApprox(vdc.WreckDurationRemaining, VehicleDamageController.WreckMeshDurationSeconds, "Wreck duration should be 30s");

		// Attempting to repair a wrecked vehicle must fail
		vdc.Repair(50.0f);
		Assert(vdc.IsWrecked, "Vehicle must remain Wrecked after repair attempt");
		AssertApprox(vdc.CurrentHealth, 0.0f, "Health must remain 0.0f for wrecked vehicle");

		vdc.QueueFree();
	}

	private void TestCockpitDashboard3D()
	{
		GD.Print("Testing CockpitDashboard3D gauge cluster rendering...");
		var dash = new CockpitDashboard3D();
		AddChild(dash);

		// Check backing plate and labels created in _Ready
		var backing = dash.GetNodeOrNull<MeshInstance3D>("BackingPlate");
		Assert(backing != null, "CockpitDashboard3D must have BackingPlate MeshInstance3D");

		// Test updating gauge states with fractional boost
		dash.UpdateDashboard(
			speedKmh: 85.4f,
			rpm: 4200.0f,
			currentHp: 100.0f,
			maxHp: 100.0f,
			state: VehicleDamageController.DamageState.Operational,
			tires: new[] { true, true, true, true },
			boostPercent: 0.85f
		);
		var boostLbl = dash.FindChild("BoostText", true, false) as Label3D;
		Assert(boostLbl != null, "CockpitDashboard3D must have BoostText Label3D");
		Assert(boostLbl!.Text.Contains("85%"), $"Boost label should show 85% for 0.85f, got '{boostLbl.Text}'");

		// Test updating gauge states with percentage boost
		dash.UpdateDashboard(
			speedKmh: 110.0f,
			rpm: 5500.0f,
			currentHp: 100.0f,
			maxHp: 100.0f,
			state: VehicleDamageController.DamageState.Operational,
			tires: new[] { true, true, true, true },
			boostPercent: 100.0f
		);
		Assert(boostLbl.Text.Contains("100%"), $"Boost label should show 100% for 100.0f, got '{boostLbl.Text}'");

		// Test updating to disabled state
		dash.UpdateDashboard(
			speedKmh: 0.0f,
			rpm: 800.0f,
			currentHp: 0.0f,
			maxHp: 100.0f,
			state: VehicleDamageController.DamageState.Disabled,
			tires: new[] { false, true, true, false },
			boostPercent: 0.0f
		);

		// Test updating to wrecked state
		dash.UpdateDashboard(
			speedKmh: 0.0f,
			rpm: 0.0f,
			currentHp: 0.0f,
			maxHp: 100.0f,
			state: VehicleDamageController.DamageState.Wrecked,
			tires: new[] { false, false, false, false },
			boostPercent: 0.0f
		);

		dash.QueueFree();
	}

	private void TestGameVehicleEntities()
	{
		GD.Print("Testing GameVehicle scenes (Raider Buggy and STV)...");
		var buggyScene = ResourceLoader.Load<PackedScene>("res://scenes/entities/RaiderBuggy_2Seat.tscn");
		Assert(buggyScene != null, "RaiderBuggy_2Seat.tscn failed to load!");

		var buggy = buggyScene!.Instantiate<GameVehicle>();
		AddChild(buggy);

		Assert(buggy.DamageController != null, "GameVehicle must have VehicleDamageController");
		Assert(buggy.CockpitDashboard != null, "GameVehicle must have CockpitDashboard3D");
		Assert(buggy is IShootable, "GameVehicle must implement IShootable");
		Assert(buggy is IExplosionReceiver, "GameVehicle must implement IExplosionReceiver");

		// Test IShootable receiving small arms shot
		IShootable shootable = buggy;
		Assert(!shootable.IsHeadHit(Vector3.Zero), "Vehicles do not have headshots");
		Assert(!shootable.IsFleshTarget, "Vehicles are not flesh targets");

		shootable.ReceiveShot(new ShotInfo
		{
			Damage = 25.0f,
			WeaponClass = "Rifle",
			HitPoint = buggy.GlobalPosition + new Vector3(0, 1.0f, 0),
			AttackerPeerId = 1
		});
		AssertApprox(buggy.DamageController.CurrentHealth, 75.0f, "Buggy health should be 75 after 25 small arms damage");

		// Test IExplosionReceiver receiving heavy blast
		IExplosionReceiver explosionReceiver = buggy;
		explosionReceiver.ReceiveBlast(100.0f, buggy.GlobalPosition, 1);
		Assert(buggy.DamageController.IsWrecked, "Heavy explosion (100 dmg) must wreck the vehicle");

		buggy.QueueFree();

		var stvScene = ResourceLoader.Load<PackedScene>("res://scenes/entities/STV_9Seat.tscn");
		Assert(stvScene != null, "STV_9Seat.tscn failed to load!");
		var stv = stvScene!.Instantiate<GameVehicle>();
		AddChild(stv);
		Assert(stv.DamageController != null, "STV must have VehicleDamageController");
		Assert(stv.MaxSeats == 9, $"STV MaxSeats must be 9, got {stv.MaxSeats}");

		// Enter 9 players
		for (int peer = 1; peer <= 9; peer++)
		{
			bool entered = stv.TryEnter(peer);
			Assert(entered, $"Peer {peer} failed to enter STV");
			if (peer == 1)
			{
				Assert(stv.IsDriver(peer), "First player to enter must be driver");
			}
			else
			{
				Assert(!stv.IsDriver(peer), $"Passenger {peer} should not be driver");
				Assert(stv.HasSeat(peer), $"Passenger {peer} should have seat");
			}
		}

		// 10th player must be rejected
		bool tenthEntered = stv.TryEnter(10);
		Assert(!tenthEntered, "10th player must not be able to enter 9-seat STV");

		// Exit driver
		stv.TryExit(1, out Vector3 exitPos);
		Assert(!stv.HasSeat(1), "Player 1 should have exited STV");

		stv.QueueFree();
	}

	private void Test12OperatorRosterAndPerks()
	{
		GD.Print("Testing 12-Operator Roster and passive perks from GDD Section 3...");

		var expectedOperators = new (string Id, string Callsign, CharacterData.CharacterClass Role, string Perk)[]
		{
			// 4 Drivers
			("driver_apex", "APEX", CharacterData.CharacterClass.Driver, "Reinforced Chassis"),
			("driver_shift", "SHIFT", CharacterData.CharacterClass.Driver, "Nitrous Overdrive"),
			("driver_ghost", "GHOST", CharacterData.CharacterClass.Driver, "Slipstream"),
			("driver_tread", "TREAD", CharacterData.CharacterClass.Driver, "Iron Rubber"),

			// 4 Shooters
			("shooter_viper", "VIPER", CharacterData.CharacterClass.Shooter, "Precision Stabilizer"),
			("shooter_rage", "RAGE", CharacterData.CharacterClass.Shooter, "Hollow-Point Feed"),
			("shooter_spark", "SPARK", CharacterData.CharacterClass.Shooter, "Quick Draw"),
			("shooter_flash", "FLASH", CharacterData.CharacterClass.Shooter, "Overclocked Mag"),

			// 4 Medics
			("medic_pulse", "PULSE", CharacterData.CharacterClass.Medic, "Rapid Triage"),
			("medic_patch", "PATCH", CharacterData.CharacterClass.Medic, "Field Mechanic"),
			("medic_stim", "STIM", CharacterData.CharacterClass.Medic, "Adrenaline Boost"),
			("medic_anchor", "ANCHOR", CharacterData.CharacterClass.Medic, "Guardian Shell"),
		};

		foreach (var op in expectedOperators)
		{
			CharacterData? data = OperatorInfo.Find(op.Id);
			Assert(data != null, $"Operator {op.Id} must be registered in OperatorInfo!");
			string callsign = OperatorInfo.Callsign(data!);
			Assert(callsign == op.Callsign, $"Operator {op.Id} callsign expected '{op.Callsign}', got '{callsign}'");
			Assert(data!.Class == op.Role, $"Operator {op.Id} class expected {op.Role}, got {data.Class}");
			Assert(data.SkillName == op.Perk, $"Operator {op.Id} perk title expected '{op.Perk}', got '{data.SkillName}'");
			Assert(!string.IsNullOrEmpty(data.TacticalDescription), $"Operator {op.Id} perk description must not be empty!");
		}

		// Verify CharacterRegistry.tres loads all 12
		var registry = ResourceLoader.Load<CharacterRegistry>("res://data/characters/CharacterRegistry.tres");
		Assert(registry != null, "CharacterRegistry.tres failed to load!");
		Assert(registry!.Characters != null && registry.Characters.Length == 12,
			$"CharacterRegistry.tres must contain exactly 12 characters, found {registry?.Characters?.Length}");

		// Test Apex perk: 20% incoming vehicle small-arms damage reduction
		var apexVdc = new VehicleDamageController { DriverCharacterId = "driver_apex" };
		apexVdc.ApplyDamage(50.0f, isHeavy: false, hitTire: false);
		AssertApprox(apexVdc.CurrentHealth, 60.0f, "Apex perk should reduce 50 damage to 40, leaving 60 HP");
		apexVdc.Free();

		// Test Tread perk: Iron Rubber prevents complete immobilization on spike strip hit
		var treadVdc = new VehicleDamageController { DriverCharacterId = "driver_tread" };
		treadVdc.HitSpikeStrip();
		Assert(!treadVdc.IsDisabled, "Tread perk prevents complete immobilization from spike strip");
		Assert(treadVdc.CurrentHealth > 0.0f, "Tread vehicle retains health and traction on spike strip hit");
		treadVdc.Free();
	}

	private void TestOperatorRotationManagerRoleLock()
	{
		GD.Print("Testing OperatorRotationManager 4-round role cap & previous-round lock...");
		var mgr = OperatorRotationManager.Instance;
		bool createdLocal = false;
		if (mgr == null)
		{
			mgr = new OperatorRotationManager();
			AddChild(mgr);
			createdLocal = true;
		}

		mgr.ResetMatch();
		Assert(mgr.CurrentRound == 1, "Match should start at Round 1");
		Assert(mgr.DriverRounds == 0, "Driver rounds should start at 0");
		Assert(mgr.ShooterRounds == 0, "Shooter rounds should start at 0");
		Assert(mgr.MedicRounds == 0, "Medic rounds should start at 0");
		Assert(mgr.PreviousRoundOperatorId == null, "PreviousRoundOperatorId should start null");

		// Round 1: Select Apex (Driver)
		bool ok = mgr.TrySelectOperator("driver_apex", out string fail);
		Assert(ok, $"Selecting driver_apex in Round 1 should succeed: {fail}");
		mgr.RecordRoundCompleted("driver_apex");

		Assert(mgr.CurrentRound == 2, "CurrentRound should advance to 2");
		Assert(mgr.DriverRounds == 1, "DriverRounds should be 1");
		Assert(mgr.PreviousRoundOperatorId == "driver_apex", "PreviousRoundOperatorId should be driver_apex");

		// Round 2: Trying to repeat driver_apex must FAIL
		ok = mgr.TrySelectOperator("driver_apex", out fail);
		Assert(!ok, "Selecting driver_apex again immediately should fail!");
		Assert(fail.Contains("previous round", StringComparison.OrdinalIgnoreCase), $"Expected repeat error, got '{fail}'");

		// Select Shift (Driver)
		ok = mgr.TrySelectOperator("driver_shift", out fail);
		Assert(ok, $"Selecting driver_shift in Round 2 should succeed: {fail}");
		mgr.RecordRoundCompleted("driver_shift");

		Assert(mgr.CurrentRound == 3, "CurrentRound should advance to 3");
		Assert(mgr.DriverRounds == 2, "DriverRounds should be 2");

		// Round 3: Select Ghost (Driver)
		ok = mgr.TrySelectOperator("driver_ghost", out fail);
		Assert(ok, "Selecting driver_ghost in Round 3 should succeed");
		mgr.RecordRoundCompleted("driver_ghost");
		Assert(mgr.DriverRounds == 3, "DriverRounds should be 3");

		// Round 4: Select Tread (Driver)
		ok = mgr.TrySelectOperator("driver_tread", out fail);
		Assert(ok, "Selecting driver_tread in Round 4 should succeed");
		mgr.RecordRoundCompleted("driver_tread");
		Assert(mgr.DriverRounds == 4, "DriverRounds should now be 4 (Role cap reached!)");

		// Round 5: 4-round Driver role cap reached. Any Driver pick must FAIL!
		Assert(mgr.IsRoleLocked(CharacterData.CharacterClass.Driver), "Driver role must now be locked (4/4 cap)");
		ok = mgr.TrySelectOperator("driver_apex", out fail);
		Assert(!ok, "Selecting any Driver when cap is 4 must fail!");
		Assert(fail.Contains("cap reached", StringComparison.OrdinalIgnoreCase) || fail.Contains("Role Locked", StringComparison.OrdinalIgnoreCase),
			$"Expected role cap error message, got '{fail}'");

		ok = mgr.TrySelectOperator("driver_shift", out fail);
		Assert(!ok, "Selecting driver_shift must fail due to role cap!");

		// Selecting a Shooter (Shooter rounds = 0) must SUCCEED!
		ok = mgr.TrySelectOperator("shooter_viper", out fail);
		Assert(ok, $"Selecting shooter_viper should succeed when Driver is capped: {fail}");

		// Best-of-7 series format tracking
		mgr.ResetMatch();
		Assert(OperatorRotationManager.WinsToWinMatch == 4, "Best-of-7 requires 4 wins");
		Assert(!mgr.IsMatchFinished, "Match should not be finished at 0-0");
		mgr.RecordRoundCompleted("driver_apex", roundWinningTeam: 0);
		Assert(mgr.Team0RoundWins == 1, "Team 0 should have 1 win");
		Assert(mgr.Team1RoundWins == 0, "Team 1 should have 0 wins");
		mgr.RecordRoundCompleted("shooter_viper", roundWinningTeam: 0);
		mgr.RecordRoundCompleted("medic_pulse", roundWinningTeam: 0);
		mgr.RecordRoundCompleted("driver_shift", roundWinningTeam: 0);
		Assert(mgr.IsMatchFinished, "Match should be finished when a team reaches 4 wins");
		Assert(mgr.WinningTeam == 0, "Team 0 should be the winning team");

		// Clean up match state so subsequent tests start with fresh state
		mgr.ResetMatch();
		Assert(mgr.CurrentRound == 1, "Match should reset to Round 1");
		Assert(mgr.DriverRounds == 0 && mgr.Team0RoundWins == 0, "Role counts and team wins should be reset");

		if (createdLocal) mgr.QueueFree();
	}

	private void TestSiegeOperatorSelectScene()
	{
		GD.Print("Testing SiegeOperatorSelect scene instantiation and 3x4 grid...");
		PlayerProfile.OperatorId = "driver_apex";
		var scene = ResourceLoader.Load<PackedScene>("res://scenes/ui/SiegeOperatorSelect.tscn");
		Assert(scene != null, "SiegeOperatorSelect.tscn failed to load!");

		var ui = scene!.Instantiate<SiegeOperatorSelect>();
		AddChild(ui);

		Assert(ui != null, "SiegeOperatorSelect instantiated null");
		Assert(ui.SelectedOperatorId == "driver_apex", $"Default selected operator should be driver_apex, got '{ui.SelectedOperatorId}'");

		// Find operator buttons inside the hierarchy
		var buttons = new List<Button>();
		FindButtonsRecursive(ui, buttons);
		// Should contain 12 operator buttons + 1 confirm button
		Assert(buttons.Count >= 13, $"Expected at least 13 buttons in SiegeOperatorSelect (12 operators + confirm), got {buttons.Count}");

		ui.QueueFree();
	}

	private static void FindButtonsRecursive(Node parent, List<Button> list)
	{
		foreach (Node child in parent.GetChildren())
		{
			if (child is Button btn) list.Add(btn);
			FindButtonsRecursive(child, list);
		}
	}

	private void TestCombinedFpsHud()
	{
		GD.Print("Testing Combined FPS HUD elements, zero minimap, and countdown-free respawning overlay...");
		var hudScene = ResourceLoader.Load<PackedScene>("res://scenes/ui/CombinedHUD.tscn");
		Assert(hudScene != null, "CombinedHUD.tscn failed to load!");

		var hud = hudScene!.Instantiate<PlayerHud>();
		AddChild(hud);

		// 1. Top-Left: Tablet button, Shield logo, Match Timer, Blue & Red team score bars
		var topLeft = hud.FindChild("CombinedHudRoot", true, false);
		Assert(topLeft != null, "CombinedHudRoot must exist");

		var matchTimer = hud.FindChildren("*", "Label", true, false);
		bool foundTimer = false;
		bool foundAlbert = false;
		bool foundEinstein = false;
		bool foundRespawning = false;
		bool foundMagAmmo = false;
		bool foundSecondary = false;

		foreach (Node node in matchTimer)
		{
			if (node is Label lbl)
			{
				if (lbl.Text == "05:00") foundTimer = true;
				if (lbl.Text.Contains("ALBERT")) foundAlbert = true;
				if (lbl.Text.Contains("EINSTEIN")) foundEinstein = true;
				if (lbl.Text == "RESPAWNING...") foundRespawning = true;
				if (lbl.Text == "30") foundMagAmmo = true;
				if (lbl.Text.Contains("SECONDARY")) foundSecondary = true;
			}
		}

		Assert(foundTimer, "Top-left match timer label ('05:00') must be present");
		Assert(foundAlbert, "Top-left Albert team score bar/label must be present");
		Assert(foundEinstein, "Top-left Einstein team score bar/label must be present");
		Assert(foundMagAmmo, "Top-right large magazine ammo count ('30') must be present");
		Assert(foundSecondary, "Top-right secondary slot indicator ('[ 2 ] SECONDARY') must be present");
		Assert(foundRespawning, "Fullscreen 'RESPAWNING...' banner must be present");

		// Performance Telemetry Overlay (FPS & Ping)
		var fpsLabel = hud.FindChild("FpsLabel", true, false) as Label;
		var pingLabel = hud.FindChild("PingLabel", true, false) as Label;
		var telemetryPill = hud.FindChild("TelemetryPill", true, false) as PanelContainer;
		Assert(fpsLabel != null, "HUD must have FpsLabel");
		Assert(pingLabel != null, "HUD must have PingLabel");
		Assert(telemetryPill != null, "HUD must have TelemetryPill");
		Assert(fpsLabel!.Text.Contains("FPS"), "FpsLabel must display FPS readout");
		Assert(pingLabel!.Text.Contains("PING"), "PingLabel must display PING readout");
		GD.Print($"✓ Performance Telemetry verified: '{fpsLabel.Text}', '{pingLabel.Text}'");

		// 2. Center: 4-tick reticle CrosshairDrawer and Floating XP container
		var crosshairDrawer = hud.FindChild("CrosshairDrawer", true, false);
		Assert(crosshairDrawer != null, "HUD must have 4-tick reticle (CrosshairDrawer) at center");

		var floatingXpContainer = hud.FindChild("FloatingXpContainer", true, false);
		Assert(floatingXpContainer != null, "FloatingXpContainer must exist");

		// Test spawning XP popups
		hud.SpawnXpPopup("+100 BASE ELIMINATION");
		hud.SpawnXpPopup("+150 HEADSHOT", new Color(1, 0.3f, 0.3f));
		var popups = floatingXpContainer!.GetChildren();
		Assert(popups.Count >= 2, $"Expected at least 2 FloatingXpPopup nodes, got {popups.Count}");

		// 3. Bottom: Kill-streak pill badge
		var pill = hud.FindChildren("*", "PanelContainer", true, false);
		bool foundPill = false;
		PanelContainer? killPillPanel = null;
		foreach (Node node in pill)
		{
			if (node is PanelContainer pc)
			{
				var subLabels = pc.FindChildren("*", "Label", true, false);
				foreach (var sl in subLabels)
				{
					if (sl is Label l && l.Text.Contains("KILL"))
					{
						foundPill = true;
						killPillPanel = pc;
					}
				}
			}
		}
		Assert(foundPill, "Kill-streak pill badge must exist at bottom center");

		// Test kill pill auto-hide timer
		hud.Call("ShowKillStreakPill", 3);
		Assert(killPillPanel != null && killPillPanel.Visible, "Kill pill badge must be visible after ShowKillStreakPill");
		hud._Process(4.0); // 4.0 seconds exceeds 3.5s timer
		Assert(!killPillPanel!.Visible, "Kill pill badge must auto-hide after 3.5s timeout");

		// 4. Zero Minimap Clutter Requirement: Verify NO Minimap or Radar exists in the HUD
		var allNodes = hud.FindChildren("*", "Node", true, false);
		foreach (Node node in allNodes)
		{
			string name = node.Name.ToString().ToLowerInvariant();
			Assert(!name.Contains("minimap"), $"HUD must have ZERO minimap clutter! Found node: {node.Name}");
			Assert(!name.Contains("radar"), $"HUD must have ZERO radar clutter! Found node: {node.Name}");
		}

		// 5. Countdown-Free RESPAWNING... overlay check
		var respawnOverlay = hud.FindChild("RespawningOverlay", true, false);
		Assert(respawnOverlay != null, "RespawningOverlay node must exist");
		Assert(respawnOverlay is Control ctrl && !ctrl.Visible, "RespawningOverlay must start hidden");

		var respawnLabels = respawnOverlay!.FindChildren("*", "Label", true, false);
		foreach (Node n in respawnLabels)
		{
			if (n is Label l)
			{
				// Banner should say RESPAWNING... or sub-text, but NEVER contain numeric countdown
				Assert(!l.Text.Contains(" 3s") && !l.Text.Contains("3.0") && !l.Text.Contains("0:0"),
					$"Respawning overlay must be COUNTDOWN-FREE! Found unexpected numeric text: '{l.Text}'");
			}
		}

		hud.QueueFree();
	}
}
