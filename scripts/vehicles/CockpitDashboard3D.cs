using System;
using Godot;

/// <summary>
/// Physical 3D Cockpit Dashboard for vehicles (Master GDD Section 6.3).
/// Positioned directly in front of the driver and gunner seats.
/// Renders speed, RPM, 100-HP vehicle integrity, tire warnings, and boost directly
/// on the physical 3D dashboard inside the cockpit view.
/// </summary>
public partial class CockpitDashboard3D : Node3D
{
    private Label3D _speedLabel = null!;
    private Label3D _rpmLabel = null!;
    private Label3D _hullLabel = null!;
    private Label3D _tiresLabel = null!;
    private Label3D _boostLabel = null!;
    private MeshInstance3D _backingMesh = null!;

    public override void _Ready()
    {
        BuildDashboard();
    }

    private void BuildDashboard()
    {
        // Dark metallic cluster backing plate
        var boxMesh = new BoxMesh { Size = new Vector3(0.55f, 0.18f, 0.02f) };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.04f, 0.05f, 0.06f),
            Metallic = 0.8f,
            Roughness = 0.3f
        };
        _backingMesh = new MeshInstance3D
        {
            Mesh = boxMesh,
            MaterialOverride = mat,
            Name = "BackingPlate"
        };
        AddChild(_backingMesh);

        // Speed readout (top left)
        _speedLabel = CreateClusterLabel("SPEED: 00 KM/H", new Vector3(-0.16f, 0.04f, 0.015f), 18, new Color(0.2f, 0.9f, 1.0f));
        _speedLabel.Name = "SpeedText";
        AddChild(_speedLabel);

        // RPM readout (top center/right)
        _rpmLabel = CreateClusterLabel("RPM: 0800", new Vector3(0.12f, 0.04f, 0.015f), 16, new Color(1.0f, 0.75f, 0.2f));
        _rpmLabel.Name = "RpmText";
        AddChild(_rpmLabel);

        // 100-HP Vehicle Hull Integrity (center)
        _hullLabel = CreateClusterLabel("HULL: 100 HP · OPERATIONAL", new Vector3(0.0f, 0.00f, 0.015f), 15, new Color(0.3f, 0.95f, 0.4f));
        _hullLabel.Name = "HullText";
        _hullLabel.HorizontalAlignment = HorizontalAlignment.Center;
        AddChild(_hullLabel);

        // Tire warnings (bottom left)
        _tiresLabel = CreateClusterLabel("TIRES: FL:OK FR:OK RL:OK RR:OK", new Vector3(-0.10f, -0.045f, 0.015f), 13, new Color(0.85f, 0.9f, 0.95f));
        _tiresLabel.Name = "TiresText";
        AddChild(_tiresLabel);

        // Boost readout (bottom right)
        _boostLabel = CreateClusterLabel("BOOST: 100%", new Vector3(0.16f, -0.045f, 0.015f), 13, new Color(0.3f, 0.75f, 1.0f));
        _boostLabel.Name = "BoostText";
        AddChild(_boostLabel);
    }

    private static Label3D CreateClusterLabel(string text, Vector3 pos, int fontSize, Color color)
    {
        return new Label3D
        {
            Text = text,
            Position = pos,
            PixelSize = 0.0018f,
            FontSize = fontSize,
            Modulate = color,
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
            NoDepthTest = false,
            OutlineSize = 4,
            OutlineModulate = new Color(0, 0, 0, 0.9f)
        };
    }

    public void UpdateDashboard(float speedKmh, float rpm, float currentHp, float maxHp,
        VehicleDamageController.DamageState state, bool[] tires, float boostPercent)
    {
        if (_speedLabel == null) return;

        // Speed
        _speedLabel.Text = $"SPEED: {Mathf.RoundToInt(Mathf.Abs(speedKmh)):D2} KM/H";

        // RPM
        _rpmLabel.Text = $"RPM: {Mathf.RoundToInt(rpm):D4}";

        // Hull / Integrity
        switch (state)
        {
            case VehicleDamageController.DamageState.Operational:
                _hullLabel.Text = $"HULL: {Mathf.CeilToInt(currentHp)} HP · OPERATIONAL";
                _hullLabel.Modulate = currentHp > 50.0f ? new Color(0.3f, 0.95f, 0.4f) : new Color(1.0f, 0.8f, 0.2f);
                break;
            case VehicleDamageController.DamageState.Disabled:
                _hullLabel.Text = "HULL: 0 HP · [ DISABLED - REPAIRABLE ]";
                _hullLabel.Modulate = new Color(1.0f, 0.4f, 0.1f);
                break;
            case VehicleDamageController.DamageState.Wrecked:
                _hullLabel.Text = "HULL: 0 HP · [ TOTAL WRECK ]";
                _hullLabel.Modulate = new Color(1.0f, 0.2f, 0.2f);
                break;
        }

        // Tires
        if (tires != null && tires.Length >= 4)
        {
            string fl = tires[0] ? "OK" : "FLAT";
            string fr = tires[1] ? "OK" : "FLAT";
            string rl = tires[2] ? "OK" : "FLAT";
            string rr = tires[3] ? "OK" : "FLAT";
            bool anyFlat = !tires[0] || !tires[1] || !tires[2] || !tires[3];
            _tiresLabel.Text = $"TIRES: FL:{fl} FR:{fr} RL:{rl} RR:{rr}";
            _tiresLabel.Modulate = anyFlat ? new Color(1.0f, 0.35f, 0.35f) : new Color(0.85f, 0.9f, 0.95f);
        }

        // Boost
        float displayBoost = boostPercent <= 1.0f && boostPercent > 0.0f ? boostPercent * 100.0f : boostPercent;
        _boostLabel.Text = $"BOOST: {Mathf.RoundToInt(displayBoost)}%";
    }
}
