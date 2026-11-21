using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// LoadoutAttachmentTab (Master GDD Section 6.2 Tab 2 & Section 7 Architecture):
/// 3D Gunsmith node configurator and 8-stat live bar comparison interface.
/// Allows players to customize Optics (with 45° canted optic toggle), Muzzle, Underbarrel,
/// Laser/Flashlight, Magazine, and Stock while tracking live bars for all 8 combat attributes.
/// </summary>
public partial class LoadoutAttachmentTab : Control
{
    private OptionButton _weaponSelector = null!;
    private OptionButton _opticsSlot = null!;
    private OptionButton _muzzleSlot = null!;
    private OptionButton _underbarrelSlot = null!;
    private OptionButton _laserSlot = null!;
    private OptionButton _magazineSlot = null!;
    private OptionButton _stockSlot = null!;
    private CheckBox _cantedSightToggle = null!;

    // 8 Stat Progress Bars & Labels
    private ProgressBar _adsBar = null!;
    private ProgressBar _switchBar = null!;
    private ProgressBar _reloadBar = null!;
    private ProgressBar _vertRecoilBar = null!;
    private ProgressBar _horizRecoilBar = null!;
    private ProgressBar _spreadBar = null!;
    private ProgressBar _visualKickBar = null!;
    private ProgressBar _ammoCapacityBar = null!;

    private Label _adsValLabel = null!;
    private Label _switchValLabel = null!;
    private Label _reloadValLabel = null!;
    private Label _vertRecoilValLabel = null!;
    private Label _horizRecoilValLabel = null!;
    private Label _spreadValLabel = null!;
    private Label _visualKickValLabel = null!;
    private Label _ammoCapacityValLabel = null!;

    public static readonly string[] WeaponRoster =
    {
        "AK-47", "M4", "P90", "Kriss Vector", "Glock 19", "FN 502", "Desert Eagle", "Shotgun", "Sledge Hammer", "Bazooka"
    };

    public string SelectedWeapon => WeaponRoster[Math.Clamp(_weaponSelector.Selected, 0, WeaponRoster.Length - 1)];

    public override void _Ready()
    {
        BuildUi();
        UpdateStatBars();
    }

    private void BuildUi()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var bg = new ColorRect { Color = new Color(0.08f, 0.09f, 0.11f, 0.95f), MouseFilter = MouseFilterEnum.Ignore };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var mainMargin = new MarginContainer();
        mainMargin.SetAnchorsPreset(LayoutPreset.FullRect);
        mainMargin.AddThemeConstantOverride("margin_left", 24);
        mainMargin.AddThemeConstantOverride("margin_right", 24);
        mainMargin.AddThemeConstantOverride("margin_top", 18);
        mainMargin.AddThemeConstantOverride("margin_bottom", 18);
        AddChild(mainMargin);

        var hBox = new HBoxContainer();
        hBox.AddThemeConstantOverride("separation", 24);
        mainMargin.AddChild(hBox);

        // LEFT COLUMN: Weapon Selector & 6 Attachment Slots + Canted Sight
        var leftCol = new VBoxContainer { CustomMinimumSize = new Vector2(380, 0) };
        leftCol.AddThemeConstantOverride("separation", 12);
        hBox.AddChild(leftCol);

        var titleLabel = new Label
        {
            Text = "GUNSMITH // ATTACHMENT CONFIGURATOR",
            ThemeTypeVariation = "HeaderLarge"
        };
        titleLabel.Modulate = new Color(1.0f, 0.8f, 0.2f);
        leftCol.AddChild(titleLabel);

        leftCol.AddChild(new Label { Text = "PRIMARY WEAPON SELECTION", Modulate = Colors.LightGray });
        _weaponSelector = new OptionButton();
        foreach (var w in WeaponRoster) _weaponSelector.AddItem(w);
        _weaponSelector.ItemSelected += (idx) => UpdateStatBars();
        leftCol.AddChild(_weaponSelector);

        leftCol.AddChild(new HSeparator());
        leftCol.AddChild(new Label { Text = "MODULAR ATTACHMENT NODES (6 SLOTS)", Modulate = Colors.CadetBlue });

        // Attachment slot builders
        _opticsSlot = CreateSlotDropdown(leftCol, "OPTICS", new[] { "Default Iron Sights", "Red Dot Reflex", "Holographic 1x", "ACOG 4x Scope" });
        _cantedSightToggle = new CheckBox { Text = "45° Canted Backup Optic Equipped", ButtonPressed = true };
        _cantedSightToggle.Toggled += (on) => UpdateStatBars();
        leftCol.AddChild(_cantedSightToggle);

        _muzzleSlot = CreateSlotDropdown(leftCol, "MUZZLE", new[] { "Standard Barrel", "Tactical Flash Hider", "Compensator V2", "Monolithic Suppressor" });
        _underbarrelSlot = CreateSlotDropdown(leftCol, "UNDERBARREL", new[] { "None", "Angled Foregrip", "Vertical Recoil Grip", "Ranger Foregrip" });
        _laserSlot = CreateSlotDropdown(leftCol, "LASER / FLASHLIGHT", new[] { "None", "5mW Green Laser", "Tactical Weapon Light", "IR PEQ-15 Box" });
        _magazineSlot = CreateSlotDropdown(leftCol, "MAGAZINE", new[] { "Standard Capacity", "Extended Drum Mag (+15)", "Lightweight Fast Mag (-5)" });
        _stockSlot = CreateSlotDropdown(leftCol, "STOCK", new[] { "Standard Stock", "Skeleton Lightweight Stock", "Heavy Padded Marksman Stock" });

        // RIGHT COLUMN: 8-Stat Comparison Live Bars
        var rightCol = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rightCol.AddThemeConstantOverride("separation", 14);
        hBox.AddChild(rightCol);

        var statsHeader = new Label
        {
            Text = "LIVE 8-STAT WEAPON METRICS & COMPARISON",
            ThemeTypeVariation = "HeaderMedium"
        };
        statsHeader.Modulate = new Color(0.3f, 0.7f, 1.0f);
        rightCol.AddChild(statsHeader);

        _adsBar = CreateStatRow(rightCol, "ADS SPEED", out _adsValLabel);
        _switchBar = CreateStatRow(rightCol, "SWITCH SPEED", out _switchValLabel);
        _reloadBar = CreateStatRow(rightCol, "RELOAD SPEED", out _reloadValLabel);
        _vertRecoilBar = CreateStatRow(rightCol, "VERTICAL RECOIL DAMPING", out _vertRecoilValLabel);
        _horizRecoilBar = CreateStatRow(rightCol, "HORIZONTAL RECOIL DAMPING", out _horizRecoilValLabel);
        _spreadBar = CreateStatRow(rightCol, "BULLET SPREAD TIGHTNESS", out _spreadValLabel);
        _visualKickBar = CreateStatRow(rightCol, "VISUAL KICK STABILIZATION", out _visualKickValLabel);
        _ammoCapacityBar = CreateStatRow(rightCol, "AMMO CAPACITY", out _ammoCapacityValLabel);
    }

    private OptionButton CreateSlotDropdown(VBoxContainer parent, string labelText, string[] options)
    {
        var row = new HBoxContainer();
        var lbl = new Label { Text = labelText, CustomMinimumSize = new Vector2(140, 0) };
        row.AddChild(lbl);

        var btn = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var opt in options) btn.AddItem(opt);
        btn.ItemSelected += (idx) => UpdateStatBars();
        row.AddChild(btn);
        parent.AddChild(row);
        return btn;
    }

    private ProgressBar CreateStatRow(VBoxContainer parent, string statName, out Label valLabel)
    {
        var row = new HBoxContainer();
        var lbl = new Label { Text = statName, CustomMinimumSize = new Vector2(210, 0) };
        row.AddChild(lbl);

        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 100,
            Value = 50,
            CustomMinimumSize = new Vector2(0, 22),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ShowPercentage = false
        };
        row.AddChild(bar);

        valLabel = new Label { Text = "50", CustomMinimumSize = new Vector2(60, 0), HorizontalAlignment = HorizontalAlignment.Right };
        row.AddChild(valLabel);

        parent.AddChild(row);
        return bar;
    }

    public void UpdateStatBars()
    {
        if (_weaponSelector == null || _adsBar == null) return;

        // Baseline weapon ratings
        int weaponIdx = _weaponSelector.Selected;
        float baseAds = 65.0f;
        float baseSwitch = 60.0f;
        float baseReload = 55.0f;
        float baseVert = 50.0f;
        float baseHoriz = 50.0f;
        float baseSpread = 60.0f;
        float baseKick = 55.0f;
        float baseAmmo = 30.0f;

        // Weapon adjustments
        switch (SelectedWeapon)
        {
            case "Kriss Vector":
            case "P90":
                baseAds += 15; baseSwitch += 15; baseReload += 10; baseAmmo = 50; break;
            case "Glock 19":
            case "FN 502":
            case "Desert Eagle":
                baseAds += 25; baseSwitch += 30; baseReload += 25; baseAmmo = 15; break;
            case "Shotgun":
                baseSpread -= 25; baseVert -= 15; baseAmmo = 6; break;
            case "Bazooka":
                baseAds -= 30; baseSwitch -= 30; baseReload -= 30; baseVert -= 35; baseAmmo = 1; break;
            case "Sledge Hammer":
                baseAmmo = 1; break;
        }

        // Attachment modifiers
        if (_cantedSightToggle.ButtonPressed) baseAds += 8; // Faster target acquisition
        if (_opticsSlot.Selected == 3) { baseAds -= 10; baseSpread += 15; } // Scope
        if (_muzzleSlot.Selected == 2) { baseVert += 15; baseHoriz += 12; } // Compensator
        if (_underbarrelSlot.Selected > 0) { baseVert += 10; baseKick += 12; } // Grip
        if (_magazineSlot.Selected == 1) { baseAmmo += 15; baseReload -= 10; } // Extended mag
        if (_magazineSlot.Selected == 2) { baseAmmo -= 5; baseReload += 15; } // Fast mag
        if (_stockSlot.Selected == 1) { baseAds += 10; baseSwitch += 12; } // Lightweight stock
        if (_stockSlot.Selected == 2) { baseVert += 14; baseHoriz += 10; } // Heavy stock

        SetBar(_adsBar, _adsValLabel, baseAds);
        SetBar(_switchBar, _switchValLabel, baseSwitch);
        SetBar(_reloadBar, _reloadValLabel, baseReload);
        SetBar(_vertRecoilBar, _vertRecoilValLabel, baseVert);
        SetBar(_horizRecoilBar, _horizRecoilValLabel, baseHoriz);
        SetBar(_spreadBar, _spreadValLabel, baseSpread);
        SetBar(_visualKickBar, _visualKickValLabel, baseKick);
        SetBar(_ammoCapacityBar, _ammoCapacityValLabel, baseAmmo, isCount: true);
    }

    private static void SetBar(ProgressBar bar, Label label, float value, bool isCount = false)
    {
        float clamped = Mathf.Clamp(value, 0, 100);
        bar.Value = clamped;
        label.Text = isCount ? Mathf.RoundToInt(value).ToString() : $"{Mathf.RoundToInt(clamped)}%";
        label.Modulate = clamped >= 60 ? Colors.LightGreen : (clamped >= 40 ? Colors.Gold : Colors.LightCoral);
    }
}
