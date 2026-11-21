using Godot;
using System;
using System.Linq;

[GlobalClass]
public partial class VerifyWeaponTableAndAttachments : Node
{
	public override async void _Ready()
	{
		GD.Print("=== Starting VerifyWeaponTableAndAttachments ===");
		try
		{
			RunAllTests();
			GD.Print("=== ALL TESTS PASSED SUCCESSFULLY! ===");
			await ToSignal(GetTree(), "process_frame");
			await ToSignal(GetTree(), "process_frame");
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
		TestWeaponRegistryResource();
		TestWeaponSwitcherLoadout();
		TestAttachmentStatModifiers();
		TestAttachmentStacking();
		TestAttachmentClampingAndEdgeCases();
		TestKarambitAndBazookaMechanics();
		TestWeaponSetsAndOperatorInfo();
	}

	private void TestWeaponRegistryResource()
	{
		GD.Print("Testing WeaponRegistry.tres loading and weapon stats...");
		var registry = ResourceLoader.Load<WeaponRegistry>("res://scripts/data/WeaponRegistry.tres");
		Assert(registry != null, "WeaponRegistry.tres failed to load!");
		Assert(registry!.Weapons != null, "registry.Weapons is null!");
		Assert(registry.Weapons.Length == 10, $"Expected exactly 10 weapons in registry, got {registry.Weapons.Length}");

		var expectedWeapons = new[]
		{
			"AK-47", "M4", "P90", "Kriss Vector", "Glock 19", "FN 502", "Desert Eagle", "Shotgun", "Karambit", "Bazooka"
		};

		var actualNames = registry.Weapons.Select(w => w.WeaponName).ToArray();
		for (int i = 0; i < expectedWeapons.Length; i++)
		{
			Assert(actualNames.Contains(expectedWeapons[i]), $"Registry missing weapon: {expectedWeapons[i]}");
		}

		// Check AK-47 (34 body, 85 head, 600 RPM, 30 mag, 2.3s reload, 3.5 vert recoil, 2.0 horiz recoil, 180ms recovery)
		var ak = registry.Weapons.First(w => w.WeaponName == "AK-47");
		AssertApprox(ak.BodyDamage, 34.0f, "AK-47 BodyDamage");
		AssertApprox(ak.HeadshotDamage, 85.0f, "AK-47 HeadshotDamage");
		AssertApprox(ak.FireRateRPM, 600.0f, "AK-47 FireRateRPM");
		Assert(ak.MagazineSize == 30, $"AK-47 MagSize expected 30, got {ak.MagazineSize}");
		AssertApprox(ak.ReloadTime, 2.3f, "AK-47 ReloadTime");
		AssertApprox(ak.RecoilVertical, 3.5f, "AK-47 RecoilVertical");
		AssertApprox(ak.RecoilHorizontal, 2.0f, "AK-47 RecoilHorizontal");
		AssertApprox(ak.RecoilRecoveryMs, 180.0f, "AK-47 RecoilRecoveryMs");
		Assert(ak.FireMode == WeaponData.FireModeType.FullAuto, "AK-47 FireMode");

		// Check M4 (30 body, 75 head, 800 RPM, 30 mag, 2.0s reload, 2.0 vert, 1.0 horiz, 120ms recovery)
		var m4 = registry.Weapons.First(w => w.WeaponName == "M4");
		AssertApprox(m4.BodyDamage, 30.0f, "M4 BodyDamage");
		AssertApprox(m4.HeadshotDamage, 75.0f, "M4 HeadshotDamage");
		AssertApprox(m4.FireRateRPM, 800.0f, "M4 FireRateRPM");
		Assert(m4.MagazineSize == 30, "M4 MagSize");
		AssertApprox(m4.ReloadTime, 2.0f, "M4 ReloadTime");
		AssertApprox(m4.RecoilVertical, 2.0f, "M4 RecoilVertical");
		AssertApprox(m4.RecoilHorizontal, 1.0f, "M4 RecoilHorizontal");
		AssertApprox(m4.RecoilRecoveryMs, 120.0f, "M4 RecoilRecoveryMs");

		// Check P90 (22 body, 55 head, 900 RPM, 50 mag, 2.2s reload, 1.2 vert, 0.8 horiz, 90ms recovery)
		var p90 = registry.Weapons.First(w => w.WeaponName == "P90");
		AssertApprox(p90.BodyDamage, 22.0f, "P90 BodyDamage");
		AssertApprox(p90.HeadshotDamage, 55.0f, "P90 HeadshotDamage");
		AssertApprox(p90.FireRateRPM, 900.0f, "P90 FireRateRPM");
		Assert(p90.MagazineSize == 50, "P90 MagSize");
		AssertApprox(p90.ReloadTime, 2.2f, "P90 ReloadTime");
		AssertApprox(p90.RecoilVertical, 1.2f, "P90 RecoilVertical");
		AssertApprox(p90.RecoilHorizontal, 0.8f, "P90 RecoilHorizontal");
		AssertApprox(p90.RecoilRecoveryMs, 90.0f, "P90 RecoilRecoveryMs");

		// Check Kriss Vector (24 body, 60 head, 1100 RPM, 25 mag, 2.0s reload, 1.0 vert, 0.6 horiz, 80ms recovery)
		var vector = registry.Weapons.First(w => w.WeaponName == "Kriss Vector");
		AssertApprox(vector.BodyDamage, 24.0f, "Kriss Vector BodyDamage");
		AssertApprox(vector.HeadshotDamage, 60.0f, "Kriss Vector HeadshotDamage");
		AssertApprox(vector.FireRateRPM, 1100.0f, "Kriss Vector FireRateRPM");
		Assert(vector.MagazineSize == 25, "Kriss Vector MagSize");
		AssertApprox(vector.ReloadTime, 2.0f, "Kriss Vector ReloadTime");
		AssertApprox(vector.RecoilVertical, 1.0f, "Kriss Vector RecoilVertical");
		AssertApprox(vector.RecoilHorizontal, 0.6f, "Kriss Vector RecoilHorizontal");
		AssertApprox(vector.RecoilRecoveryMs, 80.0f, "Kriss Vector RecoilRecoveryMs");

		// Check Glock 19 (35 body / 3-shot kill on 100 HP, 83 head, 300 RPM, 15 mag, 1.5s reload, 1.5 vert, 1.0 horiz, 150ms recovery)
		var glock = registry.Weapons.First(w => w.WeaponName == "Glock 19");
		AssertApprox(glock.BodyDamage, 35.0f, "Glock 19 BodyDamage");
		AssertApprox(glock.HeadshotDamage, 83.0f, "Glock 19 HeadshotDamage");
		Assert(Mathf.CeilToInt(100.0f / glock.BodyDamage) == 3, "Glock 19 must be 3-shot kill on 100 HP baseline");
		AssertApprox(glock.FireRateRPM, 300.0f, "Glock 19 FireRateRPM");
		Assert(glock.MagazineSize == 15, "Glock 19 MagSize");
		AssertApprox(glock.ReloadTime, 1.5f, "Glock 19 ReloadTime");
		AssertApprox(glock.RecoilVertical, 1.5f, "Glock 19 RecoilVertical");
		AssertApprox(glock.RecoilHorizontal, 1.0f, "Glock 19 RecoilHorizontal");
		AssertApprox(glock.RecoilRecoveryMs, 150.0f, "Glock 19 RecoilRecoveryMs");
		Assert(glock.FireMode == WeaponData.FireModeType.SemiAuto, "Glock 19 FireMode");

		// Check FN 502 (28 body, 70 head, 300 RPM, 15 mag, 1.5s reload, 1.5 vert, 1.0 horiz, 150ms recovery)
		var fn502 = registry.Weapons.First(w => w.WeaponName == "FN 502");
		AssertApprox(fn502.BodyDamage, 28.0f, "FN 502 BodyDamage");
		AssertApprox(fn502.HeadshotDamage, 70.0f, "FN 502 HeadshotDamage");
		AssertApprox(fn502.FireRateRPM, 300.0f, "FN 502 FireRateRPM");
		Assert(fn502.MagazineSize == 15, "FN 502 MagSize");
		AssertApprox(fn502.ReloadTime, 1.5f, "FN 502 ReloadTime");

		// Check Desert Eagle (45 body, 112.5 head, 180 RPM, 7 mag, 2.0s reload, 5.0 vert, 3.0 horiz, 350ms recovery)
		var deagle = registry.Weapons.First(w => w.WeaponName == "Desert Eagle");
		AssertApprox(deagle.BodyDamage, 45.0f, "Desert Eagle BodyDamage");
		AssertApprox(deagle.HeadshotDamage, 112.5f, "Desert Eagle HeadshotDamage");
		Assert(deagle.HeadshotDamage >= 100.0f, "Desert Eagle Headshot is 1-shot kill");
		AssertApprox(deagle.FireRateRPM, 180.0f, "Desert Eagle FireRateRPM");
		Assert(deagle.MagazineSize == 7, "Desert Eagle MagSize");
		AssertApprox(deagle.ReloadTime, 2.0f, "Desert Eagle ReloadTime");
		AssertApprox(deagle.RecoilVertical, 5.0f, "Desert Eagle RecoilVertical");
		AssertApprox(deagle.RecoilHorizontal, 3.0f, "Desert Eagle RecoilHorizontal");
		AssertApprox(deagle.RecoilRecoveryMs, 350.0f, "Desert Eagle RecoilRecoveryMs");

		// Check Shotgun (70 body, 175 head, 60 RPM, 6 mag, 0.5s shell reload, 6.0 vert, 2.5 horiz, 400ms recovery, 8 pellets)
		var shotgun = registry.Weapons.First(w => w.WeaponName == "Shotgun");
		AssertApprox(shotgun.BodyDamage, 70.0f, "Shotgun BodyDamage");
		AssertApprox(shotgun.HeadshotDamage, 175.0f, "Shotgun HeadshotDamage");
		AssertApprox(shotgun.FireRateRPM, 60.0f, "Shotgun FireRateRPM");
		Assert(shotgun.MagazineSize == 6, "Shotgun MagSize");
		AssertApprox(shotgun.ReloadTime, 0.5f, "Shotgun ReloadTime");
		AssertApprox(shotgun.RecoilVertical, 6.0f, "Shotgun RecoilVertical");
		AssertApprox(shotgun.RecoilHorizontal, 2.5f, "Shotgun RecoilHorizontal");
		AssertApprox(shotgun.RecoilRecoveryMs, 400.0f, "Shotgun RecoilRecoveryMs");
		Assert(shotgun.PelletsPerShot == 8, "Shotgun PelletsPerShot");
		Assert(shotgun.ShellByShellReload, "Shotgun ShellByShellReload");

		// Check Karambit (100 body/instant kill, 0 mag, 0 reload, 0 recoil, melee)
		var karambit = registry.Weapons.First(w => w.WeaponName == "Karambit");
		AssertApprox(karambit.BodyDamage, 100.0f, "Karambit BodyDamage");
		Assert(karambit.FireMode == WeaponData.FireModeType.Melee, "Karambit FireMode");
		Assert(karambit.MagazineSize == 0, "Karambit MagSize");
		Assert(karambit.MaxReserveAmmo == 0, "Karambit MaxReserveAmmo");
		AssertApprox(karambit.ReloadTime, 0.0f, "Karambit ReloadTime");
		AssertApprox(karambit.RecoilVertical, 0.0f, "Karambit RecoilVertical");

		// Check Bazooka (20 body to player, 40 RPM, 1 mag, 5 reserve, 3.0s reload, 8.0 vert, 1000ms recovery)
		var bazooka = registry.Weapons.First(w => w.WeaponName == "Bazooka");
		AssertApprox(bazooka.BodyDamage, 20.0f, "Bazooka BodyDamage");
		AssertApprox(bazooka.FireRateRPM, 40.0f, "Bazooka FireRateRPM");
		Assert(bazooka.MagazineSize == 1, "Bazooka MagSize");
		Assert(bazooka.MaxReserveAmmo == 5, "Bazooka MaxReserveAmmo");
		AssertApprox(bazooka.ReloadTime, 3.0f, "Bazooka ReloadTime");
		AssertApprox(bazooka.RecoilVertical, 8.0f, "Bazooka RecoilVertical");
		AssertApprox(bazooka.RecoilRecoveryMs, 1000.0f, "Bazooka RecoilRecoveryMs");

		GD.Print("All 10 weapons in WeaponRegistry.tres verified successfully!");
	}

	private void TestWeaponSwitcherLoadout()
	{
		GD.Print("Testing WeaponSwitcher.Loadout array...");
		Assert(WeaponSwitcher.Loadout.Length == 10, $"WeaponSwitcher.Loadout length must be 10, got {WeaponSwitcher.Loadout.Length}");
		var expected = new[] { "AK-47", "M4", "P90", "Kriss Vector", "Glock 19", "FN 502", "Desert Eagle", "Shotgun", "Karambit", "Bazooka" };
		for (int i = 0; i < expected.Length; i++)
		{
			Assert(WeaponSwitcher.Loadout[i] == expected[i], $"WeaponSwitcher.Loadout[{i}] expected {expected[i]}, got {WeaponSwitcher.Loadout[i]}");
		}
		GD.Print("WeaponSwitcher.Loadout array verified successfully!");
	}

	private void TestAttachmentStatModifiers()
	{
		GD.Print("Testing 8 Attachment Stat Modifiers on WeaponSwitcher...");

		var playerScene = ResourceLoader.Load<PackedScene>("res://scenes/Player.tscn");
		Assert(playerScene != null, "Failed to load scenes/Player.tscn");
		var player = playerScene.Instantiate<CharacterBody3D>();
		AddChild(player);

		var switcher = player.GetNode<WeaponSwitcher>("WeaponSwitcher");
		Assert(switcher != null, "Player missing WeaponSwitcher");

		var registry = ResourceLoader.Load<WeaponRegistry>("res://scripts/data/WeaponRegistry.tres");
		var ak = registry.Weapons.First(w => w.WeaponName == "AK-47");

		// Test 1: ADS Speed
		float baseAds = switcher.GetEffectiveAdsSpeedMultiplier();
		AssertApprox(baseAds, 1.0f, "Base ADS speed should be 1.0");
		switcher.SetAttachmentModifier("AK-47", WeaponAttachmentEffectIds.AdsSpeedMult, 1.25f);
		AssertApprox(switcher.GetEffectiveAdsSpeedMultiplier(), 1.25f, "Modified ADS speed should be 1.25");

		// Test 2: Switch Speed & Swap Cooldown
		float baseSwitch = switcher.GetEffectiveSwitchSpeedMultiplier();
		AssertApprox(baseSwitch, 1.0f, "Base switch speed should be 1.0");
		switcher.SetAttachmentModifier("AK-47", WeaponAttachmentEffectIds.SwitchSpeedMult, 1.5f);
		AssertApprox(switcher.GetEffectiveSwitchSpeedMultiplier(), 1.5f, "Modified switch speed should be 1.5");
		AssertApprox(switcher.GetEffectiveSwapCooldown(), switcher.WeaponSwapCooldown / 1.5f, "Effective swap cooldown should be reduced");

		// Test 3: Reload Speed & Reload Time
		float baseReloadSpeed = switcher.GetEffectiveReloadSpeedMultiplier();
		AssertApprox(baseReloadSpeed, 1.0f, "Base reload speed should be 1.0");
		AssertApprox(switcher.GetEffectiveReloadTime(), ak.ReloadTime, "Base reload time matches weapon");
		switcher.SetAttachmentModifier("AK-47", WeaponAttachmentEffectIds.ReloadSpeedMult, 1.25f);
		AssertApprox(switcher.GetEffectiveReloadSpeedMultiplier(), 1.25f, "Modified reload speed should be 1.25");
		AssertApprox(switcher.GetEffectiveReloadTime(), ak.ReloadTime / 1.25f, "Effective reload time should be reduced");

		// Test 4: Vertical Recoil
		AssertApprox(switcher.GetEffectiveVerticalRecoil(), ak.RecoilVertical, "Base vertical recoil matches weapon");
		switcher.SetAttachmentModifier("AK-47", WeaponAttachmentEffectIds.VerticalRecoilMult, 0.8f);
		AssertApprox(switcher.GetEffectiveVerticalRecoil(), ak.RecoilVertical * 0.8f, "Modified vertical recoil should be 80%");

		// Test 5: Horizontal Recoil
		AssertApprox(switcher.GetEffectiveHorizontalRecoil(), ak.RecoilHorizontal, "Base horizontal recoil matches weapon");
		switcher.SetAttachmentModifier("AK-47", WeaponAttachmentEffectIds.HorizontalRecoilMult, 0.7f);
		AssertApprox(switcher.GetEffectiveHorizontalRecoil(), ak.RecoilHorizontal * 0.7f, "Modified horizontal recoil should be 70%");

		// Test 6: Bullet Spread
		AssertApprox(switcher.GetEffectiveSpreadDegrees(), ak.SpreadDegrees, "Base bullet spread matches weapon");
		switcher.SetAttachmentModifier("AK-47", WeaponAttachmentEffectIds.BulletSpreadMult, 0.5f);
		AssertApprox(switcher.GetEffectiveSpreadDegrees(), ak.SpreadDegrees * 0.5f, "Modified bullet spread should be halved");

		// Test 7: Visual Kick
		AssertApprox(switcher.GetEffectiveVisualKickMultiplier(), 1.0f, "Base visual kick should be 1.0");
		switcher.SetAttachmentModifier("AK-47", WeaponAttachmentEffectIds.VisualKickMult, 0.6f);
		AssertApprox(switcher.GetEffectiveVisualKickMultiplier(), 0.6f, "Modified visual kick should be 0.6");

		// Test 8: Ammo Capacity (Delta & Mult)
		Assert(switcher.GetEffectiveMagazineSize(ak) == 30, "Base AK mag size is 30");
		switcher.SetAttachmentModifier("AK-47", WeaponAttachmentEffectIds.AmmoCapacityDelta, 10.0f);
		Assert(switcher.GetEffectiveMagazineSize(ak) == 40, "AK mag size with +10 delta is 40");
		switcher.SetAttachmentModifier("AK-47", WeaponAttachmentEffectIds.AmmoCapacityMult, 1.5f);
		// 30 * 1.5 = 45 + 10 = 55
		Assert(switcher.GetEffectiveMagazineSize(ak) == 55, "AK mag size with 1.5x mult and +10 delta is 55");

		// Test RestockAmmo respects modified MagazineSize
		switcher.RestockAmmo();
		Assert(switcher.CurrentAmmo == 55, $"RestockAmmo should fill to effective MagazineSize (55), got {switcher.CurrentAmmo}");

		// Clean up
		switcher.ClearAttachmentModifiers("AK-47");
		AssertApprox(switcher.GetEffectiveAdsSpeedMultiplier(), 1.0f, "Reset ADS speed should be 1.0");
		Assert(switcher.GetEffectiveMagazineSize(ak) == 30, "Reset AK mag size should be 30");

		player.QueueFree();
		GD.Print("All 8 Attachment Stat Modifiers verified successfully!");
	}

	private void TestAttachmentStacking()
	{
		GD.Print("Testing stacked attachments on WeaponData...");
		var weapon = new WeaponData
		{
			WeaponName = "TestGun",
			RecoilVertical = 10.0f,
			MagazineSize = 20,
		};

		var att1 = new AttachmentData
		{
			AttachmentName = "Compensator",
			Modifiers = new[] { new StatModifier { EffectId = WeaponAttachmentEffectIds.VerticalRecoilMult, Value = 0.9f } }
		};

		var att2 = new AttachmentData
		{
			AttachmentName = "Grip",
			Modifiers = new[] { new StatModifier { EffectId = WeaponAttachmentEffectIds.VerticalRecoilMult, Value = 0.9f } }
		};

		weapon.Attachments.Add(att1);
		weapon.Attachments.Add(att2);

		// Multipliers should multiply: 0.9 * 0.9 = 0.81
		float combinedRecoilMult = weapon.GetModifier(WeaponAttachmentEffectIds.VerticalRecoilMult, 1.0f);
		AssertApprox(combinedRecoilMult, 0.81f, "Stacked vertical recoil multipliers must be 0.81");

		// Stacked deltas: Extended Mag (+5) and Drum (+15)
		var mag1 = new AttachmentData
		{
			AttachmentName = "ExtMag",
			Modifiers = new[] { new StatModifier { EffectId = WeaponAttachmentEffectIds.AmmoCapacityDelta, Value = 5.0f } }
		};
		var mag2 = new AttachmentData
		{
			AttachmentName = "Drum",
			Modifiers = new[] { new StatModifier { EffectId = WeaponAttachmentEffectIds.AmmoCapacityDelta, Value = 15.0f } }
		};
		weapon.Attachments.Add(mag1);
		weapon.Attachments.Add(mag2);

		float combinedDelta = weapon.GetModifier(WeaponAttachmentEffectIds.AmmoCapacityDelta, 0.0f);
		AssertApprox(combinedDelta, 20.0f, "Stacked ammo capacity deltas must be 20.0");

		GD.Print("Attachment stacking verified successfully!");
	}

	private partial class DummyTarget : Node, IShootable
	{
		public bool IsFleshTarget { get; set; }
		public ShotInfo LastShot;
		public bool WasShotReceived;

		public bool IsHeadHit(Vector3 worldPoint) => false;
		public void ReceiveShot(ShotInfo shot)
		{
			WasShotReceived = true;
			LastShot = shot;
		}
	}

	private void TestKarambitAndBazookaMechanics()
	{
		GD.Print("Testing Karambit and Bazooka special weapon mechanics...");
		var registry = ResourceLoader.Load<WeaponRegistry>("res://scripts/data/WeaponRegistry.tres");
		var karambit = registry.Weapons.First(w => w.WeaponName == "Karambit");
		var bazooka = registry.Weapons.First(w => w.WeaponName == "Bazooka");

		// Karambit magazine size is 0 and stays 0 even if ammo modifiers are attached
		var att = new AttachmentData
		{
			Modifiers = new[] { new StatModifier { EffectId = WeaponAttachmentEffectIds.AmmoCapacityDelta, Value = 10.0f } }
		};
		try
		{
			karambit.Attachments.Add(att);
			var switcher = new WeaponSwitcher();
			Assert(switcher.GetEffectiveMagazineSize(karambit) == 0, "Karambit magazine size must remain 0");
		}
		finally
		{
			karambit.Attachments.Remove(att);
		}

		// Bazooka has Anti-Vehicle class
		Assert(bazooka.WeaponClass == "Anti-Vehicle", "Bazooka WeaponClass must be Anti-Vehicle");
		Assert(!bazooka.ImpactUsesSurfaceVariation, "Bazooka must disable surface variation for explosion");

		// Test ShotInfo.IsHeavy and ShotInfo.IsMelee
		var bazookaShot = new ShotInfo { WeaponName = bazooka.WeaponName, WeaponClass = bazooka.WeaponClass, Damage = 20.0f };
		Assert(bazookaShot.IsHeavy, "Bazooka ShotInfo must return IsHeavy = true");
		Assert(!bazookaShot.IsMelee, "Bazooka ShotInfo must return IsMelee = false");

		var karambitShot = new ShotInfo { WeaponName = karambit.WeaponName, WeaponClass = karambit.WeaponClass, Damage = 100.0f };
		Assert(karambitShot.IsMelee, "Karambit ShotInfo must return IsMelee = true");
		Assert(!karambitShot.IsHeavy, "Karambit ShotInfo must return IsHeavy = false");

		// Test WeaponSwitcher shootable damage resolution on vehicle vs flesh
		var playerScene = ResourceLoader.Load<PackedScene>("res://scenes/Player.tscn");
		var player = playerScene.Instantiate<CharacterBody3D>();
		AddChild(player);
		var playerSwitcher = player.GetNode<WeaponSwitcher>("WeaponSwitcher");

		var dummyVehicle = new DummyTarget { IsFleshTarget = false };
		var dummyPerson = new DummyTarget { IsFleshTarget = true };

		string savedPrimary1 = PlayerProfile.GetSetWeapon(PlayerProfile.ActiveWeaponSet, WeaponSlot.Primary1);
		string savedPistol = PlayerProfile.GetSetWeapon(PlayerProfile.ActiveWeaponSet, WeaponSlot.Pistol);
		try
		{
			PlayerProfile.SetSetWeapon(PlayerProfile.ActiveWeaponSet, WeaponSlot.Primary1, "Bazooka");
			PlayerProfile.SetSetWeapon(PlayerProfile.ActiveWeaponSet, WeaponSlot.Pistol, "Karambit");

			// Equip Bazooka
			bool bazookaEquipped = playerSwitcher.EquipByName("Bazooka");
			Assert(bazookaEquipped, "EquipByName Bazooka must succeed");
			Assert(playerSwitcher.WeaponName == "Bazooka", "Current weapon must be Bazooka");

			// Reflection call to HandleShootableHit
			var handleMethod = typeof(WeaponSwitcher).GetMethod("HandleShootableHit",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			Assert(handleMethod != null, "HandleShootableHit method found");

			// Bazooka on vehicle -> 100 damage (direct hit vehicle destroy)
			handleMethod!.Invoke(playerSwitcher, new object[] { dummyVehicle, Vector3.Zero, Vector3.Zero, Vector3.Forward, false });
			Assert(dummyVehicle.WasShotReceived, "Vehicle received Bazooka shot");
			AssertApprox(dummyVehicle.LastShot.Damage, 100.0f, "Bazooka on vehicle must deal 100 damage (instant destroy)");

			// Bazooka on flesh -> 20 damage (player hit)
			handleMethod.Invoke(playerSwitcher, new object[] { dummyPerson, Vector3.Zero, Vector3.Zero, Vector3.Forward, false });
			Assert(dummyPerson.WasShotReceived, "Person received Bazooka shot");
			AssertApprox(dummyPerson.LastShot.Damage, 20.0f, "Bazooka on flesh must deal 20 damage");

			// Equip Karambit
			bool karambitEquipped = playerSwitcher.EquipByName("Karambit");
			Assert(karambitEquipped, "EquipByName Karambit must succeed");
			Assert(playerSwitcher.WeaponName == "Karambit", "Current weapon must be Karambit");

			// Karambit on vehicle -> 0 damage (knife cannot destroy vehicles)
			dummyVehicle.WasShotReceived = false;
			handleMethod.Invoke(playerSwitcher, new object[] { dummyVehicle, Vector3.Zero, Vector3.Zero, Vector3.Forward, true });
			Assert(dummyVehicle.WasShotReceived, "Vehicle received Karambit hit");
			AssertApprox(dummyVehicle.LastShot.Damage, 0.0f, "Karambit on vehicle must deal 0 damage");

			// Karambit on flesh -> 100 damage (instant kill)
			dummyPerson.WasShotReceived = false;
			handleMethod.Invoke(playerSwitcher, new object[] { dummyPerson, Vector3.Zero, Vector3.Zero, Vector3.Forward, true });
			Assert(dummyPerson.WasShotReceived, "Person received Karambit hit");
			AssertApprox(dummyPerson.LastShot.Damage, 100.0f, "Karambit on flesh must deal 100 damage (instant kill)");
		}
		finally
		{
			PlayerProfile.SetSetWeapon(PlayerProfile.ActiveWeaponSet, WeaponSlot.Primary1, savedPrimary1);
			PlayerProfile.SetSetWeapon(PlayerProfile.ActiveWeaponSet, WeaponSlot.Pistol, savedPistol);
			RemoveChild(player);
			player.Free();
		}
		GD.Print("Karambit and Bazooka mechanics verified successfully!");
	}

	private void TestAttachmentClampingAndEdgeCases()
	{
		GD.Print("Testing attachment clamping and edge cases...");
		var playerScene = ResourceLoader.Load<PackedScene>("res://scenes/Player.tscn");
		var player = playerScene.Instantiate<CharacterBody3D>();
		AddChild(player);
		var switcher = player.GetNode<WeaponSwitcher>("WeaponSwitcher");

		var registry = ResourceLoader.Load<WeaponRegistry>("res://scripts/data/WeaponRegistry.tres");
		var ak = registry.Weapons.First(w => w.WeaponName == "AK-47");

		// Negative delta should clamp to at least 1
		switcher.SetAttachmentModifier("AK-47", WeaponAttachmentEffectIds.AmmoCapacityDelta, -100.0f);
		Assert(switcher.GetEffectiveMagazineSize(ak) == 1, "Negative ammo delta must clamp MagazineSize to 1");

		// Negative multiplier on ADS speed should clamp to 0.05
		switcher.SetAttachmentModifier("AK-47", WeaponAttachmentEffectIds.AdsSpeedMult, -5.0f);
		Assert(switcher.GetEffectiveAdsSpeedMultiplier() >= 0.05f, "Negative AdsSpeedMult must clamp to at least 0.05");

		// Negative multiplier on Switch speed should clamp to 0.05
		switcher.SetAttachmentModifier("AK-47", WeaponAttachmentEffectIds.SwitchSpeedMult, -5.0f);
		Assert(switcher.GetEffectiveSwitchSpeedMultiplier() >= 0.05f, "Negative SwitchSpeedMult must clamp to at least 0.05");

		// Negative multiplier on Reload speed should clamp to 0.05
		switcher.SetAttachmentModifier("AK-47", WeaponAttachmentEffectIds.ReloadSpeedMult, -5.0f);
		Assert(switcher.GetEffectiveReloadSpeedMultiplier() >= 0.05f, "Negative ReloadSpeedMult must clamp to at least 0.05");

		// Negative multiplier on Visual kick should clamp to 0.0
		switcher.SetAttachmentModifier("AK-47", WeaponAttachmentEffectIds.VisualKickMult, -2.0f);
		Assert(switcher.GetEffectiveVisualKickMultiplier() >= 0.0f, "Negative VisualKickMult must clamp to at least 0.0");

		// Clamping _currentAmmo when magazine size drops
		switcher.ClearAttachmentModifiers("AK-47");
		switcher.RestockAmmo();
		Assert(switcher.CurrentAmmo == 30, "CurrentAmmo initialized to 30");
		switcher.SetAttachmentModifier("AK-47", WeaponAttachmentEffectIds.AmmoCapacityDelta, -10.0f);
		Assert(switcher.CurrentAmmo == 20, $"CurrentAmmo should be clamped to 20 when magazine size reduces, got {switcher.CurrentAmmo}");

		switcher.ClearAttachmentModifiers("AK-47");
		player.QueueFree();
		GD.Print("Attachment clamping and edge cases verified successfully!");
	}

	private void TestWeaponSetsAndOperatorInfo()
	{
		GD.Print("Testing WeaponSets and OperatorInfo for all 10 weapons...");
		var registry = ResourceLoader.Load<WeaponRegistry>("res://scripts/data/WeaponRegistry.tres");
		Assert(registry != null, "registry loaded");

		foreach (var weapon in registry!.Weapons)
		{
			// 1. WeaponRules.CategoryOf must not be null
			WeaponCategory? cat = WeaponRules.CategoryOf(weapon);
			Assert(cat != null, $"WeaponRules.CategoryOf({weapon.WeaponName}) returned null!");

			// 2. WeaponRules.CategoryIcon must correspond to an existing SVG file
			string catIcon = WeaponRules.CategoryIcon(cat.Value);
			string catIconPath = $"res://art/ui/icons/{catIcon}.svg";
			Assert(ResourceLoader.Exists(catIconPath), $"Category icon missing for {cat}: {catIconPath}");

			// 3. OperatorInfo.WeaponIcon must correspond to an existing SVG file
			string weaponIcon = OperatorInfo.WeaponIcon(weapon);
			string weaponIconPath = $"res://art/ui/icons/{weaponIcon}.svg";
			Assert(ResourceLoader.Exists(weaponIconPath), $"Weapon icon missing for {weapon.WeaponName}: {weaponIconPath}");

			// 4. Fits in primary or pistol slot
			bool fitsPrimary = WeaponRules.Fits(weapon, WeaponSlot.Primary1);
			bool fitsPistol = WeaponRules.Fits(weapon, WeaponSlot.Pistol);
			Assert(fitsPrimary ^ fitsPistol, $"{weapon.WeaponName} must fit either Primary or Pistol, but not both or neither");
		}

		// Check specific slot assignments
		var ak = registry.Weapons.First(w => w.WeaponName == "AK-47");
		var bazooka = registry.Weapons.First(w => w.WeaponName == "Bazooka");
		var karambit = registry.Weapons.First(w => w.WeaponName == "Karambit");
		var glock = registry.Weapons.First(w => w.WeaponName == "Glock 19");

		Assert(WeaponRules.Fits(ak, WeaponSlot.Primary1), "AK-47 fits Primary1");
		Assert(WeaponRules.Fits(bazooka, WeaponSlot.Primary1), "Bazooka fits Primary1");
		Assert(WeaponRules.Fits(bazooka, WeaponSlot.Primary2), "Bazooka fits Primary2");
		Assert(!WeaponRules.Fits(bazooka, WeaponSlot.Pistol), "Bazooka does not fit Pistol");

		Assert(WeaponRules.Fits(karambit, WeaponSlot.Pistol), "Karambit fits Pistol");
		Assert(!WeaponRules.Fits(karambit, WeaponSlot.Primary1), "Karambit does not fit Primary1");

		Assert(WeaponRules.Fits(glock, WeaponSlot.Pistol), "Glock 19 fits Pistol");
		Assert(!WeaponRules.Fits(glock, WeaponSlot.Primary1), "Glock 19 does not fit Primary1");

		GD.Print("WeaponSets and OperatorInfo verified successfully!");
	}
}
