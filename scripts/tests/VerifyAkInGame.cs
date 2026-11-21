using Godot;
using System;
using System.IO;

[GlobalClass]
public partial class VerifyAkInGame : Node
{
	private const string ArtifactDir = "/home/aauel/.gemini/antigravity/brain/7361caae-b949-4c4c-8fad-03a9ec75b9bb";

	public override async void _Ready()
	{
		GD.Print("=== STARTING IN-GAME AK-47 VERIFICATION PLAYTEST ===");

		var warehouseScene = ResourceLoader.Load<PackedScene>("res://scenes/maps/DefaultWarehouse.tscn");
		if (warehouseScene != null)
		{
			var warehouse = warehouseScene.Instantiate<Node3D>();
			AddChild(warehouse);
		}

		var playerScene = ResourceLoader.Load<PackedScene>("res://scenes/Player.tscn");
		if (playerScene == null)
		{
			GD.PrintErr("FAILED to load Player.tscn");
			GetTree().Quit(1);
			return;
		}

		var player = playerScene.Instantiate<CharacterBody3D>();
		player.Name = "LocalPlayer";
		player.Position = new Vector3(0, 1.0f, 0);
		AddChild(player);
		player.SetMultiplayerAuthority(1);

		var switcher = player.GetNodeOrNull<WeaponSwitcher>("WeaponSwitcher")
			?? player.FindChild("WeaponSwitcher", true, false) as WeaponSwitcher;
		var vmManager = player.GetNodeOrNull<ViewModelManager>("Head/CameraShake/ViewModelManager")
			?? player.FindChild("ViewModelManager", true, false) as ViewModelManager;
		var movement = player.GetNodeOrNull<PlayerMovement>(".")
			?? player as PlayerMovement;

		for (int i = 0; i < 5; i++) await ToSignal(GetTree(), "process_frame");

		if (switcher != null)
		{
			switcher.ReplicatedWeaponIndex = 0; // AK-47
		}
		if (vmManager != null)
		{
			vmManager.EquipWeapon("AK-47");
		}

		for (int i = 0; i < 10; i++) await ToSignal(GetTree(), "process_frame");

		var activeVm = vmManager?.ActiveViewModel;
		if (activeVm == null)
		{
			GD.PrintErr("FAIL: ActiveViewModel is null after equipping AK-47!");
			GetTree().Quit(1);
			return;
		}

		var viewport = GetViewport();

		// 1. In-game Hipfire Idle (natural breathing)
		for (int i = 0; i < 25; i++) await ToSignal(GetTree(), "process_frame");
		SaveCapture(viewport, "ak_ingame_idle.png");

		// 2. In-game ADS (Aim Down Sights centered)
		Input.ActionPress("aim");
		for (int i = 0; i < 35; i++) await ToSignal(GetTree(), "process_frame");
		SaveCapture(viewport, "ak_ingame_ads.png");
		Input.ActionRelease("aim");
		for (int i = 0; i < 20; i++) await ToSignal(GetTree(), "process_frame");

		// 3. In-game Walk
		Input.ActionPress("move_forward");
		for (int i = 0; i < 30; i++) await ToSignal(GetTree(), "process_frame");
		SaveCapture(viewport, "ak_ingame_walk.png");

		// 4. In-game Sprint
		Input.ActionPress("sprint");
		for (int i = 0; i < 25; i++) await ToSignal(GetTree(), "process_frame");
		SaveCapture(viewport, "ak_ingame_sprint.png");
		Input.ActionRelease("sprint");
		Input.ActionRelease("move_forward");
		for (int i = 0; i < 15; i++) await ToSignal(GetTree(), "process_frame");

		// 5. In-game Fire with muzzle flash & kick
		activeVm.Play("fire");
		activeVm.PlayMuzzleFire();
		activeVm.AddRecoilKick(1.0f);
		for (int i = 0; i < 4; i++) await ToSignal(GetTree(), "process_frame");
		SaveCapture(viewport, "ak_ingame_fire.png");

		// 6. In-game Tactical Reload - Magazine Eject
		var ap = activeVm.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		activeVm.Play("reload");
		if (ap != null) ap.Seek(0.9f, true);
		for (int i = 0; i < 5; i++) await ToSignal(GetTree(), "process_frame");
		SaveCapture(viewport, "ak_ingame_reload_eject.png");

		// 7. In-game Tactical Reload - Magazine Insertion (0 clipping)
		if (ap != null) ap.Seek(1.7f, true);
		for (int i = 0; i < 5; i++) await ToSignal(GetTree(), "process_frame");
		SaveCapture(viewport, "ak_ingame_reload_insert.png");

		// 8. In-game Full Reload - Bolt charging handle rack
		activeVm.Play("reload_full");
		if (ap != null) ap.Seek(2.7f, true);
		for (int i = 0; i < 5; i++) await ToSignal(GetTree(), "process_frame");
		SaveCapture(viewport, "ak_ingame_reload_full_charge.png");

		GD.Print("=== ALL IN-GAME AK-47 VERIFICATION CAPTURES SAVED SUCCESSFULLY ===");
		GetTree().Quit(0);
	}

	private void SaveCapture(Viewport vp, string filename)
	{
		var img = vp.GetTexture()?.GetImage();
		if (img != null)
		{
			string testPath = $"scripts/tests/{filename}";
			img.SavePng(testPath);
			GD.Print($"Saved in-game render: {testPath}");

			try
			{
				string artPath = Path.Combine(ArtifactDir, filename);
				img.SavePng(artPath);
				GD.Print($"Saved artifact render: {artPath}");
			}
			catch (Exception ex)
			{
				GD.PrintErr($"Failed to copy to artifact dir: {ex.Message}");
			}
		}
	}
}
