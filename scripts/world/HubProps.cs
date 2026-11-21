using System.Collections.Generic;
using Godot;

/// <summary>
/// Bodycam Warehouse Hub Architecture & Tactical Props (Master GDD Section 6.1):
/// Converts the warehouse into a gritty, atmospheric CQB training facility:
/// 1. CQB Tactical Kill House with modular plywood walls, door breaches, window cutouts, sandbags, and tire stacks.
/// 2. 3-Lane Firing Range with shooting booths, distance markers (5M, 10M, 15M, 20M), and reactive target dummies.
/// 3. Motor Pool & Vehicle Bay with hazard floor striping, tool chests, and parked STV / Buggy.
/// 4. Ready Room & Command Station with military gear lockers, tactical whiteboard, and exit door.
/// 5. Atmospheric Industrial High-Bay Lighting with dramatic shadows and volumetric light cones.
/// </summary>
public static class HubProps
{
    private const string CarPath = "res://art/vehicles/Low_Poly_Car_-_Toyota_Land_Cruiser_70_GXL_2024.glb";
    private const string BuggyPath = "res://art/vehicles/raider_buggy.glb";

    public static void Build(Node3D world)
    {
        var root = new Node3D { Name = "HubProps" };
        world.AddChild(root);

        // 1. Atmospheric High-Bay Lighting
        AddHighBayLighting(root);

        // 2. CQB Tactical Kill House (Center Zone)
        AddKillHouse(root);

        // 3. 3-Lane Firing Range (East Zone)
        AddFiringRange(root);

        // 4. Motor Pool & Vehicle Bay (South Zone)
        AddVehicleBay(root);

        // 5. Ready Room & Command Area (West Zone)
        AddReadyRoom(root);
    }

    // ---------------------------------------------------------------------------------------------
    // 1. Atmospheric Industrial Lighting
    // ---------------------------------------------------------------------------------------------

    private static void AddHighBayLighting(Node3D root)
    {
        var lightRoot = new Node3D { Name = "IndustrialCeilingLights" };
        root.AddChild(lightRoot);

        var darkSteel = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.12f, 0.14f), Metallic = 0.8f, Roughness = 0.4f };
        var lampBulbMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.98f, 0.94f, 0.85f),
            EmissionEnabled = true,
            Emission = new Color(0.98f, 0.94f, 0.85f),
            EmissionEnergyMultiplier = 4.0f
        };

        // 6 High-bay pendant lamps hung from the ceiling trusses across the warehouse
        Vector3[] lampPositions = new[]
        {
            new Vector3(-8.0f, 7.2f, -8.0f),
            new Vector3(0.0f,  7.2f, -8.0f),
            new Vector3(8.0f,  7.2f, -8.0f),
            new Vector3(-8.0f, 7.2f,  2.0f),
            new Vector3(0.0f,  7.2f,  2.0f),
            new Vector3(8.0f,  7.2f,  2.0f),
        };

        for (int i = 0; i < lampPositions.Length; i++)
        {
            Vector3 pos = lampPositions[i];
            var fixture = new Node3D { Name = $"HighBayLamp_{i + 1}", Position = pos };
            lightRoot.AddChild(fixture);

            // Hanging cable down from ceiling (roof is at ~8.5m)
            var cable = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.015f, BottomRadius = 0.015f, Height = 1.3f },
                Position = new Vector3(0.0f, 0.65f, 0.0f),
                MaterialOverride = darkSteel
            };
            fixture.AddChild(cable);

            // Reflector shade / bell housing
            var shade = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.14f, BottomRadius = 0.45f, Height = 0.35f },
                Position = new Vector3(0.0f, 0.05f, 0.0f),
                MaterialOverride = darkSteel
            };
            fixture.AddChild(shade);

            // Glowing emitter bulb
            var bulb = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.12f, Height = 0.20f },
                Position = new Vector3(0.0f, -0.06f, 0.0f),
                MaterialOverride = lampBulbMat
            };
            fixture.AddChild(bulb);

            // OmniLight with shadow casting and warm tactical ambiance
            var light = new OmniLight3D
            {
                LightColor = new Color(0.98f, 0.94f, 0.86f),
                LightEnergy = 1.85f,
                OmniRange = 14.5f,
                OmniAttenuation = 1.15f,
                ShadowEnabled = true,
                Position = new Vector3(0.0f, -0.15f, 0.0f)
            };
            fixture.AddChild(light);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 2. CQB Tactical Kill House (Center Zone)
    // ---------------------------------------------------------------------------------------------

    private static void AddKillHouse(Node3D root)
    {
        var killHouse = new Node3D { Name = "TacticalKillHouse" };
        root.AddChild(killHouse);

        var plywoodMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.72f, 0.62f, 0.50f),
            AlbedoTexture = ResourceLoader.Load<Texture2D>("res://art/materials/tex/Planks039_Color.jpg"),
            NormalEnabled = true,
            NormalTexture = ResourceLoader.Load<Texture2D>("res://art/materials/tex/Planks039_Normal.jpg"),
            Roughness = 0.85f,
            Uv1Scale = new Vector3(2.0f, 2.0f, 1.0f)
        };
        var darkSteel = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.16f, 0.17f), Metallic = 0.8f, Roughness = 0.5f };
        var sandbagMat = new StandardMaterial3D { AlbedoColor = new Color(0.55f, 0.50f, 0.38f), Roughness = 0.95f };
        var tireRubberMat = new StandardMaterial3D { AlbedoColor = new Color(0.10f, 0.11f, 0.12f), Roughness = 0.92f };
        var drumRedMat = new StandardMaterial3D { AlbedoColor = new Color(0.65f, 0.18f, 0.15f), Metallic = 0.6f, Roughness = 0.55f };
        var drumBlueMat = new StandardMaterial3D { AlbedoColor = new Color(0.18f, 0.35f, 0.55f), Metallic = 0.6f, Roughness = 0.55f };

        // Modular Plywood Training Walls (Height 2.4m, Thickness 0.12m)
        // Room A (West: X = -5.0 to -1.0, Z = -5.5 to -1.5)
        AddSolidWall(killHouse, "WallA_North", new Vector3(-3.0f, 1.2f, -5.5f), new Vector3(4.0f, 2.4f, 0.12f), plywoodMat);
        AddSolidWall(killHouse, "WallA_West", new Vector3(-5.0f, 1.2f, -3.5f), new Vector3(0.12f, 2.4f, 4.0f), plywoodMat);

        // Room A South Wall with Doorway cutout (1.1m wide door in middle)
        AddSolidWall(killHouse, "WallA_SouthL", new Vector3(-4.4f, 1.2f, -1.5f), new Vector3(1.2f, 2.4f, 0.12f), plywoodMat);
        AddSolidWall(killHouse, "WallA_SouthR", new Vector3(-1.6f, 1.2f, -1.5f), new Vector3(1.2f, 2.4f, 0.12f), plywoodMat);
        AddSolidWall(killHouse, "WallA_DoorHeader", new Vector3(-3.0f, 2.15f, -1.5f), new Vector3(1.6f, 0.5f, 0.12f), plywoodMat);

        // Room A East Dividing Wall with Window Cutout
        AddSolidWall(killHouse, "WallA_EastBottom", new Vector3(-1.0f, 0.5f, -3.5f), new Vector3(0.12f, 1.0f, 4.0f), plywoodMat);
        AddSolidWall(killHouse, "WallA_EastTop", new Vector3(-1.0f, 2.15f, -3.5f), new Vector3(0.12f, 0.5f, 4.0f), plywoodMat);
        AddSolidWall(killHouse, "WallA_EastL", new Vector3(-1.0f, 1.35f, -5.0f), new Vector3(0.12f, 0.7f, 1.0f), plywoodMat);
        AddSolidWall(killHouse, "WallA_EastR", new Vector3(-1.0f, 1.35f, -2.0f), new Vector3(0.12f, 0.7f, 1.0f), plywoodMat);

        // Room B (Center / East: X = -0.9 to 4.5, Z = -3.5 to 1.5)
        AddSolidWall(killHouse, "WallB_North", new Vector3(2.0f, 1.2f, -3.5f), new Vector3(4.5f, 2.4f, 0.12f), plywoodMat);
        AddSolidWall(killHouse, "WallB_East", new Vector3(4.5f, 1.2f, -1.0f), new Vector3(0.12f, 2.4f, 5.0f), plywoodMat);
        AddSolidWall(killHouse, "WallB_CornerBreach", new Vector3(2.5f, 1.2f, 1.5f), new Vector3(4.0f, 2.4f, 0.12f), plywoodMat);

        // Tactical Sandbag Barricades (Low cover)
        AddSolidWall(killHouse, "Sandbags_1", new Vector3(-4.8f, 0.55f, 0.2f), new Vector3(2.2f, 1.1f, 0.45f), sandbagMat);
        AddSolidWall(killHouse, "Sandbags_2", new Vector3(1.5f, 0.55f, 2.8f), new Vector3(2.4f, 1.1f, 0.45f), sandbagMat);

        // Heavy Vehicle Tire Stacks (3 stacks of 4 tires each)
        AddTireStack(killHouse, new Vector3(-5.2f, 0.0f, -6.0f), 4, tireRubberMat);
        AddTireStack(killHouse, new Vector3(4.8f, 0.0f, -4.0f), 4, tireRubberMat);
        AddTireStack(killHouse, new Vector3(0.0f, 0.0f, 2.2f), 3, tireRubberMat);

        // 55-Gallon Steel Oil Drums (Cover barrels)
        AddOilDrum(killHouse, new Vector3(-3.2f, 0.0f, -4.8f), drumRedMat);
        AddOilDrum(killHouse, new Vector3(-2.6f, 0.0f, -4.5f), darkSteel);
        AddOilDrum(killHouse, new Vector3(3.6f, 0.0f, -2.8f), drumBlueMat);
        AddOilDrum(killHouse, new Vector3(4.0f, 0.0f, -2.2f), drumRedMat);
        AddOilDrum(killHouse, new Vector3(-4.2f, 0.0f, 0.6f), darkSteel);
        AddOilDrum(killHouse, new Vector3(3.8f, 0.0f, 1.8f), drumBlueMat);

        // Wooden Shipping Pallet with Cinder Blocks
        AddPalletStack(killHouse, new Vector3(-2.0f, 0.0f, 1.0f), 2, plywoodMat, darkSteel);
    }

    private static void AddSolidWall(Node3D parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var body = new StaticBody3D { Name = name, Position = pos };
        body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = mat });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        parent.AddChild(body);
    }

    private static void AddTireStack(Node3D parent, Vector3 pos, int count, Material mat)
    {
        var stack = new StaticBody3D { Name = $"TireStack_{pos.X}_{pos.Z}", Position = pos };
        float tireHeight = 0.28f;
        float radius = 0.42f;
        for (int i = 0; i < count; i++)
        {
            var mesh = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = tireHeight },
                Position = new Vector3(0.0f, i * tireHeight + tireHeight * 0.5f, 0.0f),
                MaterialOverride = mat
            };
            stack.AddChild(mesh);
        }
        stack.AddChild(new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = radius, Height = count * tireHeight },
            Position = new Vector3(0.0f, count * tireHeight * 0.5f, 0.0f)
        });
        parent.AddChild(stack);
    }

    private static void AddOilDrum(Node3D parent, Vector3 pos, Material mat)
    {
        var drum = new StaticBody3D { Name = $"OilDrum_{pos.X}_{pos.Z}", Position = pos };
        float height = 0.95f;
        float radius = 0.32f;
        drum.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height },
            Position = new Vector3(0.0f, height * 0.5f, 0.0f),
            MaterialOverride = mat
        });
        drum.AddChild(new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = radius, Height = height },
            Position = new Vector3(0.0f, height * 0.5f, 0.0f)
        });
        parent.AddChild(drum);
    }

    private static void AddPalletStack(Node3D parent, Vector3 pos, int palletCount, Material woodMat, Material blockMat)
    {
        var root = new StaticBody3D { Name = $"PalletStack_{pos.X}_{pos.Z}", Position = pos };
        float h = 0.14f * palletCount;
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.2f, h, 1.0f) },
            Position = new Vector3(0.0f, h * 0.5f, 0.0f),
            MaterialOverride = woodMat
        });
        // Concrete cinder blocks on top
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.9f, 0.35f, 0.75f) },
            Position = new Vector3(0.0f, h + 0.175f, 0.0f),
            MaterialOverride = blockMat
        });
        root.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(1.2f, h + 0.35f, 1.0f) },
            Position = new Vector3(0.0f, (h + 0.35f) * 0.5f, 0.0f)
        });
        parent.AddChild(root);
    }

    // ---------------------------------------------------------------------------------------------
    // 3. 3-Lane Firing Range (East Zone)
    // ---------------------------------------------------------------------------------------------

    private static void AddFiringRange(Node3D root)
    {
        var rangeRoot = new Node3D { Name = "FiringRange" };
        root.AddChild(rangeRoot);

        var timberMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.72f, 0.62f, 0.50f),
            AlbedoTexture = ResourceLoader.Load<Texture2D>("res://art/materials/tex/Planks039_Color.jpg"),
            NormalEnabled = true,
            NormalTexture = ResourceLoader.Load<Texture2D>("res://art/materials/tex/Planks039_Normal.jpg"),
            Roughness = 0.85f,
            Uv1Scale = new Vector3(2.0f, 2.0f, 1.0f)
        };
        var darkSteel = new StandardMaterial3D { AlbedoColor = new Color(0.14f, 0.15f, 0.16f), Metallic = 0.85f, Roughness = 0.4f };

        // 3 Shooting Booths along X = 7.5m, facing down the lane toward the back wall (-Z)
        float[] boothXs = { 9.0f, 11.5f, 14.0f };
        for (int b = 0; b < boothXs.Length; b++)
        {
            float bx = boothXs[b];

            // Partition Wall dividing booths
            AddSolidWall(rangeRoot, $"BoothPartition_{b}", new Vector3(bx - 1.25f, 1.2f, 1.0f), new Vector3(0.10f, 2.4f, 2.4f), timberMat);

            // Shooting Bench with wooden rifle rest table
            var benchBody = new StaticBody3D { Name = $"ShootingBench_{b + 1}", Position = new Vector3(bx, 0.45f, 0.2f) };
            benchBody.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.8f, 0.12f, 0.85f) },
                Position = new Vector3(0.0f, 0.45f, 0.0f),
                MaterialOverride = timberMat
            });
            benchBody.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(1.7f, 0.85f, 0.75f) },
                Position = Vector3.Zero,
                MaterialOverride = darkSteel
            });
            benchBody.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.8f, 0.95f, 0.85f) } });
            rangeRoot.AddChild(benchBody);

            // Overhead Lane Signs
            var laneSign = new Label3D
            {
                Name = $"LaneSign_{b + 1}",
                Text = $"[ LANE 0{b + 1} ]",
                FontSize = 36,
                PixelSize = 0.0035f,
                OutlineSize = 6,
                Modulate = new Color(0.2f, 0.85f, 1.0f), // Tactical cyan
                Position = new Vector3(bx, 2.7f, 1.0f)
            };
            rangeRoot.AddChild(laneSign);
        }
        // Right outer partition wall
        AddSolidWall(rangeRoot, "BoothPartition_Outer", new Vector3(15.25f, 1.2f, 1.0f), new Vector3(0.10f, 2.4f, 2.4f), timberMat);

        // Distance Markings on the floor down the range lanes (5M, 10M, 15M, 20M)
        (float z, string txt)[] distances = { (-2.5f, "— 5 METERS —"), (-6.5f, "— 10 METERS —"), (-9.5f, "— 15 METERS —"), (-12.5f, "— 20 METERS —") };
        for (int d = 0; d < distances.Length; d++)
        {
            var distLabel = new Label3D
            {
                Name = $"DistMark_{distances[d].txt}",
                Text = distances[d].txt,
                FontSize = 30,
                PixelSize = 0.0040f,
                OutlineSize = 6,
                Modulate = new Color(1.0f, 0.85f, 0.2f, 0.85f), // Tactical hazard yellow
                RotationDegrees = new Vector3(-90, 0, 0),
                Position = new Vector3(11.5f, 0.02f, distances[d].z)
            };
            rangeRoot.AddChild(distLabel);
        }

        // 3 Reactive Target Dummies placed at staggered tactical distances
        Vector3[] targetPositions = new[]
        {
            new Vector3(9.0f,  0.0f, -3.2f),  // Lane 1: Close range (5M)
            new Vector3(11.5f, 0.0f, -8.0f),  // Lane 2: Mid range (12M)
            new Vector3(14.0f, 0.0f, -12.0f), // Lane 3: Long range (18M)
        };

        for (int i = 0; i < targetPositions.Length; i++)
        {
            var target = new FiringRangeTarget
            {
                Name = $"RangeTarget_{i + 1}",
                Position = targetPositions[i]
            };
            rangeRoot.AddChild(target);

            // Overhead task light shining on each target
            var spot = new SpotLight3D
            {
                LightColor = new Color(0.96f, 0.98f, 1.0f),
                LightEnergy = 2.2f,
                SpotRange = 8.5f,
                SpotAngle = 45.0f,
                Position = new Vector3(targetPositions[i].X, 6.5f, targetPositions[i].Z + 1.5f),
                RotationDegrees = new Vector3(-65, 0, 0)
            };
            rangeRoot.AddChild(spot);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Motor Pool & Vehicle Bay (South Zone)
    // ---------------------------------------------------------------------------------------------

    private static void AddVehicleBay(Node3D root)
    {
        var bayRoot = new Node3D { Name = "VehicleBay" };
        root.AddChild(bayRoot);

        // Parked STV and Raider Buggy (Master GDD Section 6.1)
        AddCar(bayRoot, CarPath, 4.9f, new Vector3(-9.4f, 0.0f, 9.4f), "ParkedCar");
        AddCar(bayRoot, BuggyPath, 3.6f, new Vector3(8.2f, 0.0f, 9.4f), "ParkedBuggy");

        var darkSteel = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.13f, 0.14f), Metallic = 0.85f, Roughness = 0.4f };

        // Mechanic's Heavy Tool Chest & Workstation
        var toolBench = new StaticBody3D { Name = "MechanicBench", Position = new Vector3(-13.5f, 0.5f, 8.5f) };
        toolBench.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.2f, 1.0f, 2.8f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.65f, 0.15f, 0.15f), Roughness = 0.6f } // Tool red
        });
        toolBench.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(1.2f, 1.0f, 2.8f) } });
        bayRoot.AddChild(toolBench);

        // Spare Off-Road Tires leaning against the south wall
        var tireMat = new StandardMaterial3D { AlbedoColor = new Color(0.10f, 0.11f, 0.12f), Roughness = 0.95f };
        for (int t = 0; t < 3; t++)
        {
            var spareTire = new MeshInstance3D
            {
                Name = $"SpareTire_{t}",
                Mesh = new CylinderMesh { TopRadius = 0.45f, BottomRadius = 0.45f, Height = 0.32f },
                Position = new Vector3(-5.2f + t * 0.7f, 0.45f, 13.5f),
                RotationDegrees = new Vector3(15, 0, 90),
                MaterialOverride = tireMat
            };
            bayRoot.AddChild(spareTire);
        }

        // Industrial Roll-Up Shutter Garage Door on the South Wall (Visual Framing)
        var shutterFrameMat = new StandardMaterial3D { AlbedoColor = new Color(0.16f, 0.17f, 0.18f), Metallic = 0.9f, Roughness = 0.35f };
        var shutterSlatMat = ResourceLoader.Load<StandardMaterial3D>("res://art/materials/warehouse/WeatheredSteel.tres") ??
            new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.36f, 0.38f), Metallic = 0.7f, Roughness = 0.6f };

        var shutterRoot = new Node3D { Name = "IndustrialGarageDoor", Position = new Vector3(0.0f, 0.0f, 13.85f) };
        shutterRoot.AddChild(new MeshInstance3D
        {
            Name = "ShutterCurtain",
            Mesh = new BoxMesh { Size = new Vector3(10.5f, 5.2f, 0.15f) },
            Position = new Vector3(0.0f, 2.6f, 0.0f),
            MaterialOverride = shutterSlatMat
        });
        shutterRoot.AddChild(new MeshInstance3D
        {
            Name = "ShutterFrameL",
            Mesh = new BoxMesh { Size = new Vector3(0.35f, 5.4f, 0.25f) },
            Position = new Vector3(-5.35f, 2.7f, 0.05f),
            MaterialOverride = shutterFrameMat
        });
        shutterRoot.AddChild(new MeshInstance3D
        {
            Name = "ShutterFrameR",
            Mesh = new BoxMesh { Size = new Vector3(0.35f, 5.4f, 0.25f) },
            Position = new Vector3(5.35f, 2.7f, 0.05f),
            MaterialOverride = shutterFrameMat
        });
        shutterRoot.AddChild(new MeshInstance3D
        {
            Name = "ShutterHeader",
            Mesh = new BoxMesh { Size = new Vector3(11.2f, 0.5f, 0.35f) },
            Position = new Vector3(0.0f, 5.4f, 0.08f),
            MaterialOverride = shutterFrameMat
        });

        // Yellow Caution Lights on Garage Door Frame
        var yellowBeaconMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 0.75f, 0.1f),
            EmissionEnabled = true,
            Emission = new Color(1.0f, 0.75f, 0.1f),
            EmissionEnergyMultiplier = 3.0f
        };
        for (int s = -1; s <= 1; s += 2)
        {
            var beacon = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.08f, Height = 0.16f },
                Position = new Vector3(s * 5.35f, 5.65f, 0.12f),
                MaterialOverride = yellowBeaconMat
            };
            shutterRoot.AddChild(beacon);
        }
        bayRoot.AddChild(shutterRoot);
    }

    // ---------------------------------------------------------------------------------------------
    // 5. Ready Room & Command Area (West Zone)
    // ---------------------------------------------------------------------------------------------

    private static void AddReadyRoom(Node3D root)
    {
        var readyRoom = new Node3D { Name = "TacticalReadyRoom" };
        root.AddChild(readyRoom);

        var lockerMat = new StandardMaterial3D { AlbedoColor = new Color(0.20f, 0.24f, 0.22f), Metallic = 0.8f, Roughness = 0.45f }; // Dark tactical sage green

        // Row of Steel Lockers along the West Wall (X = -15.8m, Z = -9.0m to -4.0m)
        float lockerWidth = 0.55f;
        float lockerHeight = 2.1f;
        float lockerDepth = 0.55f;
        int lockerCount = 6;
        var lockersBody = new StaticBody3D { Name = "PersonnelLockers", Position = new Vector3(-15.6f, lockerHeight * 0.5f, -6.5f) };
        for (int l = 0; l < lockerCount; l++)
        {
            float lz = (l - (lockerCount - 1) * 0.5f) * (lockerWidth + 0.02f);
            var lMesh = new MeshInstance3D
            {
                Name = $"Locker_{l + 1}",
                Mesh = new BoxMesh { Size = new Vector3(lockerDepth, lockerHeight, lockerWidth) },
                Position = new Vector3(0.0f, 0.0f, lz),
                MaterialOverride = lockerMat
            };
            lockersBody.AddChild(lMesh);
        }
        lockersBody.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(lockerDepth, lockerHeight, lockerCount * (lockerWidth + 0.02f)) }
        });
        readyRoom.AddChild(lockersBody);

        // Tactical Briefing Whiteboard (X = -15.8m, Z = -1.5m)
        var boardBody = new StaticBody3D { Name = "CommandWhiteboard", Position = new Vector3(-15.8f, 1.8f, -1.5f) };
        boardBody.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.06f, 1.4f, 2.6f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.92f, 0.94f, 0.95f), Roughness = 0.25f } // Glossy whiteboard
        });
        boardBody.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.10f, 1.48f, 2.68f) },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.16f, 0.18f), Metallic = 0.8f } // Frame
        });
        var boardText = new Label3D
        {
            Text = "OPERATION: WRAITH // EXTRACTION PROTOCOLS\n" +
                   "• SECTOR ALBERT: HOLD WEST PERIMETER\n" +
                   "• SECTOR EINSTEIN: SECURE VEHICLE DEPOT\n" +
                   "• LIVE COMBAT RULES: MELEE INSTANT 1-HIT ELIMINATION",
            FontSize = 24,
            PixelSize = 0.0035f,
            OutlineSize = 4,
            Modulate = new Color(0.1f, 0.12f, 0.15f),
            RotationDegrees = new Vector3(0, 90, 0),
            Position = new Vector3(0.05f, 0.0f, 0.0f)
        };
        boardBody.AddChild(boardText);
        boardBody.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.12f, 1.5f, 2.7f) } });
        readyRoom.AddChild(boardBody);

        // Heavy Steel Emergency Door with Illuminated Green EXIT Sign (X = -15.8m, Z = 2.0m)
        var exitDoor = new Node3D { Name = "EmergencyExitDoor", Position = new Vector3(-15.85f, 0.0f, 2.2f) };
        exitDoor.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.10f, 2.3f, 1.2f) },
            Position = new Vector3(0.0f, 1.15f, 0.0f),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.32f, 0.18f, 0.18f), Metallic = 0.7f, Roughness = 0.5f }
        });
        var exitSign = new Label3D
        {
            Text = "[ EXIT ]",
            FontSize = 42,
            PixelSize = 0.0040f,
            OutlineSize = 6,
            Modulate = new Color(0.1f, 1.0f, 0.35f), // High-intensity emergency green
            RotationDegrees = new Vector3(0, 90, 0),
            Position = new Vector3(0.08f, 2.45f, 0.0f)
        };
        exitDoor.AddChild(exitSign);

        // Emergency green glow light
        var exitLight = new OmniLight3D
        {
            LightColor = new Color(0.15f, 1.0f, 0.35f),
            LightEnergy = 1.6f,
            OmniRange = 4.5f,
            Position = new Vector3(0.2f, 2.45f, 0.0f)
        };
        exitDoor.AddChild(exitLight);
        readyRoom.AddChild(exitDoor);
    }

    // ---------------------------------------------------------------------------------------------
    // Vehicle Bay Helpers
    // ---------------------------------------------------------------------------------------------

    private static void AddCar(Node3D root, string path, float lengthMetres, Vector3 spot, string name)
    {
        var scene = ResourceLoader.Load<PackedScene>(path);
        if (scene == null) { GD.PushWarning($"Hub: car model missing ({path})."); return; }
        var car = scene.Instantiate<Node3D>();
        car.Name = name;
        root.AddChild(car);

        Aabb raw = CombinedBounds(car);
        if (raw.Size == Vector3.Zero) { GD.PushWarning("Hub: car model has no meshes."); return; }
        bool lengthOnX = raw.Size.X >= raw.Size.Z;
        float scale = lengthMetres / Mathf.Max(raw.Size.X, raw.Size.Z);
        car.Scale = Vector3.One * scale;
        car.RotationDegrees = new Vector3(0.0f, lengthOnX ? 0.0f : 90.0f, 0.0f);

        Aabb fitted = CombinedBounds(car);
        car.Position += new Vector3(spot.X - fitted.GetCenter().X, -fitted.Position.Y, spot.Z - fitted.GetCenter().Z);

        Aabb bounds = CombinedBounds(car);
        var body = new StaticBody3D { Name = name + "Collision" };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = bounds.Size }, Position = bounds.GetCenter() });
        root.AddChild(body);

        var damageCtrl = new VehicleDamageController
        {
            Name = "VehicleDamageController",
            MaxHealth = 100.0f,
            CurrentHealth = 100.0f
        };
        car.AddChild(damageCtrl);
    }

    public static Aabb CombinedBounds(Node3D node)
    {
        bool any = false;
        Aabb total = default;
        foreach (Node child in node.FindChildren("*", "MeshInstance3D", true, false))
        {
            if (child is not MeshInstance3D mesh || mesh.Mesh == null) continue;
            Aabb local = mesh.GetAabb();
            Aabb world = mesh.GlobalTransform * local;
            total = any ? total.Merge(world) : world;
            any = true;
        }
        return any ? total : default;
    }
}
