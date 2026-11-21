using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// LiveSpectatorCam (Master GDD Section 2.5 & Section 7 Architecture):
/// CS2/PUBG-style live broadcast observer camera system.
/// Allows tournament hosts and match spectators to cycle smoothly through any active player's
/// first-person or vehicle cockpit perspective in real time with tactical broadcast HUD.
/// </summary>
public partial class LiveSpectatorCam : CanvasLayer
{
    public static LiveSpectatorCam? Instance { get; private set; }

    [Signal] public delegate void ObservedPlayerChangedEventHandler(long peerId, string playerName);

    private readonly List<CharacterBody3D> _activePlayers = new();
    private int _targetIndex = 0;
    private Camera3D? _observerCamera;
    private bool _isActive = false;

    // HUD Elements
    private Control _overlay = null!;
    private Label _targetNameLabel = null!;
    private Label _teamBadgeLabel = null!;
    private Label _weaponLabel = null!;
    private Label _healthLabel = null!;
    private Label _perspectiveLabel = null!;
    private bool _preferCockpit = true;

    public bool IsActive => _isActive;
    public CharacterBody3D? CurrentTarget => _activePlayers.Count > 0 && _targetIndex >= 0 && _targetIndex < _activePlayers.Count
        ? _activePlayers[_targetIndex] : null;

    public override void _Ready()
    {
        Instance = this;
        Layer = 80;
        BuildHud();
        SetSpectatorActive(false);
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildHud()
    {
        _overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_overlay);

        // Top live broadcast indicator
        var topBar = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(0, 48),
            Alignment = BoxContainer.AlignmentMode.Center
        };
        topBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        _overlay.AddChild(topBar);

        var recDot = new ColorRect { Color = Colors.Red, CustomMinimumSize = new Vector2(12, 12), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        topBar.AddChild(recDot);

        var liveLabel = new Label
        {
            Text = " LIVE BROADCAST OBSERVER // SPECTATOR FEED",
            ThemeTypeVariation = "HeaderMedium"
        };
        liveLabel.Modulate = Colors.White;
        topBar.AddChild(liveLabel);

        // Bottom tactical banner
        var bottomPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(650, 110)
        };
        bottomPanel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        bottomPanel.Position = new Vector2(0, -130);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.08f, 0.1f, 0.88f),
            BorderColor = new Color(0.3f, 0.6f, 0.9f),
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 10,
            ContentMarginBottom = 10
        };
        bottomPanel.AddThemeStyleboxOverride("panel", style);
        _overlay.AddChild(bottomPanel);

        var col = new VBoxContainer();
        bottomPanel.AddChild(col);

        var headerRow = new HBoxContainer();
        col.AddChild(headerRow);

        _teamBadgeLabel = new Label { Text = "[TEAM ALBERT]", Modulate = new Color(0.3f, 0.7f, 1.0f) };
        headerRow.AddChild(_teamBadgeLabel);

        _targetNameLabel = new Label { Text = "OPERATOR_NAME (LVL 55)", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        headerRow.AddChild(_targetNameLabel);

        _perspectiveLabel = new Label { Text = "FIRST-PERSON VIEW", Modulate = Colors.Gold };
        headerRow.AddChild(_perspectiveLabel);

        var statsRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        col.AddChild(statsRow);

        _healthLabel = new Label { Text = "HP: 100 / 100", Modulate = Colors.LightGreen, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        statsRow.AddChild(_healthLabel);

        _weaponLabel = new Label { Text = "WEAPON: AK-47 [30/90]", Modulate = Colors.LightSkyBlue };
        statsRow.AddChild(_weaponLabel);

        var hintLabel = new Label
        {
            Text = "◀ [A] / [D] ▶ Cycle Players  ·  [C] Toggle Cockpit/FPS  ·  [F1] Exit Spectator",
            Modulate = Colors.Gray,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        col.AddChild(hintLabel);
    }

    public void SetSpectatorActive(bool active)
    {
        _isActive = active;
        _overlay.Visible = active;

        if (active)
        {
            RefreshPlayerList();
            SwitchToTarget(_targetIndex);
        }
        else
        {
            if (_observerCamera != null && IsInstanceValid(_observerCamera))
            {
                _observerCamera.ClearCurrent();
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.F1)
            {
                SetSpectatorActive(!_isActive);
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!_isActive) return;

            if (key.Keycode == Key.D || key.Keycode == Key.Right)
            {
                CycleNext();
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == Key.A || key.Keycode == Key.Left)
            {
                CyclePrev();
                GetViewport().SetInputAsHandled();
            }
            else if (key.Keycode == Key.C)
            {
                _preferCockpit = !_preferCockpit;
                SwitchToTarget(_targetIndex);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public void RefreshPlayerList()
    {
        _activePlayers.Clear();
        foreach (Node node in GetTree().GetNodesInGroup("players"))
        {
            if (node is CharacterBody3D player && IsInstanceValid(player))
            {
                _activePlayers.Add(player);
            }
        }
    }

    public void CycleNext()
    {
        RefreshPlayerList();
        if (_activePlayers.Count == 0) return;
        _targetIndex = (_targetIndex + 1) % _activePlayers.Count;
        SwitchToTarget(_targetIndex);
    }

    public void CyclePrev()
    {
        RefreshPlayerList();
        if (_activePlayers.Count == 0) return;
        _targetIndex = (_targetIndex - 1 + _activePlayers.Count) % _activePlayers.Count;
        SwitchToTarget(_targetIndex);
    }

    public void SwitchToTarget(int index)
    {
        if (_activePlayers.Count == 0) return;
        _targetIndex = Math.Clamp(index, 0, _activePlayers.Count - 1);
        CharacterBody3D target = _activePlayers[_targetIndex];

        long peerId = PlayerIdentity.IdOf(target);
        string name = target.HasMeta("player_name") ? target.GetMeta("player_name").AsString() : target.Name.ToString();
        _targetNameLabel.Text = $"{name.ToUpper()} (LVL {ScoreAndProgression.CalculateRank((int)peerId * 1000)})";

        int team = MatchManager.Instance?.GetTeam(peerId) ?? 0;
        _teamBadgeLabel.Text = team == 0 ? "[TEAM ALBERT]" : "[TEAM EINSTEIN]";
        _teamBadgeLabel.Modulate = team == 0 ? new Color(0.3f, 0.7f, 1.0f) : new Color(1.0f, 0.55f, 0.2f);

        // Check health
        var health = target.GetNodeOrNull<Health>("Health");
        if (health != null)
        {
            if (health.CurrentShield > 0.0f)
            {
                _healthLabel.Text = $"HP: {Mathf.RoundToInt(health.CurrentHealth)} / {Mathf.RoundToInt(health.MaxHealth)}  |  SHIELD: {Mathf.RoundToInt(health.CurrentShield)}";
            }
            else
            {
                _healthLabel.Text = $"HP: {Mathf.RoundToInt(health.CurrentHealth)} / {Mathf.RoundToInt(health.MaxHealth)}";
            }
        }

        // Check weapon
        var weaponSwitcher = target.GetNodeOrNull<WeaponSwitcher>("Head/CameraShake/FirstPersonCamera/ViewModelRoot/WeaponSwitcher");
        if (weaponSwitcher != null)
        {
            _weaponLabel.Text = $"WEAPON: {weaponSwitcher.CurrentWeaponName}";
        }

        // Camera perspective: FPS or Vehicle Cockpit
        Camera3D? targetCam = null;
        if (_preferCockpit)
        {
            // Look for vehicle cockpit camera if driving or crewing
            var vehicle = target.GetParent() as GameVehicle;
            if (vehicle != null)
            {
                targetCam = vehicle.GetNodeOrNull<Camera3D>("CockpitCamera")
                    ?? vehicle.GetNodeOrNull<Camera3D>("DashboardCamera");
                _perspectiveLabel.Text = "COCKPIT PERSPECTIVE";
            }
        }

        if (targetCam == null)
        {
            targetCam = target.GetNodeOrNull<Camera3D>("Head/CameraShake/FirstPersonCamera");
            _perspectiveLabel.Text = "FIRST-PERSON PERSPECTIVE";
        }

        if (targetCam != null)
        {
            targetCam.MakeCurrent();
            _observerCamera = targetCam;
        }

        EmitSignal(SignalName.ObservedPlayerChanged, peerId, name);
    }
}
