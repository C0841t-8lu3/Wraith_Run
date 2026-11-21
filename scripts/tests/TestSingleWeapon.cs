using Godot;
using System;

[GlobalClass]
public partial class TestSingleWeapon : Node
{
	[Export] public string WeaponName = "Bazooka";

	public override async void _Ready()
	{
		string[] userArgs = OS.GetCmdlineUserArgs();
		if (userArgs.Length > 0 && !string.IsNullOrEmpty(userArgs[0]))
		{
			WeaponName = userArgs[0];
		}
		GD.Print($"=== TESTING WEAPON {WeaponName} ===");
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

		string tresName = WeaponName.Replace(" ", "").Replace("-", "");
		string tresPath = $"res://data/viewmodels/{tresName}.tres";
		var data = ResourceLoader.Load<ViewModelWeaponData>(tresPath);
		if (data == null)
		{
			GD.PrintErr($"Failed to load {tresPath}");
			GetTree().Quit();
			return;
		}

		var vm = new ViewModelBase { Name = $"{tresName}VM" };
		vm.Configure(data);
		camera.AddChild(vm);
		vm.Play("idle");

		for (int i = 0; i < 15; i++)
		{
			await ToSignal(GetTree(), "process_frame");
		}

		foreach (Node node in camera.GetChildren())
		{
			PrintHierarchy(node, 0, camera);
		}

		Image? img = viewport.GetTexture()?.GetImage();
		if (img != null)
		{
			string savePath = $"/home/aauel/Documents/Projects/Games/wraith_run_godot/scripts/tests/{tresName}_test.png";
			img.SavePng(savePath);
			GD.Print($"Saved test render to {savePath}");
		}

		if (data.HasScope || data.AimPositionOffset != Vector3.Zero || WeaponName == "Sniper")
		{
			vm.SetAiming(true);
			for (int i = 0; i < 40; i++)
			{
				await ToSignal(GetTree(), "process_frame");
			}
			GD.Print($"ADS DEBUG: AimOffset={data.AimPositionOffset} RotOffset={data.AimRotationOffsetDegrees} FinalPos={vm.Position} FinalRot={vm.RotationDegrees}");
			Image? adsImg = viewport.GetTexture()?.GetImage();
			if (adsImg != null)
			{
				string adsPath = $"/home/aauel/Documents/Projects/Games/wraith_run_godot/scripts/tests/{tresName}_ads_test.png";
				adsImg.SavePng(adsPath);
				GD.Print($"Saved ADS test render to {adsPath}");
			}
		}

		GetTree().Quit();
	}

	private void PrintHierarchy(Node node, int depth, Camera3D cam)
	{
		string indent = new string(' ', depth * 2);
		if (node is Node3D n3d)
		{
			Vector3 camSpacePos = cam.GlobalTransform.AffineInverse() * n3d.GlobalPosition;
			bool vis = n3d.Visible;
			GD.Print($"{indent}- {n3d.Name} ({n3d.GetType().Name}) vis={vis} camPos={camSpacePos} scale={n3d.Scale}");
			if (node is MeshInstance3D mesh && mesh.Mesh != null)
			{
				Aabb worldAabb = mesh.GlobalTransform * mesh.GetAabb();
				Aabb camAabb = cam.GlobalTransform.AffineInverse() * worldAabb;
				GD.Print($"{indent}  [MESH AABB in CAM]: min={camAabb.Position} max={camAabb.End} center={camAabb.GetCenter()}");
			}
		}
		foreach (Node child in node.GetChildren())
		{
			PrintHierarchy(child, depth + 1, cam);
		}
	}
}
