using Godot;
using System;

/// <summary>
/// FiringRangeTarget (Master GDD Section 6.1):
/// Interactive ballistic target dummy for the live Firing Range in the Warehouse Hub.
/// Implements IShootable with separate body and head (2.5x) hitboxes to test recoil,
/// bullet spread, canted optics, and damage falloff.
/// </summary>
public partial class FiringRangeTarget : StaticBody3D, IShootable
{
    [Export] public float MaxHealth = 100.0f;
    [Export] public float HeadshotMultiplier = 2.5f;

    public float CurrentHealth { get; private set; }
    private Label3D? _damageLabel;
    private Tween? _resetTween;
    private MeshInstance3D? _headMesh;
    private MeshInstance3D? _torsoMesh;

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
        CollisionLayer = 1 | (1 << 2); // Environment + Shootable layer
        BuildVisualsAndColliders();
    }

    private void BuildVisualsAndColliders()
    {
        // Torso
        var torso = new MeshInstance3D
        {
            Name = "TorsoMesh",
            Mesh = new BoxMesh { Size = new Vector3(0.5f, 0.9f, 0.25f) },
            Position = new Vector3(0.0f, 1.15f, 0.0f)
        };
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.22f, 0.35f, 0.45f), // Steel blue tactical silhouette
            Roughness = 0.8f
        };
        torso.MaterialOverride = mat;
        AddChild(torso);
        _torsoMesh = torso;

        var torsoCol = new CollisionShape3D
        {
            Name = "TorsoCollider",
            Shape = new BoxShape3D { Size = new Vector3(0.5f, 0.9f, 0.25f) },
            Position = new Vector3(0.0f, 1.15f, 0.0f)
        };
        AddChild(torsoCol);

        // Head
        var head = new MeshInstance3D
        {
            Name = "HeadMesh",
            Mesh = new SphereMesh { Radius = 0.16f, Height = 0.32f },
            Position = new Vector3(0.0f, 1.8f, 0.0f)
        };
        var headMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.75f, 0.25f, 0.25f), // Red bullseye head target
            Roughness = 0.6f
        };
        head.MaterialOverride = headMat;
        AddChild(head);
        _headMesh = head;

        var headCol = new CollisionShape3D
        {
            Name = "HeadCollider",
            Shape = new SphereShape3D { Radius = 0.18f },
            Position = new Vector3(0.0f, 1.8f, 0.0f)
        };
        headCol.AddToGroup("head_hitbox");
        AddChild(headCol);

        // Stand / Base post
        var post = new MeshInstance3D
        {
            Name = "StandMesh",
            Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.06f, Height = 0.7f },
            Position = new Vector3(0.0f, 0.35f, 0.0f)
        };
        post.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.15f, 0.15f, 0.15f) };
        AddChild(post);

        // Floating Damage Label
        _damageLabel = new Label3D
        {
            Name = "DamageLabel",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Position = new Vector3(0.0f, 2.2f, 0.0f),
            FontSize = 32,
            Modulate = Colors.Yellow,
            Text = "",
            NoDepthTest = true
        };
        AddChild(_damageLabel);
    }

    public bool IsFleshTarget => true;

    public bool IsHeadHit(Vector3 worldPoint)
    {
        return worldPoint.Y >= (GlobalPosition.Y + 1.6f);
    }

    public void ReceiveShot(ShotInfo shot)
    {
        bool isHead = shot.IsHeadshot || IsHeadHit(shot.HitPoint);
        float mult = isHead ? HeadshotMultiplier : 1.0f;
        float finalDmg = shot.Damage * mult;

        CurrentHealth = Mathf.Max(0.0f, CurrentHealth - finalDmg);

        if (_damageLabel != null)
        {
            string txt = isHead ? $"HEADSHOT! -{Mathf.RoundToInt(finalDmg)}" : $"-{Mathf.RoundToInt(finalDmg)}";
            _damageLabel.Text = txt;
            _damageLabel.Modulate = isHead ? Colors.OrangeRed : Colors.Yellow;
        }

        // Brief flinch animation
        if (_resetTween != null && _resetTween.IsValid()) _resetTween.Kill();
        _resetTween = CreateTween();
        _resetTween.TweenProperty(this, "rotation_degrees:x", -15.0f, 0.08f);
        _resetTween.TweenProperty(this, "rotation_degrees:x", 0.0f, 0.25f);
        _resetTween.TweenInterval(1.5f);
        _resetTween.TweenCallback(Callable.From(() =>
        {
            CurrentHealth = MaxHealth;
            if (_damageLabel != null) _damageLabel.Text = "";
        }));
    }

    public void OnShot(Vector3 hitPoint, Vector3 normal, float damage, long shooterPeerId, string weaponName, bool isHeadshot = false)
    {
        ReceiveShot(new ShotInfo
        {
            Damage = damage,
            IsHeadshot = isHeadshot,
            HitPoint = hitPoint,
            AttackerPeerId = shooterPeerId,
            WeaponName = weaponName
        });
    }
}
