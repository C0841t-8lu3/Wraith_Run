using Godot;
using System;

[GlobalClass]
public partial class AkDiagnosticRunner : Node
{
	public override async void _Ready()
	{
		GD.Print("=== RUNNING AK-47 ALL-STATES DIAGNOSTIC & RENDERER ===");
		var viewport = GetViewport();

		var env = new Godot.Environment();
		env.BackgroundMode = Godot.Environment.BGMode.Color;
		env.BackgroundColor = new Color(0.2f, 0.22f, 0.25f);
		env.AmbientLightColor = new Color(0.9f, 0.9f, 0.9f);
		env.AmbientLightEnergy = 1.0f;

		var world3D = GetTree().Root.World3D ?? new World3D();
		world3D.Environment = env;

		var light = new DirectionalLight3D();
		light.RotationDegrees = new Vector3(-45, 45, 0);
		light.LightEnergy = 1.5f;
		AddChild(light);

		var camera = new Camera3D();
		camera.Fov = 75.0f;
		camera.Near = 0.01f;
		camera.Far = 100.0f;
		camera.Current = true;
		AddChild(camera);

		var data = ResourceLoader.Load<ViewModelWeaponData>("res://data/viewmodels/AK47.tres");
		if (data == null)
		{
			GD.PrintErr("Failed to load AK47.tres");
			GetTree().Quit();
			return;
		}

		var vm = new ViewModelBase { Name = "AKVM" };
		vm.Configure(data);
		camera.AddChild(vm);

		var ap = vm.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		GD.Print($"Found AnimationPlayer: {ap != null}");

		// 1. Idle (Hipfire)
		vm.Play("idle");
		for (int i = 0; i < 15; i++)
		{
			vm._Process(0.016);
			await ToSignal(GetTree(), "process_frame");
		}
		SaveViewport(viewport, "scripts/tests/ak_diag_idle.png");

		// 2. Walk frame
		vm.Play("walk");
		for (int i = 0; i < 20; i++)
		{
			vm._Process(0.016);
			await ToSignal(GetTree(), "process_frame");
		}
		SaveViewport(viewport, "scripts/tests/ak_diag_walk.png");

		// 3. Run frame
		vm.Play("run");
		for (int i = 0; i < 15; i++)
		{
			vm._Process(0.016);
			await ToSignal(GetTree(), "process_frame");
		}
		SaveViewport(viewport, "scripts/tests/ak_diag_run.png");

		// 4. Fire frame (kick)
		vm.Play("fire");
		vm.AddRecoilKick(1.0f);
		for (int i = 0; i < 5; i++)
		{
			vm._Process(0.016);
			await ToSignal(GetTree(), "process_frame");
		}
		SaveViewport(viewport, "scripts/tests/ak_diag_fire.png");

		// 5. ADS Sight Picture
		vm.Play("idle");
		vm.SetAiming(true);
		for (int i = 0; i < 40; i++)
		{
			vm._Process(0.016);
			await ToSignal(GetTree(), "process_frame");
		}
		SaveViewport(viewport, "scripts/tests/ak_diag_ads.png");

		var akMesh = vm.FindChild("Ak", true, false) as MeshInstance3D;
		if (akMesh != null)
		{
			Vector3 postLocal = new Vector3(-0.000005f, 0.004261f, 0.001211f);
			Vector3 postGlobal = akMesh.GlobalTransform * postLocal;
			Vector2 postScreen = camera.UnprojectPosition(postGlobal);
			Vector3 postInCam = camera.GlobalTransform.AffineInverse() * postGlobal;
			GD.Print($"AK47 ADS SIGHT ALIGNMENT: ScreenPos={postScreen} (Target is (640, 360)), InCam={postInCam}");
		}

		// 6. Reload sequence
		vm.SetAiming(false);
		for (int i = 0; i < 20; i++)
		{
			vm._Process(0.016);
			await ToSignal(GetTree(), "process_frame");
		}

		if (ap != null)
		{
			vm.Play("reload");
			float[] reloadTimes = { 0.3f, 0.7f, 1.1f, 1.4f, 1.8f, 2.2f };
			for (int idx = 0; idx < reloadTimes.Length; idx++)
			{
				ap.Seek(reloadTimes[idx], true);
				vm._Process(0.016);
				for (int i = 0; i < 5; i++)
				{
					vm._Process(0.016);
					await ToSignal(GetTree(), "process_frame");
				}
				SaveViewport(viewport, $"scripts/tests/ak_diag_reload_{idx}.png");
			}

			// 7. Reload Full sequence
			vm.Play("reload_full");
			float[] fullTimes = { 0.5f, 1.2f, 1.8f, 2.4f, 3.0f };
			for (int idx = 0; idx < fullTimes.Length; idx++)
			{
				ap.Seek(fullTimes[idx], true);
				vm._Process(0.016);
				for (int i = 0; i < 5; i++)
				{
					vm._Process(0.016);
					await ToSignal(GetTree(), "process_frame");
				}
				SaveViewport(viewport, $"scripts/tests/ak_diag_reload_full_{idx}.png");
			}
		}

		GD.Print("=== DIAGNOSTIC COMPLETE ===");
		GetTree().Quit();
	}

	private void SaveViewport(Viewport vp, string path)
	{
		var img = vp.GetTexture()?.GetImage();
		if (img != null)
		{
			img.SavePng(path);
			GD.Print($"Saved: {path}");
		}
	}
}
