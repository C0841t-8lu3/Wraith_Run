using System;
using System.Collections.Generic;
using System.Text;
using Godot;

/// <summary>
/// The tablet: the hub's one menu. Press Esc (or the MENU button on a phone) and the player lifts
/// the tablet; there is no full-screen menu any more. Sections:
///   PLAY      not in a lobby: HOST / JOIN. In a lobby: the lobby (host: map, bots, START GAME; others: READY).
///   OPERATOR  pick and customise the operator (the same screens the old main menu used).
///   STATUS    who is in the room, which side they are on, who is ready.
///   CHAT      the lobby text chat (your team only in team modes).
///   SETTINGS  the normal settings panel.
/// Weapons are chosen at the weapon wall in the room, not here.
///
/// A small chat feed stays on screen while the tablet is down; Enter opens the message line.
/// </summary>
public partial class HubTablet : CanvasLayer
{
    public static HubTablet? Instance { get; private set; }

    private enum Section { Home, Play, Loadout, Operator, Status, Chat, Settings }
    private enum PlayStep { Home, Host, Join, Solo }

    private const int MaxFeedLines = 7;

    private Control _root = null!;
    private HBoxContainer _topBar = null!;
    private Control? _feedRoot;
    private Control _content = null!;
    private TabletDevice? _device;
    private bool _pointerInside;
    private Vector2 _lastPixel;
    private HubController? _hub;
    private SettingsPanel? _settingsPanel;
    private bool _confirmLeave;
    private bool _connectionLost;

    /// <summary>Set before the tablet enters the tree to make it the in-game menu of this player (a match or
    /// a match) rather than the hub's: same device, same look, but the pages are RESUME / LEAVE,
    /// OPERATOR and SETTINGS, and PauseMenu decides when it opens and closes.</summary>
    public CharacterBody3D? GamePlayer { get; set; }
    private bool InGame => GamePlayer != null;
    private bool InSession => _hub != null && _hub.InSession;
    private string StatusLine => _hub?.StatusLine ?? string.Empty;
    public bool IsOpen => _open;
    public TabletDevice? Device => _device;
    public Control? Content => _content;
    public Control? Root => _root;
    /// <summary>A key row in SETTINGS > CONTROLS is waiting for a key (Escape then means cancel the rebind).</summary>
    public bool IsCapturingKey => _settingsPanel != null && IsInstanceValid(_settingsPanel) && _settingsPanel.IsInsideTree() && _settingsPanel.IsCapturingKey;

    private Section _section = Section.Home;
    private PlayStep _playStep = PlayStep.Home;
    private bool _open;
    private bool _leaving;
    private string? _operatorPreview;
    private long _activeMenuPeerId = -1;

    // chat
    private readonly List<(string Sender, int Team, string Text)> _log = new();
    private Label? _feed;
    private LineEdit? _quickChat;
    private bool _typing;
    private RichTextLabel? _chatLog;

    // host settings picked before the lobby exists
    private string _gameName = "Wraith Run";

    // join screen
    private PacketPeerUdp? _listener;
    private readonly Dictionary<string, (string Name, string Host, int Port, int Players, string Map, string Mode, ulong Seen)> _games = new();
    private VBoxContainer? _gamesList;
    private Label? _joinStatus;
    private double _pollTimer;
    private string _manualAddress = string.Empty;

    // live refresh of lobby / status screens
    private readonly Dictionary<long, bool> _lastReady = new();
    private double _liveTimer;
    private bool _lobbyDirty;
    private string _liveSignature = string.Empty;

    // keeps a page's scroll position when the same page is rebuilt (live lobby refresh, picking a choice)
    private ScrollContainer? _scroll;
    private string _scrollKey = string.Empty;
    private int _pendingScroll;

    public override void _Ready()
    {
        Instance = this;
        Layer = 30;
        ProcessMode = ProcessModeEnum.Always;
        BuildShell();
        if (!InGame)
        {
            _hub = HubController.Instance;
            BuildChatFeed();
            if (_hub != null)
            {
                _hub.ChatReceived += OnChat;
                _hub.RosterChanged += () => _lobbyDirty = true;
            }
            if (MatchManager.Instance != null) MatchManager.Instance.LobbyChanged += OnLobbyChanged;
        }
        AttachTabletModel();
    }

    public override void _ExitTree()
    {
        _leaving = true;
        if (Instance == this) Instance = null;
        StopListening();
        if (_open) GameUiState.Reset();
        if (MatchManager.Instance != null) MatchManager.Instance.LobbyChanged -= OnLobbyChanged;
    }

    // -------------------------------------------------------------------------------------------
    // Open / close
    // -------------------------------------------------------------------------------------------

    public void Toggle()
    {
        if (_open) Close(); else Open();
    }

    private void Open()
    {
        if (_open || GameUiState.ResultsOpen) return;
        Camera3D? camera = LocalCamera();
        if (GetInputScreen() == null || camera == null) return; // the player or the tablet viewport is not in the room yet
        if (_typing) EndQuickChat(send: false);
        _open = true;
        _device.Fit(camera);
        GameUiState.SetMenuOpen(true);
        if (GamePlayer != null)
        {
            // a finger still on the joystick or FIRE would otherwise stay "pressed" for good; the scoreboard must not sit under the tablet
            GamePlayer.GetNodeOrNull<TouchControls>("TouchControls")?.ReleaseAllHeld();
            GamePlayer.GetNodeOrNull<Scoreboard>("Hud/Scoreboard")?.HideNow();
        }
        if (!OS.HasFeature("mobile")) Input.MouseMode = Input.MouseModeEnum.Visible;
        _device.Raise();
        SetOverlays(true);
        _root.Visible = true;
        ShowSection(_section);
    }

    private void Close()
    {
        if (!_open || _connectionLost) return; // with the host gone the only way out is LEAVE
        _confirmLeave = false;
        _activeMenuPeerId = -1;
        _open = false;
        StopListening();
        GameUiState.SetMenuOpen(false);
        _root.Visible = false;
        if (_device != null && IsInstanceValid(_device)) _device.Lower();
        SetOverlays(false);
        _pointerInside = false;
        if (!OS.HasFeature("mobile")) Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Input(InputEvent @event)
    {
        // Leaving for another scene (a match, the hub): the tablet is done and its screen may already be gone.
        if (_leaving || !IsInsideTree()) return;
        // While the tablet is up, pointer and keyboard go to ITS screen, through the glass - except while the
        // quit question is showing: its buttons are on the main screen and need the pointer themselves.
        bool quitUp = _quitConfirm != null && _quitConfirm.Visible;
        if (_open && _device != null && !quitUp)
        {
            if (@event is InputEventKey hot && hot.Pressed && !hot.Echo && !TypingOnScreen())
            {
                if (hot.PhysicalKeycode == Key.Equal || hot.Keycode == Key.Equal) { MarkInputHandled(); Close(); return; }
                if (hot.PhysicalKeycode == Key.Q) { MarkInputHandled(); CycleTab(-1); return; }
                if (hot.PhysicalKeycode == Key.E) { MarkInputHandled(); CycleTab(1); return; }
                if (hot.PhysicalKeycode == Key.Space) { MarkInputHandled(); PrimaryAction(); return; }
            }
            // Escape follows the same route as the on-screen BACK button. A controls rebind owns
            // Escape while it is listening, so leave that event for SettingsPanel.
            bool back = !IsCapturingKey && (@event.IsActionPressed(GameUiState.PauseAction) || @event.IsActionPressed("ui_cancel"));
            if (back) { MarkInputHandled(); BackPressed(); return; }
            if (ForwardToScreen(@event))
            {
                MarkInputHandled();
                return;
            }
        }
        // In a match or the range PauseMenu opens and closes the tablet; the hub keys below are the hub's.
        if (InGame) return;
        if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (_typing)
            {
                if (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter) { MarkInputHandled(); EndQuickChat(send: true); }
                else if (key.Keycode == Key.Escape) { MarkInputHandled(); EndQuickChat(send: false); }
                return;
            }
            if (!_open && (key.Keycode == Key.Enter || key.Keycode == Key.KpEnter))
            {
                MarkInputHandled();
                BeginQuickChat();
                return;
            }
            if (!_open && (key.PhysicalKeycode == Key.Equal || key.Keycode == Key.Equal) && !TypingOnScreen())
            {
                MarkInputHandled();
                Open();
                return;
            }
        }
        // Escape owns the tablet everywhere: open it from the hub, then follow the visible BACK
        // button while it is raised. Tab remains an optional shortcut for opening and closing it.
        bool escape = @event.IsActionPressed(GameUiState.PauseAction);
        if (escape)
        {
            MarkInputHandled();
            if (_quitConfirm != null && _quitConfirm.Visible) HideQuitConfirm();
            else if (!_open) Open();
            return;
        }
        if (_quitConfirm != null && _quitConfirm.Visible)
        {
            if (@event.IsActionPressed("ui_cancel")) { MarkInputHandled(); HideQuitConfirm(); }
            return;
        }
        bool tab = @event.IsActionPressed(GameUiState.ScoreboardAction) && !@event.IsEcho();
        bool backRequested = _open && @event.IsActionPressed("ui_cancel");
        if (!tab && !backRequested) return;
        if (GameUiState.ResultsOpen) return;
        MarkInputHandled();
        // BUG FIX: this used to call BackPressed() unconditionally, which can only navigate back or
        // Close() - so with the tablet already closed, pressing Tab did nothing (Close() on an
        // unopened tablet is a no-op). That silently broke the "Tab remains an optional shortcut for
        // opening and closing it" promise above the escape branch, leaving Escape as the only key
        // that actually opened the tablet. Tab now opens it from closed, same as Escape.
        if (tab && !_open) Open();
        else BackPressed();
    }

    public override void _Notification(int what)
    {
        if (what == (int)Node.NotificationWMGoBackRequest && !GameUiState.ResultsOpen && !InGame) Toggle();
    }

    public override void _Process(double delta)
    {
        if (_leaving) return;
        if (_open && _section == Section.Play && _playStep == PlayStep.Join) PollDiscovery(delta);

        _liveTimer += delta;
        if (_liveTimer < 0.5) return;
        _liveTimer = 0;
        if (!_open || !Multiplayer.HasMultiplayerPeer()) return;
        if (!(_lobbyDirty || _section == Section.Status || _section == Section.Home || (_section == Section.Play && InSession))) return;

        // Rebuild only when something this page shows has actually changed. Rebuilding on a timer threw
        // the reader back to the top of the page and could swallow a click that landed mid-rebuild.
        _lobbyDirty = false;
        string signature = LiveSignature();
        if (signature == _liveSignature) return;
        _liveSignature = signature;
        if (!RefreshLiveSection()) BuildTopBar();
    }

    /// <summary>Everything the live pages (home, lobby, players) show, as one string, so a refresh can tell
    /// whether anything changed.</summary>
    private string LiveSignature()
    {
        if (!Multiplayer.HasMultiplayerPeer()) return string.Empty;
        var sb = new StringBuilder();
        sb.Append(InSession).Append('|').Append(StatusLine).Append('|').Append(PlayerDirectory.Version).Append('|');
        sb.Append(Multiplayer.GetPeers().Length).Append('|').Append(_activeMenuPeerId).Append('|');
        MatchManager? match = MatchManager.Instance;
        if (match == null) return sb.ToString();
        sb.Append((int)match.GameMode).Append('|').Append((int)match.SelectedMap).Append('|').Append(match.BotCount).Append('|');
        sb.Append(match.TimeLimitMinutes).Append('|').Append(match.FriendlyFire).Append('|').Append(match.TeamIndicators).Append('|').Append((int)match.BotLevel).Append('|');
        sb.Append(match.CanHostStartMatch).Append('|').Append(match.LocalPlayerReady).Append('|');
        foreach (long id in match.LobbyPeerIds())
            sb.Append(id).Append(':').Append(match.IsPeerReady(id)).Append(':').Append(match.GetTeam(id)).Append(':')
              .Append(GameUiState.PlayerName(id)).Append(';');
        return sb.ToString();
    }

    private void OnLobbyChanged()
    {
        // Live notification feed: tell everybody when somebody flips READY.
        MatchManager? match = MatchManager.Instance;
        if (match == null) return;
        foreach (long id in match.LobbyPeerIds())
        {
            bool ready = match.IsPeerReady(id);
            if (id != 1 && _lastReady.TryGetValue(id, out bool before) && before != ready)
                AddLog("", -1, $"{GameUiState.PlayerName(id)} is {(ready ? "READY" : "NOT READY")}");
            _lastReady[id] = ready;
        }
        _lobbyDirty = true;
    }

    /// <summary>Rebuilds the current page if it is one of the live ones. Returns whether it did.</summary>
    private bool RefreshLiveSection()
    {
        // Only the cheap-to-rebuild screens are rebuilt live.
        if (_section == Section.Status || _section == Section.Home || (_section == Section.Play && InSession))
        {
            ShowSection(_section);
            return true;
        }
        return false;
    }

    // -------------------------------------------------------------------------------------------
    // Shell
    // -------------------------------------------------------------------------------------------

    private void BuildShell()
    {
        // The menu is drawn into the tablet's own screen (a fixed-size viewport), so everything here is
        // laid out for that screen - not for the game window. Layout: one top bar (back, PLAY, tabs,
        // status chips) and the whole rest of the glass for the page.
        _device = new TabletDevice();
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        MenuStyle.Fill(_root);
        _device.Screen.AddChild(_root);

        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.085f, 0.022f, 0.025f));
        gradient.SetColor(1, new Color(0.012f, 0.014f, 0.018f));
        var backdrop = new TextureRect
        {
            Texture = new GradientTexture2D { Gradient = gradient, FillFrom = new Vector2(0.15f, 0.0f), FillTo = new Vector2(0.55f, 1.0f), Width = 256, Height = 256 },
            StretchMode = TextureRect.StretchModeEnum.Scale,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        MenuStyle.Fill(backdrop);
        _root.AddChild(backdrop);

        var page = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        MenuStyle.Fill(page);
        page.OffsetLeft = 26; page.OffsetRight = -26; page.OffsetTop = 18; page.OffsetBottom = -22;
        page.AddThemeConstantOverride("separation", 14);
        _root.AddChild(page);

        _topBar = new HBoxContainer { CustomMinimumSize = new Vector2(0, 52) };
        _topBar.AddThemeConstantOverride("separation", 14);
        page.AddChild(_topBar);

        _content = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill, ClipContents = true };
        page.AddChild(_content);
    }

    private const string CameraPath = "Head/CameraShake/FirstPersonCamera";

    private int LocalPeerId() => Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 1;

    private Camera3D? LocalCamera()
    {
        if (GamePlayer != null)
            return IsInstanceValid(GamePlayer) ? GamePlayer.GetNodeOrNull<Camera3D>(CameraPath) : null;
        return GetTree()?.CurrentScene?.GetNodeOrNull<Camera3D>($"Players/Player_{LocalPeerId()}/{CameraPath}");
    }

    /// <summary>Hang the tablet on the local player's camera as soon as that camera exists.</summary>
    private void AttachTabletModel() => CallDeferred(nameof(MountDevice));

    private void MountDevice()
    {
        if (_device == null || !IsInstanceValid(_device)) return;
        Camera3D? camera = LocalCamera();
        if (camera == null)
        {
            // the hub spawns the player a moment after this scene is ready; try again shortly
            GetTree().CreateTimer(0.4).Timeout += MountDevice;
            return;
        }
        if (_device.GetParent() == null) camera.AddChild(_device);
    }

    private SubViewport? GetInputScreen()
    {
        TabletDevice? device = _device;
        if (device == null || !IsInstanceValid(device) || !device.IsInsideTree()) return null;
        SubViewport screen = device.Screen;
        return IsInstanceValid(screen) && screen.IsInsideTree() ? screen : null;
    }

    /// <summary>Input can finish dispatching while a scene transition removes this tablet or its
    /// viewport. Only mark the event handled while the owning viewport is still alive.</summary>
    private void MarkInputHandled()
    {
        if (!IsInsideTree()) return;
        Viewport? viewport = GetViewport();
        if (viewport != null && IsInstanceValid(viewport) && viewport.IsInsideTree())
            viewport.SetInputAsHandled();
    }

    public bool TypingOnScreen()
    {
        if (GetInputScreen()?.GuiGetFocusOwner() is LineEdit or TextEdit) return true;
        if (IsInsideTree() && GetViewport()?.GuiGetFocusOwner() is LineEdit or TextEdit) return true;
        return false;
    }

    private void SetOverlays(bool tabletUp)
    {
        if (_feedRoot != null) _feedRoot.Visible = !tabletUp;
        CanvasLayer? hud = GamePlayer != null
            ? (IsInstanceValid(GamePlayer) ? GamePlayer.GetNodeOrNull<CanvasLayer>("Hud") : null)
            : GetTree()?.CurrentScene?.GetNodeOrNull<CanvasLayer>($"Players/Player_{LocalPeerId()}/Hud");
        if (hud != null) hud.Visible = !tabletUp;
    }

    private bool ForwardToScreen(InputEvent e)
    {
        SubViewport? screen = GetInputScreen();
        // The tablet is mounted onto the player's camera asynchronously. Input can arrive during
        // that gap (or while a scene is being torn down), so never PushInput into an unattached
        // SubViewport; Godot reports errors and can throw from that call when it is outside the tree.
        if (screen == null || !screen.IsInsideTree()) return true;
        switch (e)
        {
            case InputEventMouseMotion or InputEventMouseButton:
            {
                Camera3D? camera = LocalCamera();
                if (camera == null) return true;
                Vector2 window = e is InputEventMouseMotion motion ? motion.Position : ((InputEventMouseButton)e).Position;
                if (!_device.ScreenPoint(camera, window, out Vector2 pixel))
                {
                    if (_pointerInside)
                    {
                        _pointerInside = false; // let go of any hover on the screen
                        screen.PushInput(new InputEventMouseMotion { Position = new Vector2(-300, -300), GlobalPosition = new Vector2(-300, -300) }, true);
                    }
                    return true;
                }
                _pointerInside = true;
                var copy = (InputEventMouse)e.Duplicate();
                copy.Position = pixel;
                copy.GlobalPosition = pixel;
                if (copy is InputEventMouseMotion moved) moved.Relative = pixel - _lastPixel;
                _lastPixel = pixel;
                screen.PushInput(copy, true);
                return true;
            }
            case InputEventKey:
                screen.PushInput(e, true);
                return true;
        }
        return false;
    }

    private static readonly Section[] TabOrder = { Section.Play, Section.Loadout, Section.Operator, Section.Settings };
    private static readonly Section[] GameTabOrder = { Section.Play, Section.Loadout, Section.Operator, Section.Settings };

    private static Button Sized(Button button, float width)
    {
        button.CustomMinimumSize = new Vector2(width, button.CustomMinimumSize.Y);
        return button;
    }

    private static Control KeyCap(string key) => MenuStyle.Chip(key, new Color(0.95f, 0.93f, 0.93f), 12, true);

    private Button Tab(string label, Section section)
    {
        bool active = _section == section;
        var button = new Button { Text = label, Flat = true, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 46) };
        button.AddThemeFontOverride("font", MenuStyle.Bold);
        button.AddThemeFontSizeOverride("font_size", 21);
        foreach (string color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
            button.AddThemeColorOverride(color, active ? MenuStyle.TextMain : (color == "font_color" ? MenuStyle.TextDim : MenuStyle.TextMain));
        var underline = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), BorderColor = MenuStyle.Accent, BorderWidthBottom = active ? 4 : 0, ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginBottom = 4 };
        foreach (string box in new[] { "normal", "hover", "pressed", "focus" })
            button.AddThemeStyleboxOverride(box, underline);
        button.Pressed += () => { _playStep = PlayStep.Home; ShowSection(section); };
        return button;
    }

    private string PrimaryLabel()
    {
        if (InGame) return "RESUME";
        if (!InSession) return "PLAY";
        if (Multiplayer.IsServer()) return "START";
        return MatchManager.Instance.LocalPlayerReady ? "UNREADY" : "READY";
    }

    /// <summary>The big red button: PLAY before there is a lobby, then START GAME (host) or READY (others).</summary>
    private void PrimaryAction()
    {
        if (InGame) { Close(); return; }
        MatchManager match = MatchManager.Instance;
        if (!InSession) { _playStep = PlayStep.Solo; ShowSection(Section.Play); return; }
        if (Multiplayer.IsServer())
        {
            if (match.CanHostStartMatch) _hub?.StartMatch();
            else { _playStep = PlayStep.Home; ShowSection(Section.Play); } // the lobby page says what is missing
            return;
        }
        match.SetLocalReady(!match.LocalPlayerReady);
        CallDeferred(nameof(RebuildTopBar));
    }

    private void RebuildTopBar() { if (_open) BuildTopBar(); }

    private void BackPressed()
    {
        if (!NavigateBack()) Close();
    }

    /// <summary>Moves to the tablet's parent page. Returns false at the home page so callers can
    /// close the device there, which is exactly what the visible BACK button does.</summary>
    public bool NavigateBack()
    {
        if (_section == Section.Play && !InSession && (_playStep == PlayStep.Host || _playStep == PlayStep.Join))
        {
            _playStep = PlayStep.Home;
            ShowSection(Section.Play);
            return true;
        }
        if (_section == Section.Home) return false;
        _playStep = PlayStep.Home;
        ShowSection(Section.Home);
        return true;
    }

    private void CycleTab(int direction)
    {
        Section[] order = InGame ? GameTabOrder : TabOrder;
        int index = System.Array.IndexOf(order, _section);
        index = index < 0 ? 0 : (index + direction + order.Length) % order.Length;
        _playStep = PlayStep.Home;
        ShowSection(order[index]);
    }

    private void BuildTopBar()
    {
        foreach (Node child in _topBar.GetChildren()) { _topBar.RemoveChild(child); child.QueueFree(); }

        _topBar.AddChild(Sized(MenuStyle.Outline("BACK", BackPressed, "arrow-left", 46), 130));

        var play = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        play.AddThemeConstantOverride("separation", 6);
        play.AddChild(KeyCap("SPACE"));
        play.AddChild(MenuStyle.Primary(PrimaryLabel(), new Vector2(160, 46), PrimaryAction));
        _topBar.AddChild(play);

        var tabs = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        tabs.AddThemeConstantOverride("separation", 4);
        tabs.AddChild(KeyCap("Q"));
        tabs.AddChild(Tab("PLAY", Section.Play));
        tabs.AddChild(Tab("LOADOUT", Section.Loadout));
        tabs.AddChild(Tab("OPERATORS", Section.Operator));
        tabs.AddChild(Tab("SETTINGS", Section.Settings));
        tabs.AddChild(KeyCap("E"));
        _topBar.AddChild(tabs);

        _topBar.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        // who is in the room (the hub only), settings (there is no exit button: Esc asks to leave)
        if (!InGame)
        {
            var people = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
            people.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0, 0, 0, 0.35f), BorderColor = MenuStyle.Hairline,
                BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
                ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 6, ContentMarginBottom = 6,
            });
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(MenuStyle.Icon("users", 18));
            int inRoom = Math.Max(1, MatchManager.Instance.LobbyPeerIds().Length);
            row.AddChild(MenuStyle.Text(inRoom.ToString(), 20, MenuStyle.TextMain, MenuStyle.Bold));
            people.AddChild(row);
            _topBar.AddChild(people);
        }

        _topBar.AddChild(MenuStyle.IconButton("settings", 24, _section == Section.Settings ? MenuStyle.Accent : MenuStyle.TextMain, () => { _playStep = PlayStep.Home; ShowSection(Section.Settings); }));
    }

    /// <summary>Jumps to a tablet page by name (Home, Play, Operator, Status, Chat, Settings). Used by
    /// the in-game debug tools and automated checks; harmless to call from anywhere once the tablet exists.</summary>
    public void ShowSectionByName(string name)
    {
        if (System.Enum.TryParse(name, true, out Section section)) ShowSection(section);
    }

    private void ShowSection(Section section)
    {
        // Same page rebuilt (a refresh, or picking a choice on it): keep it scrolled where it was.
        string pageKey = $"{section}:{_playStep}:{InSession}";
        _pendingScroll = _scroll != null && IsInstanceValid(_scroll) && _scrollKey == pageKey ? _scroll.ScrollVertical : 0;
        _scrollKey = pageKey;
        _scroll = null;

        _section = section;
        if (section != Section.Play) _activeMenuPeerId = -1;
        StopListening();
        foreach (Node child in _content.GetChildren()) { _content.RemoveChild(child); child.QueueFree(); }
        BuildTopBar();
        switch (section)
        {
            case Section.Home: BuildHome(); break;
            case Section.Play:
                PlayHeader();
                BuildPlay();
                break;
            case Section.Loadout:
                PageHeader("LOADOUT", "3D gunsmith node configurator & 8-stat bars");
                BuildLoadout();
                break;
            case Section.Operator:
                PageHeader("OPERATORS", "12-operator roster · 4-round role cap");
                BuildOperator();
                break;
            case Section.Status:
                PageHeader("PLAYERS", InSession ? "in this room" : "alone in the warehouse");
                Scrolled(BuildStatus);
                break;
            case Section.Chat:
                PageHeader("CHAT", "talk to the room");
                BuildChat();
                break;
            case Section.Settings:
                PageHeader("SETTINGS", "controls, sound and display");
                _settingsPanel = new SettingsPanel();
                _content.AddChild(ContentFrame(_settingsPanel));
                break;
        }
        _liveSignature = LiveSignature();
    }

    private void PlayHeader()
    {
        if (InSession)
        {
            PageHeader("LOBBY", Multiplayer.IsServer() ? "you are hosting" : "in a lobby");
            return;
        }
        switch (_playStep)
        {
            case PlayStep.Solo: PageHeader("PLAY", "vs bots"); break;
            case PlayStep.Host: PageHeader("CUSTOM GAME", "host a game"); break;
            case PlayStep.Join: PageHeader("CUSTOM GAME", "join a game"); break;
            default: PageHeader("CUSTOM GAME", "host or join"); break;
        }
    }

    private void Scrolled(Action<VBoxContainer> build)
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, MouseFilter = Control.MouseFilterEnum.Pass };
        ContentFrame(scroll);
        _content.AddChild(scroll);
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(column);
        build(column);
        _scroll = scroll;
        if (_pendingScroll > 0) RestoreScroll(scroll, _pendingScroll);
    }

    /// <summary>The page has not been laid out yet when it is built, so the scroll position is put back one frame later.</summary>
    private async void RestoreScroll(ScrollContainer scroll, int value)
    {
        if (!IsInsideTree()) return;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (IsInstanceValid(scroll) && scroll.IsInsideTree()) scroll.ScrollVertical = value;
    }

    // -------------------------------------------------------------------------------------------
    // PLAY
    // -------------------------------------------------------------------------------------------

    private void BuildPlay()
    {
        if (InSession) { Scrolled(BuildLobby); return; }
        switch (_playStep)
        {
            case PlayStep.Solo: Scrolled(BuildSolo); break;
            case PlayStep.Host: Scrolled(BuildHost); break;
            case PlayStep.Join: BuildJoin(); break;
            default: BuildPlayHome(); break;
        }
    }

    /// <summary>Title row for a tablet page: the display face, a subtitle chip, and a red rule under it. The same
    /// type and colours as the home page, so every page of the tablet reads as one design.</summary>
    private void PageHeader(string title, string subtitle)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        row.AddThemeConstantOverride("separation", 16);
        row.AddChild(MenuStyle.Text(title, 50, MenuStyle.TextMain, MenuStyle.Bold));
        if (!string.IsNullOrEmpty(subtitle))
        {
            var chip = MenuStyle.Chip(subtitle, MenuStyle.Accent, 14, false);
            chip.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(chip);
        }
        MenuStyle.Place(row, 0, 0, 1, 0, 36, 18, -36, 76);
        _content.AddChild(row);

        Color ruleColour = MenuStyle.Accent;
        ruleColour.A = 0.55f;
        var rule = new ColorRect { Color = ruleColour, MouseFilter = Control.MouseFilterEnum.Ignore };
        MenuStyle.Place(rule, 0, 0, 1, 0, 36, 82, -36, 84);
        _content.AddChild(rule);
    }

    /// <summary>Places a page's content below the page header, inside the tablet's screen margins.</summary>
    private static Control ContentFrame(Control content)
    {
        MenuStyle.Place(content, 0, 0, 1, 1, 36, 100, -36, -14);
        return content;
    }

    private void BuildHome()
    {
        if (InGame) { BuildGameHome(); return; }
        MatchManager match = MatchManager.Instance;
        bool inSession = InSession;

        // Background: the wordmark, very faint, so the screen has depth without noise.
        var mark = MenuStyle.Text("WRAITH RUN", 150, new Color(0.9f, 0.08f, 0.08f, 0.06f), MenuStyle.Bold);
        MenuStyle.Place(mark, 0, 0, 1, 0, 0, 40, 0, 300);
        mark.MouseFilter = Control.MouseFilterEnum.Ignore;
        _content.AddChild(mark);

        // Left column: identity, status and the controls at a glance.
        var left = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        MenuStyle.Place(left, 0, 0.12f, 0.50f, 1, 0, 0, -24, -8);
        left.Alignment = BoxContainer.AlignmentMode.End;
        left.AddThemeConstantOverride("separation", 14);
        _content.AddChild(left);

        var tag = MenuStyle.Chip(inSession ? (Multiplayer.IsServer() ? "YOU ARE HOSTING" : "IN A LOBBY") : "THE HUB", MenuStyle.Accent, 14, true);
        tag.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        left.AddChild(tag);

        left.AddChild(MenuStyle.Text(inSession && match != null ? match.ModeRules.DisplayName.ToUpperInvariant() : "WRAITH RUN", 84, MenuStyle.TextMain, MenuStyle.Bold));
        if (inSession && match != null)
            left.AddChild(MenuStyle.Text(match.MapDisplayName.ToUpperInvariant(), 26, MenuStyle.Accent, MenuStyle.Bold));

        string body = inSession
            ? (HubController.SplitRooms(match)
                ? $"You are on {MatchManager.TeamName(match.GetTeam(Multiplayer.GetUniqueId()))}. Your team has its own warehouse until the match starts."
                : "Everyone is in the same warehouse until the match starts.")
            : "Walk the warehouse and take weapons off the wall. Then host a game or join one on this Wi-Fi.";
        left.AddChild(MenuStyle.Wrapped(body, 20, MenuStyle.TextDim, MenuStyle.Medium));
        if (!string.IsNullOrEmpty(StatusLine))
            left.AddChild(MenuStyle.Text(StatusLine, 18, MenuStyle.Accent, MenuStyle.SemiBold));

        // Right column: the actions, compact so the three of them read at a glance.
        var right = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        MenuStyle.Place(right, 0.60f, 0.22f, 1, 0.96f, 0, 0, -4, 0);
        right.AddThemeConstantOverride("separation", 10);
        right.Alignment = BoxContainer.AlignmentMode.Center;
        _content.AddChild(right);

        if (inSession)
        {
            right.AddChild(MenuStyle.CompactHero("LOBBY", "Ready-up and players", () => { _playStep = PlayStep.Home; ShowSection(Section.Play); }));
            right.AddChild(MenuStyle.CompactHero("LEAVE LOBBY", "Back to the warehouse", () => _hub?.LeaveSession()));
        }
        else
        {
            right.AddChild(MenuStyle.CompactHero("PLAY", "Solo match against bots", () => { _playStep = PlayStep.Solo; ShowSection(Section.Play); }));
            right.AddChild(MenuStyle.CompactHero("CUSTOM GAME", "Host or join on this Wi-Fi", () => { _playStep = PlayStep.Home; ShowSection(Section.Play); }));
        }
    }

    // -------------------------------------------------------------------------------------------
    // In a match
    // -------------------------------------------------------------------------------------------

    /// <summary>Called by PauseMenu when the host drops: the tablet opens and stays open on a single way out.</summary>
    public void ShowConnectionLost()
    {
        _connectionLost = true;
        _confirmLeave = false;
        if (!_open) Open();
        if (_open) ShowSection(Section.Home);
    }

    /// <summary>Escape / Back while the "leave this match?" question is up answers NO. Returns whether it did.</summary>
    public bool CancelLeaveQuestion()
    {
        if (!_confirmLeave) return false;
        _confirmLeave = false;
        ShowSection(Section.Home);
        return true;
    }

    private void BuildGameHome()
    {
        MatchManager? match = MatchManager.Instance;

        var mark = MenuStyle.Text("WRAITH RUN", 150, new Color(0.9f, 0.08f, 0.08f, 0.06f), MenuStyle.Bold);
        MenuStyle.Place(mark, 0, 0, 1, 0, 0, 40, 0, 300);
        mark.MouseFilter = Control.MouseFilterEnum.Ignore;
        _content.AddChild(mark);

        var left = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        MenuStyle.Place(left, 0, 0.12f, 0.50f, 1, 0, 0, -24, -8);
        left.Alignment = BoxContainer.AlignmentMode.End;
        left.AddThemeConstantOverride("separation", 14);
        _content.AddChild(left);

        var tag = MenuStyle.Chip(_connectionLost ? "HOST LINK LOST" : "IN A MATCH", MenuStyle.Accent, 14, true);
        tag.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        left.AddChild(tag);

        string title = match != null ? match.ModeRules.DisplayName.ToUpperInvariant() : "WRAITH RUN";
        left.AddChild(MenuStyle.Wrapped(title, 60, MenuStyle.TextMain, MenuStyle.Bold));
        if (match != null)
            left.AddChild(MenuStyle.Text(match.MapDisplayName.ToUpperInvariant(), 26, MenuStyle.Accent, MenuStyle.Bold));
        string body = _connectionLost
            ? "The host closed the match or the connection dropped. There is nothing left to play."
            : "The match keeps running while the tablet is up, so keep an eye out.";
        left.AddChild(MenuStyle.Wrapped(body, 20, MenuStyle.TextDim, MenuStyle.Medium));

        var right = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        MenuStyle.Place(right, 0.60f, 0.22f, 1, 0.96f, 0, 0, -4, 0);
        right.AddThemeConstantOverride("separation", 10);
        right.Alignment = BoxContainer.AlignmentMode.Center;
        _content.AddChild(right);

        if (_confirmLeave)
        {
            bool hosting = GetNodeOrNull<LanSession>("/root/LanSession")?.IsHosting ?? false;
            right.AddChild(MenuStyle.Wrapped(hosting
                ? "You are hosting this match. Leaving ends it for every player connected to you."
                : "Leave this match and go back to the warehouse? Your score for this match will be lost.",
                20, hosting ? MenuStyle.Accent : MenuStyle.TextDim, MenuStyle.Medium));
            right.AddChild(MenuStyle.CompactHero("YES, LEAVE", "End my match", LeaveNow));
            right.AddChild(MenuStyle.CompactHero("NO, STAY", "Back to the tablet", () => CancelLeaveQuestion()));
            return;
        }
        if (!_connectionLost) right.AddChild(MenuStyle.CompactHero("RESUME", "Back to the game", Close));
        right.AddChild(MenuStyle.CompactHero("LEAVE MATCH", "Back to the warehouse", LeaveGame));
    }

    private void LeaveGame()
    {
        // With the host gone there is nothing to ask: go straight out.
        if (_connectionLost) { LeaveNow(); return; }
        _confirmLeave = true;
        ShowSection(Section.Home);
    }

    private void LeaveNow()
    {
        if (_leaving) return;
        _connectionLost = false; // the way out is always allowed
        Close();
        _leaving = true;
        GameSession.LeaveToMainMenu(GetTree());
    }

    /// <summary>CUSTOM GAME: the two ways in - host a lobby, or join one on this Wi-Fi.</summary>
    private void BuildPlayHome()
    {
        var box = new VBoxContainer();
        MenuStyle.Place(box, 0.5f, 0.5f, 0.5f, 0.5f, -230, -120, 230, 120);
        box.AddThemeConstantOverride("separation", 12);
        _content.AddChild(box);
        box.AddChild(MenuStyle.Wrapped("Host a game for your friends, or join one on this Wi-Fi.", 18, MenuStyle.TextDim, MenuStyle.Medium));
        box.AddChild(MenuStyle.CompactHero("HOST", "Choose the mode and the map", () => { _playStep = PlayStep.Host; ShowSection(Section.Play); }, 68));
        box.AddChild(MenuStyle.CompactHero("JOIN", "Find a game on this Wi-Fi", () => { _playStep = PlayStep.Join; ShowSection(Section.Play); }, 68));
        if (!string.IsNullOrEmpty(StatusLine))
            box.AddChild(MenuStyle.Wrapped(StatusLine, 17, MenuStyle.Accent, MenuStyle.SemiBold));
    }

    /// <summary>Team Deathmatch first (the hub's default), then every other mode once. SelectableModes already
    /// lists Team Deathmatch, so it must not be added a second time.</summary>
    private static List<MatchManager.Mode> HubModes()
    {
        var modes = new List<MatchManager.Mode> { MatchManager.Mode.TeamDeathmatch };
        foreach (MatchManager.Mode candidate in MatchManager.SelectableModes)
            if (candidate != MatchManager.Mode.TeamDeathmatch) modes.Add(candidate);
        return modes;
    }

    // -------------------------------------------------------------------------------------------
    // Match set-up controls (shared by PLAY and CUSTOM GAME)
    // -------------------------------------------------------------------------------------------

    private static readonly int[] DurationChoices = { 3, 5, 8, 13 };
    /// <summary>Total players in a vs-bots match, counting you. All even, so the two teams always split cleanly.</summary>
    private static readonly int[] PlayerChoices = { 4, 6, 8, 10, 12, 14, 16, 18 };
    private static readonly string[] YesNo = { "No", "Yes" };
    private static readonly string[] BotLevelNames = { "Easy", "Normal", "Advanced", "Mixed" };

    private bool _hostBotsEnabled;

    /// <summary>Clean OptionButton drop-down for the match mode (Master GDD & prompt.md Bug 3).</summary>
    private static Control ModeDropdown(MatchManager match, Action onChanged)
    {
        List<MatchManager.Mode> modes = HubModes();
        if (!modes.Contains(match.GameMode)) match.GameMode = modes[0];

        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(MenuStyle.Text("Mode", 14, MenuStyle.TextDim, MenuStyle.SemiBold));

        var opt = new OptionButton
        {
            CustomMinimumSize = new Vector2(0, 36),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        int selected = 0;
        for (int i = 0; i < modes.Count; i++)
        {
            GameModeBase rules = GameModeBase.For(modes[i]);
            opt.AddItem(rules.DisplayName, i);
            if (modes[i] == match.GameMode) selected = i;
        }
        opt.Selected = selected;
        opt.AddThemeFontOverride("font", MenuStyle.SemiBold);
        opt.AddThemeFontSizeOverride("font_size", 14);
        opt.ItemSelected += index =>
        {
            match.GameMode = modes[(int)index];
            onChanged();
        };
        box.AddChild(opt);
        return box;
    }

    /// <summary>Clean OptionButton drop-down for the arena map.</summary>
    private static Control MapDropdown(MatchManager match, Action onChanged)
    {
        MatchManager.ArenaMap[] maps = MatchManager.SelectableMaps;
        if (System.Array.IndexOf(maps, match.SelectedMap) < 0) match.SelectedMap = maps[0];

        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(MenuStyle.Text("Map", 14, MenuStyle.TextDim, MenuStyle.SemiBold));

        var opt = new OptionButton
        {
            CustomMinimumSize = new Vector2(0, 36),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        int selected = 0;
        for (int i = 0; i < maps.Length; i++)
        {
            opt.AddItem(MatchManager.MapName(maps[i]), i);
            if (maps[i] == match.SelectedMap) selected = i;
        }
        opt.Selected = selected;
        opt.AddThemeFontOverride("font", MenuStyle.SemiBold);
        opt.AddThemeFontSizeOverride("font_size", 14);
        opt.ItemSelected += index =>
        {
            match.SelectedMap = maps[(int)index];
            onChanged();
        };
        box.AddChild(opt);
        return box;
    }

    private static Control MaxPlayersRow(MatchManager match, Action onChanged)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 4);

        var labelRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var title = MenuStyle.Text("Max Players", 14, MenuStyle.TextDim, MenuStyle.SemiBold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        labelRow.AddChild(title);
        var countChip = MenuStyle.Chip($"{match.LobbyPlayerCount}", MenuStyle.Accent, 12, false);
        labelRow.AddChild(countChip);
        box.AddChild(labelRow);

        var inputRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        inputRow.AddThemeConstantOverride("separation", 6);

        var minusBtn = MenuStyle.Outline("-", () =>
        {
            int minP = match.ModeRules.MinimumPlayers;
            if (match.LobbyPlayerCount > minP)
            {
                match.LobbyPlayerCount = Math.Max(minP, match.LobbyPlayerCount - 2);
                onChanged();
            }
        }, null, 30);
        minusBtn.CustomMinimumSize = new Vector2(32, 30);
        inputRow.AddChild(minusBtn);

        var lineEdit = new LineEdit
        {
            Text = match.LobbyPlayerCount.ToString(),
            Alignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 30),
            FocusMode = Control.FocusModeEnum.None,
            Editable = false,
        };
        MenuStyle.StyleLineEdit(lineEdit);
        inputRow.AddChild(lineEdit);

        var plusBtn = MenuStyle.Outline("+", () =>
        {
            int maxP = match.ModeRules.MaximumPlayers;
            if (match.LobbyPlayerCount < maxP)
            {
                match.LobbyPlayerCount = Math.Min(maxP, match.LobbyPlayerCount + 2);
                onChanged();
            }
        }, null, 30);
        plusBtn.CustomMinimumSize = new Vector2(32, 30);
        inputRow.AddChild(plusBtn);

        box.AddChild(inputRow);
        return box;
    }

    private static Control BotDifficultySlider(MatchManager match, Action onChanged)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 4);

        int currentLevel = Math.Clamp((int)match.BotLevel + 1, 1, 5);
        var labelRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var title = MenuStyle.Text("Bots Difficulty", 14, MenuStyle.TextDim, MenuStyle.SemiBold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        labelRow.AddChild(title);
        var valLabel = MenuStyle.Text($"LEVEL {currentLevel}", 12, MenuStyle.Accent, MenuStyle.Bold);
        labelRow.AddChild(valLabel);
        box.AddChild(labelRow);

        var slider = new HSlider
        {
            MinValue = 1,
            MaxValue = 5,
            Step = 1,
            Value = currentLevel,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 24),
            FocusMode = Control.FocusModeEnum.None,
        };
        slider.ValueChanged += val =>
        {
            match.BotLevel = (BotDifficulty)Math.Clamp((int)val - 1, 0, 3);
            valLabel.Text = $"LEVEL {(int)val}";
            onChanged();
        };
        box.AddChild(slider);
        return box;
    }

    /// <summary>How long the match runs. There is no score limit: the clock decides every match.</summary>
    private static Control DurationRow(MatchManager match, Action onChanged)
    {
        int selected = System.Array.IndexOf(DurationChoices, match.TimeLimitMinutes);
        if (selected < 0) { match.TimeLimitMinutes = 5; selected = System.Array.IndexOf(DurationChoices, 5); }
        var labels = new string[DurationChoices.Length];
        for (int i = 0; i < labels.Length; i++) labels[i] = $"{DurationChoices[i]}m";
        return MenuStyle.Segmented("Duration", labels, selected, index =>
        {
            match.TimeLimitMinutes = DurationChoices[index];
            onChanged();
        });
    }

    private static Control BotDifficultyRow(MatchManager match, Action onChanged) =>
        MenuStyle.Segmented("Bots Difficulty", BotLevelNames, (int)match.BotLevel, index =>
        {
            match.BotLevel = (BotDifficulty)index;
            onChanged();
        });

    private static Control YesNoRow(string label, bool value, Action<bool> set, Action onChanged) =>
        MenuStyle.Segmented(label, YesNo, value ? 1 : 0, index =>
        {
            set(index == 1);
            onChanged();
        });

    private static VBoxContainer Column(int separation = 12)
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", separation);
        return column;
    }

    private static HBoxContainer TwoColumns(VBoxContainer left, VBoxContainer right)
    {
        var split = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        split.AddThemeConstantOverride("separation", 26);
        split.AddChild(left);
        split.AddChild(right);
        return split;
    }

    /// <summary>CREATE LOBBY: mode, map, length and the optional rules, then a lobby. Mode and map are picked here,
    /// once; the lobby shows them but does not ask again. There is no player limit: anyone on the Wi-Fi can join.</summary>
    private void BuildHost(VBoxContainer column)
    {
        MatchManager match = MatchManager.Instance;
        Action refresh = () => ShowSection(Section.Play);

        VBoxContainer left = Column();
        left.AddChild(MenuStyle.Text("Game Name", 18, MenuStyle.TextDim, MenuStyle.Medium));
        var name = new LineEdit { PlaceholderText = "Game name", Text = _gameName, MaxLength = 32 };
        MenuStyle.StyleLineEdit(name);
        name.TextChanged += text => _gameName = text;
        left.AddChild(name);
        left.AddChild(ModeDropdown(match, refresh));
        left.AddChild(MapDropdown(match, refresh));
        left.AddChild(DurationRow(match, refresh));

        VBoxContainer right = Column();
        if (match.IsTeamMode)
        {
            right.AddChild(YesNoRow("Player Marker", match.TeamIndicators, v => match.TeamIndicators = v, refresh));
            right.AddChild(YesNoRow("Friendly Fire", match.FriendlyFire, v => match.FriendlyFire = v, refresh));
        }
        right.AddChild(YesNoRow("Enable Bots", _hostBotsEnabled, v => _hostBotsEnabled = v, refresh));
        if (_hostBotsEnabled) right.AddChild(BotDifficultyRow(match, refresh));

        column.AddChild(TwoColumns(left, right));

        var status = MenuStyle.Text(StatusLine, 17, MenuStyle.Accent, MenuStyle.SemiBold);
        column.AddChild(status);
        Button create = MenuStyle.Primary("CREATE LOBBY", new Vector2(0, 62), () =>
        {
            // bots are added (and counted) in the lobby, where it is known who joined
            match.BotCount = _hostBotsEnabled ? Math.Clamp(3, Math.Max(1, match.ModeRules.MinimumPlayers - 1), 15) : 0;
            SyncLobbySize();
            if (_hub != null && _hub.HostSession(_gameName)) { _playStep = PlayStep.Home; ShowSection(Section.Play); }
            else status.Text = StatusLine;
        });
        create.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        column.AddChild(create);
    }

    /// <summary>The player totals the chosen mode allows, drawn from PlayerChoices. A mode with a fixed size (Duo
    /// Buggy: exactly 4) gets just that one.</summary>
    private static int[] SoloPlayerChoices(GameModeBase rules)
    {
        var list = new List<int>();
        foreach (int n in PlayerChoices)
            if (n >= rules.MinimumPlayers && n <= rules.MaximumPlayers) list.Add(n);
        if (list.Count == 0) list.Add(Math.Clamp(rules.MaximumPlayers, rules.MinimumPlayers, rules.MaximumPlayers));
        return list.ToArray();
    }

    /// <summary>Puts the player total (you + bots) on one of the choices the mode allows. With no earlier choice
    /// it starts at six players.</summary>
    private static int ClampSoloPlayers(MatchManager match)
    {
        int[] choices = SoloPlayerChoices(match.ModeRules);
        int total = match.BotCount + 1;
        if (System.Array.IndexOf(choices, total) < 0)
            total = System.Array.IndexOf(choices, 6) >= 0 ? 6 : choices[0];
        match.BotCount = total - 1;
        SyncLobbySize();
        return total;
    }

    /// <summary>PLAY: a private match against bots. Settings on the left, who is playing on the right, then START.</summary>
    private void BuildSolo(VBoxContainer column)
    {
        MatchManager match = MatchManager.Instance;
        Action refresh = () => { ClampSoloPlayers(match); ShowSection(Section.Play); };
        int total = ClampSoloPlayers(match);
        int[] choices = SoloPlayerChoices(match.ModeRules);

        VBoxContainer left = Column();
        left.AddChild(ModeDropdown(match, refresh));
        left.AddChild(MapDropdown(match, refresh));
        left.AddChild(DurationRow(match, refresh));
        var counts = new string[choices.Length];
        for (int i = 0; i < counts.Length; i++) counts[i] = choices[i].ToString();
        left.AddChild(MenuStyle.Segmented("Max Players", counts, System.Array.IndexOf(choices, total), index =>
        {
            match.BotCount = choices[index] - 1;
            SyncLobbySize();
            ShowSection(Section.Play);
        }));
        left.AddChild(BotDifficultyRow(match, refresh));

        VBoxContainer right = BuildRoster(match, total);
        column.AddChild(TwoColumns(left, right));

        var status = MenuStyle.Text(StatusLine, 17, MenuStyle.Accent, MenuStyle.SemiBold);
        column.AddChild(status);
        Button start = MenuStyle.Primary("START MATCH", new Vector2(0, 62), () =>
        {
            ClampSoloPlayers(match);
            if (!match.CanHostStartMatch) { status.Text = "This mode needs more players than that."; return; }
            _hub?.StartMatch();
        });
        start.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        column.AddChild(start);
    }

    /// <summary>Who will be in the match: you and the bots, split the way the match will split them (the same rule
    /// as MatchManager.AssignTeamAndBroadcast). The bot names are the ones the match will really use.</summary>
    private VBoxContainer BuildRoster(MatchManager match, int total)
    {
        VBoxContainer panel = Column(14);
        string me = GameUiState.PlayerName(LocalPeerId());
        string tag = match.BotLevel switch
        {
            BotDifficulty.Easy => " [E]",
            BotDifficulty.Normal => " [N]",
            BotDifficulty.Advanced => " [A]",
            _ => string.Empty,
        };
        int bots = total - 1;

        if (!match.IsTeamMode)
        {
            var everyone = new List<(string Text, bool Me)> { (me, true) };
            for (int i = 0; i < bots; i++) everyone.Add((match.PlannedBotName(i) + tag, false));
            panel.AddChild(RosterBlock("Players", Colors.White, everyone, total));
            return panel;
        }

        var teams = new[] { new List<(string Text, bool Me)>(), new List<(string Text, bool Me)>() };
        int myTeam = Math.Max(0, match.GetTeam(LocalPeerId()));
        teams[myTeam].Add((me, true));
        for (int i = 0; i < bots; i++)
        {
            int team = teams[0].Count <= teams[1].Count ? 0 : 1; // exactly MatchManager's rule
            teams[team].Add((match.PlannedBotName(i) + tag, false));
        }
        for (int team = 0; team < 2; team++)
        {
            string name = MatchManager.TeamName(team);
            string title = name.Length > 0 ? char.ToUpperInvariant(name[0]) + name.Substring(1).ToLowerInvariant() + " Team" : "Team";
            panel.AddChild(RosterBlock(title, MatchManager.TeamColor(team), teams[team], (total + 1 - team) / 2));
        }
        return panel;
    }

    /// <summary>One team's block: "Alpha Team  3/3", a thin line, then a slim row per player.</summary>
    private static Control RosterBlock(string title, Color accent, List<(string Text, bool Me)> members, int capacity)
    {
        VBoxContainer block = Column(2);
        var head = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        Label name = MenuStyle.Text(title, 20, MenuStyle.TextMain, MenuStyle.SemiBold);
        name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(name);
        head.AddChild(MenuStyle.Text($"{members.Count}/{capacity}", 20, MenuStyle.TextDim, MenuStyle.SemiBold, HorizontalAlignment.Right));
        block.AddChild(head);
        block.AddChild(new ColorRect { Color = accent, CustomMinimumSize = new Vector2(0, 2), MouseFilter = Control.MouseFilterEnum.Ignore });
        foreach ((string text, bool isMe) in members) block.AddChild(RosterRow(text, isMe));
        for (int i = members.Count; i < capacity; i++) block.AddChild(RosterRow("-", false));
        return block;
    }

    private static Control RosterRow(string text, bool me)
    {
        var row = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat { BgColor = me ? new Color(1.0f, 0.82f, 0.32f, 0.14f) : new Color(0.085f, 0.09f, 0.10f) };
        style.ContentMarginLeft = 12;
        style.ContentMarginRight = 12;
        style.ContentMarginTop = 6;
        style.ContentMarginBottom = 6;
        row.AddThemeStyleboxOverride("panel", style);
        Label label = MenuStyle.Text(text, 19, me ? new Color(1.0f, 0.82f, 0.32f) : MenuStyle.TextDim, MenuStyle.SemiBold);
        label.ClipText = true;
        row.AddChild(label);
        return row;
    }

    /// <summary>Bots fill a lobby that is short of people (only the host can add them). The count is capped by what
    /// the mode allows, counting the people already in.</summary>
    private Control BotStepper(Action refresh)
    {
        MatchManager match = MatchManager.Instance;
        int humans = Math.Max(1, match.LobbyPeerIds().Length);
        int max = Math.Max(0, match.ModeRules.MaximumPlayers - humans);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        row.AddChild(MenuStyle.Text("Bots", 18, MenuStyle.TextDim, MenuStyle.Medium));
        row.AddChild(MenuStyle.Outline("-", () => { match.BotCount = Math.Max(0, match.BotCount - 1); SyncLobbySize(); refresh(); }, null, 40));
        row.AddChild(MenuStyle.Text(match.BotCount.ToString(), 22, MenuStyle.TextMain, MenuStyle.Bold, HorizontalAlignment.Center));
        row.AddChild(MenuStyle.Outline("+", () => { match.BotCount = Math.Min(max, match.BotCount + 1); SyncLobbySize(); refresh(); }, null, 40));
        return row;
    }

    private static void SyncLobbySize()
    {
        MatchManager match = MatchManager.Instance;
        int humans = match.LobbyPeerIds().Length;
        if (humans < 1) humans = 1;
        match.LobbyPlayerCount = Math.Clamp(humans + match.BotCount, match.ModeRules.MinimumPlayers, match.ModeRules.MaximumPlayers);
    }

    public void BuildLobbyForTest(VBoxContainer column) => BuildLobby(column);
    public void BuildJoinForTest()
    {
        _open = true;
        _section = Section.Play;
        _playStep = PlayStep.Join;
        BuildJoin();
    }
    public VBoxContainer? GamesListForTest => _gamesList;

    private void BuildLobby(VBoxContainer column)
    {
        MatchManager match = MatchManager.Instance;
        GameModeBase rules = match.ModeRules;
        bool host = !Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer();

        // 3-Column Custom Game Lobby (Master GDD & prompt.md Bug 3):
        // Col 1: Left Column (Host Configuration)
        // Col 2: Center Column (Albert Team vs. Einstein Team / Attackers vs. Defenders)
        // Col 3: Right Column (Social & Invites)
        var layout = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        layout.AddThemeConstantOverride("separation", 14);
        column.AddChild(layout);

        var (alberts, einsteins, albertBots, einsteinBots) = GetLobbyRosters(match);
        int capacity = Math.Max(5, Math.Max(alberts.Count + albertBots.Count, einsteins.Count + einsteinBots.Count));

        // Column 1: Left Column (Host Configuration)
        var leftCol = Column(10);
        leftCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        BuildHostConfigColumn(leftCol, match, rules, host);
        layout.AddChild(leftCol);

        // Column 2: Center Column (Stacked Team Containers)
        var centerCol = Column(10);
        centerCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        BuildStackedTeamsColumn(centerCol, alberts, einsteins, albertBots, einsteinBots, capacity, host, match);
        layout.AddChild(centerCol);

        // Column 3: Right Column (Social & Invites)
        var rightCol = Column(10);
        rightCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        BuildSocialAndInvitesPanel(rightCol);
        layout.AddChild(rightCol);
    }

    private (List<(long Id, string Name, bool IsMe, bool IsHost, bool IsReady)> Alberts,
             List<(long Id, string Name, bool IsMe, bool IsHost, bool IsReady)> Einsteins,
             List<string> AlbertBots,
             List<string> EinsteinBots)
    GetLobbyRosters(MatchManager match)
    {
        var alberts = new List<(long Id, string Name, bool IsMe, bool IsHost, bool IsReady)>();
        var einsteins = new List<(long Id, string Name, bool IsMe, bool IsHost, bool IsReady)>();
        var albertBots = new List<string>();
        var einsteinBots = new List<string>();

        long[] ids = match.LobbyPeerIds();
        if (ids.Length == 0) ids = new long[] { LocalPeerId() };

        long myId = LocalPeerId();
        foreach (long id in ids)
        {
            string name = GameUiState.PlayerName(id);
            bool isMe = id == myId;
            bool isHost = id == 1;
            bool isReady = id == 1 || match.IsPeerReady(id);
            int team = match.GetTeam(id);
            if (team < 0) team = (alberts.Count <= einsteins.Count) ? 0 : 1;

            if (team == 0) alberts.Add((id, name, isMe, isHost, isReady));
            else einsteins.Add((id, name, isMe, isHost, isReady));
        }

        for (int i = 0; i < match.BotCount; i++)
        {
            string botName = match.PlannedBotName(i);
            int countA = alberts.Count + albertBots.Count;
            int countE = einsteins.Count + einsteinBots.Count;
            if (countA <= countE) albertBots.Add(botName);
            else einsteinBots.Add(botName);
        }

        return (alberts, einsteins, albertBots, einsteinBots);
    }

    private void BuildTeamColumn(
        VBoxContainer col,
        int teamIndex,
        string teamTitle,
        Color teamAccent,
        List<(long Id, string Name, bool IsMe, bool IsHost, bool IsReady)> humans,
        List<string> bots,
        int capacity,
        bool isHost,
        MatchManager match)
    {
        var panel = MenuStyle.Panel(teamTitle, "users", out VBoxContainer body);
        panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(panel);

        int memberCount = humans.Count + bots.Count;
        var subHeader = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var titleLabel = MenuStyle.Text(teamTitle, 20, teamAccent, MenuStyle.Bold);
        titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        subHeader.AddChild(titleLabel);
        var countChip = MenuStyle.Chip($"{memberCount}/{capacity}", teamAccent, 13, false);
        subHeader.AddChild(countChip);
        body.AddChild(subHeader);

        body.AddChild(new ColorRect { Color = teamAccent, CustomMinimumSize = new Vector2(0, 2), MouseFilter = Control.MouseFilterEnum.Ignore });

        foreach (var p in humans)
            body.AddChild(BuildPlayerRow(p.Id, p.Name, p.IsMe, p.IsHost, p.IsReady, teamIndex, isHost, match));

        foreach (var b in bots)
            body.AddChild(BuildBotRow(b, teamAccent));

        for (int i = memberCount; i < capacity; i++)
            body.AddChild(BuildEmptySlotRow());
    }

    private Control BuildPlayerRow(
        long peerId,
        string name,
        bool isMe,
        bool isPeerHost,
        bool isReady,
        int teamIndex,
        bool isLocalHost,
        MatchManager match)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 2);

        var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var style = new StyleBoxFlat
        {
            BgColor = isMe ? new Color(0.18f, 0.14f, 0.08f, 0.9f) : new Color(0.08f, 0.09f, 0.11f, 0.85f),
            BorderColor = isMe ? new Color(1.0f, 0.82f, 0.32f, 0.5f) : new Color(0.25f, 0.25f, 0.28f, 0.3f),
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        style.ContentMarginLeft = 8;
        style.ContentMarginRight = 8;
        style.ContentMarginTop = 6;
        style.ContentMarginBottom = 6;
        card.AddThemeStyleboxOverride("panel", style);

        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);

        row.AddChild(MenuStyle.Icon("user", 16, 16, isMe ? new Color(1.0f, 0.82f, 0.32f) : MenuStyle.TextDim));

        // Rank badge
        var rankBadge = MenuStyle.Chip("LVL 12", new Color(0.35f, 0.55f, 0.85f), 11, false);
        row.AddChild(rankBadge);

        // Host crown
        if (isPeerHost)
        {
            var crown = MenuStyle.Text("👑", 14, new Color(1.0f, 0.82f, 0.2f), MenuStyle.Bold);
            row.AddChild(crown);
        }

        string displayName = name + (isMe ? " (you)" : "");
        var nameLabel = MenuStyle.Text(displayName, 15, isMe ? new Color(1.0f, 0.85f, 0.4f) : MenuStyle.TextMain, MenuStyle.SemiBold);
        nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        nameLabel.ClipText = true;
        row.AddChild(nameLabel);

        var readyBadge = MenuStyle.Chip(
            isReady ? "READY" : "WAITING",
            isReady ? new Color(0.35f, 0.85f, 0.40f) : new Color(0.95f, 0.40f, 0.30f),
            12,
            false);
        row.AddChild(readyBadge);

        if (isLocalHost)
        {
            var dotsBtn = new Button
            {
                Text = "⋮",
                Flat = true,
                CustomMinimumSize = new Vector2(28, 26),
                FocusMode = Control.FocusModeEnum.None,
            };
            dotsBtn.AddThemeFontOverride("font", MenuStyle.Bold);
            dotsBtn.AddThemeFontSizeOverride("font_size", 16);
            dotsBtn.AddThemeColorOverride("font_color", _activeMenuPeerId == peerId ? MenuStyle.Accent : MenuStyle.TextDim);
            dotsBtn.AddThemeColorOverride("font_hover_color", MenuStyle.Accent);
            dotsBtn.Pressed += () =>
            {
                _activeMenuPeerId = _activeMenuPeerId == peerId ? -1 : peerId;
                ShowSection(Section.Play);
            };
            row.AddChild(dotsBtn);
        }

        card.AddChild(row);
        box.AddChild(card);

        // Host 3-dot menu drawer
        if (isLocalHost && _activeMenuPeerId == peerId)
        {
            var menuDrawer = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var drawerStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.05f, 0.06f, 0.95f),
                BorderColor = MenuStyle.Accent,
            };
            drawerStyle.SetBorderWidthAll(1);
            drawerStyle.SetCornerRadiusAll(4);
            drawerStyle.ContentMarginLeft = 6;
            drawerStyle.ContentMarginRight = 6;
            drawerStyle.ContentMarginTop = 4;
            drawerStyle.ContentMarginBottom = 4;
            menuDrawer.AddThemeStyleboxOverride("panel", drawerStyle);

            var actions = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            actions.AddThemeConstantOverride("separation", 6);

            int targetTeam = teamIndex == 0 ? 1 : 0;
            string switchLabel = targetTeam == 0 ? "⇄ Move to Albert" : "⇄ Move to Einstein";
            var switchBtn = MenuStyle.Outline(switchLabel, () =>
            {
                match.SetPeerTeam(peerId, targetTeam);
                _activeMenuPeerId = -1;
                ShowSection(Section.Play);
            }, null, 30);
            switchBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            switchBtn.AddThemeFontSizeOverride("font_size", 13);
            actions.AddChild(switchBtn);

            if (peerId != 1)
            {
                var kickBtn = MenuStyle.Outline("✕ Kick", () =>
                {
                    match.KickPlayer(peerId);
                    _activeMenuPeerId = -1;
                    ShowSection(Section.Play);
                }, null, 30);
                kickBtn.AddThemeFontSizeOverride("font_size", 13);
                kickBtn.AddThemeColorOverride("font_color", new Color(1.0f, 0.35f, 0.35f));
                actions.AddChild(kickBtn);
            }

            var closeBtn = new Button { Text = "✕", Flat = true, CustomMinimumSize = new Vector2(26, 26) };
            closeBtn.Pressed += () =>
            {
                _activeMenuPeerId = -1;
                ShowSection(Section.Play);
            };
            actions.AddChild(closeBtn);

            menuDrawer.AddChild(actions);
            box.AddChild(menuDrawer);
        }

        return box;
    }

    private static Control BuildBotRow(string botName, Color teamAccent)
    {
        var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.07f, 0.08f, 0.7f),
            BorderColor = new Color(0.2f, 0.2f, 0.22f, 0.2f),
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        style.ContentMarginLeft = 8;
        style.ContentMarginRight = 8;
        style.ContentMarginTop = 6;
        style.ContentMarginBottom = 6;
        card.AddThemeStyleboxOverride("panel", style);

        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(MenuStyle.Icon("bot", 16, 16, MenuStyle.TextMuted));
        var nameLabel = MenuStyle.Text($"[BOT] {botName}", 15, MenuStyle.TextDim, MenuStyle.Medium);
        nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        nameLabel.ClipText = true;
        row.AddChild(nameLabel);
        row.AddChild(MenuStyle.Chip("READY", new Color(0.35f, 0.85f, 0.40f, 0.8f), 11, false));
        card.AddChild(row);
        return card;
    }

    private static Control BuildEmptySlotRow()
    {
        var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.04f, 0.05f, 0.4f),
            BorderColor = new Color(0.18f, 0.18f, 0.20f, 0.2f),
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        style.ContentMarginLeft = 8;
        style.ContentMarginRight = 8;
        style.ContentMarginTop = 6;
        style.ContentMarginBottom = 6;
        card.AddThemeStyleboxOverride("panel", style);

        var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.AddChild(MenuStyle.Text("- Open Slot -", 14, MenuStyle.TextMuted, MenuStyle.Medium));
        card.AddChild(row);
        return card;
    }

    private void BuildHostConfigColumn(VBoxContainer col, MatchManager match, GameModeBase rules, bool host)
    {
        var configPanel = MenuStyle.Panel("HOST CONFIGURATION", "crosshair", out VBoxContainer body);
        body.AddThemeConstantOverride("separation", 8);
        col.AddChild(configPanel);

        // Header banner with START MATCH button (or READY for clients)
        string need = ReadyBlocker(match);
        if (host)
        {
            Button start = MenuStyle.Primary("START MATCH", new Vector2(0, 50), () => _hub?.StartMatch());
            start.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            start.Disabled = !match.CanHostStartMatch;
            body.AddChild(start);

            string statusMsg = need.Length == 0 ? "Everyone is ready!" : need;
            Color statusColor = need.Length == 0 ? new Color(0.35f, 0.85f, 0.4f) : MenuStyle.Accent;
            body.AddChild(MenuStyle.Text(statusMsg, 13, statusColor, MenuStyle.SemiBold, HorizontalAlignment.Center));
        }
        else
        {
            bool ready = match.LocalPlayerReady;
            Button readyBtn = MenuStyle.Primary(ready ? "NOT READY" : "READY", new Vector2(0, 50), () =>
            {
                match.SetLocalReady(!ready);
                CallDeferred(nameof(RefreshLiveSectionDeferred));
            });
            readyBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            body.AddChild(readyBtn);

            body.AddChild(MenuStyle.Text(
                ready ? "You are READY. Waiting for host to start..." : "Press READY when prepared.",
                13,
                MenuStyle.TextDim,
                MenuStyle.SemiBold,
                HorizontalAlignment.Center));
        }

        body.AddChild(new ColorRect { Color = new Color(0.25f, 0.28f, 0.32f, 0.4f), CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore });

        if (host)
        {
            // Mode & Map OptionButtons
            body.AddChild(ModeDropdown(match, () =>
            {
                match.BroadcastHostConfig();
                ShowSection(Section.Play);
            }));

            body.AddChild(MapDropdown(match, () =>
            {
                match.BroadcastHostConfig();
                ShowSection(Section.Play);
            }));

            // Duration pills (3m, 5m, 8m, 13m)
            body.AddChild(DurationRow(match, () =>
            {
                match.BroadcastHostConfig();
                ShowSection(Section.Play);
            }));

            // Max Players input
            body.AddChild(MaxPlayersRow(match, () =>
            {
                match.BroadcastHostConfig();
                ShowSection(Section.Play);
            }));

            // Player Marker toggle (No / Yes)
            body.AddChild(YesNoRow("Player Markers", match.TeamIndicators, v => match.TeamIndicators = v, () =>
            {
                match.BroadcastHostConfig();
                ShowSection(Section.Play);
            }));

            // Friendly Fire toggle (No / Yes)
            if (match.IsTeamMode)
            {
                body.AddChild(YesNoRow("Friendly Fire", match.FriendlyFire, v => match.FriendlyFire = v, () =>
                {
                    match.BroadcastHostConfig();
                    ShowSection(Section.Play);
                }));
            }

            // Enable Bots toggle (No / Yes)
            body.AddChild(YesNoRow("Enable Bots", match.BotCount > 0, v =>
            {
                match.BotCount = v ? (match.BotCount > 0 ? match.BotCount : 2) : 0;
                SyncLobbySize();
            }, () =>
            {
                match.BroadcastHostConfig();
                ShowSection(Section.Play);
            }));

            // Bots Difficulty slider (1-5) & Bot Toggles (1-5)
            var botsPanel = MenuStyle.Panel("BOT TOGGLES (1–5)", "bot", out VBoxContainer botsBody);
            botsBody.AddThemeConstantOverride("separation", 6);
            body.AddChild(botsPanel);

            botsBody.AddChild(BotDifficultySlider(match, () =>
            {
                match.BroadcastHostConfig();
                ShowSection(Section.Play);
            }));

            var toggleRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            toggleRow.AddThemeConstantOverride("separation", 4);

            bool isZero = match.BotCount == 0;
            toggleRow.AddChild(CreateBotToggleBtn("OFF", isZero, host, () =>
            {
                match.BotCount = 0;
                SyncLobbySize();
                match.BroadcastHostConfig();
                ShowSection(Section.Play);
            }));

            for (int b = 1; b <= 5; b++)
            {
                int botNum = b;
                bool isActive = match.BotCount == botNum;
                toggleRow.AddChild(CreateBotToggleBtn(botNum.ToString(), isActive, host, () =>
                {
                    match.BotCount = match.BotCount == botNum ? 0 : botNum;
                    SyncLobbySize();
                    match.BroadcastHostConfig();
                    ShowSection(Section.Play);
                }));
            }
            botsBody.AddChild(toggleRow);
        }
        else
        {
            body.AddChild(MenuStyle.Text($"Mode: {rules.DisplayName}", 14, MenuStyle.TextMain, MenuStyle.SemiBold));
            body.AddChild(MenuStyle.Text($"Map: {match.MapDisplayName}", 14, MenuStyle.TextDim));
            body.AddChild(MenuStyle.Text($"Duration: {match.TimeLimitMinutes}m", 14, MenuStyle.TextDim));
            body.AddChild(MenuStyle.Text($"Max Players: {match.LobbyPlayerCount}", 14, MenuStyle.TextDim));
            body.AddChild(MenuStyle.Text($"Bots: {(match.BotCount > 0 ? $"{match.BotCount} active" : "Disabled")}", 14, MenuStyle.TextDim));
        }
    }

    private void BuildStackedTeamsColumn(
        VBoxContainer col,
        List<(long Id, string Name, bool IsMe, bool IsHost, bool IsReady)> alberts,
        List<(long Id, string Name, bool IsMe, bool IsHost, bool IsReady)> einsteins,
        List<string> albertBots,
        List<string> einsteinBots,
        int capacity,
        bool isHost,
        MatchManager match)
    {
        bool isConvoy = match.GameMode == MatchManager.Mode.ConvoyAssault;
        string team0Title = isConvoy ? "ATTACKERS" : "TEAM ALBERT";
        string team1Title = isConvoy ? "DEFENDERS" : "TEAM EINSTEIN";

        BuildTeamColumn(col, 0, team0Title, MatchManager.TeamColor(0), alberts, albertBots, capacity, isHost, match);
        BuildTeamColumn(col, 1, team1Title, MatchManager.TeamColor(1), einsteins, einsteinBots, capacity, isHost, match);
    }

    private void BuildSocialAndInvitesPanel(VBoxContainer col)
    {
        var panel = MenuStyle.Panel("SOCIAL & INVITES", "users", out VBoxContainer body);
        body.AddThemeConstantOverride("separation", 8);
        col.AddChild(panel);

        // Discovered player invite list
        var inviteHeader = new HBoxContainer();
        inviteHeader.AddChild(MenuStyle.Text("DISCOVERED CREWS", 14, MenuStyle.Accent, MenuStyle.Bold));
        body.AddChild(inviteHeader);

        var discoveredBox = new VBoxContainer();
        discoveredBox.AddThemeConstantOverride("separation", 4);
        body.AddChild(discoveredBox);

        bool hasDiscovered = false;
        foreach (var session in LanMenu.DiscoveredSessions.Values)
        {
            hasDiscovered = true;
            var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddThemeConstantOverride("separation", 6);
            var nameLbl = MenuStyle.Text(session.Name, 13, MenuStyle.TextMain, MenuStyle.SemiBold);
            nameLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(nameLbl);

            var inviteBtn = MenuStyle.Outline("Invite to lobby", () =>
            {
                AddLog("SYSTEM", -1, $"Invite sent to {session.Name}");
            }, null, 24);
            inviteBtn.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(inviteBtn);
            discoveredBox.AddChild(row);
        }

        if (!hasDiscovered)
        {
            var emptyRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var emptyLbl = MenuStyle.Text("Scanning for nearby crews...", 13, MenuStyle.TextMuted, MenuStyle.Medium);
            emptyLbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            emptyRow.AddChild(emptyLbl);

            var inviteBtn = MenuStyle.Outline("Invite to lobby", () =>
            {
                AddLog("SYSTEM", -1, "Broadcast invite sent to nearby crews.");
            }, null, 24);
            inviteBtn.AddThemeFontSizeOverride("font_size", 11);
            emptyRow.AddChild(inviteBtn);
            discoveredBox.AddChild(emptyRow);
        }

        // Lobby chat box with "Send message..." input bar
        var chatHeader = new HBoxContainer();
        chatHeader.AddChild(MenuStyle.Text("LOBBY CHAT", 14, MenuStyle.Accent, MenuStyle.Bold));
        body.AddChild(chatHeader);

        var chatBox = new PanelContainer { CustomMinimumSize = new Vector2(0, 70), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var chatStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.04f, 0.05f, 0.06f, 0.8f),
            BorderColor = new Color(0.2f, 0.22f, 0.25f, 0.5f),
        };
        chatStyle.SetBorderWidthAll(1);
        chatStyle.SetCornerRadiusAll(4);
        chatStyle.ContentMarginLeft = 6;
        chatStyle.ContentMarginRight = 6;
        chatStyle.ContentMarginTop = 4;
        chatStyle.ContentMarginBottom = 4;
        chatBox.AddThemeStyleboxOverride("panel", chatStyle);

        var chatScroll = new ScrollContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var chatMessages = new VBoxContainer();
        chatMessages.AddChild(MenuStyle.Text("Lobby chat ready. Press Enter or type below:", 12, MenuStyle.TextMuted, MenuStyle.Medium));
        chatScroll.AddChild(chatMessages);
        chatBox.AddChild(chatScroll);
        body.AddChild(chatBox);

        var chatInputRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        chatInputRow.AddThemeConstantOverride("separation", 6);
        var chatInput = new LineEdit
        {
            PlaceholderText = "Send message...",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 32),
            MaxLength = 140,
        };
        MenuStyle.StyleLineEdit(chatInput);
        chatInput.TextSubmitted += text =>
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _hub?.SendChat(text.Trim());
            chatInput.Text = string.Empty;
        };
        chatInputRow.AddChild(chatInput);

        var sendBtn = MenuStyle.Primary("Send", new Vector2(50, 32), () =>
        {
            if (string.IsNullOrWhiteSpace(chatInput.Text)) return;
            _hub?.SendChat(chatInput.Text.Trim());
            chatInput.Text = string.Empty;
        });
        chatInputRow.AddChild(sendBtn);
        body.AddChild(chatInputRow);
    }

    private static Button CreateBotToggleBtn(string label, bool active, bool enabled, Action onPressed)
    {
        var btn = new Button
        {
            Text = label,
            CustomMinimumSize = new Vector2(0, 34),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
            Disabled = !enabled,
        };
        btn.AddThemeFontOverride("font", MenuStyle.Bold);
        btn.AddThemeFontSizeOverride("font_size", 14);

        var style = new StyleBoxFlat
        {
            BgColor = active ? MenuStyle.Accent : new Color(0.12f, 0.13f, 0.15f),
            BorderColor = active ? MenuStyle.BorderHot : MenuStyle.Hairline,
        };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(4);
        btn.AddThemeStyleboxOverride("normal", style);
        btn.AddThemeStyleboxOverride("hover", style);
        btn.AddThemeStyleboxOverride("pressed", style);
        btn.AddThemeStyleboxOverride("disabled", style);

        btn.AddThemeColorOverride("font_color", active ? Colors.White : MenuStyle.TextDim);
        btn.AddThemeColorOverride("font_hover_color", Colors.White);
        btn.AddThemeColorOverride("font_disabled_color", active ? Colors.White : MenuStyle.TextMuted);

        if (enabled) btn.Pressed += onPressed;
        return btn;
    }

    private void RefreshLiveSectionDeferred() { if (_open) ShowSection(_section); }

    private string ReadyBlocker(MatchManager match)
    {
        if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) return string.Empty;
        int humans = Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetPeers().Length + 1 : 1;
        if (humans + match.BotCount < match.ModeRules.MinimumPlayers)
            return $"Needs {match.ModeRules.MinimumPlayers} players: wait for friends or add bots.";
        if (Multiplayer.HasMultiplayerPeer())
        {
            foreach (int peer in Multiplayer.GetPeers())
                if (!match.IsPeerReady(peer)) return $"{GameUiState.PlayerName(peer)} is not ready.";
        }
        return string.Empty;
    }

    // ---- JOIN (Connected to LanMenu automatic hotspot session detection) ----------------------

    private void BuildJoin()
    {
        var column = new VBoxContainer();
        ContentFrame(column);
        column.AddThemeConstantOverride("separation", 12);
        _content.AddChild(column);

        var panel = MenuStyle.Panel("CUSTOM GAME DISCOVERY", "wifi", out VBoxContainer body);
        panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        column.AddChild(panel);

        _joinStatus = MenuStyle.Text("Searching for Wi-Fi and mobile hotspot games...", 17, MenuStyle.TextDim, MenuStyle.Medium);
        body.AddChild(_joinStatus);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        body.AddChild(scroll);
        _gamesList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _gamesList.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_gamesList);

        var manual = new HBoxContainer();
        manual.AddThemeConstantOverride("separation", 10);
        var address = new LineEdit { PlaceholderText = "or type the host's IP address (e.g. 192.168.43.1)", Text = _manualAddress, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        MenuStyle.StyleLineEdit(address);
        address.TextChanged += text => _manualAddress = text;
        manual.AddChild(address);
        manual.AddChild(MenuStyle.Outline("CONNECT", () => { if (!string.IsNullOrWhiteSpace(address.Text)) JoinGame(address.Text); }, null, 46));
        column.AddChild(manual);

        StartListening();
        RefreshGames();
    }

    private void StartListening()
    {
        StopListening();
        LanMenu.HotspotSessionsUpdated += OnHotspotSessionsUpdated;
        LanMenu.StartHotspotDiscovery();
    }

    private void StopListening()
    {
        LanMenu.HotspotSessionsUpdated -= OnHotspotSessionsUpdated;
        LanMenu.StopHotspotDiscovery();
    }

    private void OnHotspotSessionsUpdated()
    {
        CallDeferred(nameof(RefreshGamesDeferred));
    }

    private void RefreshGamesDeferred()
    {
        if (_open && _section == Section.Play && _playStep == PlayStep.Join) RefreshGames();
    }

    private void PollDiscovery(double delta)
    {
        LanMenu.PollHotspotDiscovery(delta);
    }

    private void RefreshGames()
    {
        if (_gamesList == null || !IsInstanceValid(_gamesList)) return;
        foreach (Node child in _gamesList.GetChildren()) { _gamesList.RemoveChild(child); child.QueueFree(); }

        var sessions = LanMenu.DiscoveredSessions.Values;
        int count = LanMenu.DiscoveredSessions.Count;
        if (_joinStatus != null)
            _joinStatus.Text = count == 0 ? "Listening for games on Wi-Fi and mobile hotspot..." : $"{count} game(s) detected.";

        // Highlight hotspot session if detected
        foreach (var game in sessions)
        {
            if (game.IsHotspot)
            {
                var alert = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                var alertStyle = new StyleBoxFlat
                {
                    BgColor = new Color(0.15f, 0.10f, 0.02f, 0.9f),
                    BorderColor = new Color(1.0f, 0.75f, 0.2f),
                };
                alertStyle.SetBorderWidthAll(1);
                alertStyle.SetCornerRadiusAll(4);
                alertStyle.ContentMarginLeft = 12;
                alertStyle.ContentMarginRight = 12;
                alertStyle.ContentMarginTop = 8;
                alertStyle.ContentMarginBottom = 8;
                alert.AddThemeStyleboxOverride("panel", alertStyle);

                var aRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                aRow.AddThemeConstantOverride("separation", 12);
                aRow.AddChild(MenuStyle.Icon("wifi", 22, 22, new Color(1.0f, 0.75f, 0.2f)));

                var textCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                textCol.AddThemeConstantOverride("separation", -2);
                textCol.AddChild(MenuStyle.Text("HOTSPOT SESSION DETECTED", 16, new Color(1.0f, 0.75f, 0.2f), MenuStyle.Bold));
                textCol.AddChild(MenuStyle.Text($"{game.Name}  ·  {game.Mode}  ·  {game.Host}:{game.Port}", 14, MenuStyle.TextDim));
                aRow.AddChild(textCol);

                aRow.AddChild(MenuStyle.Primary("CONNECT", new Vector2(130, 36), () => JoinGame(game.Host, game.Port)));
                alert.AddChild(aRow);
                _gamesList.AddChild(alert);
            }
        }

        foreach (var game in sessions)
        {
            string host = game.Host;
            int port = game.Port;
            string portSuffix = port != LanSession.GamePort ? $":{port}" : "";
            string hotspotTag = game.IsHotspot ? " [HOTSPOT]" : "";
            _gamesList.AddChild(MenuStyle.Row(game.Name + hotspotTag, $"{game.Mode}  |  {game.Map}  |  {host}{portSuffix}  |  {game.Players} in room", () => JoinGame(host, port)));
        }
    }

    private void JoinGame(string address, int port = LanSession.GamePort)
    {
        _playStep = PlayStep.Home;
        if (_hub != null && _hub.JoinSession(address, port))
        {
            if (_joinStatus != null) _joinStatus.Text = $"Connecting to {address}...";
            Close();
        }
        else if (_joinStatus != null) _joinStatus.Text = StatusLine;
    }

    // -------------------------------------------------------------------------------------------
    // OPERATOR / STATUS / CHAT
    // -------------------------------------------------------------------------------------------

    private void BuildLoadout()
    {
        var loadoutScene = ResourceLoader.Load<PackedScene>("res://scenes/ui/LoadoutAttachmentTab.tscn");
        Control loadoutView = loadoutScene != null ? loadoutScene.Instantiate<Control>() : new LoadoutAttachmentTab();
        loadoutView.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        loadoutView.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _content.AddChild(ContentFrame(loadoutView));
    }

    private void BuildOperator()
    {
        var host = new Control { ClipContents = true };
        ContentFrame(host);
        _content.AddChild(host);

        var siegeSelect = new SiegeOperatorSelect();
        siegeSelect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        host.AddChild(siegeSelect);
    }

    private void BuildStatus(VBoxContainer column)
    {
        var panel = MenuStyle.Panel("IN THE ROOM", "users", out VBoxContainer body);
        FillPlayerRows(body);
        column.AddChild(panel);
        if (!InSession)
            column.AddChild(MenuStyle.Wrapped("You are alone in the warehouse. Host a game or join one to meet other players.", 18, MenuStyle.TextMuted));
    }

    private void FillPlayerRows(VBoxContainer body)
    {
        MatchManager match = MatchManager.Instance;
        long[] ids = match.LobbyPeerIds();
        if (ids.Length == 0) ids = new long[] { Multiplayer.GetUniqueId() };
        foreach (long id in ids)
        {
            bool ready = id == 1 || match.IsPeerReady(id);
            int team = match.IsTeamMode ? match.GetTeam(id) : -1; // crews also show their side
            string who = GameUiState.PlayerName(id) + (id == Multiplayer.GetUniqueId() ? "  (you)" : "") + (id == 1 ? "  -  host" : "");
            // The host is shown ready, as in the lobby: only the people who have to press READY matter.
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            row.AddChild(MenuStyle.Text(ready ? "READY" : "NOT READY", 15, ready ? new Color(0.35f, 0.85f, 0.4f) : new Color(0.95f, 0.3f, 0.25f), MenuStyle.Bold));
            row.AddChild(MenuStyle.Text(who, 20, MenuStyle.TextMain, MenuStyle.SemiBold));
            if (team >= 0)
                row.AddChild(MenuStyle.Chip(MatchManager.TeamName(team), team == 0 ? new Color(0.3f, 0.6f, 1f) : new Color(1f, 0.55f, 0.2f), 13, false));
            body.AddChild(row);
        }
        if (match.BotCount > 0) body.AddChild(MenuStyle.Text($"+ {match.BotCount} bot(s) when the match starts", 16, MenuStyle.TextMuted, MenuStyle.Medium));
    }

    private void BuildChat()
    {
        var column = new VBoxContainer();
        ContentFrame(column);
        column.AddThemeConstantOverride("separation", 10);
        _content.AddChild(column);
        MatchManager match = MatchManager.Instance;
        string channel = HubController.SplitRooms(match) && InSession ? $"{MatchManager.TeamName(match.GetTeam(Multiplayer.GetUniqueId()))} only" : "everyone in the room";
        column.AddChild(MenuStyle.Text($"Messages go to: {channel}", 16, MenuStyle.TextMuted, MenuStyle.SemiBold));

        _chatLog = new RichTextLabel { BbcodeEnabled = true, ScrollFollowing = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill, FitContent = false };
        _chatLog.AddThemeFontOverride("normal_font", MenuStyle.Medium);
        _chatLog.AddThemeFontSizeOverride("normal_font_size", 18);
        column.AddChild(_chatLog);
        foreach (var line in _log) AppendLogLine(_chatLog, line.Sender, line.Team, line.Text);

        var input = new LineEdit { PlaceholderText = "Type a message and press Enter", MaxLength = 140 };
        MenuStyle.StyleLineEdit(input);
        input.TextSubmitted += text => { _hub?.SendChat(text); input.Text = string.Empty; input.GrabFocus(); };
        column.AddChild(input);
    }

    // -------------------------------------------------------------------------------------------
    // Chat feed (always on screen) and quick message
    // -------------------------------------------------------------------------------------------

    private void BuildChatFeed()
    {
        var feedRoot = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        MenuStyle.Fill(feedRoot);
        AddChild(feedRoot);
        _feedRoot = feedRoot;
        _feed = MenuStyle.Text("", 18, MenuStyle.TextMain, MenuStyle.Medium);
        _feed.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        MenuStyle.Place(_feed, 0, 1, 0, 1, 24, -250, 560, -64);
        _feed.VerticalAlignment = VerticalAlignment.Bottom;
        feedRoot.AddChild(_feed);

        _quickChat = new LineEdit { PlaceholderText = "Message (Enter to send, Esc to cancel)", MaxLength = 140, Visible = false };
        MenuStyle.StyleLineEdit(_quickChat);
        MenuStyle.Place(_quickChat, 0, 1, 0, 1, 24, -56, 560, -16);
        feedRoot.AddChild(_quickChat);


    }

    private void BeginQuickChat()
    {
        if (_quickChat == null || !InSession) { AddLog("", -1, "Chat opens once you host or join a lobby."); return; }
        _typing = true;
        GameUiState.SetMenuOpen(true);
        _quickChat.Text = string.Empty;
        _quickChat.Visible = true;
        _quickChat.GrabFocus();
    }

    private void EndQuickChat(bool send)
    {
        if (_quickChat == null) return;
        string text = _quickChat.Text;
        _quickChat.Visible = false;
        _quickChat.ReleaseFocus();
        _typing = false;
        GameUiState.SetMenuOpen(false);
        if (send) _hub?.SendChat(text);
    }

    private void OnChat(string sender, int team, string text) => AddLog(sender, team, text);

    private void AddLog(string sender, int team, string text)
    {
        _log.Add((sender, team, text));
        if (_log.Count > 80) _log.RemoveAt(0);
        if (_feed != null)
        {
            var sb = new StringBuilder();
            int start = Math.Max(0, _log.Count - MaxFeedLines);
            for (int i = start; i < _log.Count; i++)
                sb.AppendLine(string.IsNullOrEmpty(_log[i].Sender) ? _log[i].Text : $"{_log[i].Sender}: {_log[i].Text}");
            _feed.Text = sb.ToString().TrimEnd();
        }
        if (_chatLog != null && IsInstanceValid(_chatLog)) AppendLogLine(_chatLog, sender, team, text);
    }

    private static void AppendLogLine(RichTextLabel label, string sender, int team, string text)
    {
        if (string.IsNullOrEmpty(sender)) { label.AppendText($"[color=#9a9a9a]{Escape(text)}[/color]\n"); return; }
        string color = team == 0 ? "#6aa8ff" : team == 1 ? "#ff9a40" : "#e8e8e8";
        label.AppendText($"[color={color}]{Escape(sender)}[/color]: {Escape(text)}\n");
    }

    private static string Escape(string text) => text.Replace("[", "[lb]");

    // ---- quit confirmation (Esc in the hub) ----------------------------------------------------
    private Control? _quitConfirm;

    private Button? _quitStay;

    private void ShowQuitConfirm()
    {
        if (_quitConfirm == null) BuildQuitConfirm();
        // The question needs a pointer. The mouse used to stay captured (hidden, locked to the middle of the
        // window) and the player kept walking and looking underneath, so the buttons could barely be hit.
        GameUiState.SetMenuOpen(true);
        if (!OS.HasFeature("mobile")) Input.MouseMode = Input.MouseModeEnum.Visible;
        _quitConfirm!.Visible = true;
        _quitStay?.GrabFocus(); // the safe answer is the default one (keyboard / gamepad)
    }

    private void HideQuitConfirm()
    {
        if (_quitConfirm != null) _quitConfirm.Visible = false;
        // back to where we were: still on the tablet (pointer free) or playing (pointer captured)
        GameUiState.SetMenuOpen(_open);
        if (!_open && !OS.HasFeature("mobile")) Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    private void BuildQuitConfirm()
    {
        var overlay = new ColorRect { Color = new Color(0f, 0f, 0f, 0.78f), MouseFilter = Control.MouseFilterEnum.Stop };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var panel = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            LayoutMode = 1,
        };
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.AddThemeConstantOverride("separation", 18);
        var question = new Label { Text = "QUIT WRAITH RUN?", HorizontalAlignment = HorizontalAlignment.Center };
        question.AddThemeFontOverride("font", MenuStyle.Bold);
        question.AddThemeFontSizeOverride("font_size", 34);
        panel.AddChild(question);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 24);
        row.AddChild(MenuStyle.Card("power", "QUIT", null, new Vector2(200, 54), () => GameSession.QuitToDesktop(GetTree())));
        Button stay = MenuStyle.Card("arrow-left", "STAY", null, new Vector2(200, 54), HideQuitConfirm);
        row.AddChild(stay);
        _quitStay = stay;
        panel.AddChild(row);
        overlay.AddChild(panel);
        overlay.Visible = false;
        // its own layer, above the match HUD, scoreboard and loading layers, so nothing can sit on top of the buttons
        var layer = new CanvasLayer { Name = "QuitConfirmLayer", Layer = 90 };
        layer.AddChild(overlay);
        AddChild(layer);
        _quitConfirm = overlay;
    }
}
