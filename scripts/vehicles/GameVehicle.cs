using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Gameplay vehicle controller with 100-HP damage model (Master GDD Section 1, 4.1, 6.3).
/// Supports:
/// - 100-HP Base Vehicle Integrity with two-state damage model (recoverable disable vs Bazooka instant wreck).
/// - Small-arms fire to tires and spike strips trigger recoverable disable state.
/// - Heavy anti-vehicle weapons (Bazooka) trigger immediate total destruction (30s burned-out wreck mesh).
/// - 3D cockpit gauges dashboard rendering speed, RPM, 100-HP integrity, tire warnings, and boost.
/// - Locked vehicle roles: Driver controls movement only; Shooter controls weapons. No seat swapping.
/// </summary>
public partial class GameVehicle : CharacterBody3D, IShootable, IExplosionReceiver
{
    public const string LandCruiserPath = "res://art/vehicles/Low_Poly_Car_-_Toyota_Land_Cruiser_70_GXL_2024.glb";
    public const string BuggyPath = "res://art/vehicles/raider_buggy.glb";

    [Export] public string ModelPath { get; set; } = BuggyPath;
    [Export] public string DisplayName { get; set; } = "Raider Buggy";
    [Export] public float TargetLength { get; set; } = 4.2f;
    [Export] public float MaxSpeed { get; set; } = 25.0f;
    [Export] public float Acceleration { get; set; } = 10.0f;
    [Export] public float BrakePower { get; set; } = 20.0f;
    [Export] public float SteeringRate { get; set; } = 1.55f;

    [Export] public int DriverPeerId { get; set; }
    [Export] public int LastDriverPeerId { get; set; }
    [Export] public int PassengerPeerId { get; set; }

    public VehicleDamageController DamageController { get; private set; } = null!;
    private CockpitDashboard3D? _cockpitDashboard;
    public CockpitDashboard3D? CockpitDashboard => _cockpitDashboard;

    private Node3D _visual = null!;
    private Camera3D? _activeSeatCamera;
    private float _speed;
    private float _steer;
    private float _throttle;
    private bool _brake;
    private bool _handbrake;
    private bool _boosting;
    private float _boostFuel = 100.0f;
    private float _inputSendTimer;
    private Label3D? _prompt;
    private Vector3 _driverSeat = new(-0.48f, 0.38f, -0.12f);
    private Vector3 _passengerSeat = new(0.48f, 0.38f, -0.12f);
    private const float EnterRange = 3.2f;
    private static readonly Vector3 Gravity = new(0, -18.0f, 0);

    public int MaxSeats => DisplayName.Contains("STV") || TargetLength > 4.5f ? 9 : 2;
    public readonly int[] PassengerPeerIds = new int[8];

    public bool IsDriver(int peerId) => peerId > 0 && DriverPeerId == peerId;
    public bool IsPassenger(int peerId)
    {
        if (peerId <= 0) return false;
        if (PassengerPeerId == peerId) return true;
        for (int i = 0; i < MaxSeats - 1; i++)
            if (PassengerPeerIds[i] == peerId) return true;
        return false;
    }
    public bool HasSeat(int peerId) => IsDriver(peerId) || IsPassenger(peerId);

    public int GetPassengerIndex(int peerId)
    {
        for (int i = 0; i < MaxSeats - 1; i++)
            if (PassengerPeerIds[i] == peerId) return i;
        return -1;
    }

    public Vector3 SeatPosition(int peerId)
    {
        if (IsDriver(peerId)) return ToGlobal(_driverSeat);
        int pIndex = GetPassengerIndex(peerId);
        if (pIndex < 0) pIndex = 0;
        return ToGlobal(GetLocalSeatPosition(pIndex));
    }

    private Vector3 GetLocalSeatPosition(int passengerIndex)
    {
        if (MaxSeats <= 2 || passengerIndex == 0) return _passengerSeat;
        return passengerIndex switch
        {
            1 => new Vector3(-0.48f, 0.45f, -0.95f),  // Mid Left
            2 => new Vector3(0.48f, 0.45f, -0.95f),   // Mid Right
            3 => new Vector3(0.00f, 0.45f, -0.95f),   // Mid Center
            4 => new Vector3(-0.48f, 0.45f, -1.85f),  // Rear Left
            5 => new Vector3(0.48f, 0.45f, -1.85f),   // Rear Right
            6 => new Vector3(0.00f, 0.45f, -1.85f),   // Rear Center
            7 => new Vector3(0.00f, 0.85f, -0.60f),   // Roof Gunner / Hatch
            _ => _passengerSeat,
        };
    }

    public Vector3 SeatExitPosition(int peerId)
    {
        float side = IsDriver(peerId) ? -1.0f : 1.0f;
        int pIndex = GetPassengerIndex(peerId);
        float zOffset = pIndex switch
        {
            1 or 2 or 3 => -0.8f,
            4 or 5 or 6 => -1.6f,
            _ => 0.15f
        };
        return ToGlobal(new Vector3(side * 1.55f, 0.15f, zOffset));
    }

    public override void _Ready()
    {
        AddToGroup("gameplay_vehicles");
        CollisionLayer = 1 << 4;
        CollisionMask = 1;
        AddChild(new CollisionShape3D
        {
            Name = "Hull",
            Shape = new BoxShape3D { Size = new Vector3(1.9f, 1.45f, TargetLength) },
            Position = new Vector3(0, 0.75f, 0),
        });

        DamageController = new VehicleDamageController { Name = "DamageController" };
        AddChild(DamageController);
        DamageController.DamageStateChanged += OnDamageStateChanged;
        DamageController.WreckExpired += OnWreckExpired;

        _visual = new Node3D { Name = "Visual" };
        AddChild(_visual);
        BuildVisual();

        // 3D Cockpit Gauges Dashboard
        _cockpitDashboard = new CockpitDashboard3D { Name = "CockpitDashboard" };
        _cockpitDashboard.Position = _driverSeat + new Vector3(0.0f, 0.05f, -0.65f);
        _cockpitDashboard.RotationDegrees = new Vector3(-15.0f, 0, 0);
        AddChild(_cockpitDashboard);

        _prompt = new Label3D
        {
            Name = "Prompt",
            Text = DisplayName,
            Position = new Vector3(0, 2.0f, 0),
            PixelSize = 0.004f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            Modulate = new Color(0.75f, 0.95f, 1.0f),
        };
        AddChild(_prompt);
        VehicleInput.EnsureRegistered();
        GetNodeOrNull<MultiplayerSynchronizer>("MultiplayerSynchronizer")?.SetMultiplayerAuthority(1);
        RefreshSeatLocks();
    }

    private void BuildVisual()
    {
        var packed = ResourceLoader.Load<PackedScene>(ModelPath);
        if (packed == null)
        {
            GD.PushWarning($"Gameplay vehicle model was not found: {ModelPath}");
            return;
        }
        var model = packed.Instantiate<Node3D>();
        _visual.AddChild(model);
        FixMaterials(model);
        FitModel(model);
    }

    private static void FixMaterials(Node root)
    {
        var pending = new Stack<Node>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            Node node = pending.Pop();
            foreach (Node child in node.GetChildren()) pending.Push(child);
            if (node is not MeshInstance3D mesh || mesh.Mesh == null) continue;
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                if (mesh.GetActiveMaterial(i) is not BaseMaterial3D source) continue;
                var material = (BaseMaterial3D)source.Duplicate();
                material.Transparency = BaseMaterial3D.TransparencyEnum.Disabled;
                material.CullMode = BaseMaterial3D.CullModeEnum.Back;
                mesh.SetSurfaceOverrideMaterial(i, material);
            }
        }
    }

    private void FitModel(Node3D model)
    {
        var meshes = model.FindChildren("*", "MeshInstance3D", true, false);
        Transform3D toModel = model.GlobalTransform.AffineInverse();
        var bounds = new Aabb();
        bool found = false;
        foreach (Node item in meshes)
        {
            if (item is not MeshInstance3D mesh || mesh.Mesh == null) continue;
            Aabb box = toModel * mesh.GlobalTransform * mesh.Mesh.GetAabb();
            bounds = found ? bounds.Merge(box) : box;
            found = true;
        }
        if (!found || bounds.Size.LengthSquared() < 0.0001f) return;

        bool longX = bounds.Size.X >= bounds.Size.Z;
        float length = longX ? bounds.Size.X : bounds.Size.Z;
        float scale = TargetLength / Mathf.Max(0.001f, length);
        _visual.Scale = Vector3.One * scale;

        float lowY = float.MinValue, highY = float.MinValue;
        foreach (Node item in meshes)
        {
            if (item is not MeshInstance3D mesh || mesh.Mesh == null) continue;
            Aabb box = toModel * mesh.GlobalTransform * mesh.Mesh.GetAabb();
            float centre = longX ? box.GetCenter().X : box.GetCenter().Z;
            float pivot = longX ? bounds.Position.X + bounds.Size.X * 0.5f : bounds.Position.Z + bounds.Size.Z * 0.5f;
            if (centre < pivot) lowY = Mathf.Max(lowY, box.End.Y);
            else highY = Mathf.Max(highY, box.End.Y);
        }
        float yaw = longX ? (lowY < highY ? -90f : 90f) : (lowY < highY ? 0f : 180f);
        _visual.RotationDegrees = new Vector3(0, yaw, 0);
        Vector3 rotatedCentre = new Basis(Vector3.Up, Mathf.DegToRad(yaw)) * bounds.GetCenter();
        _visual.Position = new Vector3(-rotatedCentre.X * scale, -bounds.Position.Y * scale, -rotatedCentre.Z * scale);

        if (DisplayName.Contains("Buggy"))
        {
            _driverSeat = new Vector3(-0.43f, 0.48f, -0.13f);
            _passengerSeat = new Vector3(0.43f, 0.48f, -0.13f);
        }
        else
        {
            _driverSeat = new Vector3(-0.48f, 0.45f, -0.15f);
            _passengerSeat = new Vector3(0.48f, 0.45f, -0.15f);
        }

        if (_cockpitDashboard != null)
        {
            _cockpitDashboard.Position = _driverSeat + new Vector3(0.0f, 0.05f, -0.65f);
        }
    }

    public override void _Process(double delta)
    {
        if (!Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer())
            ProcessServerInteraction();
        else
            ProcessClientInteraction();

        RefreshSeatLocks();
        UpdatePrompt();

        // Update 3D cockpit dashboard
        float speedKmh = _speed * 3.6f;
        float rpm = 800.0f + Mathf.Abs(_throttle) * 4500.0f + Mathf.Clamp(Mathf.Abs(_speed) / MaxSpeed, 0f, 1f) * 2000.0f;
        _cockpitDashboard?.UpdateDashboard(speedKmh, rpm, DamageController.CurrentHealth, DamageController.MaxHealth,
            DamageController.CurrentState, DamageController.TiresIntact, _boostFuel);

    }

    private void ProcessServerInteraction()
    {
        int id = Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 1;
        var player = FindPlayer(id);
        if (player == null || !PlayerIdentity.IsLocalHuman(player)) return;
        if (Input.IsActionJustPressed("vehicle_interact")) RequestSeatChange(id);
        if (IsDriver(id)) ReadDriverInput();
    }

    private void ProcessClientInteraction()
    {
        int id = Multiplayer.GetUniqueId();
        var player = FindPlayer(id);
        if (player == null || !PlayerIdentity.IsLocalHuman(player)) return;
        if (Input.IsActionJustPressed("vehicle_interact")) RpcId(1, MethodName.RpcRequestSeatChange, id);
        if (IsDriver(id))
        {
            ReadDriverInput();
            _inputSendTimer += (float)GetProcessDeltaTime();
            if (_inputSendTimer >= 0.05f)
            {
                _inputSendTimer = 0;
                RpcId(1, MethodName.RpcSetDriverInput, _steer, _throttle, _brake, _handbrake);
            }
        }
    }

    private void ReadDriverInput()
    {
        if (!DamageController.CanDrive)
        {
            _steer = 0;
            _throttle = 0;
            _brake = true;
            _handbrake = true;
            _boosting = false;
            return;
        }

        int localId = Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 1;
        var driverPlayer = FindPlayer(localId);
        var driverHealth = driverPlayer?.GetNodeOrNull<Health>("Health");
        if (driverHealth != null && (driverHealth.IsDead || driverHealth.IsDowned))
        {
            _steer = 0;
            _throttle = 0;
            _brake = true;
            _handbrake = true;
            _boosting = false;
            return;
        }

        _steer = Input.GetAxis("vehicle_left", "vehicle_right");
        if (MatchManager.Instance?.ActiveController is PureChaseController pureChase && pureChase.IsGhostPossessionActive)
        {
            _steer = -_steer;
        }
        _throttle = Input.GetActionStrength("vehicle_gas") - Input.GetActionStrength("vehicle_reverse");
        _brake = Input.IsActionPressed("vehicle_brake");
        _handbrake = Input.IsActionPressed("vehicle_handbrake");
        _boosting = Input.IsActionPressed("vehicle_boost") || (Input.IsKeyPressed(Key.Shift) && _throttle > 0.1f);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcRequestSeatChange(int peerId)
    {
        if (!Multiplayer.IsServer()) return;
        int sender = Multiplayer.GetRemoteSenderId();
        if (sender == peerId) RequestSeatChange(sender);
    }

    public bool TryExit(int peerId, out Vector3 exit)
    {
        exit = Vector3.Zero;
        if (!HasSeat(peerId)) return false;
        if (!DamageController.IsDisabled && !DamageController.IsWrecked && Mathf.Abs(_speed) > 6.0f) return false;
        exit = SeatExitPosition(peerId);
        if (DriverPeerId == peerId) DriverPeerId = 0;
        for (int i = 0; i < PassengerPeerIds.Length; i++)
        {
            if (PassengerPeerIds[i] == peerId) PassengerPeerIds[i] = 0;
        }
        PassengerPeerId = PassengerPeerIds[0];
        BroadcastSeatChange(DriverPeerId, PassengerPeerIds, peerId, exit);
        return true;
    }

    private void RequestSeatChange(int peerId)
    {
        if (!Multiplayer.IsServer() && Multiplayer.HasMultiplayerPeer()) return;

        // Seat swapping is completely removed: pressing interact inside exits to the ground!
        if (HasSeat(peerId))
        {
            TryExit(peerId, out _);
            return;
        }

        var player = FindPlayer(peerId);
        if (player == null) return;
        var health = player.GetNodeOrNull<Health>("Health");
        if (health != null && (health.IsDead || health.IsDowned)) return;

        if (GlobalPosition.DistanceTo(player.GlobalPosition) > EnterRange) return;

        // If vehicle is disabled, pressing interact repairs it!
        if (DamageController.IsDisabled)
        {
            float repairAmount = 35.0f;
            var loadout = player.GetNodeOrNull<CharacterLoadout>("CharacterLoadout");
            if (loadout?.CharacterId == "medic_patch") repairAmount = 50.0f; // Patch perk: +50 HP
            DamageController.Repair(repairAmount);
            if (Multiplayer.HasMultiplayerPeer())
                Rpc(MethodName.RpcSyncDamage, DamageController.CurrentHealth, (int)DamageController.CurrentState,
                    DamageController.TiresIntact[0], DamageController.TiresIntact[1], DamageController.TiresIntact[2], DamageController.TiresIntact[3]);
            return;
        }

        // If vehicle is completely wrecked, no one can enter
        if (DamageController.IsWrecked) return;

        TryEnter(peerId);
    }

    public bool TryEnter(int peerId)
    {
        if (HasSeat(peerId) || DamageController.IsWrecked) return false;
        var enterPlayer = FindPlayer(peerId);
        var enterHealth = enterPlayer?.GetNodeOrNull<Health>("Health");
        if (enterHealth != null && (enterHealth.IsDead || enterHealth.IsDowned)) return false;
        if (DriverPeerId == 0)
        {
            DriverPeerId = peerId;
            BroadcastSeatChange(DriverPeerId, PassengerPeerIds, 0, Vector3.Zero);
            return true;
        }
        for (int i = 0; i < MaxSeats - 1; i++)
        {
            if (PassengerPeerIds[i] == 0)
            {
                PassengerPeerIds[i] = peerId;
                PassengerPeerId = PassengerPeerIds[0];
                BroadcastSeatChange(DriverPeerId, PassengerPeerIds, 0, Vector3.Zero);
                return true;
            }
        }
        return false;
    }

    private void BroadcastSeatChange(int driver, int[] passengers, int exitingPeer, Vector3 exitPosition)
    {
        if (Multiplayer.HasMultiplayerPeer()) Rpc(MethodName.RpcSetAllSeats, driver, passengers, exitingPeer, exitPosition);
        else RpcSetAllSeats(driver, passengers, exitingPeer, exitPosition);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcSetAllSeats(int driver, int[] passengers, int exitingPeer, Vector3 exitPosition)
    {
        if (driver > 0) LastDriverPeerId = driver;
        DriverPeerId = driver;
        for (int i = 0; i < PassengerPeerIds.Length; i++)
        {
            PassengerPeerIds[i] = (passengers != null && i < passengers.Length) ? passengers[i] : 0;
        }
        PassengerPeerId = PassengerPeerIds[0];
        if (exitingPeer != 0 && FindPlayer(exitingPeer) is PlayerMovement exiting)
            exiting.LeaveVehicle(this, exitPosition);
        RefreshSeatLocks();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcSetSeats(int driver, int passenger, int exitingPeer, Vector3 exitPosition)
    {
        RpcSetAllSeats(driver, new[] { passenger }, exitingPeer, exitPosition);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void RpcSetDriverInput(float steer, float throttle, bool brake, bool handbrake)
    {
        if (!Multiplayer.IsServer()) return;
        int sender = Multiplayer.GetRemoteSenderId();
        if (!IsDriver(sender)) return;
        _steer = Mathf.Clamp(steer, -1f, 1f);
        _throttle = Mathf.Clamp(throttle, -1f, 1f);
        _brake = brake;
        _handbrake = handbrake;
    }

    public override void _PhysicsProcess(double deltaValue)
    {
        if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) return;
        float dt = (float)deltaValue;

        bool shiftPerk = DamageController.DriverCharacterId == "driver_shift";
        float boostDrainRate = shiftPerk ? 20.0f : 28.0f;
        float boostRechargeRate = shiftPerk ? 25.0f : 18.0f;

        if (DriverPeerId != 0 && _boosting && _boostFuel > 0.0f && DamageController.CanDrive && _throttle > 0.1f)
        {
            _boostFuel = Mathf.Max(0.0f, _boostFuel - boostDrainRate * dt);
        }
        else
        {
            _boostFuel = Mathf.Min(100.0f, _boostFuel + boostRechargeRate * dt);
        }

        if (!DamageController.CanDrive)
        {
            // Disabled or Wrecked: cut power and come to a halt
            _speed = Mathf.MoveToward(_speed, 0, 8.0f * dt);
        }
        else if (DriverPeerId != 0)
        {
            // Tire damage penalty (Tread Iron Rubber reduces penalty)
            bool isTread = DamageController.DriverCharacterId == "driver_tread";
            float minTireMult = isTread ? 0.45f : 0.25f;
            float tirePenaltyPerTire = isTread ? 0.12f : 0.20f;
            float tireMult = Mathf.Max(minTireMult, 1.0f - DamageController.FlatTireCount * tirePenaltyPerTire);

            float boostSpeedMult = (_boosting && _boostFuel > 0.0f && _throttle > 0.1f) ? 1.35f : 1.0f;
            float boostAccelMult = (_boosting && _boostFuel > 0.0f && _throttle > 0.1f) ? 1.50f : 1.0f;

            float effectiveMaxSpeed = MaxSpeed * tireMult * boostSpeedMult;
            float target = _throttle * effectiveMaxSpeed;
            float rate = Mathf.Abs(_throttle) > 0.01f ? (Acceleration * tireMult * boostAccelMult) : ( _brake ? BrakePower : 2.5f );
            if (_handbrake) rate *= 2.2f;
            _speed = Mathf.MoveToward(_speed, target, rate * dt);
            float steerScale = Mathf.Clamp(Mathf.Abs(_speed) / 3.0f, 0.15f, 1.0f);
            RotateY(-_steer * SteeringRate * steerScale * dt * Mathf.Sign(_speed == 0 ? 1 : _speed));
        }
        else _speed = Mathf.MoveToward(_speed, 0, 3.0f * dt);

        Velocity = -GlobalBasis.Z * _speed + new Vector3(0, Velocity.Y, 0);
        if (MatchManager.Instance?.ActiveController is PureChaseController chase && chase.IsPullVortexActive)
        {
            Vector3 toCenter = chase.VortexCenter - GlobalPosition;
            toCenter.Y = 0;
            float dist = toCenter.Length();
            if (dist > 0.5f)
            {
                Vector3 pull = toCenter.Normalized() * Mathf.Clamp(20.0f / (dist * 0.1f + 1.0f), 2.0f, 15.0f);
                Velocity += new Vector3(pull.X, 0, pull.Z);
            }
        }
        if (!IsOnFloor()) Velocity += Gravity * dt;
        else if (Velocity.Y < 0) Velocity = new Vector3(Velocity.X, -0.1f, Velocity.Z);
        MoveAndSlide();
        RefreshSeatLocks();
    }

    private void RefreshSeatLocks()
    {
        if (DriverPeerId > 0 && FindPlayer(DriverPeerId) is PlayerMovement drv)
        {
            drv.EnterVehicle(this, true, SeatPosition(DriverPeerId));
            var loadout = drv.GetNodeOrNull<CharacterLoadout>("CharacterLoadout");
            DamageController.DriverCharacterId = loadout?.CharacterId ?? string.Empty;
        }
        else
        {
            DamageController.DriverCharacterId = string.Empty;
        }

        for (int i = 0; i < MaxSeats - 1; i++)
        {
            int pId = PassengerPeerIds[i];
            if (pId > 0 && FindPlayer(pId) is PlayerMovement pass)
            {
                pass.EnterVehicle(this, false, SeatPosition(pId));
            }
        }

        int local = Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 1;
        if (!IsDriver(local) && _activeSeatCamera != null)
        {
            _activeSeatCamera.Current = false;
            _activeSeatCamera = null;
        }
    }

    public void ToggleDriverCamera(int peerId)
    {
        if (!IsDriver(peerId) || FindPlayer(peerId) is not PlayerMovement player) return;
        player.SetVehicleCameraView(false, this);
    }

    private void UpdatePrompt()
    {
        if (_prompt == null) return;
        int local = Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 1;
        var player = FindPlayer(local);
        var health = player?.GetNodeOrNull<Health>("Health");
        if (health != null && (health.IsDead || health.IsDowned))
        {
            _prompt.Visible = false;
            return;
        }
        _prompt.Visible = true;
        bool near = player != null && GlobalPosition.DistanceTo(player.GlobalPosition) <= EnterRange;

        if (DamageController.IsWrecked)
        {
            _prompt.Text = "[DESTROYED WRECK]";
            _prompt.Modulate = new Color(1.0f, 0.2f, 0.2f);
            return;
        }

        if (DamageController.IsDisabled)
        {
            _prompt.Text = HasSeat(local)
                ? "VEHICLE DISABLED  •  E EXIT"
                : near ? $"{DisplayName} [DISABLED]  •  E REPAIR" : $"{DisplayName} [DISABLED]";
            _prompt.Modulate = new Color(1.0f, 0.45f, 0.15f);
            return;
        }

        _prompt.Modulate = new Color(0.75f, 0.95f, 1.0f);
        _prompt.Text = HasSeat(local)
            ? (IsDriver(local) ? "DRIVER  •  E EXIT" : "GUNNER  •  E EXIT")
            : near ? $"{DisplayName}  •  E ENTER" : DisplayName;
    }

    private void OnDamageStateChanged(int newState)
    {
        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
        {
            Rpc(MethodName.RpcSyncDamage, DamageController.CurrentHealth, newState,
                DamageController.TiresIntact[0], DamageController.TiresIntact[1], DamageController.TiresIntact[2], DamageController.TiresIntact[3]);
        }
        if ((VehicleDamageController.DamageState)newState == VehicleDamageController.DamageState.Wrecked)
        {
            ApplyWreckVisual();
        }
    }

    private void ApplyWreckVisual()
    {
        // Eject all seats
        int d = DriverPeerId;
        DriverPeerId = 0;
        if (d != 0 && FindPlayer(d) is PlayerMovement drv) drv.LeaveVehicle(this, SeatExitPosition(d));

        for (int i = 0; i < PassengerPeerIds.Length; i++)
        {
            int p = PassengerPeerIds[i];
            PassengerPeerIds[i] = 0;
            if (p != 0 && FindPlayer(p) is PlayerMovement pass) pass.LeaveVehicle(this, SeatExitPosition(p));
        }
        PassengerPeerId = 0;

        // Apply burned-out wreck materials
        if (_visual != null)
        {
            var meshes = _visual.FindChildren("*", "MeshInstance3D", true, false);
            var burnedMat = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.12f, 0.12f, 0.12f),
                Roughness = 0.95f,
                Metallic = 0.05f
            };
            foreach (Node child in meshes)
            {
                if (child is MeshInstance3D mesh)
                {
                    mesh.MaterialOverride = burnedMat;
                }
            }
        }
    }

    private void OnWreckExpired()
    {
        if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) return;
        QueueFree();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RpcSyncDamage(float health, int state, bool fl, bool fr, bool rl, bool rr)
    {
        DamageController.CurrentHealth = health;
        DamageController.CurrentState = (VehicleDamageController.DamageState)state;
        DamageController.TiresIntact[0] = fl;
        DamageController.TiresIntact[1] = fr;
        DamageController.TiresIntact[2] = rl;
        DamageController.TiresIntact[3] = rr;

        if (DamageController.IsWrecked)
        {
            ApplyWreckVisual();
        }
    }

    // --- IShootable Implementation ---
    public bool IsHeadHit(Vector3 worldPoint) => false;
    public bool IsFleshTarget => false;

    public void ReceiveShot(ShotInfo shot)
    {
        bool hitTire = shot.HitPoint.Y - GlobalPosition.Y < 0.45f;
        int tireIndex = -1;
        if (hitTire)
        {
            Vector3 local = ToLocal(shot.HitPoint);
            bool front = local.Z < 0;
            bool left = local.X < 0;
            tireIndex = front ? (left ? 0 : 1) : (left ? 2 : 3);
        }

        float shotDamage = shot.Damage;
        // Rage perk: Hollow-Point Feed (+10% bonus damage against enemy vehicle armor and tires)
        if (shot.AttackerPeerId > 0 && FindPlayer((int)shot.AttackerPeerId)?.GetNodeOrNull<CharacterLoadout>("CharacterLoadout")?.CharacterId == "shooter_rage")
        {
            shotDamage *= 1.10f;
        }

        float oldHp = DamageController.CurrentHealth;
        DamageController.ApplyDamage(shotDamage, shot.IsHeavy, hitTire, tireIndex, shot.AttackerPeerId);
        MatchManager.Instance?.ActiveController?.OnVehicleDamaged(this, shotDamage, oldHp, DamageController.CurrentHealth, shot.AttackerPeerId, shot.IsHeavy);
        if (DamageController.IsWrecked)
        {
            ApplyWreckVisual();
        }
        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
        {
            Rpc(MethodName.RpcSyncDamage, DamageController.CurrentHealth, (int)DamageController.CurrentState,
                DamageController.TiresIntact[0], DamageController.TiresIntact[1], DamageController.TiresIntact[2], DamageController.TiresIntact[3]);
        }
    }

    // --- IExplosionReceiver Implementation ---
    public Vector3 BlastPosition => GlobalPosition + new Vector3(0, 0.75f, 0);

    public void ReceiveBlast(float damage, Vector3 origin, long sourcePeerId)
    {
        bool isHeavy = damage >= 90.0f;
        float oldHp = DamageController.CurrentHealth;
        DamageController.ApplyDamage(damage, isHeavy, hitTire: false, -1, sourcePeerId);
        MatchManager.Instance?.ActiveController?.OnVehicleDamaged(this, damage, oldHp, DamageController.CurrentHealth, sourcePeerId, isHeavy);
        if (DamageController.IsWrecked)
        {
            ApplyWreckVisual();
        }
        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
        {
            Rpc(MethodName.RpcSyncDamage, DamageController.CurrentHealth, (int)DamageController.CurrentState,
                DamageController.TiresIntact[0], DamageController.TiresIntact[1], DamageController.TiresIntact[2], DamageController.TiresIntact[3]);
        }
    }

    /// <summary>Called when vehicle rolls over a defender's spike strip.</summary>
    public void HitSpikeStrip(long attackerId = 0)
    {
        DamageController.HitSpikeStrip(attackerId);
        MatchManager.Instance?.ActiveController?.OnSpikeStripHit(this, attackerId);
        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
        {
            Rpc(MethodName.RpcSyncDamage, DamageController.CurrentHealth, (int)DamageController.CurrentState,
                DamageController.TiresIntact[0], DamageController.TiresIntact[1], DamageController.TiresIntact[2], DamageController.TiresIntact[3]);
        }
    }

    /// <summary>Resets vehicle damage, tires, materials, speed, and synchronizes state for a new round.</summary>
    public void ResetVehicle()
    {
        DamageController.Reset();
        if (_visual != null)
        {
            var meshes = _visual.FindChildren("*", "MeshInstance3D", true, false);
            foreach (Node child in meshes)
            {
                if (child is MeshInstance3D mesh)
                {
                    mesh.MaterialOverride = null;
                }
            }
        }
        _speed = 0;
        _steer = 0;
        _throttle = 0;
        _boostFuel = 100.0f;
        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
        {
            Rpc(MethodName.RpcSyncDamage, DamageController.CurrentHealth, (int)DamageController.CurrentState,
                DamageController.TiresIntact[0], DamageController.TiresIntact[1], DamageController.TiresIntact[2], DamageController.TiresIntact[3]);
        }
    }

    private static CharacterBody3D? FindPlayer(int peerId)
    {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null) return null;
        foreach (Node node in tree.GetNodesInGroup("players"))
        {
            if (node is CharacterBody3D body && PlayerIdentity.IdOf(body) == peerId) return body;
        }
        if (tree.Root != null)
        {
            var found = FindPlayerRecursive(tree.Root, peerId);
            if (found != null) return found;
        }
        return null;
    }

    private static CharacterBody3D? FindPlayerRecursive(Node parent, int peerId)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is CharacterBody3D body && PlayerIdentity.IdOf(body) == peerId) return body;
            var sub = FindPlayerRecursive(child, peerId);
            if (sub != null) return sub;
        }
        return null;
    }
}

public static class VehicleInput
{
    public static void EnsureRegistered()
    {
        Register("vehicle_interact", Key.E);
        Register("vehicle_gas", Key.W);
        Register("vehicle_reverse", Key.S);
        Register("vehicle_left", Key.A);
        Register("vehicle_right", Key.D);
        Register("vehicle_brake", Key.Space);
        Register("vehicle_handbrake", Key.Shift);
        Register("vehicle_boost", Key.Shift);
    }

    private static void Register(string action, Key key)
    {
        if (InputMap.HasAction(action)) return;
        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }
}
