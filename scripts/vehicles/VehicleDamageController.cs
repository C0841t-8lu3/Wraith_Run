using System;
using Godot;

/// <summary>
/// 100-HP Vehicle Damage Controller (Master GDD Section 4.1).
/// Features a two-state damage model:
/// 1. Disable & Repair (Small Arms / Spike Strips): Reducing HP to 0 via small-arms fire or hitting
///    a spike strip halts the vehicle for a recoverable repair window (e.g. 20s). During this window,
///    medics/repair tools can restore HP to return the vehicle to operational state.
/// 2. Total Destruction (Heavy Weapons / Bazooka): A direct hit from the Bazooka destroys the vehicle
///    immediately with no repair path, leaving a burned-out wreck mesh for 30 seconds.
/// </summary>
public partial class VehicleDamageController : Node
{
    public enum DamageState
    {
        Operational = 0,
        Disabled = 1,
        Wrecked = 2
    }

    [Signal] public delegate void DamageStateChangedEventHandler(int newState);
    [Signal] public delegate void HealthChangedEventHandler(float currentHealth, float maxHealth);
    [Signal] public delegate void TireBlownEventHandler(int tireIndex);
    [Signal] public delegate void VehicleDisabledEventHandler();
    [Signal] public delegate void VehicleWreckedEventHandler();
    [Signal] public delegate void VehicleRepairedEventHandler(float amount);
    [Signal] public delegate void WreckExpiredEventHandler();

    [Export] public float MaxHealth { get; set; } = 100.0f;
    [Export] public float CurrentHealth { get; set; } = 100.0f;
    [Export] public DamageState CurrentState { get; set; } = DamageState.Operational;
    [Export] public string DriverCharacterId { get; set; } = string.Empty;

    public const float DefaultRepairWindowSeconds = 20.0f;
    public const float WreckMeshDurationSeconds = 30.0f;

    public float RepairWindowRemaining { get; private set; }
    public float WreckDurationRemaining { get; private set; }

    /// <summary>Tire integrity: 0=FL, 1=FR, 2=RL, 3=RR.</summary>
    public bool[] TiresIntact { get; } = new bool[4] { true, true, true, true };

    public bool IsOperational => CurrentState == DamageState.Operational;
    public bool IsDisabled => CurrentState == DamageState.Disabled;
    public bool IsWrecked => CurrentState == DamageState.Wrecked;
    public bool CanDrive => CurrentState == DamageState.Operational;
    public bool CanRepair => CurrentState == DamageState.Disabled || (CurrentState == DamageState.Operational && CurrentHealth < MaxHealth);

    public int FlatTireCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < 4; i++) if (!TiresIntact[i]) count++;
            return count;
        }
    }

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
        CurrentState = DamageState.Operational;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (CurrentState == DamageState.Disabled)
        {
            if (RepairWindowRemaining > 0.0f)
            {
                RepairWindowRemaining -= dt;
            }
        }
        else if (CurrentState == DamageState.Wrecked)
        {
            if (WreckDurationRemaining > 0.0f)
            {
                WreckDurationRemaining -= dt;
                if (WreckDurationRemaining <= 0.0f)
                {
                    WreckDurationRemaining = 0.0f;
                    EmitSignal(SignalName.WreckExpired);
                }
            }
        }
    }

    /// <summary>
    /// Applies damage to vehicle.
    /// If isHeavy (Bazooka / Anti-Vehicle Rocket): Instant wreck! Total destruction with no repair path.
    /// If small arms: reduces HP, can disable vehicle into recoverable repair window.
    /// </summary>
    public void ApplyDamage(float amount, bool isHeavy, bool hitTire, int tireIndex = -1, long attackerId = 0)
    {
        if (CurrentState == DamageState.Wrecked) return; // already wrecked

        if (isHeavy)
        {
            // Total Destruction (Bazooka / Heavy Weapon)
            CurrentHealth = 0.0f;
            CurrentState = DamageState.Wrecked;
            WreckDurationRemaining = WreckMeshDurationSeconds;
            for (int i = 0; i < 4; i++) TiresIntact[i] = false;

            EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
            EmitSignal(SignalName.DamageStateChanged, (int)DamageState.Wrecked);
            EmitSignal(SignalName.VehicleWrecked);
            return;
        }

        // Apex perk: Reinforced Chassis - 20% incoming small-arms/collision damage reduction
        if (DriverCharacterId == "driver_apex")
        {
            amount *= 0.80f;
        }

        // Small arms fire
        if (hitTire && tireIndex >= 0 && tireIndex < 4)
        {
            TiresIntact[tireIndex] = false;
            EmitSignal(SignalName.TireBlown, tireIndex);
        }

        CurrentHealth = Mathf.Max(0.0f, CurrentHealth - amount);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);

        if (CurrentHealth <= 0.0f && CurrentState == DamageState.Operational)
        {
            // Recoverable disable (small arms reduced HP to 0)
            CurrentState = DamageState.Disabled;
            RepairWindowRemaining = DefaultRepairWindowSeconds;
            EmitSignal(SignalName.DamageStateChanged, (int)DamageState.Disabled);
            EmitSignal(SignalName.VehicleDisabled);
        }
    }

    /// <summary>
    /// Spike strip hit: blow out all tires and trigger recoverable disable window.
    /// If driver is Tread (Iron Rubber perk): retains traction and prevents complete immobilization.
    /// </summary>
    public void HitSpikeStrip(long attackerId = 0)
    {
        if (CurrentState == DamageState.Wrecked) return;

        bool isTread = DriverCharacterId == "driver_tread";
        for (int i = 0; i < 4; i++)
        {
            TiresIntact[i] = false;
            EmitSignal(SignalName.TireBlown, i);
        }

        if (isTread)
        {
            // Iron Rubber perk: prevents complete immobilization
            CurrentHealth = Mathf.Max(15.0f, CurrentHealth * 0.25f);
        }
        else
        {
            CurrentHealth = 0.0f;
            CurrentState = DamageState.Disabled;
            RepairWindowRemaining = DefaultRepairWindowSeconds;
            EmitSignal(SignalName.DamageStateChanged, (int)DamageState.Disabled);
            EmitSignal(SignalName.VehicleDisabled);
        }

        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
    }

    /// <summary>
    /// Repair vehicle: restores HP. If disabled, restoring HP above 0 returns vehicle to Operational state!
    /// </summary>
    public void Repair(float amount)
    {
        if (CurrentState == DamageState.Wrecked || amount <= 0.0f) return;

        CurrentHealth = Mathf.Clamp(CurrentHealth + amount, 0.0f, MaxHealth);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        EmitSignal(SignalName.VehicleRepaired, amount);

        if (CurrentHealth > 0.0f && CurrentState == DamageState.Disabled)
        {
            // Returned to operational!
            CurrentState = DamageState.Operational;
            RepairWindowRemaining = 0.0f;
            // Repair tires
            for (int i = 0; i < 4; i++) TiresIntact[i] = true;
            EmitSignal(SignalName.DamageStateChanged, (int)DamageState.Operational);
        }
    }

    public void BlowTire(int tireIndex)
    {
        if (tireIndex >= 0 && tireIndex < 4 && TiresIntact[tireIndex])
        {
            TiresIntact[tireIndex] = false;
            EmitSignal(SignalName.TireBlown, tireIndex);
        }
    }

    public void Reset()
    {
        CurrentHealth = MaxHealth;
        CurrentState = DamageState.Operational;
        RepairWindowRemaining = 0.0f;
        WreckDurationRemaining = 0.0f;
        for (int i = 0; i < 4; i++) TiresIntact[i] = true;
        EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        EmitSignal(SignalName.DamageStateChanged, (int)DamageState.Operational);
    }
}
