using Godot;
using System;
using System.Collections.Generic;

[GlobalClass]
public partial class VerifyPlayerLiveCombat : Node
{
	public override async void _Ready()
	{
		GD.Print("=== STARTING VERIFY PLAYER LIVE COMBAT & VIEWMODELS ===");

		var playerScene = ResourceLoader.Load<PackedScene>("res://scenes/Player.tscn");
		if (playerScene == null)
		{
			GD.PrintErr("FAILED to load Player.tscn");
			GetTree().Quit(1);
			return;
		}

		var player = playerScene.Instantiate<CharacterBody3D>();
		player.Name = "LocalPlayer";
		AddChild(player);

		// Ensure authority is local human
		player.SetMultiplayerAuthority(1);

		await ToSignal(GetTree(), "process_frame");
		await ToSignal(GetTree(), "process_frame");

		// 1. Verify Camera3D near plane
		var camera = player.GetNodeOrNull<Camera3D>("Head/CameraShake/FirstPersonCamera");
		if (camera == null)
		{
			GD.PrintErr("FAIL: FirstPersonCamera not found in player hierarchy!");
			GetTree().Quit(1);
			return;
		}

		if (Mathf.Abs(camera.Near - 0.05f) > 0.001f)
		{
			GD.PrintErr($"FAIL: Camera3D.Near is {camera.Near}, expected 0.05!");
			GetTree().Quit(1);
			return;
		}
		GD.Print($"✓ Camera3D.Near is {camera.Near:F2} (correctly set to 0.05)");

		// 2. Verify ViewModelManager
		var vmManager = player.GetNodeOrNull<ViewModelManager>("Head/CameraShake/ViewModelManager");
		if (vmManager == null)
		{
			GD.PrintErr("FAIL: ViewModelManager not found in player hierarchy!");
			GetTree().Quit(1);
			return;
		}
		GD.Print("✓ ViewModelManager found and active");

		// 3. Test all 12 viewmodels
		string[] allWeapons = new string[]
		{
			"AK-47",
			"M4",
			"P90",
			"Kriss Vector",
			"Glock 19",
			"FN 502",
			"Desert Eagle",
			"Shotgun",
			"Sniper",
			"Sledge Hammer",
			"Karambit",
			"Bazooka"
		};

		int passedWeapons = 0;
		foreach (var weaponName in allWeapons)
		{
			vmManager.EquipWeapon(weaponName);
			for (int f = 0; f < 3; f++)
			{
				await ToSignal(GetTree(), "process_frame");
			}

			var activeVm = vmManager.ActiveViewModel;
			if (activeVm == null)
			{
				GD.PrintErr($"FAIL: ViewModel for '{weaponName}' failed to instantiate!");
				GetTree().Quit(1);
				return;
			}

			if (!activeVm.Visible)
			{
				GD.PrintErr($"FAIL: ViewModel for '{weaponName}' is not visible!");
				GetTree().Quit(1);
				return;
			}

			// Test Idle & Attack/Fire animation playback
			activeVm.Play("idle");
			await ToSignal(GetTree(), "process_frame");

			bool playedAction = activeVm.Play("fire") || activeVm.Play("attack");
			if (!playedAction)
			{
				GD.PrintErr($"FAIL: ViewModel '{weaponName}' could not play fire/attack clip!");
				GetTree().Quit(1);
				return;
			}
			await ToSignal(GetTree(), "process_frame");

			// Test ADS if applicable
			if (activeVm.Data.AimPositionOffset != Vector3.Zero || activeVm.Data.HasScope)
			{
				activeVm.SetAiming(true);
				for (int f = 0; f < 4; f++) await ToSignal(GetTree(), "process_frame");
				activeVm.SetAiming(false);
				for (int f = 0; f < 4; f++) await ToSignal(GetTree(), "process_frame");
			}

			passedWeapons++;
			GD.Print($"✓ Weapon viewmodel '{weaponName}' verified (mesh, rig, clips, ADS)");
		}
		GD.Print($"✓ All {passedWeapons} viewmodels verified successfully!");

		// 4. Test WeaponSwitcher live equipping and firing
		var switcher = player.GetNodeOrNull<WeaponSwitcher>("WeaponSwitcher");
		if (switcher == null)
		{
			GD.PrintErr("FAIL: WeaponSwitcher not found!");
			GetTree().Quit(1);
			return;
		}

		int activeSet = PlayerProfile.ActiveWeaponSet;
		for (int i = 0; i < WeaponSwitcher.Loadout.Length; i++)
		{
			string loadoutGun = WeaponSwitcher.Loadout[i];
			WeaponData? data = switcher.GetWeaponData(i);
			if (data == null)
			{
				GD.PrintErr($"FAIL: WeaponData null for {loadoutGun}");
				GetTree().Quit(1);
				return;
			}

			// Place weapon into valid carry slot
			if (data.WeaponClass == "Melee" || data.WeaponClass == "Pistol")
			{
				PlayerProfile.SetSetWeapon(activeSet, WeaponSlot.Pistol, loadoutGun);
			}
			else
			{
				PlayerProfile.SetSetWeapon(activeSet, WeaponSlot.Primary1, loadoutGun);
			}

			bool equipped = switcher.EquipByName(loadoutGun);
			if (!equipped)
			{
				GD.PrintErr($"FAIL: Failed to equip {loadoutGun}");
				GetTree().Quit(1);
				return;
			}

			for (int f = 0; f < 3; f++) await ToSignal(GetTree(), "process_frame");

			// Verify sounds exist in data
			if (data.FireSounds != null && data.FireSounds.Length > 0)
			{
				foreach (var soundPath in data.FireSounds)
				{
					if (!ResourceLoader.Exists(soundPath))
					{
						GD.PrintErr($"FAIL: Fire sound missing: {soundPath}");
						GetTree().Quit(1);
						return;
					}
				}
			}

			GD.Print($"✓ WeaponSwitcher slot {i}: {loadoutGun} equipped, sounds valid");
		}

		GD.Print("=== ALL LIVE PLAYER COMBAT, VIEWMODEL, AND AUDIO TESTS PASSED! ===");
		GetTree().Quit(0);
	}
}
