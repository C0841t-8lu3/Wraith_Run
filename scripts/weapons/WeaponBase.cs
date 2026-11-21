using Godot;
using System;

/// <summary>
/// WeaponBase (Master GDD Section 4.2 & Section 7 Architecture):
/// Modular weapon base node for the 10 core firearms and future expandable weapons.
/// Houses attachment sockets, muzzle anchor, audio emitters, and recoil kick controllers.
/// </summary>
public partial class WeaponBase : Node3D
{
    [Export] public string WeaponName { get; set; } = "AK-47";
    [Export] public int MagazineCapacity { get; set; } = 30;
    [Export] public int CurrentAmmo { get; set; } = 30;
    [Export] public int ReserveAmmo { get; set; } = 90;
    [Export] public float FireRateRpm { get; set; } = 600.0f;
    [Export] public float ReloadTimeSeconds { get; set; } = 2.3f;

    // Sockets for modular 3D attachments (Master GDD Section 6.2 Tab 2)
    public Marker3D? MuzzlePoint { get; private set; }
    public Node3D? OpticsSocket { get; private set; }
    public Node3D? UnderbarrelSocket { get; private set; }
    public Node3D? LaserSocket { get; private set; }
    public Node3D? MagazineSocket { get; private set; }
    public Node3D? StockSocket { get; private set; }

    public override void _Ready()
    {
        MuzzlePoint = GetNodeOrNull<Marker3D>("MuzzlePoint");
        OpticsSocket = GetNodeOrNull<Node3D>("Sockets/OpticsSocket");
        UnderbarrelSocket = GetNodeOrNull<Node3D>("Sockets/UnderbarrelSocket");
        LaserSocket = GetNodeOrNull<Node3D>("Sockets/LaserSocket");
        MagazineSocket = GetNodeOrNull<Node3D>("Sockets/MagazineSocket");
        StockSocket = GetNodeOrNull<Node3D>("Sockets/StockSocket");
    }

    public void FireShot()
    {
        if (CurrentAmmo > 0)
        {
            CurrentAmmo--;
        }
    }

    public void Reload()
    {
        int needed = MagazineCapacity - CurrentAmmo;
        int taken = Math.Min(needed, ReserveAmmo);
        CurrentAmmo += taken;
        ReserveAmmo -= taken;
    }
}
