using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Autoloaded tactical loading screen (scenes/ui/LoadingScreen.tscn / scripts/autoloads/LoadingScreen.cs).
/// Provides an asynchronous scene loader using ResourceLoader.LoadThreadedRequest and
/// ResourceLoader.LoadThreadedGetStatus, a smooth glowing tactical progress bar,
/// and a rotating tactical hint carousel with keybinds, game mode rules, and operator tips.
/// </summary>
public partial class LoadingScreen : CanvasLayer
{
	public static LoadingScreen Instance { get; private set; } = null!;

	private const float MinDisplaySeconds = 1.2f;
	private const float FadeSeconds = 0.35f;
	private const float TipRotateSeconds = 3.0f;

	private static readonly string[] TacticalHints =
	{
		// Controls & Keybinds
		"[CONTROLS]  W / A / S / D  to Move  ·  SHIFT  to Sprint  ·  C  to Crouch  ·  SPACE  to Jump",
		"[COMBAT]  RMB  to Aim Down Sights (ADS)  ·  LMB  to Fire  ·  R  to Reload  ·  1 / 2 / 3  to Swap Weapons",
		"[INTERFACE]  TAB / ESC  to pull out Tactical Tablet  ·  M  for Match Overview  ·  ENTER  for Quick Chat",

		// Game Modes
		"[TEAM DEATHMATCH]  Albert Team vs. Einstein Team · Style bonuses (+150 Headshot, +125 Hip-Fire, +175 360 No-Scope)",
		"[DEATHMATCH]  Classic Free-For-All arena · Zero storms or gimmicks · Operators fight strictly for personal score",
		"[CONVOY ASSAULT]  Up to 9v9 STV extraction corridor · 7 rounds (first to 4 wins) · Teams swap sides every round",
		"[PURE CHASE]  2v2 Duo Buggy combat race · Watch for 8-second visual omen warnings before Cursed Land Events strike",

		// Operator Classes
		"[DRIVERS]  Apex, Shift, Ghost, Tread excel at high-speed maneuvers, collision resilience, and tire durability",
		"[SHOOTERS]  Viper, Rage, Spark, Flash maximize weapon swap speed, magazine capacity, and vehicular firing stability",
		"[MEDICS]  Pulse, Patch, Stim, Anchor deliver rapid revivals, on-foot field vehicle repairs, and guardian shields",

		// Tactical Tips
		"[TACTICAL]  Aim Down Sights eliminates weapon sway for pinpoint accuracy across medium and long ranges",
		"[TACTICAL]  The Bazooka destroys enemy vehicles in a single direct hit; sidearms provide rapid backup in close quarters"
	};

	private Control _root = null!;
	private ProgressBar _progressBar = null!;
	private Label _progressLabel = null!;
	private Label _statusLabel = null!;
	private Label _hintLabel = null!;
	private Timer _hintTimer = null!;
	private Tween? _fadeTween;
	private Tween? _hintTween;

	private ulong _shownAtMsec;
	private bool _isOverlayVisible;
	private int _currentHintIndex = -1;
	private readonly RandomNumberGenerator _rng = new();

	// Async Loading State
	private string? _pendingScenePath;
	private Action? _onLoadComplete;
	private bool _isLoadingAsync;
	private float _displayProgress;
	private float _targetProgress;

	public override void _Ready()
	{
		Instance = this;
		Layer = 100;
		ProcessMode = ProcessModeEnum.Always;
		_rng.Randomize();
		BuildTacticalLayout();
		SetOverlayVisible(false, immediate: true);
		SetProcess(false);
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;

		if (_isLoadingAsync && !string.IsNullOrEmpty(_pendingScenePath))
		{
			var progressArray = new Godot.Collections.Array();
			ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(_pendingScenePath, progressArray);

			if (progressArray.Count > 0)
			{
				float loadedFraction = (float)progressArray[0];
				_targetProgress = Mathf.Max(_targetProgress, loadedFraction * 100.0f);
			}

			// Smooth progress bar advancement
			_displayProgress = Mathf.MoveToward(_displayProgress, _targetProgress, dt * 120.0f);
			UpdateProgressBar(_displayProgress);

			if (status == ResourceLoader.ThreadLoadStatus.Loaded && _displayProgress >= 99.0f)
			{
				_isLoadingAsync = false;
				SetProcess(false);
				FinalizeAsyncLoad();
			}
			else if (status == ResourceLoader.ThreadLoadStatus.Failed || status == ResourceLoader.ThreadLoadStatus.InvalidResource)
			{
				_isLoadingAsync = false;
				SetProcess(false);
				GD.PushError($"LoadingScreen: Failed to load scene asynchronously '{_pendingScenePath}'. Status: {status}");
				FinishHide();
			}
		}
		else if (_displayProgress < _targetProgress)
		{
			_displayProgress = Mathf.MoveToward(_displayProgress, _targetProgress, dt * 80.0f);
			UpdateProgressBar(_displayProgress);
		}
	}

	/// <summary>
	/// Initiates asynchronous threaded scene loading with animated tactical loading screen.
	/// </summary>
	public void LoadSceneAsync(string scenePath, Action? onLoaded = null)
	{
		_pendingScenePath = scenePath;
		_onLoadComplete = onLoaded;
		_isLoadingAsync = true;
		_displayProgress = 0.0f;
		_targetProgress = 5.0f;

		ShowLoadingInternal("PREPARING BATTLEFIELD...", "LOADING ASSETS");

		Error err = ResourceLoader.LoadThreadedRequest(scenePath, "", useSubThreads: true);
		if (err != Error.Ok)
		{
			GD.PushError($"LoadingScreen: LoadThreadedRequest failed for '{scenePath}', error={err}");
			_isLoadingAsync = false;
			GetTree().ChangeSceneToFile(scenePath);
			FinishHide();
			return;
		}

		SetProcess(true);
	}

	private void FinalizeAsyncLoad()
	{
		if (string.IsNullOrEmpty(_pendingScenePath)) return;

		var packedScene = ResourceLoader.LoadThreadedGet(_pendingScenePath) as PackedScene;
		if (packedScene != null)
		{
			GetTree().ChangeSceneToPacked(packedScene);
			_onLoadComplete?.Invoke();
		}
		else
		{
			GetTree().ChangeSceneToFile(_pendingScenePath);
			_onLoadComplete?.Invoke();
		}

		_pendingScenePath = null;
		_onLoadComplete = null;
		Hide();
	}

	/// <summary>Synchronous/manual show trigger for backward compatibility.</summary>
	public new void Show()
	{
		_isLoadingAsync = false;
		_displayProgress = 0.0f;
		_targetProgress = 92.0f;
		ShowLoadingInternal("DEPLOYING OPERATORS...", "INITIALIZING SQUAD");
		SetProcess(true);
	}

	/// <summary>Hides the overlay with minimum display guarantee and smooth fade.</summary>
	public new void Hide()
	{
		if (!_isOverlayVisible) return;
		float elapsed = (Time.GetTicksMsec() - _shownAtMsec) / 1000.0f;
		float remaining = Mathf.Max(0.0f, MinDisplaySeconds - elapsed);
		if (remaining <= 0.0f)
		{
			FinishHide();
			return;
		}
		var timer = GetTree().CreateTimer(remaining);
		timer.Timeout += FinishHide;
	}

	private void ShowLoadingInternal(string statusText, string subStatus)
	{
		_shownAtMsec = Time.GetTicksMsec();
		_statusLabel.Text = statusText;
		_currentHintIndex = -1;
		RollTacticalHint(immediate: true);
		_hintTimer.Start(TipRotateSeconds);
		SetOverlayVisible(true, immediate: true);
		UpdateProgressBar(_displayProgress);
	}

	private void FinishHide()
	{
		if (!_isOverlayVisible) return;
		_hintTimer.Stop();
		_targetProgress = 100.0f;
		_displayProgress = 100.0f;
		UpdateProgressBar(100.0f);

		_fadeTween?.Kill();
		_fadeTween = CreateTween();
		_fadeTween.TweenProperty(_root, "modulate:a", 0.0, FadeSeconds).SetTrans(Tween.TransitionType.Sine);
		_fadeTween.TweenCallback(Callable.From(() =>
		{
			SetOverlayVisible(false, immediate: true);
			SetProcess(false);
		}));
	}

	private void SetOverlayVisible(bool visible, bool immediate)
	{
		_isOverlayVisible = visible;
		Visible = visible;
		if (immediate && visible) _root.Modulate = new Color(1, 1, 1, 1);
	}

	private void UpdateProgressBar(float value)
	{
		if (_progressBar != null) _progressBar.Value = Mathf.Clamp(value, 0.0f, 100.0f);
		if (_progressLabel != null) _progressLabel.Text = $"{Mathf.RoundToInt(value)}%";
	}

	private void RollTacticalHint(bool immediate = false)
	{
		if (TacticalHints.Length == 0) return;
		int next = _rng.RandiRange(0, TacticalHints.Length - 1);
		if (next == _currentHintIndex) next = (next + 1) % TacticalHints.Length;
		_currentHintIndex = next;

		if (immediate)
		{
			_hintLabel.Text = TacticalHints[_currentHintIndex];
			_hintLabel.Modulate = new Color(1, 1, 1, 1);
			return;
		}

		_hintTween?.Kill();
		_hintTween = CreateTween();
		_hintTween.TweenProperty(_hintLabel, "modulate:a", 0.0, 0.2);
		_hintTween.TweenCallback(Callable.From(() =>
		{
			_hintLabel.Text = TacticalHints[_currentHintIndex];
		}));
		_hintTween.TweenProperty(_hintLabel, "modulate:a", 1.0, 0.25);
	}

	private void BuildTacticalLayout()
	{
		_root = new Control { Name = "TacticalLoadingRoot" };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(_root);

		// Tactical dark background
		var darkBg = new ColorRect
		{
			Name = "DarkTacticalBg",
			Color = new Color(0.045f, 0.052f, 0.06f, 1.0f)
		};
		darkBg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(darkBg);

		// Subtle tactical vignette/scrim overlay
		var vignette = new ColorRect
		{
			Name = "TacticalVignette",
			Color = new Color(0.015f, 0.02f, 0.025f, 0.6f)
		};
		vignette.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(vignette);

		// Tactical center brand element
		var centerBrand = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		centerBrand.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		centerBrand.Position = new Vector2(0, -60);
		_root.AddChild(centerBrand);

		var brandBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
		brandBox.AddThemeConstantOverride("separation", 6);
		centerBrand.AddChild(brandBox);

		var titleLabel = new Label
		{
			Text = "WRAITH RUN",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		titleLabel.AddThemeFontOverride("font", MenuStyle.Bold);
		titleLabel.AddThemeFontSizeOverride("font_size", 42);
		titleLabel.AddThemeColorOverride("font_color", new Color(0.96f, 0.97f, 1.0f));
		titleLabel.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.75f, 0.95f, 0.7f));
		titleLabel.AddThemeConstantOverride("outline_size", 8);
		brandBox.AddChild(titleLabel);

		var subTitleLabel = new Label
		{
			Text = "TACTICAL EXTRACTION COMBAT",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		subTitleLabel.AddThemeFontOverride("font", MenuStyle.SemiBold);
		subTitleLabel.AddThemeFontSizeOverride("font_size", 14);
		subTitleLabel.AddThemeColorOverride("font_color", new Color(0.45f, 0.75f, 0.95f, 0.9f));
		brandBox.AddChild(subTitleLabel);

		// Bottom Tactical HUD Card for Progress & Hints
		var bottomContainer = new VBoxContainer
		{
			Name = "BottomTacticalCard",
			Alignment = BoxContainer.AlignmentMode.Center
		};
		bottomContainer.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
		bottomContainer.OffsetTop = -180;
		bottomContainer.OffsetBottom = -32;
		bottomContainer.OffsetLeft = 64;
		bottomContainer.OffsetRight = -64;
		bottomContainer.AddThemeConstantOverride("separation", 10);
		_root.AddChild(bottomContainer);

		// Status Row: "PREPARING BATTLEFIELD..." + "92%"
		var statusRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		bottomContainer.AddChild(statusRow);

		_statusLabel = new Label
		{
			Name = "StatusLabel",
			Text = "PREPARING BATTLEFIELD...",
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		_statusLabel.AddThemeFontOverride("font", MenuStyle.Bold);
		_statusLabel.AddThemeFontSizeOverride("font_size", 16);
		_statusLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.92f, 0.96f));
		statusRow.AddChild(_statusLabel);

		_progressLabel = new Label
		{
			Name = "ProgressLabel",
			Text = "0%",
			HorizontalAlignment = HorizontalAlignment.Right
		};
		_progressLabel.AddThemeFontOverride("font", MenuStyle.Bold);
		_progressLabel.AddThemeFontSizeOverride("font_size", 16);
		_progressLabel.AddThemeColorOverride("font_color", new Color(0.0f, 0.85f, 1.0f));
		statusRow.AddChild(_progressLabel);

		// Glowing Tactical Progress Bar
		_progressBar = new ProgressBar
		{
			Name = "ProgressBar",
			MinValue = 0,
			MaxValue = 100,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0, 10)
		};
		var barBg = new StyleBoxFlat
		{
			BgColor = new Color(0.08f, 0.11f, 0.14f, 0.9f),
			BorderColor = new Color(0.2f, 0.28f, 0.35f, 0.6f),
			BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
			CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3, CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3
		};
		var barFill = new StyleBoxFlat
		{
			BgColor = new Color(0.0f, 0.78f, 1.0f, 1.0f),
			ShadowColor = new Color(0.0f, 0.85f, 1.0f, 0.55f),
			ShadowSize = 6,
			CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3, CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3
		};
		_progressBar.AddThemeStyleboxOverride("background", barBg);
		_progressBar.AddThemeStyleboxOverride("fill", barFill);
		bottomContainer.AddChild(_progressBar);

		// Tactical Rotating Hint Carousel
		var hintPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
		var hintStyle = new StyleBoxFlat
		{
			BgColor = new Color(0.06f, 0.08f, 0.10f, 0.85f),
			BorderColor = new Color(0.18f, 0.24f, 0.30f, 0.5f),
			BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
			CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4
		};
		hintPanel.AddThemeStyleboxOverride("panel", hintStyle);
		bottomContainer.AddChild(hintPanel);

		var hintMargin = new MarginContainer();
		hintMargin.AddThemeConstantOverride("margin_left", 14);
		hintMargin.AddThemeConstantOverride("margin_right", 14);
		hintMargin.AddThemeConstantOverride("margin_top", 8);
		hintMargin.AddThemeConstantOverride("margin_bottom", 8);
		hintPanel.AddChild(hintMargin);

		_hintLabel = new Label
		{
			Name = "HintLabel",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		_hintLabel.AddThemeFontOverride("font", MenuStyle.Medium);
		_hintLabel.AddThemeFontSizeOverride("font_size", 13);
		_hintLabel.AddThemeColorOverride("font_color", new Color(0.78f, 0.84f, 0.90f));
		hintMargin.AddChild(_hintLabel);

		// Hint rotation timer
		_hintTimer = new Timer { Name = "HintTimer", OneShot = false, WaitTime = TipRotateSeconds };
		_hintTimer.Timeout += () => RollTacticalHint(immediate: false);
		AddChild(_hintTimer);
	}
}
