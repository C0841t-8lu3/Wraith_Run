using Godot;
using System;

[GlobalClass]
public partial class TestSledgeRender : Node
{
	public override async void _Ready()
	{
		GD.Print("=== TESTING SLEDGE HAMMER VIEWMODEL RENDER IN GODOT ===");

		var subViewport = new SubViewport();
		subViewport.Size = new Vector2I(1280, 720);
		subViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
		AddChild(subViewport);

		var env = new Godot.Environment();
		env.BackgroundMode = Godot.Environment.BGMode.Color;
		env.BackgroundColor = new Color(0.2f, 0.22f, 0.25f);
		env.AmbientLightColor = new Color(0.9f, 0.9f, 0.9f);
		env.AmbientLightEnergy = 1.0f;

		var world3D = new World3D();
		world3D.Environment = env;
		subViewport.World3D = world3D;

		var light = new DirectionalLight3D();
		light.RotationDegrees = new Vector3(-45, 45, 0);
		light.LightEnergy = 1.5f;
		subViewport.AddChild(light);

		var camera = new Camera3D();
		camera.Fov = 75.0f;
		camera.Near = 0.005f;
		camera.Far = 100.0f;
		camera.Current = true;
		subViewport.AddChild(camera);

		var data = ResourceLoader.Load<ViewModelWeaponData>("res://data/viewmodels/Karambit.tres");
		GD.Print($"Loaded ViewModelWeaponData: weapon='{data.WeaponName}', rig='{data.RigScene?.ResourcePath}'");

		var vm = new ViewModelBase { Name = "SledgeHammerVM" };
		vm.Configure(data);
		camera.AddChild(vm);

		var tex = subViewport.GetTexture();
		string[] anims = { "idle", "walk", "run", "attack", "drawn", "put" };
		foreach (var animName in anims)
		{
			vm.Play(animName);
			for (int i = 0; i < 15; i++)
			{
				await ToSignal(GetTree(), "process_frame");
			}

			if (tex != null)
			{
				Image imgAnim = tex.GetImage();
				if (imgAnim != null)
				{
					string animPath = $"/tmp/godot_sledge_{animName}_render.png";
					imgAnim.SavePng(animPath);
					GD.Print($"Successfully saved {animName} render -> {animPath}");
				}
			}
		}

		GD.Print("=== SLEDGE RENDER TEST COMPLETE ===");
		GetTree().Quit();
	}
}
