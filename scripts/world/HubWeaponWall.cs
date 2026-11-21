using System.Collections.Generic;
using Godot;

/// <summary>
/// Tactical Armory Weapon Wall (Master GDD Section 6.1):
/// Industrial tactical weapon depot mounted on the north wall of the Bodycam Warehouse Hub.
/// Houses the full arsenal with dedicated overhead task lighting, structural steel framing,
/// workbenches with ammo cans, mounting pegs, and diegetic interaction prompts.
///
/// Supports both Karambit and Sledge Hammer alongside the complete firearms roster.
/// </summary>
public partial class HubWeaponWall : Node3D
{
    private const uint SlotLayer = 1u << 19;
    private const float ReachMetres = 4.8f;
    private const float WallZ = -13.85f;
    private const float SlotSpacing = 2.45f;

    private readonly List<(string Name, Node3D Marker, MeshInstance3D Highlight)> _slots = new();
    private Label? _prompt;
    private CanvasLayer? _ui;
    private Control? _picker;
    private string? _lookingAt;
    private HubController _hub = null!;

    public static void Build(Node3D world)
    {
        var wall = new HubWeaponWall { Name = "HubWeaponWall" };
        world.AddChild(wall);
    }

    public override void _Ready()
    {
        _hub = HubController.Instance!;
        BuildArmoryArchitecture();
        BuildSlots();
        BuildUi();
    }

    // ---- Visual & Physical Construction ---------------------------------------------------------

    private static readonly string[] PrimaryWeapons =
    {
        "AK-47", "M4", "P90", "Kriss Vector", "Shotgun", "Bazooka"
    };

    private static readonly string[] SecondaryAndMeleeWeapons =
    {
        "Glock 19", "FN 502", "Desert Eagle", "Karambit", "Sledge Hammer"
    };

    private void BuildArmoryArchitecture()
    {
        var darkSteel = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.12f, 0.13f, 0.15f),
            Metallic = 0.85f,
            Roughness = 0.35f
        };

        var pegboardMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.78f, 0.80f, 0.82f),
            AlbedoTexture = ResourceLoader.Load<Texture2D>("res://art/materials/warehouse/WeaponPegboard.png"),
            Metallic = 0.25f,
            Roughness = 0.75f,
            Uv1Scale = new Vector3(8.0f, 3.0f, 1.0f)
        };

        var diamondPlateMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.36f, 0.38f),
            AlbedoTexture = ResourceLoader.Load<Texture2D>("res://art/materials/tex/DiamondPlate009_Color.jpg"),
            NormalEnabled = true,
            NormalTexture = ResourceLoader.Load<Texture2D>("res://art/materials/tex/DiamondPlate009_Normal.jpg"),
            Metallic = 0.85f,
            Roughness = 0.4f,
            Uv1Scale = new Vector3(4.0f, 4.0f, 1.0f)
        };

        float totalWidth = 15.6f;
        float boardHeight = 4.8f;
        var boardSize = new Vector3(totalWidth, boardHeight, 0.12f);
        var boardPos = new Vector3(0.0f, 3.25f, WallZ);

        // 1. Perforated Mounting Backboard
        var backboard = new MeshInstance3D
        {
            Name = "ArmoryPegboard",
            Mesh = new BoxMesh { Size = boardSize },
            Position = boardPos,
            MaterialOverride = pegboardMat
        };
        AddChild(backboard);

        // Solid collider for wall
        var wallBody = new StaticBody3D { Name = "ArmoryWallCollision" };
        wallBody.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = boardSize }, Position = boardPos });
        AddChild(wallBody);

        // 2. Heavy Structural Steel Framing
        float frameT = 0.28f;
        AddFrame("FrameTop", new Vector3(0.0f, boardPos.Y + boardHeight * 0.5f + frameT * 0.5f, WallZ), new Vector3(totalWidth + 0.5f, frameT, 0.25f), darkSteel);
        AddFrame("FrameBottom", new Vector3(0.0f, boardPos.Y - boardHeight * 0.5f - frameT * 0.5f, WallZ), new Vector3(totalWidth + 0.5f, frameT, 0.25f), darkSteel);
        AddFrame("FrameLeft", new Vector3(-totalWidth * 0.5f - frameT * 0.5f, boardPos.Y, WallZ), new Vector3(frameT, boardHeight + frameT * 2, 0.25f), darkSteel);
        AddFrame("FrameRight", new Vector3(totalWidth * 0.5f + frameT * 0.5f, boardPos.Y, WallZ), new Vector3(frameT, boardHeight + frameT * 2, 0.25f), darkSteel);

        // Vertical intermediate divider columns
        AddFrame("DividerCol1", new Vector3(-totalWidth * 0.17f, boardPos.Y, WallZ + 0.02f), new Vector3(0.12f, boardHeight, 0.18f), darkSteel);
        AddFrame("DividerCol2", new Vector3(totalWidth * 0.17f, boardPos.Y, WallZ + 0.02f), new Vector3(0.12f, boardHeight, 0.18f), darkSteel);

        // 3. Overhead Gantry & Task Lighting
        float gantryZ = WallZ + 0.85f;
        float gantryY = boardPos.Y + boardHeight * 0.5f + 0.45f;
        AddFrame("GantryBeam", new Vector3(0.0f, gantryY, gantryZ), new Vector3(totalWidth + 0.2f, 0.16f, 0.16f), darkSteel);

        // Cantilever support arms connecting gantry beam back to wall
        float[] armXs = { -6.5f, -2.2f, 2.2f, 6.5f };
        foreach (float armX in armXs)
        {
            AddFrame($"GantryArm_{armX}", new Vector3(armX, gantryY, WallZ + 0.42f), new Vector3(0.14f, 0.14f, 0.85f), darkSteel);
        }

        // 3 Dedicated Industrial Spotlights illuminating the weapon wall
        float[] lightXs = { -5.0f, 0.0f, 5.0f };
        var lampMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.95f, 0.98f, 1.0f),
            EmissionEnabled = true,
            Emission = new Color(0.95f, 0.98f, 1.0f),
            EmissionEnergyMultiplier = 3.5f
        };

        for (int i = 0; i < lightXs.Length; i++)
        {
            float lx = lightXs[i];
            var spotNode = new Node3D { Name = $"ArmorySpotlight_{i + 1}", Position = new Vector3(lx, gantryY - 0.08f, gantryZ) };
            AddChild(spotNode);

            // Lamp housing fixture
            var lampMesh = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.20f, Height = 0.28f },
                RotationDegrees = new Vector3(-45, 0, 0),
                MaterialOverride = darkSteel
            };
            spotNode.AddChild(lampMesh);

            var lensMesh = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.15f, Height = 0.06f },
                Position = new Vector3(0.0f, -0.12f, 0.12f),
                RotationDegrees = new Vector3(-45, 0, 0),
                MaterialOverride = lampMat
            };
            spotNode.AddChild(lensMesh);

            // Real light source shining down at 45 degrees onto weapons
            var spot = new SpotLight3D
            {
                Name = "SpotLight",
                LightColor = new Color(0.96f, 0.98f, 1.0f),
                LightEnergy = 4.0f,
                SpotRange = 12.0f,
                SpotAngle = 65.0f,
                SpotAttenuation = 0.85f,
                ShadowEnabled = true,
                RotationDegrees = new Vector3(-45, 0, 0)
            };
            spotNode.AddChild(spot);
        }

        var armoryFill = new OmniLight3D
        {
            Name = "ArmoryWallFillLight",
            LightColor = new Color(0.95f, 0.97f, 1.0f),
            LightEnergy = 2.4f,
            OmniRange = 13.5f,
            Position = new Vector3(0.0f, 3.4f, WallZ + 2.4f)
        };
        AddChild(armoryFill);

        // 4. Stencil Header & Category Banners
        var headerSign = new Label3D
        {
            Name = "ArmoryHeader",
            Text = "WRAITH // TACTICAL ARMORY & LOADOUT DEPOT",
            FontSize = 56,
            PixelSize = 0.0038f,
            OutlineSize = 10,
            OutlineModulate = new Color(0, 0, 0, 0.9f),
            Modulate = new Color(1.0f, 0.65f, 0.15f), // Tactical amber
            Position = new Vector3(0.0f, boardPos.Y + boardHeight * 0.5f + 0.15f, WallZ + 0.15f)
        };
        AddChild(headerSign);

        var topTierLabel = new Label3D
        {
            Name = "TopTierLabel",
            Text = "— PRIMARY ASSAULT & SPECIALIZED FIREARMS —",
            FontSize = 32,
            PixelSize = 0.0035f,
            OutlineSize = 6,
            Modulate = new Color(0.65f, 0.75f, 0.85f),
            Position = new Vector3(0.0f, 4.95f, WallZ + 0.08f)
        };
        AddChild(topTierLabel);

        var btmTierLabel = new Label3D
        {
            Name = "BtmTierLabel",
            Text = "— SIDEARMS & TACTICAL MELEE WEAPONS —",
            FontSize = 32,
            PixelSize = 0.0035f,
            OutlineSize = 6,
            Modulate = new Color(0.65f, 0.75f, 0.85f),
            Position = new Vector3(0.0f, 2.75f, WallZ + 0.08f)
        };
        AddChild(btmTierLabel);

        // 5. Heavy Armory Counter / Workbench along the base
        float counterDepth = 0.95f;
        float counterHeight = 0.90f;
        var counterPos = new Vector3(0.0f, counterHeight * 0.5f, WallZ + counterDepth * 0.5f);
        var counterBody = new StaticBody3D { Name = "ArmoryCounter" };
        counterBody.AddChild(new MeshInstance3D
        {
            Name = "CounterTop",
            Mesh = new BoxMesh { Size = new Vector3(totalWidth, 0.12f, counterDepth) },
            Position = new Vector3(0.0f, counterHeight * 0.5f - 0.06f, 0.0f),
            MaterialOverride = diamondPlateMat
        });
        counterBody.AddChild(new MeshInstance3D
        {
            Name = "CounterCabinet",
            Mesh = new BoxMesh { Size = new Vector3(totalWidth - 0.2f, counterHeight - 0.12f, counterDepth - 0.1f) },
            Position = new Vector3(0.0f, -0.06f, 0.0f),
            MaterialOverride = darkSteel
        });
        counterBody.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(totalWidth, counterHeight, counterDepth) },
            Position = Vector3.Zero
        });
        counterBody.Position = counterPos;
        AddChild(counterBody);

        // Add olive drab ammo cans on the counter
        BuildAmmoCans(counterBody, counterHeight * 0.5f, counterDepth);
    }

    private static void BuildAmmoCans(Node3D counter, float topY, float depth)
    {
        var ammoMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.28f, 0.35f, 0.22f), // Olive drab military green
            Metallic = 0.7f,
            Roughness = 0.55f
        };

        (float x, float rot)[] canSpots = { (-5.8f, 5.0f), (-5.4f, -8.0f), (-1.2f, 12.0f), (4.8f, -4.0f), (5.3f, 6.0f) };
        for (int i = 0; i < canSpots.Length; i++)
        {
            var can = new MeshInstance3D
            {
                Name = $"AmmoCan_{i}",
                Mesh = new BoxMesh { Size = new Vector3(0.32f, 0.22f, 0.16f) },
                Position = new Vector3(canSpots[i].x, topY + 0.11f, (i % 2 == 0 ? -0.1f : 0.15f)),
                RotationDegrees = new Vector3(0.0f, canSpots[i].rot, 0.0f),
                MaterialOverride = ammoMat
            };
            counter.AddChild(can);
        }
    }

    private void AddFrame(string name, Vector3 position, Vector3 size, Material material)
    {
        AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = new BoxMesh { Size = size },
            Position = position,
            MaterialOverride = material
        });
    }

    // ---- Slots & Weapon Mounting -----------------------------------------------------------------

    private void BuildSlots()
    {
        // Row 0: Primary Firearms (6 weapons)
        float row0Y = 4.10f;
        float row0Count = PrimaryWeapons.Length;
        for (int i = 0; i < PrimaryWeapons.Length; i++)
        {
            string wName = PrimaryWeapons[i];
            float x = (i - (row0Count - 1) * 0.5f) * SlotSpacing;
            CreateWeaponStation(wName, new Vector3(x, row0Y, WallZ + 0.08f), true);
        }

        // Row 1: Sidearms & Melee Weapons (5 weapons)
        float row1Y = 1.95f;
        float row1Count = SecondaryAndMeleeWeapons.Length;
        for (int i = 0; i < SecondaryAndMeleeWeapons.Length; i++)
        {
            string wName = SecondaryAndMeleeWeapons[i];
            float x = (i - (row1Count - 1) * 0.5f) * (SlotSpacing * 1.15f);
            CreateWeaponStation(wName, new Vector3(x, row1Y, WallZ + 0.08f), false);
        }
    }

    private void CreateWeaponStation(string weaponName, Vector3 pos, bool isPrimary)
    {
        var marker = new Node3D { Name = $"Slot_{weaponName.Replace(" ", "_")}", Position = pos };
        AddChild(marker);

        WeaponData data = GetWeaponData(weaponName);

        // Mounting Pegs / Steel Cradle Brackets
        var pegMat = new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.27f, 0.30f), Metallic = 0.9f, Roughness = 0.3f };
        float pegSpacing = isPrimary ? 0.38f : 0.14f;
        for (int p = -1; p <= 1; p += 2)
        {
            var peg = new MeshInstance3D
            {
                Name = $"Peg_{p}",
                Mesh = new CylinderMesh { TopRadius = 0.018f, BottomRadius = 0.018f, Height = 0.18f },
                Position = new Vector3(p * pegSpacing, -0.06f, 0.08f),
                RotationDegrees = new Vector3(90, 0, 0),
                MaterialOverride = pegMat
            };
            marker.AddChild(peg);
        }

        // 3D Weapon Model
        AddModel(marker, weaponName, data);

        // Nameplate & Stats
        var nameLabel = new Label3D
        {
            Text = weaponName.ToUpperInvariant(),
            FontSize = 44,
            PixelSize = 0.0036f,
            OutlineSize = 8,
            OutlineModulate = new Color(0, 0, 0, 0.9f),
            Modulate = new Color(0.96f, 0.96f, 0.98f),
            Position = new Vector3(0.0f, -0.55f, 0.05f)
        };
        marker.AddChild(nameLabel);

        string statText = data.FireMode == WeaponData.FireModeType.Melee
            ? "100 DMG  |  TACTICAL MELEE"
            : $"{data.BodyDamage:0} DMG  |  {data.FireRateRPM:0} RPM";

        var statLabel = new Label3D
        {
            Text = statText,
            FontSize = 26,
            PixelSize = 0.0030f,
            OutlineSize = 6,
            OutlineModulate = new Color(0, 0, 0, 0.9f),
            Modulate = new Color(0.70f, 0.80f, 0.90f),
            Position = new Vector3(0.0f, -0.72f, 0.05f)
        };
        marker.AddChild(statLabel);

        // Subtle Selection Highlight Bracket
        var highlightMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.2f, 0.85f, 1.0f, 0.4f),
            EmissionEnabled = true,
            Emission = new Color(0.2f, 0.85f, 1.0f),
            EmissionEnergyMultiplier = 1.5f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        var highlight = new MeshInstance3D
        {
            Name = "Highlight",
            Mesh = new BoxMesh { Size = new Vector3(SlotSpacing * 0.92f, 1.35f, 0.02f) },
            Position = new Vector3(0.0f, -0.1f, 0.02f),
            MaterialOverride = highlightMat,
            Visible = false
        };
        marker.AddChild(highlight);

        // Interaction Area3D
        var area = new Area3D
        {
            Name = "PickArea",
            CollisionLayer = SlotLayer,
            CollisionMask = 0,
            Monitorable = true,
            Monitoring = false
        };
        area.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(SlotSpacing * 1.15f, 1.8f, 2.2f) },
            Position = new Vector3(0.0f, 0.0f, 0.8f)
        });
        marker.AddChild(area);

        marker.SetMeta("weapon", weaponName);
        _slots.Add((weaponName, marker, highlight));
    }

    private static void AddModel(Node3D marker, string weaponName, WeaponData data)
    {
        string path = data.ModelScenePath;
        if (string.IsNullOrEmpty(path))
        {
            if (weaponName == "Sledge Hammer") path = "res://art/weapons/Melee/SledgeHammer.glb";
            else if (weaponName == "Karambit") path = "res://art/weapons/Melee/Karambit.glb";
        }
        if (string.IsNullOrEmpty(path)) return;

        var scene = ResourceLoader.Load<PackedScene>(path);
        if (scene == null) return;
        var holder = new Node3D { Name = "Display" };
        marker.AddChild(holder);
        var model = scene.Instantiate<Node3D>();
        holder.AddChild(model);

        Aabb box = HubProps.CombinedBounds(model);
        if (box.Size == Vector3.Zero) return;
        Vector3 size = box.Size;
        float longest = Mathf.Max(size.X, Mathf.Max(size.Y, size.Z));
        float target = (data.WeaponClass.ToLowerInvariant().Contains("pistol") || weaponName == "Karambit")
            ? 0.36f
            : 0.98f;
        holder.Scale = Vector3.One * (target / longest);
        if (longest == size.Y) holder.RotationDegrees = new Vector3(0, 0, 90);
        else if (longest == size.Z) holder.RotationDegrees = new Vector3(0, 90, 0);

        Aabb fitted = HubProps.CombinedBounds(holder);
        holder.Position -= fitted.GetCenter() - marker.GlobalPosition;
        holder.Position += new Vector3(0.0f, 0.0f, 0.08f);
    }

    private static WeaponData GetWeaponData(string name)
    {
        WeaponData? data = OperatorInfo.FindWeapon(name);
        if (data != null) return data;
        if (name == "Sledge Hammer")
        {
            return new WeaponData
            {
                WeaponName = "Sledge Hammer",
                WeaponClass = "Melee",
                Description = "Heavy demolition sledgehammer. Delivers crushing instant-kill blows in close quarters.",
                FireMode = WeaponData.FireModeType.Melee,
                BodyDamage = 100.0f,
                HeadshotDamage = 100.0f,
                FireRateRPM = 60.0f,
                MagazineSize = 0,
                ModelScenePath = "res://art/weapons/Melee/SledgeHammer.glb"
            };
        }
        return new WeaponData { WeaponName = name, WeaponClass = "Assault Rifle", BodyDamage = 30.0f, FireRateRPM = 600.0f };
    }

    // ---- Interaction & UI ------------------------------------------------------------------------

    private void BuildUi()
    {
        _ui = new CanvasLayer { Layer = 25 };
        AddChild(_ui);
        _prompt = MenuStyle.Text("", 24, MenuStyle.TextMain, MenuStyle.Bold, HorizontalAlignment.Center);
        MenuStyle.Place(_prompt, 0.5f, 0.62f, 0.5f, 0.62f, -320, 0, 320, 40);
        _ui.AddChild(_prompt);
    }

    private Camera3D? LocalCamera() =>
        GetTree().CurrentScene?.GetNodeOrNull<Camera3D>($"Players/Player_{Multiplayer.GetUniqueId()}/Head/CameraShake/FirstPersonCamera");

    public override void _Process(double delta)
    {
        if (_prompt == null) return;
        string? prevLooking = _lookingAt;
        _lookingAt = null;

        Camera3D? camera = LocalCamera();
        if (camera != null && _picker == null && !GameUiState.MenuOpen)
        {
            Vector3 from = camera.GlobalPosition;
            Vector3 to = from - camera.GlobalTransform.Basis.Z * ReachMetres;
            var query = PhysicsRayQueryParameters3D.Create(from, to, SlotLayer);
            query.CollideWithAreas = true;
            query.CollideWithBodies = false;
            var hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
            if (hit.Count > 0 && hit["collider"].AsGodotObject() is Node hitNode && hitNode.GetParent() is Node3D slot && slot.HasMeta("weapon"))
                _lookingAt = slot.GetMeta("weapon").AsString();
        }

        if (_lookingAt != prevLooking)
        {
            foreach (var slot in _slots)
            {
                slot.Highlight.Visible = slot.Name == _lookingAt;
            }
        }

        _prompt.Text = _lookingAt == null ? string.Empty : $"[E]  TAKE  {_lookingAt.ToUpperInvariant()}";
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_picker != null)
        {
            if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed(GameUiState.PauseAction))
            {
                GetViewport().SetInputAsHandled();
                ClosePicker();
            }
            return;
        }
        if (_lookingAt == null) return;
        if (@event.IsActionPressed("interact") && !@event.IsEcho())
        {
            GetViewport().SetInputAsHandled();
            OpenPicker(_lookingAt);
        }
    }

    // ---- Loadout Selection & Quick Take -----------------------------------------------------------

    private void OpenPicker(string weaponName)
    {
        WeaponData data = GetWeaponData(weaponName);
        if (_ui == null) return;
        GameUiState.SetMenuOpen(true);
        if (!OS.HasFeature("mobile")) Input.MouseMode = Input.MouseModeEnum.Visible;

        _picker = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        MenuStyle.Fill(_picker);
        _ui.AddChild(_picker);
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = Control.MouseFilterEnum.Ignore };
        MenuStyle.Fill(dim);
        _picker.AddChild(dim);

        var panel = MenuStyle.Panel(weaponName.ToUpperInvariant(), "rifle", out VBoxContainer body);
        MenuStyle.Place(panel, 0.5f, 0.5f, 0.5f, 0.5f, -310, -240, 310, 240);
        _picker.AddChild(panel);
        BuildPickerBody(body, data);
    }

    private void BuildPickerBody(VBoxContainer body, WeaponData data)
    {
        foreach (Node child in body.GetChildren()) { body.RemoveChild(child); child.QueueFree(); }

        string stats = data.FireMode == WeaponData.FireModeType.Melee
            ? "100 Body Damage  |  Instant 1-Hit Kill  |  Tactical Melee"
            : $"{data.BodyDamage:0} dmg  |  {data.FireRateRPM:0} rpm  |  {data.MagazineSize} rd";
        body.AddChild(MenuStyle.Text(stats, 18, MenuStyle.TextDim, MenuStyle.Medium));

        int set = PlayerProfile.ActiveWeaponSet;

        if (data.WeaponName == "Karambit" || data.WeaponName == "Sledge Hammer")
        {
            string currentPistol = PlayerProfile.GetSetWeapon(set, WeaponSlot.Pistol);
            bool already = currentPistol == "Karambit" && WeaponSwitcher.ActiveMeleeWeapon == data.WeaponName;
            string label = already
                ? $"EQUIPPED AS ACTIVE MELEE WEAPON"
                : $"EQUIP AS ACTIVE MELEE WEAPON (replaces {currentPistol})";
            Button take = MenuStyle.Outline(label, () => Take(data, WeaponSlot.Pistol, body), null, 52);
            take.Disabled = already;
            body.AddChild(take);
        }
        else
        {
            bool offered = false;
            for (int slotIndex = 0; slotIndex < WeaponRules.SlotCount; slotIndex++)
            {
                WeaponSlot slot = (WeaponSlot)slotIndex;
                if (!WeaponRules.Fits(data, slot)) continue;
                offered = true;
                string holding = PlayerProfile.GetSetWeapon(set, slot);
                bool already = holding == data.WeaponName;
                WeaponSlot chosen = slot;
                string label = already
                    ? $"{WeaponRules.SlotName(slot)}  —  currently carrying"
                    : $"TAKE AS {WeaponRules.SlotName(slot)}  (replaces {holding})";
                Button take = MenuStyle.Outline(label, () => Take(data, chosen, body), null, 52);
                take.Disabled = already;
                body.AddChild(take);
            }
            if (!offered) body.AddChild(MenuStyle.Text("This weapon cannot be carried in active sets.", 18, MenuStyle.TextMuted, MenuStyle.Medium));
        }

        body.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        body.AddChild(MenuStyle.Text("ACTIVE LOADOUT", 14, MenuStyle.TextMuted, MenuStyle.Bold));
        for (int slotIndex = 0; slotIndex < WeaponRules.SlotCount; slotIndex++)
        {
            WeaponSlot slot = (WeaponSlot)slotIndex;
            string weaponInSlot = PlayerProfile.GetSetWeapon(set, slot);
            if (slot == WeaponSlot.Pistol && weaponInSlot == "Karambit" && WeaponSwitcher.ActiveMeleeWeapon == "Sledge Hammer")
                weaponInSlot = "Sledge Hammer";
            body.AddChild(MenuStyle.Text($"{WeaponRules.SlotName(slot)}:  {weaponInSlot}", 17, MenuStyle.TextMain, MenuStyle.Medium));
        }
        body.AddChild(MenuStyle.Outline("DONE", ClosePicker, null, 46));
    }

    private void Take(WeaponData data, WeaponSlot slot, VBoxContainer body)
    {
        int set = PlayerProfile.ActiveWeaponSet;
        string name = data.WeaponName;

        if (name == "Karambit" || name == "Sledge Hammer")
        {
            WeaponSwitcher.ActiveMeleeWeapon = name;
            PlayerProfile.SetSetWeapon(set, WeaponSlot.Pistol, "Karambit");
        }
        else if (WeaponRules.IsPrimary(slot))
        {
            WeaponSlot other = slot == WeaponSlot.Primary1 ? WeaponSlot.Primary2 : WeaponSlot.Primary1;
            if (PlayerProfile.GetSetWeapon(set, other) == name) PlayerProfile.SwapPrimaries(set);
            else PlayerProfile.SetSetWeapon(set, slot, name);
        }
        else
        {
            PlayerProfile.SetSetWeapon(set, slot, name);
        }

        // Play tactical equip sound
        GameAudio.Audio.Play("weapons/melee/equip");

        // Immediately equip into hands
        Node? player = GetTree().CurrentScene?.GetNodeOrNull($"Players/Player_{Multiplayer.GetUniqueId()}/WeaponSwitcher");
        if (player is WeaponSwitcher switcher)
        {
            switcher.EquipByName(name);
        }

        BuildPickerBody(body, data);
    }

    private void ClosePicker()
    {
        _picker?.QueueFree();
        _picker = null;
        GameUiState.SetMenuOpen(false);
        if (!OS.HasFeature("mobile")) Input.MouseMode = Input.MouseModeEnum.Captured;
    }
}
