using Godot;
using System;

[GlobalClass]
public partial class DiagnoseAkRig : Node
{
	public override async void _Ready()
	{
		GD.Print("=== DIAGNOSING AK RIG ===");
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

		// 1. Check raw FBX instantiation
		var fbxScene = ResourceLoader.Load<PackedScene>("res://art/animations/player/combat/AK/source/FpsAnims.fbx");
		var rawFbx = fbxScene.Instantiate<Node3D>();
		AddChild(rawFbx);

		var authorCam = rawFbx.FindChild("Camera", true, false) as Camera3D;
		if (authorCam != null)
		{
			GD.Print($"Raw FBX Camera: global_pos={authorCam.GlobalPosition} global_rot={authorCam.GlobalRotationDegrees} fov={authorCam.Fov}");
			Transform3D camInRoot = rawFbx.GlobalTransform.AffineInverse() * authorCam.GlobalTransform;
			GD.Print($"Raw FBX CamInRoot: pos={camInRoot.Origin} rot={camInRoot.Basis.GetEuler() * (180.0f / Mathf.Pi)}");
		}

		var skel = rawFbx.FindChild("Skeleton3D", true, false) as Skeleton3D;
		if (skel != null)
		{
			int headCamIdx = skel.FindBone("Head_Cam");
			if (headCamIdx >= 0)
			{
				var headCamPose = skel.GetBoneGlobalPose(headCamIdx);
				GD.Print($"Skeleton Head_Cam bone pose: origin={headCamPose.Origin} rot={headCamPose.Basis.GetEuler() * (180.0f / Mathf.Pi)}");
			}
			int rootIdx = skel.FindBone("Root");
			if (rootIdx >= 0)
			{
				var rootPose = skel.GetBoneGlobalPose(rootIdx);
				GD.Print($"Skeleton Root bone pose: origin={rootPose.Origin} rot={rootPose.Basis.GetEuler() * (180.0f / Mathf.Pi)}");
			}
			int pbodyIdx = skel.FindBone("PBody");
			if (pbodyIdx >= 0)
			{
				var pbodyPose = skel.GetBoneGlobalPose(pbodyIdx);
				GD.Print($"Skeleton PBody bone pose: origin={pbodyPose.Origin} rot={pbodyPose.Basis.GetEuler() * (180.0f / Mathf.Pi)}");
			}
		}

		rawFbx.QueueFree();
		await ToSignal(GetTree(), "process_frame");

		// 2. Test variations of ViewPosition
		var data = ResourceLoader.Load<ViewModelWeaponData>("res://data/viewmodels/AK47.tres");
		data.UseRigCameraTransform = false;

		// Test a few Z and X offsets
		Vector3[] testPositions = new Vector3[]
		{
			new Vector3(0.06f, -0.261f, -0.043f), // Original
			new Vector3(0.08f, -0.24f, -0.15f),
			new Vector3(0.10f, -0.22f, -0.18f),
			new Vector3(0.12f, -0.22f, -0.20f),
			new Vector3(0.10f, -0.20f, -0.22f),
			new Vector3(0.08f, -0.18f, -0.22f),
		};

		for (int idx = 0; idx < testPositions.Length; idx++)
		{
			data.ViewPosition = testPositions[idx];
			var vm = new ViewModelBase { Name = $"AK47VM_{idx}" };
			vm.Configure(data);
			camera.AddChild(vm);
			vm.Play("idle");

			for (int i = 0; i < 15; i++)
			{
				await ToSignal(GetTree(), "process_frame");
			}

			var vmSkel = vm.FindChild("Skeleton3D", true, false) as Skeleton3D;
			if (vmSkel != null)
			{
				int rHand = vmSkel.FindBone("Hand_R");
				int lHand = vmSkel.FindBone("Hand_L");
				Vector3 rPos = camera.GlobalTransform.AffineInverse() * (vmSkel.GlobalTransform * vmSkel.GetBoneGlobalPose(rHand).Origin);
				Vector3 lPos = camera.GlobalTransform.AffineInverse() * (vmSkel.GlobalTransform * vmSkel.GetBoneGlobalPose(lHand).Origin);
				GD.Print($"Test {idx} (pos={testPositions[idx]}): Hand_R={rPos}, Hand_L={lPos}");
			}

			Image? img = viewport.GetTexture()?.GetImage();
			if (img != null)
			{
				string path = $"/home/aauel/Documents/Projects/Games/wraith_run_godot/scripts/tests/ak_test_pos_{idx}.png";
				img.SavePng(path);
				GD.Print($"Saved {path}");
			}

			camera.RemoveChild(vm);
			vm.QueueFree();
			await ToSignal(GetTree(), "process_frame");
		}

		GetTree().Quit();
	}
}
