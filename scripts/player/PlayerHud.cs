using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Combined FPS HUD (Master GDD Section 1, 6.3 & scenes/ui/CombinedHUD.tscn):
/// - Top-Left: Tablet menu button (=), metallic mode shield / team logo, match timer, stacked blue/red team score bars.
/// - Top-Right: Active weapon silhouette with subtle glow, large magazine count stacked over reserve ammo, secondary/melee slot icon.
/// - Center & Bottom: 4-tick reticle, floating yellow +XP / HIGHLIGHT pop-ups to the upper-right of the crosshair, red-outlined kill-streak pill badge at bottom center.
/// - Zero minimap clutter anywhere.
/// - Countdown-free 'RESPAWNING...' screen: stark, static "RESPAWNING..." banner with zero numeric countdown (3s standard, 9s for Convoy Assault).
/// </summary>
public partial class PlayerHud : CanvasLayer
{
    private const float LowHealthFraction = 0.3f;
    private const int MaxFeedLines = 5;

    private Health? _health;
    private WeaponSwitcher? _weapon;
    private PlayerScore? _score;
    private KillFeed? _killFeed;

    // --- Top-Left: Match Status & Score Bars ---
    private Control _topLeftContainer = null!;
    private PanelContainer _telemetryPill = null!;
    private Label _fpsLabel = null!;
    private Label _pingLabel = null!;
    private float _telemetryTimer;
    private const float TelemetryInterval = 0.2f;

    public Label FpsLabel => _fpsLabel;
    public Label PingLabel => _pingLabel;
    public PanelContainer TelemetryPill => _telemetryPill;

    private Label _matchTimerLabel = null!;
    private ProgressBar _blueTeamBar = null!;
    private Label _blueTeamLabel = null!;
    private ProgressBar _redTeamBar = null!;
    private Label _redTeamLabel = null!;
    private Button _tabletButton = null!;
    private TextureRect? _shieldIcon;

    // --- Top-Right: Weapon & Ammo ---
    private Control _topRightContainer = null!;
    private TextureRect _weaponIcon = null!;
    private Label _weaponNameLabel = null!;
    private Label _magAmmoLabel = null!;
    private Label _reserveAmmoLabel = null!;
    private Label _secondarySlotLabel = null!;

    // --- Center: 4-Tick Crosshair & Floating XP Pop-ups ---
    private Control _crosshairRoot = null!;
    private Control _sniperScopeRoot = null!;
    private Control _floatingXpContainer = null!;
    private readonly List<FloatingXpPopup> _activePopups = new();

    // --- Bottom: Kill-Streak Pill Badge ---
    private PanelContainer _killPillContainer = null!;
    private Label _killPillLabel = null!;
    private int _currentKillStreak;
    private float _killPillTimer;

    // --- Countdown-free RESPAWNING... Overlay ---
    private Control _respawningOverlay = null!;
    private Label _respawningBanner = null!;
    private Label _respawningSubText = null!;

    // --- Kill feed & Prompts ---
    private VBoxContainer _killFeedList = null!;
    private readonly List<(Label Line, ulong ExpiryMsec)> _feedLines = new();
    private Label _promptLabel = null!;
    private Label _matchOverLabel = null!;

    public override void _Ready()
    {
        var player = GetParent() as CharacterBody3D;
        if (player != null && !PlayerIdentity.IsLocalHuman(player))
        {
            SetProcess(false);
            return;
        }

        if (player != null)
        {
            _health = player.GetNodeOrNull<Health>("Health");
            _weapon = player.GetNodeOrNull<WeaponSwitcher>("WeaponSwitcher");
            _score = player.GetNodeOrNull<PlayerScore>("Score");
        }

        _killFeed = KillFeed.From(this);

        if (_killFeed != null) _killFeed.KillAnnounced += OnKillAnnounced;
        if (_score != null) _score.MultiKillAwarded += OnMultiKillAwarded;
        if (CareerTracker.Instance != null) CareerTracker.Instance.MedalEarned += OnMedalEarned;
        if (MatchManager.Instance != null) MatchManager.Instance.MatchEnded += OnMatchEnded;

        if (_health != null)
        {
            _health.Died += OnPlayerDied;
            _health.Respawned += OnPlayerRespawned;
        }

        Build();
        if (player != null)
            BuildMenus(player);
    }

    private void BuildMenus(CharacterBody3D player)
    {
        GameUiState.EnsureInputActions();
        AddChild(new Scoreboard { Name = "Scoreboard", Player = player });
        AddChild(new PauseMenu { Name = "PauseMenu", Player = player });
    }

    public override void _ExitTree()
    {
        if (_killFeed != null && IsInstanceValid(_killFeed)) _killFeed.KillAnnounced -= OnKillAnnounced;
        if (_score != null && IsInstanceValid(_score)) _score.MultiKillAwarded -= OnMultiKillAwarded;
        if (CareerTracker.Instance != null) CareerTracker.Instance.MedalEarned -= OnMedalEarned;
        if (MatchManager.Instance != null && IsInstanceValid(MatchManager.Instance))
            MatchManager.Instance.MatchEnded -= OnMatchEnded;

        if (_health != null && IsInstanceValid(_health))
        {
            _health.Died -= OnPlayerDied;
            _health.Respawned -= OnPlayerRespawned;
        }
    }

    private void Build()
    {
        BuildPrompt();

        var root = new Control { Name = "CombinedHudRoot", MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        // 1. Center Crosshair (4-tick reticle)
        Build4TickReticle(root);
        BuildSniperScopeOverlay(root);

        // 2. Floating XP Popups Container (upper-right of crosshair)
        BuildFloatingXpContainer(root);

        // 3. Top-Left: Status Shield, Timer, Blue/Red score bars, Tablet button
        BuildTopLeftMatchStatus(root);

        // 4. Top-Right: Weapon Icon, Stacked Mag/Reserve Ammo, Secondary Slot
        BuildTopRightWeaponSection(root);

        // 5. Bottom Center: Red-Outlined Kill-Streak Pill Badge
        BuildKillPillBadge(root);

        // 6. Bottom Left: Kill Feed
        BuildKillFeed(root);

        // 7. Fullscreen Countdown-Free 'RESPAWNING...' Overlay
        BuildRespawningOverlay(root);

        // 8. Match Over Center
        var matchOverCenter = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        matchOverCenter.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        matchOverCenter.Position = new Vector2(0, -80);
        root.AddChild(matchOverCenter);
        _matchOverLabel = new Label { Visible = false, HorizontalAlignment = HorizontalAlignment.Center };
        _matchOverLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        _matchOverLabel.AddThemeFontSizeOverride("font_size", 36);
        matchOverCenter.AddChild(_matchOverLabel);
    }

    private void Build4TickReticle(Control root)
    {
        var center = new CenterContainer
        {
            Name = "CrosshairCenter",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        _crosshairRoot = new Control
        {
            CustomMinimumSize = new Vector2(40, 40),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        center.AddChild(_crosshairRoot);

        // Custom drawn 4-tick reticle
        var crosshairDrawer = new CrosshairDrawer { Name = "CrosshairDrawer" };
        _crosshairRoot.AddChild(crosshairDrawer);
    }

    private void BuildSniperScopeOverlay(Control root)
    {
        _sniperScopeRoot = new Control
        {
            Name = "SniperScopeRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false
        };
        _sniperScopeRoot.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_sniperScopeRoot);

        var scopeDrawer = new SniperScopeDrawer { Name = "SniperScopeDrawer" };
        _sniperScopeRoot.AddChild(scopeDrawer);
    }

    private void BuildFloatingXpContainer(Control root)
    {
        var center = new CenterContainer
        {
            Name = "FloatingXpCenter",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(center);

        _floatingXpContainer = new Control
        {
            Name = "FloatingXpContainer",
            Position = new Vector2(48, -48),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        center.AddChild(_floatingXpContainer);
    }

    private void BuildTopLeftMatchStatus(Control root)
    {
        _topLeftContainer = new VBoxContainer
        {
            Position = new Vector2(24, 16),
            CustomMinimumSize = new Vector2(260, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _topLeftContainer.AddThemeConstantOverride("separation", 6);
        root.AddChild(_topLeftContainer);

        // Performance Telemetry: FPS & Ping Pill
        BuildTelemetry();

        // Header row: [=] Tablet Button + Metallic Shield Logo + Match Timer
        var headerHBox = new HBoxContainer();
        headerHBox.AddThemeConstantOverride("separation", 10);
        _topLeftContainer.AddChild(headerHBox);

        _tabletButton = new Button
        {
            Text = " = ",
            CustomMinimumSize = new Vector2(36, 28)
        };
        _tabletButton.AddThemeFontOverride("font", MenuStyle.Bold);
        _tabletButton.AddThemeFontSizeOverride("font_size", 16);
        _tabletButton.Pressed += () => HubTablet.Instance?.Toggle();
        headerHBox.AddChild(_tabletButton);

        // Metallic mode shield icon
        var shieldTexture = ResourceLoader.Load<Texture2D>("res://art/ui/icons/class-operator.svg");
        if (shieldTexture != null)
        {
            _shieldIcon = new TextureRect
            {
                Texture = shieldTexture,
                CustomMinimumSize = new Vector2(24, 24),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SelfModulate = new Color(0.85f, 0.9f, 1.0f)
            };
            headerHBox.AddChild(_shieldIcon);
        }

        _matchTimerLabel = new Label
        {
            Text = "05:00",
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _matchTimerLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        _matchTimerLabel.AddThemeFontSizeOverride("font_size", 22);
        _matchTimerLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.95f));
        headerHBox.AddChild(_matchTimerLabel);

        // Blue Team Score Bar (Team Albert)
        var blueBox = new VBoxContainer();
        blueBox.AddThemeConstantOverride("separation", 2);
        _topLeftContainer.AddChild(blueBox);

        _blueTeamLabel = new Label { Text = "ALBERT  0" };
        _blueTeamLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        _blueTeamLabel.AddThemeFontSizeOverride("font_size", 13);
        _blueTeamLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.75f, 1.0f));
        blueBox.AddChild(_blueTeamLabel);

        _blueTeamBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(240, 8),
            ShowPercentage = false,
            MinValue = 0,
            MaxValue = 50,
            Value = 0
        };
        var blueFill = new StyleBoxFlat { BgColor = new Color(0.2f, 0.55f, 0.95f), CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2, CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2 };
        var barBg = new StyleBoxFlat { BgColor = new Color(0.08f, 0.1f, 0.12f, 0.8f), CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2, CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2 };
        _blueTeamBar.AddThemeStyleboxOverride("fill", blueFill);
        _blueTeamBar.AddThemeStyleboxOverride("background", barBg);
        blueBox.AddChild(_blueTeamBar);

        // Red Team Score Bar (Team Einstein)
        var redBox = new VBoxContainer();
        redBox.AddThemeConstantOverride("separation", 2);
        _topLeftContainer.AddChild(redBox);

        _redTeamLabel = new Label { Text = "EINSTEIN  0" };
        _redTeamLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        _redTeamLabel.AddThemeFontSizeOverride("font_size", 13);
        _redTeamLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.4f, 0.35f));
        redBox.AddChild(_redTeamLabel);

        _redTeamBar = new ProgressBar
        {
            CustomMinimumSize = new Vector2(240, 8),
            ShowPercentage = false,
            MinValue = 0,
            MaxValue = 50,
            Value = 0
        };
        var redFill = new StyleBoxFlat { BgColor = new Color(0.9f, 0.25f, 0.25f), CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2, CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2 };
        _redTeamBar.AddThemeStyleboxOverride("fill", redFill);
        _redTeamBar.AddThemeStyleboxOverride("background", (StyleBoxFlat)barBg.Duplicate());
        redBox.AddChild(_redTeamBar);
    }

    private void BuildTelemetry()
    {
        _telemetryPill = new PanelContainer
        {
            Name = "TelemetryPill",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin
        };
        var pillStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.06f, 0.08f, 0.72f),
            CornerRadiusBottomLeft = 3,
            CornerRadiusBottomRight = 3,
            CornerRadiusTopLeft = 3,
            CornerRadiusTopRight = 3,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 3,
            ContentMarginBottom = 3,
            BorderWidthBottom = 1,
            BorderWidthTop = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = new Color(0.2f, 0.25f, 0.32f, 0.45f)
        };
        _telemetryPill.AddThemeStyleboxOverride("panel", pillStyle);

        var innerHBox = new HBoxContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        innerHBox.AddThemeConstantOverride("separation", 8);
        _telemetryPill.AddChild(innerHBox);

        _fpsLabel = new Label
        {
            Name = "FpsLabel",
            Text = "FPS: --"
        };
        _fpsLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        _fpsLabel.AddThemeFontSizeOverride("font_size", 12);
        _fpsLabel.AddThemeColorOverride("font_color", new Color(0.35f, 0.85f, 0.45f));
        innerHBox.AddChild(_fpsLabel);

        var divider = new Label
        {
            Text = "•",
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        divider.AddThemeFontOverride("font", MenuStyle.Bold);
        divider.AddThemeFontSizeOverride("font_size", 11);
        divider.AddThemeColorOverride("font_color", new Color(0.45f, 0.5f, 0.58f, 0.7f));
        innerHBox.AddChild(divider);

        _pingLabel = new Label
        {
            Name = "PingLabel",
            Text = "PING: 0 ms"
        };
        _pingLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        _pingLabel.AddThemeFontSizeOverride("font_size", 12);
        _pingLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.82f, 1.0f));
        innerHBox.AddChild(_pingLabel);

        _topLeftContainer.AddChild(_telemetryPill);
        UpdateTelemetry();
    }

    private void BuildTopRightWeaponSection(Control root)
    {
        _topRightContainer = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(220, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _topRightContainer.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _topRightContainer.Position = new Vector2(-244, 20);
        _topRightContainer.AddThemeConstantOverride("separation", 4);
        root.AddChild(_topRightContainer);

        // Weapon silhouette icon row with subtle glow
        var iconRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End
        };
        iconRow.AddThemeConstantOverride("separation", 10);
        _topRightContainer.AddChild(iconRow);

        _weaponNameLabel = new Label
        {
            Text = "AK-47",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _weaponNameLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        _weaponNameLabel.AddThemeFontSizeOverride("font_size", 18);
        _weaponNameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.95f, 1.0f));
        iconRow.AddChild(_weaponNameLabel);

        _weaponIcon = new TextureRect
        {
            CustomMinimumSize = new Vector2(48, 28),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SelfModulate = new Color(0.4f, 0.85f, 1.0f, 0.9f)
        };
        iconRow.AddChild(_weaponIcon);

        // Large Magazine count stacked over Reserve Ammo
        var ammoBox = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End
        };
        ammoBox.AddThemeConstantOverride("separation", 0);
        _topRightContainer.AddChild(ammoBox);

        _magAmmoLabel = new Label
        {
            Text = "30",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _magAmmoLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        _magAmmoLabel.AddThemeFontSizeOverride("font_size", 42);
        _magAmmoLabel.AddThemeColorOverride("font_color", Colors.White);
        ammoBox.AddChild(_magAmmoLabel);

        _reserveAmmoLabel = new Label
        {
            Text = "RESERVE  90",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _reserveAmmoLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        _reserveAmmoLabel.AddThemeFontSizeOverride("font_size", 15);
        _reserveAmmoLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.72f, 0.8f));
        ammoBox.AddChild(_reserveAmmoLabel);

        // Secondary / melee slot icon indicator
        var slotRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        slotRow.AddThemeConstantOverride("separation", 6);
        _topRightContainer.AddChild(slotRow);

        _secondarySlotLabel = new Label
        {
            Text = "[ 2 ] SECONDARY",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _secondarySlotLabel.AddThemeFontOverride("font", MenuStyle.SemiBold);
        _secondarySlotLabel.AddThemeFontSizeOverride("font_size", 12);
        _secondarySlotLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.58f, 0.65f));
        slotRow.AddChild(_secondarySlotLabel);
    }

    private void BuildKillPillBadge(Control root)
    {
        var pillCenter = new CenterContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        pillCenter.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        pillCenter.Position = new Vector2(0, -90);
        root.AddChild(pillCenter);

        _killPillContainer = new PanelContainer
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var pillStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.1f, 0.04f, 0.04f, 0.9f),
            BorderColor = new Color(0.95f, 0.2f, 0.2f),
            BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14, CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14
        };
        _killPillContainer.AddThemeStyleboxOverride("panel", pillStyle);
        pillCenter.AddChild(_killPillContainer);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_top", 4);
        margin.AddThemeConstantOverride("margin_bottom", 4);
        _killPillContainer.AddChild(margin);

        _killPillLabel = new Label
        {
            Text = "1st KILL",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _killPillLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        _killPillLabel.AddThemeFontSizeOverride("font_size", 18);
        _killPillLabel.AddThemeColorOverride("font_color", new Color(1f, 0.35f, 0.35f));
        margin.AddChild(_killPillLabel);
    }

    private void BuildKillFeed(Control root)
    {
        bool touch = SettingsManager.Instance != null && SettingsManager.Instance.ShouldShowTouchControls();

        // Kill feed
        _killFeedList = new VBoxContainer { CustomMinimumSize = new Vector2(touch ? 260 : 420, 0) };
        _killFeedList.AddThemeConstantOverride("separation", 2);
        _killFeedList.SetAnchorsPreset(touch ? Control.LayoutPreset.TopLeft : Control.LayoutPreset.BottomLeft);
        _killFeedList.Position = touch ? new Vector2(24, 175) : new Vector2(24, -180);
        _killFeedList.MouseFilter = Control.MouseFilterEnum.Ignore;
        root.AddChild(_killFeedList);
    }

    private void BuildRespawningOverlay(Control root)
    {
        _respawningOverlay = new Control
        {
            Name = "RespawningOverlay",
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _respawningOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_respawningOverlay);

        // Dark vignette overlay
        var dimRect = new ColorRect
        {
            Color = new Color(0.08f, 0.02f, 0.02f, 0.85f)
        };
        dimRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _respawningOverlay.AddChild(dimRect);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _respawningOverlay.AddChild(center);

        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddThemeConstantOverride("separation", 12);
        center.AddChild(box);

        // Stark, static "RESPAWNING..." banner with zero numeric countdown
        _respawningBanner = new Label
        {
            Text = "RESPAWNING...",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _respawningBanner.AddThemeFontOverride("font", MenuStyle.Bold);
        _respawningBanner.AddThemeFontSizeOverride("font_size", 48);
        _respawningBanner.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.95f));
        _respawningBanner.AddThemeColorOverride("font_outline_color", new Color(0.75f, 0.15f, 0.15f));
        _respawningBanner.AddThemeConstantOverride("outline_size", 10);
        box.AddChild(_respawningBanner);

        _respawningSubText = new Label
        {
            Text = "STAND BY FOR DEPLOYMENT",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _respawningSubText.AddThemeFontOverride("font", MenuStyle.SemiBold);
        _respawningSubText.AddThemeFontSizeOverride("font_size", 18);
        _respawningSubText.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.8f));
        box.AddChild(_respawningSubText);
    }

    private void OnPlayerDied()
    {
        _respawningOverlay.Visible = true;
        _currentKillStreak = 0;
        _killPillTimer = 0.0f;
        _killPillContainer.Visible = false;

        bool convoy = MatchManager.Instance?.GameMode == MatchManager.Mode.ConvoyAssault;
        _respawningSubText.Text = convoy
            ? "CONVOY ASSAULT TACTICAL DELAY"
            : "STAND BY FOR DEPLOYMENT";
    }

    private void OnPlayerRespawned()
    {
        _respawningOverlay.Visible = false;
    }

    public void SpawnXpPopup(string text, Color? color = null)
    {
        Color popupColor = color ?? new Color(1.0f, 0.88f, 0.15f); // Vibrant yellow
        var popup = new FloatingXpPopup(text, popupColor);
        popup.Position = new Vector2(0, _activePopups.Count * 26.0f);
        _floatingXpContainer.AddChild(popup);
        _activePopups.Add(popup);
    }

    private void OnKillAnnounced(long killerPeerId, long victimPeerId, int styleFlags, int basePoints,
        int totalPoints, long assistPeerId, int assistPoints, bool struggles)
    {
        string line = KillFeed.DescribeKill(killerPeerId, victimPeerId, styleFlags, totalPoints);
        if (assistPeerId >= 0) line += $"  (assist {BotRoster.DisplayName(assistPeerId)} +{assistPoints})";
        AddFeedLine(line);

        long myPeerId = Multiplayer.GetUniqueId();
        if (killerPeerId == myPeerId)
        {
            // Awarded kill: trigger floating XP popups for each style bonus and base kill
            _currentKillStreak++;
            ShowKillStreakPill(_currentKillStreak);

            SpawnXpPopup($"+{basePoints} BASE ELIMINATION");

            KillStyle styles = (KillStyle)styleFlags;
            if ((styles & KillStyle.Headshot) != 0)
                SpawnXpPopup($"+{KillStyleBonus.HeadshotBonus} HEADSHOT", new Color(1.0f, 0.35f, 0.35f));
            if ((styles & KillStyle.HipFire) != 0)
                SpawnXpPopup($"+{KillStyleBonus.HipFireBonus} HIP-FIRE");
            if ((styles & KillStyle.NoScope360) != 0)
                SpawnXpPopup($"+{KillStyleBonus.NoScope360Bonus} 360 NO-SCOPE", new Color(0.35f, 0.85f, 1.0f));
            if ((styles & KillStyle.VergeOfDeath) != 0)
                SpawnXpPopup($"+{KillStyleBonus.VergeOfDeathBonus} ON VERGE OF DEATH", new Color(1.0f, 0.5f, 0.15f));
        }
        else if (assistPeerId == myPeerId)
        {
            SpawnXpPopup($"+{assistPoints} ASSIST", new Color(0.4f, 0.8f, 1.0f));
        }
    }

    private void ShowKillStreakPill(int streak)
    {
        _killPillContainer.Visible = true;
        _killPillTimer = 3.5f;
        string ordinal = streak switch
        {
            1 => "1st",
            2 => "2nd",
            3 => "3rd",
            _ => $"{streak}th"
        };
        _killPillLabel.Text = $"{ordinal} KILL";
    }

    private void OnMultiKillAwarded(int bonusPoints)
    {
        AddFeedLine($"MULTI-KILL  +{bonusPoints}");
        SpawnXpPopup($"+{bonusPoints} ONE-SHOT MULTI-KILL", new Color(1.0f, 0.75f, 0.2f));
    }

    private void OnMedalEarned(string label)
    {
        AddFeedLine($"MEDAL  {label}");
        SpawnXpPopup($"MEDAL: {label}", new Color(0.5f, 0.95f, 0.6f));
    }

    private void OnMatchEnded(int winner, string reason)
    {
        string reasonText = reason == "time" ? "Time Limit" : "Score Limit";
        bool teamMode = MatchManager.Instance?.IsTeamMode == true;
        string resultText = teamMode
            ? (winner < 0 ? "MATCH DRAW" : $"TEAM {MatchManager.TeamName(winner)} WINS")
            : (winner < 0 ? "MATCH DRAW" : $"{BotRoster.DisplayName(winner)} WINS");
        _matchOverLabel.Text = $"{resultText}\n({reasonText} Reached)";
        _matchOverLabel.Visible = true;

        Callable.From(() => ShowResults(winner, reason)).CallDeferred();
    }

    private void ShowResults(int winner, string reason)
    {
        if (!IsInsideTree() || GetNodeOrNull("MatchResults") != null) return;
        AddChild(new MatchResults
        {
            Name = "MatchResults",
            Player = (CharacterBody3D)GetParent(),
            WinningTeam = winner,
            Reason = reason,
        });
    }

    private void AddFeedLine(string text)
    {
        var line = new Label { Text = text };
        line.AddThemeFontOverride("font", MenuStyle.Medium);
        line.AddThemeFontSizeOverride("font_size", 14);
        _killFeedList.AddChild(line);
        _feedLines.Add((line, Time.GetTicksMsec() + (ulong)(KillFeed.MessageLifetimeSeconds * 1000.0f)));
        while (_feedLines.Count > MaxFeedLines) RemoveFeedLine(0);
    }

    private void RemoveFeedLine(int index)
    {
        Label line = _feedLines[index].Line;
        if (IsInstanceValid(line)) line.QueueFree();
        _feedLines.RemoveAt(index);
    }

    private void ExpireFeedLines()
    {
        ulong now = Time.GetTicksMsec();
        for (int i = _feedLines.Count - 1; i >= 0; i--)
            if (now >= _feedLines[i].ExpiryMsec) RemoveFeedLine(i);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Crosshair visibility: hide during ADS or when driving a vehicle
        if (_crosshairRoot != null)
        {
            var pm = GetParent() as PlayerMovement;
            bool hideCrosshair = pm != null && (pm.IsAiming || (pm.IsInVehicle && pm.IsVehicleDriver));
            _crosshairRoot.Visible = !hideCrosshair;
        }

        // Sniper Scope Overlay: show during ADS with scoped weapon
        if (_sniperScopeRoot != null)
        {
            var pm = GetParent() as PlayerMovement;
            bool isAiming = pm != null && pm.IsAiming;
            bool isScopedWeapon = _weapon != null && (_weapon.WeaponName.Equals("Sniper", StringComparison.OrdinalIgnoreCase) ||
                _weapon.CurrentWeaponData?.WeaponClass.Equals("Sniper", StringComparison.OrdinalIgnoreCase) == true);
            _sniperScopeRoot.Visible = isAiming && isScopedWeapon;
        }

        // Performance Telemetry: FPS & Ping update
        _telemetryTimer += dt;
        if (_telemetryTimer >= TelemetryInterval)
        {
            _telemetryTimer = 0.0f;
            UpdateTelemetry();
        }

        // Kill pill badge auto-hide timer
        if (_killPillTimer > 0.0f)
        {
            _killPillTimer -= dt;
            if (_killPillTimer <= 0.0f)
            {
                _killPillContainer.Visible = false;
            }
        }

        // Health update
        if (_health != null)
        {
            _respawningOverlay.Visible = _health.IsDead;
        }

        // Match Status & Timer update
        MatchManager? match = MatchManager.Instance;
        if (match != null)
        {
            float seconds = match.TimeRemainingSeconds >= 0 ? match.TimeRemainingSeconds : match.ElapsedSeconds;
            int remaining = Mathf.Max(0, Mathf.RoundToInt(seconds));
            int mins = remaining / 60;
            int secs = remaining % 60;
            if (match.ActiveController is PureChaseController chase && chase.IsOmenWarningActive)
            {
                _matchTimerLabel.Text = $"⚠ OMEN {chase.CurrentEvent}: {Mathf.CeilToInt(chase.PhaseTimer)}s ⚠";
                _matchTimerLabel.SelfModulate = new Color(1.0f, 0.35f, 0.2f);
            }
            else if (match.ActiveController is PureChaseController activeChase && activeChase.IsCursedEventActive)
            {
                _matchTimerLabel.Text = $"💀 {activeChase.CurrentEvent} ({Mathf.CeilToInt(activeChase.PhaseTimer)}s) 💀";
                _matchTimerLabel.SelfModulate = new Color(0.85f, 0.45f, 1.0f);
            }
            else
            {
                _matchTimerLabel.Text = $"{mins:D2}:{secs:D2}";
                _matchTimerLabel.SelfModulate = Colors.White;
            }

            if (match.IsTeamMode)
            {
                int score0 = match.GetTeamScore(0);
                int score1 = match.GetTeamScore(1);
                if (match.ActiveController is ConvoyAssaultController convoy)
                {
                    _blueTeamLabel.Text = $"{MatchManager.TeamName(0).ToUpperInvariant()}  {score0}  [{convoy.Team0RoundWins}W]";
                    _redTeamLabel.Text = $"{MatchManager.TeamName(1).ToUpperInvariant()}  {score1}  [{convoy.Team1RoundWins}W]";
                }
                else
                {
                    _blueTeamLabel.Text = $"{MatchManager.TeamName(0).ToUpperInvariant()}  {score0}";
                    _redTeamLabel.Text = $"{MatchManager.TeamName(1).ToUpperInvariant()}  {score1}";
                }
                _blueTeamBar.MaxValue = Mathf.Max(50, Mathf.Max(score0, score1) + 10);
                _redTeamBar.MaxValue = _blueTeamBar.MaxValue;
                _blueTeamBar.Value = score0;
                _redTeamBar.Value = score1;
            }
            else
            {
                int totalPoints = _score?.TotalPoints ?? 0;
                int kills = _score?.Kills ?? 0;
                _blueTeamLabel.Text = $"SCORE  {totalPoints}";
                _redTeamLabel.Text = $"KILLS  {kills}";
                _blueTeamBar.MaxValue = Mathf.Max(1000, totalPoints + 200);
                _redTeamBar.MaxValue = Mathf.Max(30, kills + 5);
                _blueTeamBar.Value = totalPoints;
                _redTeamBar.Value = kills;
            }
        }

        // Mode shield logo tint based on player's team
        if (_shieldIcon != null && MatchManager.Instance != null)
        {
            int myTeam = MatchManager.Instance.GetTeam(Multiplayer.GetUniqueId());
            if (myTeam >= 0 && myTeam < MatchManager.TeamColors.Length)
                _shieldIcon.SelfModulate = MatchManager.TeamColors[myTeam];
            else
                _shieldIcon.SelfModulate = new Color(0.85f, 0.9f, 1.0f);
        }

        // Weapon & Ammo readout
        if (_weapon != null)
        {
            string weaponName = _weapon.WeaponName;
            _weaponNameLabel.Text = string.IsNullOrEmpty(weaponName) ? "UNARMED" : weaponName;

            WeaponData? data = _weapon.CurrentWeaponData;
            if (data != null)
            {
                string iconPath = $"res://art/ui/icons/{OperatorInfo.WeaponIcon(data)}.svg";
                if (_weaponIcon.Texture == null || _weaponIcon.Texture.ResourcePath != iconPath)
                {
                    _weaponIcon.Texture = ResourceLoader.Load<Texture2D>(iconPath);
                }
            }

            if (_weapon.IsReloading)
            {
                _magAmmoLabel.Text = "--";
                _reserveAmmoLabel.Text = "RELOADING...";
                _reserveAmmoLabel.AddThemeColorOverride("font_color", new Color(1f, 0.7f, 0.2f));
            }
            else if (data != null && (data.FireMode == WeaponData.FireModeType.Melee || data.MagazineSize <= 0 || data.WeaponClass == "Melee"))
            {
                _magAmmoLabel.Text = "—";
                _reserveAmmoLabel.Text = "MELEE";
                _reserveAmmoLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.72f, 0.8f));
            }
            else
            {
                _magAmmoLabel.Text = $"{_weapon.CurrentAmmo}";
                bool isSidearm = data?.WeaponClass == "Sidearm" || data?.WeaponClass == "Pistol";
                _reserveAmmoLabel.Text = isSidearm ? "RESERVE  ∞" : $"RESERVE  {_weapon.CurrentReserve}";
                _reserveAmmoLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.72f, 0.8f));
            }

            // Dynamic secondary / carried slot indicator
            int eq = _weapon.EquippedIndex;
            int p1 = _weapon.CarriedIndexFor(WeaponSlot.Primary1);
            int p2 = _weapon.CarriedIndexFor(WeaponSlot.Primary2);
            int pistol = _weapon.CarriedIndexFor(WeaponSlot.Pistol);

            if (eq == p1)
            {
                var alt = p2 >= 0 ? _weapon.GetWeaponData(p2) : (pistol >= 0 ? _weapon.GetWeaponData(pistol) : null);
                _secondarySlotLabel.Text = alt != null ? $"[ 2 ] {alt.WeaponName.ToUpperInvariant()}" : "[ 2 ] SECONDARY";
            }
            else if (eq == p2)
            {
                var prim = p1 >= 0 ? _weapon.GetWeaponData(p1) : null;
                _secondarySlotLabel.Text = prim != null ? $"[ 1 ] {prim.WeaponName.ToUpperInvariant()}" : "[ 1 ] PRIMARY";
            }
            else
            {
                var prim = p1 >= 0 ? _weapon.GetWeaponData(p1) : null;
                _secondarySlotLabel.Text = prim != null ? $"[ 1 ] {prim.WeaponName.ToUpperInvariant()}" : "[ 1 ] PRIMARY";
            }
        }

        // Process active floating XP popups
        for (int i = _activePopups.Count - 1; i >= 0; i--)
        {
            var popup = _activePopups[i];
            if (!popup.Update(dt))
            {
                _activePopups.RemoveAt(i);
                popup.QueueFree();
            }
        }

        ExpireFeedLines();
    }



    public void SetPrompt(string? text)
    {
        if (_promptLabel == null) return;
        _promptLabel.Text = text ?? string.Empty;
        _promptLabel.Visible = !string.IsNullOrEmpty(text);
    }

    private void UpdateTelemetry()
    {
        if (_telemetryPill == null || !IsInstanceValid(_telemetryPill)) return;

        bool showOverlay = SettingsManager.Instance?.ShowPerformanceOverlay ?? true;
        _telemetryPill.Visible = showOverlay;
        if (!showOverlay) return;

        int fps = Mathf.Max(0, Mathf.RoundToInt((float)Engine.GetFramesPerSecond()));
        _fpsLabel.Text = $"FPS: {fps}";

        if (fps >= 60)
            _fpsLabel.AddThemeColorOverride("font_color", new Color(0.35f, 0.85f, 0.45f));
        else if (fps >= 30)
            _fpsLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.84f, 0.2f));
        else
            _fpsLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.3f, 0.25f));

        int pingMs = GetNetworkPingMs();
        _pingLabel.Text = $"PING: {pingMs} ms";

        if (pingMs <= 50)
            _pingLabel.AddThemeColorOverride("font_color", new Color(0.4f, 0.82f, 1.0f));
        else if (pingMs <= 100)
            _pingLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.84f, 0.2f));
        else
            _pingLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.3f, 0.25f));
    }

    private int GetNetworkPingMs()
    {
        if (Multiplayer.MultiplayerPeer is ENetMultiplayerPeer enet)
        {
            if (Multiplayer.IsServer())
            {
                var peers = Multiplayer.GetPeers();
                if (peers.Length == 0) return 0;

                double totalRtt = 0;
                int validPeers = 0;
                foreach (int peerId in peers)
                {
                    var p = enet.GetPeer(peerId);
                    if (p != null)
                    {
                        totalRtt += p.GetStatistic(ENetPacketPeer.PeerStatistic.RoundTripTime);
                        validPeers++;
                    }
                }
                return validPeers > 0 ? Mathf.RoundToInt((float)(totalRtt / validPeers)) : 0;
            }
            else
            {
                var serverPeer = enet.GetPeer(1);
                if (serverPeer != null)
                {
                    double rtt = serverPeer.GetStatistic(ENetPacketPeer.PeerStatistic.RoundTripTime);
                    return Mathf.Max(0, Mathf.RoundToInt((float)rtt));
                }
            }
        }
        return 0;
    }

    private void BuildPrompt()
    {
        var bottom = new VBoxContainer
        {
            AnchorLeft = 0.0f, AnchorRight = 1.0f, AnchorTop = 1.0f, AnchorBottom = 1.0f,
            OffsetTop = -230.0f, OffsetBottom = -150.0f,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _promptLabel = new Label { Visible = false, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        _promptLabel.AddThemeFontOverride("font", MenuStyle.Bold);
        _promptLabel.AddThemeFontSizeOverride("font_size", 30);
        _promptLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.93f, 0.9f));
        _promptLabel.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
        _promptLabel.AddThemeConstantOverride("outline_size", 8);
        bottom.AddChild(_promptLabel);
        AddChild(bottom);
    }

    /// <summary>
    /// Custom 4-tick reticle canvas item.
    /// </summary>
    private partial class CrosshairDrawer : Control
    {
        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            Vector2 c = Size * 0.5f;
            Color shadow = new Color(0, 0, 0, 0.8f);
            Color reticle = new Color(0.95f, 0.98f, 1.0f, 0.92f);
            float gap = 5.0f;
            float length = 11.0f;
            float width = 2.0f;

            // Draw shadow outline
            DrawLine(c + new Vector2(0, -gap), c + new Vector2(0, -(gap + length)), shadow, width + 2);
            DrawLine(c + new Vector2(0, gap), c + new Vector2(0, gap + length), shadow, width + 2);
            DrawLine(c + new Vector2(-gap, 0), c + new Vector2(-(gap + length), 0), shadow, width + 2);
            DrawLine(c + new Vector2(gap, 0), c + new Vector2(gap + length, 0), shadow, width + 2);

            // Draw crisp lines
            DrawLine(c + new Vector2(0, -gap), c + new Vector2(0, -(gap + length)), reticle, width);
            DrawLine(c + new Vector2(0, gap), c + new Vector2(0, gap + length), reticle, width);
            DrawLine(c + new Vector2(-gap, 0), c + new Vector2(-(gap + length), 0), reticle, width);
            DrawLine(c + new Vector2(gap, 0), c + new Vector2(gap + length, 0), reticle, width);

            // Center gap dot
            DrawCircle(c, 1.2f, reticle);
        }
    }

    /// <summary>
    /// Tactical sniper optic scope overlay with outer vignette and mil-dot reticle.
    /// </summary>
    private partial class SniperScopeDrawer : Control
    {
        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
        }

        public override void _Draw()
        {
            Vector2 c = Size * 0.5f;
            float minDim = Mathf.Min(Size.X, Size.Y);
            float radius = minDim * 0.44f;

            Color black = new Color(0.015f, 0.015f, 0.02f, 1.0f);
            Color darkGlass = new Color(0.02f, 0.05f, 0.04f, 0.12f);
            Color reticleColor = new Color(0.06f, 0.06f, 0.06f, 0.95f);
            Color illumCenter = new Color(0.95f, 0.15f, 0.15f, 0.92f);

            // 1. Fullscreen dark mask around the circular scope aperture
            DrawRect(new Rect2(0, 0, Size.X, c.Y - radius), black, true);
            DrawRect(new Rect2(0, c.Y + radius, Size.X, Size.Y - (c.Y + radius)), black, true);
            DrawRect(new Rect2(0, c.Y - radius, c.X - radius, radius * 2.0f), black, true);
            DrawRect(new Rect2(c.X + radius, c.Y - radius, Size.X - (c.X + radius), radius * 2.0f), black, true);

            // Heavy scope bezel border
            DrawArc(c, radius, 0, Mathf.Tau, 64, black, 20.0f);
            DrawArc(c, radius + 10.0f, 0, Mathf.Tau, 64, black, 10.0f);
            DrawArc(c, radius - 2.0f, 0, Mathf.Tau, 64, new Color(0.12f, 0.12f, 0.14f, 0.85f), 2.5f);

            // Subtle lens tint
            DrawCircle(c, radius - 3.0f, darkGlass);

            // 2. High-precision tactical mil-dot crosshair lines
            float lineExt = radius * 0.95f;
            float thickDist = radius * 0.45f;
            float hairWidth = 2.0f;
            float postWidth = 5.0f;

            // Thick outer posts
            DrawLine(c + new Vector2(0, -lineExt), c + new Vector2(0, -thickDist), reticleColor, postWidth);
            DrawLine(c + new Vector2(0, thickDist), c + new Vector2(0, lineExt), reticleColor, postWidth);
            DrawLine(c + new Vector2(-lineExt, 0), c + new Vector2(-thickDist, 0), reticleColor, postWidth);
            DrawLine(c + new Vector2(thickDist, 0), c + new Vector2(lineExt, 0), reticleColor, postWidth);

            // Fine central crosshair
            DrawLine(c + new Vector2(0, -thickDist), c + new Vector2(0, thickDist), reticleColor, hairWidth);
            DrawLine(c + new Vector2(-thickDist, 0), c + new Vector2(thickDist, 0), reticleColor, hairWidth);

            // Tactical hash marks / mil-dots along crosshair
            float hashSpacing = radius * 0.075f;
            for (int i = 1; i <= 4; i++)
            {
                float d = i * hashSpacing;
                float markLen = (i % 2 == 0) ? 7.0f : 4.5f;
                // Horizontal markings
                DrawLine(c + new Vector2(-d, -markLen), c + new Vector2(-d, markLen), reticleColor, 1.5f);
                DrawLine(c + new Vector2(d, -markLen), c + new Vector2(d, markLen), reticleColor, 1.5f);
                // Vertical markings
                DrawLine(c + new Vector2(-markLen, -d), c + new Vector2(markLen, -d), reticleColor, 1.5f);
                DrawLine(c + new Vector2(-markLen, d), c + new Vector2(markLen, d), reticleColor, 1.5f);
            }

            // Illuminated center red dot
            DrawCircle(c, 2.5f, illumCenter);
        }
    }

    /// <summary>
    /// Floating yellow +XP pop-up item.
    /// </summary>
    private partial class FloatingXpPopup : Label
    {
        private float _lifetime = 1.8f;
        private readonly Color _baseColor;

        public FloatingXpPopup(string text, Color color)
        {
            Text = text;
            _baseColor = color;
            MouseFilter = MouseFilterEnum.Ignore;
            AddThemeFontOverride("font", MenuStyle.Bold);
            AddThemeFontSizeOverride("font_size", 18);
            AddThemeColorOverride("font_color", color);
            AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.95f));
            AddThemeConstantOverride("outline_size", 6);
            Name = "FloatingXpPopup";
        }

        public bool Update(float dt)
        {
            _lifetime -= dt;
            if (_lifetime <= 0.0f) return false;

            Position += new Vector2(0, -32.0f * dt);
            float alpha = Mathf.Clamp(_lifetime / 1.0f, 0.0f, 1.0f);
            Modulate = new Color(1, 1, 1, alpha);
            return true;
        }
    }
}
