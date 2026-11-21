using Godot;
using System;

[GlobalClass]
public partial class SolveAkSightAlignment : Node
{
	public override async void _Ready()
	{
		GD.Print("=== SOLVING EXACT AK SIGHT ALIGNMENT ===");
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
		var vm = new ViewModelBase { Name = "AK47VM" };
		vm.Configure(data);
		camera.AddChild(vm);
		vm.Play("idle");

		for (int i = 0; i < 15; i++)
		{
			await ToSignal(GetTree(), "process_frame");
		}

		var akMesh = vm.FindChild("Ak", true, false) as MeshInstance3D;
		var vmSkel = vm.FindChild("Skeleton3D", true, false) as Skeleton3D;
		if (akMesh == null || vmSkel == null)
		{
			GD.PrintErr("Missing mesh or skel");
			GetTree().Quit(1);
			return;
		}
		int sightBone = vmSkel.FindBone("Bone_R.013");

		// Let's test with a well-framed hipfire position where hands and gun look natural:
		// In test_pos_1: pos = (0.08, -0.24, -0.15), rot = (0, 180, 0)
		// Or pos = (0.07, -0.22, -0.12)
		Vector3 hipPos = new Vector3(0.08f, -0.22f, -0.12f);
		Vector3 hipRot = new Vector3(0.0f, 180.0f, 0.0f);

		data.ViewPosition = hipPos;
		data.ViewRotationDegrees = hipRot;
		data.AimPositionOffset = Vector3.Zero;
		data.AimRotationOffsetDegrees = Vector3.Zero;
		vm.ApplyData();
		vm.SetAiming(false);

		for (int i = 0; i < 15; i++)
		{
			await ToSignal(GetTree(), "process_frame");
		}

		// Find actual camera-space positions of sights in hipfire
		Transform3D camInverse = camera.GlobalTransform.AffineInverse();
		Transform3D boneGlobal = vmSkel.GlobalTransform * vmSkel.GetBoneGlobalPose(sightBone);
		Transform3D boneRest = vmSkel.GetBoneGlobalRest(sightBone);
		Transform3D boneDelta = boneGlobal * boneRest.AffineInverse();

		Vector3 fsLocal = new Vector3(0.0f, -0.008232f, 0.00198f);
		Vector3 rsLocal = new Vector3(0.0f, -0.00288f, 0.002019f);

		Vector3 rsInCamHip = camInverse * (boneDelta * rsLocal);
		Vector3 fsInCamHip = camInverse * (boneDelta * fsLocal);

		GD.Print($"Hipfire Rear Sight in Cam: {rsInCamHip}");
		GD.Print($"Hipfire Front Sight in Cam: {fsInCamHip}");

		// Now we want an offset (posOffset, rotOffset) such that:
		// in ADS, rot = hipRot + rotOffset, pos = hipPos + posOffset.
		// Let's test a fine-grid search around the sight angles to find the EXACT
		// alignment where rsInCam.X = 0, rsInCam.Y = 0, fsInCam.X = 0, fsInCam.Y = 0!
		// Vector from RS to FS in hipfire:
		Vector3 sightDirHip = (fsInCamHip - rsInCamHip).Normalized();
		GD.Print($"Hipfire sight direction: {sightDirHip}");

		// Desired sight dir is (0, 0, -1).
		// In Godot Euler angles (YXZ):
		// yaw = atan2(-sightDirHip.X, -sightDirHip.Z)
		// pitch = asin(sightDirHip.Y)
		float yawRad = Mathf.Atan2(-sightDirHip.X, -sightDirHip.Z);
		float pitchRad = Mathf.Asin(sightDirHip.Y);

		GD.Print($"Sight yaw angle: {Mathf.RadToDeg(yawRad)} deg, pitch angle: {Mathf.RadToDeg(pitchRad)} deg");

		// We can test candidate AimRotationOffsetDegrees:
		// Yaw offset = -RadToDeg(yawRad)
		// Pitch offset = RadToDeg(pitchRad)
		// Also roll: check if rifle top rail has roll tilt!
		Vector3 rsUpLocal = rsLocal + new Vector3(0, 0, 0.001f);
		Vector3 rsUpInCamHip = camInverse * (boneDelta * rsUpLocal);
		Vector3 upDirHip = (rsUpInCamHip - rsInCamHip).Normalized();
		GD.Print($"Hipfire sight UP direction: {upDirHip}");
		float rollRad = Mathf.Atan2(upDirHip.X, upDirHip.Y);
		GD.Print($"Sight roll angle: {Mathf.RadToDeg(rollRad)} deg");

		Vector3 curRotOffset = new Vector3(1.54f, -15.85f, 12.37f);
		Vector3 curPosOffset = new Vector3(-0.0889f, -0.0697f, 0.0f);

		for (int iter = 0; iter < 10; iter++)
		{
			data.AimPositionOffset = curPosOffset;
			data.AimRotationOffsetDegrees = curRotOffset;
			vm.ApplyData();
			vm.SetAiming(true);

			for (int f = 0; f < 10; f++)
			{
				vm._Process(0.016);
				await ToSignal(GetTree(), "process_frame");
			}

			Transform3D camInv = camera.GlobalTransform.AffineInverse();
			Transform3D bGlobal = vmSkel.GlobalTransform * vmSkel.GetBoneGlobalPose(sightBone);
			Transform3D bRest = vmSkel.GetBoneGlobalRest(sightBone);
			Transform3D bDelta = bGlobal * bRest.AffineInverse();

			Vector3 rs = camInv * (bDelta * rsLocal);
			Vector3 fs = camInv * (bDelta * fsLocal);

			float deltaZ = -(fs.Z - rs.Z);
			float errYawDeg = Mathf.RadToDeg(Mathf.Atan2(fs.X - rs.X, deltaZ));
			float errPitchDeg = Mathf.RadToDeg(Mathf.Atan2(fs.Y - rs.Y, deltaZ));

			Vector2 rsScreen = camera.UnprojectPosition(rs);
			Vector2 fsScreen = camera.UnprojectPosition(fs);
			Vector2 screenCenter = viewport.GetVisibleRect().Size * 0.5f;

			GD.Print($"Iteration {iter}:");
			GD.Print($"  FS screen: {fsScreen} (offset: {fsScreen - screenCenter})");
			GD.Print($"  RS screen: {rsScreen} (offset: {rsScreen - screenCenter})");
			GD.Print($"  Angles err: yaw={errYawDeg:F4} deg, pitch={errPitchDeg:F4} deg");
			GD.Print($"  RotOffset: {curRotOffset}, PosOffset: {curPosOffset}");

			// Adjust rotation to make sight vector parallel to cam -Z:
			// If errYaw > 0, FS is to the right of RS, so gun is pointing right. Need to yaw left (decrease curRotOffset.Y).
			// If errPitch > 0, FS is above RS, so gun is pointing up. Need to pitch down (decrease curRotOffset.X).
			curRotOffset.Y -= errYawDeg * 0.8f;
			curRotOffset.X -= errPitchDeg * 0.8f;

			// Adjust position so that FS is at screen center:
			// In camera space, X right, Y up, -Z forward.
			// At FS depth (e.g. fs.Z ~ -0.43), shifting gun X by -fs.X centers it in X!
			// Shifting gun Y by -fs.Y centers it in Y!
			curPosOffset.X -= fs.X * 0.8f;
			curPosOffset.Y -= fs.Y * 0.8f;

			if (Mathf.Abs(errYawDeg) < 0.01f && Mathf.Abs(errPitchDeg) < 0.01f && (fsScreen - screenCenter).Length() < 0.5f)
			{
				GD.Print($"CONVERGED AT ITERATION {iter}!");
				break;
			}
		}

		Image? adsImg = viewport.GetTexture()?.GetImage();
		if (adsImg != null)
		{
			adsImg.SavePng("/home/aauel/Documents/Projects/Games/wraith_run_godot/scripts/tests/ak_solved_ads.png");
			GD.Print("Saved ak_solved_ads.png");
		}

		GetTree().Quit();
	}
}
