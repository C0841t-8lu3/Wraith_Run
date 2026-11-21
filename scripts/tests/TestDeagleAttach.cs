using Godot;
using System;

[GlobalClass]
public partial class TestDeagleAttach : Node
{
	public override async void _Ready()
	{
		GD.Print("=== TESTING DESERT EAGLE ATTACHMENT & ORIENTATION ===");
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

		var data = ResourceLoader.Load<ViewModelWeaponData>("res://data/viewmodels/DesertEagle.tres");
		if (data == null)
		{
			GD.PrintErr("Failed to load DesertEagle.tres");
			GetTree().Quit();
			return;
		}

		var vm = new ViewModelBase { Name = "DeagleVM" };
		vm.Configure(data);
		camera.AddChild(vm);

		// Let's test a few rotations:
		Vector3[] testRotations = new Vector3[]
		{
			new Vector3(0, 0, 0),
			new Vector3(0, -90, 0),
			new Vector3(0, 90, 0),
			new Vector3(0, 180, 0),
			new Vector3(0, 0, 90),
			new Vector3(0, 0, -90)
		};

		for (int i = 0; i < testRotations.Length; i++)
		{
			data.AttachRotationDegrees = testRotations[i];
			vm.ApplyData();
			for (int f = 0; f < 5; f++)
			{
				vm._Process(0.016);
				await ToSignal(GetTree(), "process_frame");
			}
			SaveViewport(viewport, $"scripts/tests/deagle_rot_test_{i}.png");
			GD.Print($"Rendered rot {testRotations[i]} to deagle_rot_test_{i}.png");
		}

		GetTree().Quit();
	}

	private void SaveViewport(Viewport vp, string path)
	{
		var img = vp.GetTexture()?.GetImage();
		if (img != null) img.SavePng(path);
	}
}
