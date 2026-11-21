using Godot;
using System;

[GlobalClass]
public partial class TuneDeagle : Node
{
	public override async void _Ready()
	{
		GD.Print("=== VERIFYING DESERT EAGLE CONFIGURATION ===");
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
			GetTree().Quit(1);
			return;
		}

		var vm = new ViewModelBase { Name = "DeagleVM" };
		vm.Configure(data);
		camera.AddChild(vm);

		// Render hipfire
		for (int f = 0; f < 10; f++)
		{
			vm._Process(0.016);
			await ToSignal(GetTree(), "process_frame");
		}
		SaveViewport(viewport, "scripts/tests/deagle_hip_final.png");

		// Render ADS
		vm.SetAiming(true);
		for (int f = 0; f < 40; f++)
		{
			vm._Process(0.016);
			await ToSignal(GetTree(), "process_frame");
		}
		SaveViewport(viewport, "scripts/tests/deagle_ads_final.png");

		// Inspect where front_sight and rear_sight are on screen
		var frontSight = vm.FindChild("front_sight", true, false) as MeshInstance3D;
		var rearSight = vm.FindChild("rear_sight", true, false) as MeshInstance3D;
		if (frontSight != null && rearSight != null)
		{
			Vector3 fsPos = frontSight.GlobalTransform.Origin;
			Vector3 rsPos = rearSight.GlobalTransform.Origin;
			Vector2 fsScreen = camera.UnprojectPosition(fsPos);
			Vector2 rsScreen = camera.UnprojectPosition(rsPos);
			float fsDist = fsScreen.DistanceTo(new Vector2(640, 360));
			GD.Print($"VERIFIED ADS SIGHT ALIGNMENT: FrontSight Screen={fsScreen}, RearSight Screen={rsScreen}, distFromCenter={fsDist:F2}px");

			if (fsDist > 3.0f)
			{
				GD.PrintErr($"ADS sight alignment exceeds 3.0px tolerance (was {fsDist:F2}px)!");
				GetTree().Quit(1);
				return;
			}
		}
		else
		{
			GD.PrintErr("Could not find front_sight or rear_sight mesh nodes!");
			GetTree().Quit(1);
			return;
		}

		GD.Print("DESERT EAGLE VERIFICATION PASSED SUCCESSFULLY!");
		GetTree().Quit(0);

		GD.Print("DESERT EAGLE VERIFICATION PASSED SUCCESSFULLY!");
		GetTree().Quit(0);
	}

	private void SaveViewport(Viewport vp, string path)
	{
		var img = vp.GetTexture()?.GetImage();
		if (img != null) img.SavePng(path);
	}
}
