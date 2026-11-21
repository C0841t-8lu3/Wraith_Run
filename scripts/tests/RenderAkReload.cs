using Godot;
using System;

[GlobalClass]
public partial class RenderAkReload : Node
{
	public override async void _Ready()
	{
		GD.Print("=== RENDERING AK RELOAD FRAMES ===");
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
		data.ViewPosition = new Vector3(0.08f, -0.22f, -0.12f);
		data.ViewRotationDegrees = new Vector3(0.0f, 180.0f, 0.0f);

		var vm = new ViewModelBase { Name = "AK47VM" };
		vm.Configure(data);
		camera.AddChild(vm);

		for (int i = 0; i < 15; i++)
		{
			await ToSignal(GetTree(), "process_frame");
		}

		var ap = vm.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
		if (ap == null)
		{
			GetTree().Quit(1);
			return;
		}

		ap.Play("Rig|AK_Reload");
		float[] sampleTimes = { 0.0f, 0.4f, 0.8f, 1.1f, 1.3f, 1.5f, 1.8f, 2.2f, 2.6f };

		for (int idx = 0; idx < sampleTimes.Length; idx++)
		{
			float t = sampleTimes[idx];
			ap.Seek(t, true);
			for (int f = 0; f < 3; f++)
			{
				await ToSignal(GetTree(), "process_frame");
			}

			Image? img = viewport.GetTexture()?.GetImage();
			if (img != null)
			{
				string path = $"/home/aauel/Documents/Projects/Games/wraith_run_godot/scripts/tests/ak_reload_frame_{idx}.png";
				img.SavePng(path);
				GD.Print($"Saved {path} at t={t:F2}s");
			}
		}

		GetTree().Quit();
	}
}
