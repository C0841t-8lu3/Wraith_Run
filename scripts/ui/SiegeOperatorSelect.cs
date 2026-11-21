using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Rainbow Six Siege-style 12-Operator selection grid (Master GDD Section 2.3, 3, 6.2 Tab 3).
/// Displays a 3x4 grid (4 Drivers, 4 Shooters, 4 Medics) with 3D/stage preview, passive perk details,
/// previous-round repeat lock, and 4-round role cap tracker.
/// </summary>
public partial class SiegeOperatorSelect : Control
{
    [Signal] public delegate void OperatorConfirmedEventHandler(string operatorId);

    private readonly Dictionary<string, Button> _operatorButtons = new();
    private string _selectedOperatorId = "driver_apex";

    private Label _roundLabel = null!;
    private Label _driverRoleLabel = null!;
    private Label _shooterRoleLabel = null!;
    private Label _medicRoleLabel = null!;

    private Label _callsignLabel = null!;
    private Label _realNameLabel = null!;
    private Label _classLabel = null!;
    private Label _perkNameLabel = null!;
    private Label _perkDescLabel = null!;
    private Label _loadoutLabel = null!;
    private Label _lockWarningLabel = null!;
    private Button _confirmButton = null!;
    private OperatorStage? _stage;

    public string SelectedOperatorId => _selectedOperatorId;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        BuildUI();
        if (!string.IsNullOrEmpty(PlayerProfile.OperatorId) && OperatorInfo.Find(PlayerProfile.OperatorId) != null)
        {
            _selectedOperatorId = PlayerProfile.OperatorId;
        }
        SelectOperator(_selectedOperatorId);

        if (OperatorRotationManager.Instance != null)
        {
            OperatorRotationManager.Instance.RoleCountsChanged += RefreshRoleStatus;
            OperatorRotationManager.Instance.RoundAdvanced += _ => RefreshRoleStatus();
        }
    }

    public override void _ExitTree()
    {
        if (OperatorRotationManager.Instance != null)
        {
            OperatorRotationManager.Instance.RoleCountsChanged -= RefreshRoleStatus;
            OperatorRotationManager.Instance.RoundAdvanced -= _ => RefreshRoleStatus();
        }
    }

    private void BuildUI()
    {
        // Dark tactical background
        var bg = new ColorRect
        {
            Color = new Color(0.06f, 0.07f, 0.08f, 0.96f)
        };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var mainHBox = new HBoxContainer();
        mainHBox.SetAnchorsPreset(LayoutPreset.FullRect);
        mainHBox.AddThemeConstantOverride("separation", 16);
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        margin.AddChild(mainHBox);
        AddChild(margin);

        // LEFT COLUMN: 3x4 Grid & Role Trackers
        var leftCol = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        leftCol.AddThemeConstantOverride("separation", 10);
        mainHBox.AddChild(leftCol);

        // Header with Match & Round Tracker
        var headerBox = new HBoxContainer();
        headerBox.AddThemeConstantOverride("separation", 14);
        leftCol.AddChild(headerBox);

        var titleLabel = new Label
        {
            Text = "OPERATOR ROSTER",
        };
        titleLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        titleLabel.AddThemeFontSizeOverride("font_size", 20);
        titleLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.95f));
        headerBox.AddChild(titleLabel);

        int round = OperatorRotationManager.Instance?.CurrentRound ?? 1;
        _roundLabel = new Label
        {
            Text = $"CONVOY ASSAULT · ROUND {round} OF 7 · BEST OF 7"
        };
        _roundLabel.AddThemeFontOverride("font", MenuStyle.SemiBold);
        _roundLabel.AddThemeFontSizeOverride("font_size", 14);
        _roundLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.75f, 1.0f));
        headerBox.AddChild(_roundLabel);

        // Grid Scroll container
        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        leftCol.AddChild(scroll);

        var gridContainer = new VBoxContainer();
        gridContainer.AddThemeConstantOverride("separation", 12);
        gridContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(gridContainer);

        // ROW 1: DRIVERS (4)
        BuildRoleSection(gridContainer, CharacterData.CharacterClass.Driver, "DRIVERS",
            out _driverRoleLabel, new Color(0.9f, 0.6f, 0.2f),
            new[] { "driver_apex", "driver_shift", "driver_ghost", "driver_tread" });

        // ROW 2: SHOOTERS (4)
        BuildRoleSection(gridContainer, CharacterData.CharacterClass.Shooter, "SHOOTERS",
            out _shooterRoleLabel, new Color(0.85f, 0.3f, 0.3f),
            new[] { "shooter_viper", "shooter_rage", "shooter_spark", "shooter_flash" });

        // ROW 3: MEDICS (4)
        BuildRoleSection(gridContainer, CharacterData.CharacterClass.Medic, "MEDICS",
            out _medicRoleLabel, new Color(0.3f, 0.85f, 0.5f),
            new[] { "medic_pulse", "medic_patch", "medic_stim", "medic_anchor" });

        // RIGHT COLUMN: 3D Stage & Detail Card
        var rightCol = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rightCol.AddThemeConstantOverride("separation", 10);
        mainHBox.AddChild(rightCol);

        // 3D character preview viewport / stage
        var stagePanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 190),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        var stageStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.06f, 0.9f),
            BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            BorderColor = new Color(0.2f, 0.24f, 0.28f),
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6
        };
        stagePanel.AddThemeStyleboxOverride("panel", stageStyle);
        rightCol.AddChild(stagePanel);

        _stage = new OperatorStage();
        stagePanel.AddChild(_stage);

        // Details panel
        var detailsPanel = new PanelContainer();
        var detailsStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.11f, 0.95f),
            BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            BorderColor = new Color(0.25f, 0.3f, 0.35f),
            CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6
        };
        detailsPanel.AddThemeStyleboxOverride("panel", detailsStyle);
        rightCol.AddChild(detailsPanel);

        var detailsBox = new VBoxContainer();
        detailsBox.AddThemeConstantOverride("separation", 5);
        var detailsMargin = new MarginContainer();
        detailsMargin.AddThemeConstantOverride("margin_left", 12);
        detailsMargin.AddThemeConstantOverride("margin_right", 12);
        detailsMargin.AddThemeConstantOverride("margin_top", 10);
        detailsMargin.AddThemeConstantOverride("margin_bottom", 10);
        detailsMargin.AddChild(detailsBox);
        detailsPanel.AddChild(detailsMargin);

        _callsignLabel = new Label();
        _callsignLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        _callsignLabel.AddThemeFontSizeOverride("font_size", 22);
        _callsignLabel.AddThemeColorOverride("font_color", Colors.White);
        detailsBox.AddChild(_callsignLabel);

        var subHBox = new HBoxContainer();
        subHBox.AddThemeConstantOverride("separation", 10);
        detailsBox.AddChild(subHBox);

        _classLabel = new Label();
        _classLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        _classLabel.AddThemeFontSizeOverride("font_size", 13);
        subHBox.AddChild(_classLabel);

        _realNameLabel = new Label();
        _realNameLabel.AddThemeFontOverride("font", MenuStyle.Medium);
        _realNameLabel.AddThemeFontSizeOverride("font_size", 14);
        _realNameLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.8f));
        subHBox.AddChild(_realNameLabel);

        var sep = new HSeparator();
        detailsBox.AddChild(sep);

        var perkHeader = new Label { Text = "TACTICAL PASSIVE PERK" };
        perkHeader.AddThemeFontOverride("font", MenuStyle.SemiBold);
        perkHeader.AddThemeFontSizeOverride("font_size", 12);
        perkHeader.AddThemeColorOverride("font_color", new Color(0.9f, 0.75f, 0.3f));
        detailsBox.AddChild(perkHeader);

        _perkNameLabel = new Label();
        _perkNameLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        _perkNameLabel.AddThemeFontSizeOverride("font_size", 16);
        _perkNameLabel.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.8f));
        detailsBox.AddChild(_perkNameLabel);

        _perkDescLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _perkDescLabel.AddThemeFontOverride("font", MenuStyle.Medium);
        _perkDescLabel.AddThemeFontSizeOverride("font_size", 13);
        _perkDescLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.87f, 0.9f));
        detailsBox.AddChild(_perkDescLabel);

        var loadoutHeader = new Label { Text = "DEFAULT LOADOUT" };
        loadoutHeader.AddThemeFontOverride("font", MenuStyle.SemiBold);
        loadoutHeader.AddThemeFontSizeOverride("font_size", 12);
        loadoutHeader.AddThemeColorOverride("font_color", new Color(0.4f, 0.75f, 1.0f));
        detailsBox.AddChild(loadoutHeader);

        _loadoutLabel = new Label();
        _loadoutLabel.AddThemeFontOverride("font", MenuStyle.Medium);
        _loadoutLabel.AddThemeFontSizeOverride("font_size", 13);
        _loadoutLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.95f, 1.0f));
        detailsBox.AddChild(_loadoutLabel);

        _lockWarningLabel = new Label { Visible = false, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _lockWarningLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        _lockWarningLabel.AddThemeFontSizeOverride("font_size", 13);
        _lockWarningLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.35f, 0.35f));
        detailsBox.AddChild(_lockWarningLabel);

        _confirmButton = new Button
        {
            Text = "CONFIRM OPERATOR",
            CustomMinimumSize = new Vector2(0, 38)
        };
        _confirmButton.AddThemeFontOverride("font", MenuStyle.Bold);
        _confirmButton.AddThemeFontSizeOverride("font_size", 16);
        _confirmButton.Pressed += OnConfirmPressed;
        detailsBox.AddChild(_confirmButton);

        RefreshRoleStatus();
    }

    private void BuildRoleSection(VBoxContainer parent, CharacterData.CharacterClass role, string title,
        out Label roleCountLabel, Color accentColor, string[] operatorIds)
    {
        var sectionBox = new VBoxContainer();
        sectionBox.AddThemeConstantOverride("separation", 6);
        parent.AddChild(sectionBox);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 8);
        sectionBox.AddChild(header);

        var titleLbl = new Label { Text = title };
        titleLbl.AddThemeFontOverride("font", MenuStyle.Bold);
        titleLbl.AddThemeFontSizeOverride("font_size", 15);
        titleLbl.AddThemeColorOverride("font_color", accentColor);
        header.AddChild(titleLbl);

        roleCountLabel = new Label { Text = "[ 0 / 4 ROUNDS ]" };
        roleCountLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        roleCountLabel.AddThemeFontSizeOverride("font_size", 13);
        roleCountLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.8f));
        header.AddChild(roleCountLabel);

        var grid = new HBoxContainer();
        grid.AddThemeConstantOverride("separation", 8);
        sectionBox.AddChild(grid);

        foreach (string id in operatorIds)
        {
            CharacterData? data = OperatorInfo.Find(id);
            if (data == null) continue;

            var cardBtn = new Button
            {
                CustomMinimumSize = new Vector2(120, 86),
                ToggleMode = false
            };
            string opId = id;
            cardBtn.Pressed += () => SelectOperator(opId);

            var cardLayout = new VBoxContainer();
            cardLayout.SetAnchorsPreset(LayoutPreset.FullRect);
            cardLayout.AddThemeConstantOverride("separation", 2);
            var cardMargin = new MarginContainer();
            cardMargin.SetAnchorsPreset(LayoutPreset.FullRect);
            cardMargin.AddThemeConstantOverride("margin_left", 6);
            cardMargin.AddThemeConstantOverride("margin_right", 6);
            cardMargin.AddThemeConstantOverride("margin_top", 5);
            cardMargin.AddThemeConstantOverride("margin_bottom", 5);
            cardMargin.AddChild(cardLayout);
            cardBtn.AddChild(cardMargin);

            var topRow = new HBoxContainer();
            cardLayout.AddChild(topRow);

            var nameLbl = new Label
            {
                Text = OperatorInfo.Callsign(data),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            nameLbl.AddThemeFontOverride("font", MenuStyle.Bold);
            nameLbl.AddThemeFontSizeOverride("font_size", 14);
            topRow.AddChild(nameLbl);

            var perkLbl = new Label
            {
                Text = data.SkillName,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            perkLbl.AddThemeFontOverride("font", MenuStyle.Medium);
            perkLbl.AddThemeFontSizeOverride("font_size", 11);
            perkLbl.AddThemeColorOverride("font_color", new Color(0.8f, 0.82f, 0.85f));
            cardLayout.AddChild(perkLbl);

            var statusLbl = new Label
            {
                Name = "StatusLabel",
                Text = "AVAILABLE",
                HorizontalAlignment = HorizontalAlignment.Right
            };
            statusLbl.AddThemeFontOverride("font", MenuStyle.Bold);
            statusLbl.AddThemeFontSizeOverride("font_size", 10);
            statusLbl.AddThemeColorOverride("font_color", new Color(0.4f, 0.85f, 0.5f));
            cardLayout.AddChild(statusLbl);

            _operatorButtons[id] = cardBtn;
            grid.AddChild(cardBtn);
        }
    }

    public void SelectOperator(string operatorId)
    {
        _selectedOperatorId = operatorId;
        CharacterData? data = OperatorInfo.Find(operatorId);
        if (data == null) return;

        _callsignLabel.Text = OperatorInfo.Callsign(data);
        _realNameLabel.Text = OperatorInfo.RealName(data);
        _classLabel.Text = OperatorInfo.ClassName(data.Class);
        _classLabel.AddThemeColorOverride("font_color", data.Class switch
        {
            CharacterData.CharacterClass.Driver => new Color(0.9f, 0.6f, 0.2f),
            CharacterData.CharacterClass.Shooter => new Color(0.85f, 0.3f, 0.3f),
            CharacterData.CharacterClass.Medic => new Color(0.3f, 0.85f, 0.5f),
            _ => Colors.White,
        });

        _perkNameLabel.Text = data.SkillName;
        _perkDescLabel.Text = data.TacticalDescription;

        string prim = PlayerProfile.GetSetWeapon(PlayerProfile.ActiveWeaponSet, WeaponSlot.Primary1);
        string sec = PlayerProfile.GetSetWeapon(PlayerProfile.ActiveWeaponSet, WeaponSlot.Pistol);
        if (string.IsNullOrEmpty(prim)) prim = PlayerProfile.StartingWeaponName;
        if (string.IsNullOrEmpty(sec)) sec = "Glock 19";
        _loadoutLabel.Text = $"Primary: {prim}   ·   Sidearm: {sec}";

        string lockReason = string.Empty;
        bool isLocked = OperatorRotationManager.Instance != null && OperatorRotationManager.Instance.IsOperatorLocked(operatorId, out lockReason);
        _lockWarningLabel.Visible = isLocked;
        _lockWarningLabel.Text = isLocked ? $"⚠ {lockReason.ToUpperInvariant()}" : string.Empty;
        _confirmButton.Disabled = isLocked;
        _confirmButton.Text = isLocked ? "LOCKED" : "CONFIRM OPERATOR";

        _stage?.ShowOperator(data, OperatorInfo.FindWeapon(PlayerProfile.StartingWeaponName));

        UpdateCardStyles();
    }

    public void RefreshRoleStatus()
    {
        var mgr = OperatorRotationManager.Instance;
        int dRounds = mgr?.DriverRounds ?? 0;
        int sRounds = mgr?.ShooterRounds ?? 0;
        int mRounds = mgr?.MedicRounds ?? 0;

        _driverRoleLabel.Text = $"[ {dRounds} / 4 ROUNDS ]" + (dRounds >= 4 ? "  LOCKED" : "");
        _driverRoleLabel.AddThemeColorOverride("font_color", dRounds >= 4 ? new Color(1f, 0.3f, 0.3f) : new Color(0.7f, 0.75f, 0.8f));

        _shooterRoleLabel.Text = $"[ {sRounds} / 4 ROUNDS ]" + (sRounds >= 4 ? "  LOCKED" : "");
        _shooterRoleLabel.AddThemeColorOverride("font_color", sRounds >= 4 ? new Color(1f, 0.3f, 0.3f) : new Color(0.7f, 0.75f, 0.8f));

        _medicRoleLabel.Text = $"[ {mRounds} / 4 ROUNDS ]" + (mRounds >= 4 ? "  LOCKED" : "");
        _medicRoleLabel.AddThemeColorOverride("font_color", mRounds >= 4 ? new Color(1f, 0.3f, 0.3f) : new Color(0.7f, 0.75f, 0.8f));

        if (_roundLabel != null && mgr != null)
            _roundLabel.Text = $"CONVOY ASSAULT · ROUND {mgr.CurrentRound} OF 7 · BEST OF 7";

        UpdateCardStyles();
        SelectOperator(_selectedOperatorId);
    }

    private void UpdateCardStyles()
    {
        var mgr = OperatorRotationManager.Instance;

        foreach (var (id, btn) in _operatorButtons)
        {
            bool isSelected = id == _selectedOperatorId;
            string lockReason = string.Empty;
            bool isLocked = mgr != null && mgr.IsOperatorLocked(id, out lockReason);
            var statusLbl = btn.GetNodeOrNull<Label>("MarginContainer/VBoxContainer/StatusLabel");

            var style = new StyleBoxFlat
            {
                CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                BorderWidthBottom = isSelected ? 3 : 1,
                BorderWidthTop = isSelected ? 3 : 1,
                BorderWidthLeft = isSelected ? 3 : 1,
                BorderWidthRight = isSelected ? 3 : 1,
            };

            if (isLocked)
            {
                style.BgColor = new Color(0.12f, 0.05f, 0.05f, 0.85f);
                style.BorderColor = new Color(0.6f, 0.15f, 0.15f, 0.8f);
                if (statusLbl != null)
                {
                    statusLbl.Text = lockReason.Contains("4-round") ? "ROLE CAP 4/4" : "PREV ROUND";
                    statusLbl.AddThemeColorOverride("font_color", new Color(1f, 0.35f, 0.35f));
                }
            }
            else if (isSelected)
            {
                style.BgColor = new Color(0.12f, 0.22f, 0.35f, 0.95f);
                style.BorderColor = new Color(0.35f, 0.75f, 1.0f);
                if (statusLbl != null)
                {
                    statusLbl.Text = "SELECTED";
                    statusLbl.AddThemeColorOverride("font_color", new Color(0.35f, 0.85f, 1.0f));
                }
            }
            else
            {
                style.BgColor = new Color(0.1f, 0.12f, 0.14f, 0.85f);
                style.BorderColor = new Color(0.22f, 0.26f, 0.3f);
                if (statusLbl != null)
                {
                    statusLbl.Text = "AVAILABLE";
                    statusLbl.AddThemeColorOverride("font_color", new Color(0.4f, 0.85f, 0.5f));
                }
            }

            btn.AddThemeStyleboxOverride("normal", style);
            btn.AddThemeStyleboxOverride("hover", style);
            btn.AddThemeStyleboxOverride("pressed", style);
        }
    }

    private void OnConfirmPressed()
    {
        var mgr = OperatorRotationManager.Instance;
        if (mgr != null)
        {
            if (mgr.TrySelectOperator(_selectedOperatorId, out string failureReason))
            {
                PlayerProfile.OperatorId = _selectedOperatorId;
                _confirmButton.Text = "OPERATOR CONFIRMED ✓";
                UiSounds.Play(UiSounds.Sound.Click);
                EmitSignal(SignalName.OperatorConfirmed, _selectedOperatorId);
            }
            else
            {
                _lockWarningLabel.Visible = true;
                _lockWarningLabel.Text = $"⚠ {failureReason.ToUpperInvariant()}";
            }
        }
        else
        {
            PlayerProfile.OperatorId = _selectedOperatorId;
            _confirmButton.Text = "OPERATOR CONFIRMED ✓";
            UiSounds.Play(UiSounds.Sound.Click);
            EmitSignal(SignalName.OperatorConfirmed, _selectedOperatorId);
        }
    }
}
