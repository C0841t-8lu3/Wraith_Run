using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Read-only helpers the lobby screens share: the sorted operator roster, how to display a
/// callsign, which kit lines a system actually reads today, and each operator's default colours.
/// Plain static class - no scene node needed.
/// </summary>
public static class OperatorInfo
{
    private const string OperatorRegistryPath = "res://data/characters/CharacterRegistry.tres";
    private const string WeaponRegistryPath = "res://scripts/data/WeaponRegistry.tres";

    private static List<CharacterData>? _operators;
    private static List<WeaponData>? _weapons;

    /// <summary>Kit lines whose effect id is read by gameplay code right now. Everything else on
    /// the roster is real data waiting on a system (vehicles, garage, abilities) - the lobby lists
    /// it anyway and tags it PLANNED. Keep in step with CharacterEffectIds.cs and the
    /// GetModifier call sites (Health, PlayerMovement, WeaponSwitcher).</summary>
    private static readonly HashSet<string> WiredEffects = new()
    {
        CharacterEffectIds.OnFootMaxHealthMult,
        CharacterEffectIds.MoveSpeedMult,
        CharacterEffectIds.CrouchWalkSpeedMult,
        CharacterEffectIds.WeaponRecoilMult,
    };

    public static bool IsWired(string effectId) => WiredEffects.Contains(effectId);

    // ---- operators --------------------------------------------------------------------------

    public static IReadOnlyList<CharacterData> Operators
    {
        get
        {
            if (_operators != null) return _operators;
            _operators = new List<CharacterData>();
            var registry = ResourceLoader.Load<CharacterRegistry>(OperatorRegistryPath);
            if (registry != null)
            {
                foreach (CharacterData character in registry.Characters)
                    if (character != null) _operators.Add(character);
            }
            if (_operators.Count == 0)
            {
                _operators.AddRange(CreateDefault12Operators());
            }
            _operators.Sort((a, b) =>
            {
                int byClass = a.Class.CompareTo(b.Class);
                return byClass != 0 ? byClass : string.Compare(Callsign(a), Callsign(b), StringComparison.OrdinalIgnoreCase);
            });
            return _operators;
        }
    }

    public static IReadOnlyList<CharacterData> ConvoyRoster => Operators;

    private static List<CharacterData> CreateDefault12Operators()
    {
        return new List<CharacterData>
        {
            // 4 Drivers
            new CharacterData
            {
                CharacterId = "driver_apex",
                DisplayName = "Cole \"Apex\" Vance",
                Class = CharacterData.CharacterClass.Driver,
                SkillName = "Reinforced Chassis",
                TacticalDescription = "Reduces incoming vehicle collision damage and small-arms tire damage by 20%.",
                MeshScenePath = "res://art/characters/AshenCorvus.fbx",
                JacketColor = new Color(0.72f, 0.45f, 0.22f),
                PantsColor = new Color(0.2f, 0.2f, 0.2f),
                AccentColor = new Color(0.85f, 0.35f, 0.1f)
            },
            new CharacterData
            {
                CharacterId = "driver_shift",
                DisplayName = "Elena \"Shift\" Rostova",
                Class = CharacterData.CharacterClass.Driver,
                SkillName = "Nitrous Overdrive",
                TacticalDescription = "Grants a 25% faster cooldown and extended active duration on vehicle boost charges.",
                MeshScenePath = "res://art/characters/AshenCorvus.fbx",
                JacketColor = new Color(0.65f, 0.5f, 0.3f),
                PantsColor = new Color(0.18f, 0.18f, 0.22f),
                AccentColor = new Color(0.9f, 0.6f, 0.15f)
            },
            new CharacterData
            {
                CharacterId = "driver_ghost",
                DisplayName = "Tariq \"Ghost\" Al-Mansoor",
                Class = CharacterData.CharacterClass.Driver,
                SkillName = "Slipstream",
                TacticalDescription = "Reduces vehicle signature visibility and enemy turret/pursuer tracking duration.",
                MeshScenePath = "res://art/characters/AshenCorvus.fbx",
                JacketColor = new Color(0.4f, 0.42f, 0.45f),
                PantsColor = new Color(0.15f, 0.16f, 0.18f),
                AccentColor = new Color(0.3f, 0.7f, 0.85f)
            },
            new CharacterData
            {
                CharacterId = "driver_tread",
                DisplayName = "Marcus \"Tread\" Boone",
                Class = CharacterData.CharacterClass.Driver,
                SkillName = "Iron Rubber",
                TacticalDescription = "Improves off-road traction and prevents complete immobilization when tires are shot out.",
                MeshScenePath = "res://art/characters/AshenCorvus.fbx",
                JacketColor = new Color(0.55f, 0.38f, 0.25f),
                PantsColor = new Color(0.25f, 0.22f, 0.2f),
                AccentColor = new Color(0.75f, 0.5f, 0.2f)
            },
            // 4 Shooters
            new CharacterData
            {
                CharacterId = "shooter_viper",
                DisplayName = "Sloane \"Viper\" Kelly",
                Class = CharacterData.CharacterClass.Shooter,
                SkillName = "Precision Stabilizer",
                TacticalDescription = "Reduces weapon recoil by 15% while firing from inside a moving vehicle.",
                MeshScenePath = "res://art/characters/AshenCorvus.fbx",
                JacketColor = new Color(0.28f, 0.35f, 0.25f),
                PantsColor = new Color(0.18f, 0.2f, 0.16f),
                AccentColor = new Color(0.85f, 0.2f, 0.2f)
            },
            new CharacterData
            {
                CharacterId = "shooter_rage",
                DisplayName = "Dax \"Rage\" Holloway",
                Class = CharacterData.CharacterClass.Shooter,
                SkillName = "Hollow-Point Feed",
                TacticalDescription = "Deals 10% bonus damage against enemy vehicle armor plating and tires.",
                MeshScenePath = "res://art/characters/AshenCorvus.fbx",
                JacketColor = new Color(0.38f, 0.32f, 0.28f),
                PantsColor = new Color(0.16f, 0.15f, 0.15f),
                AccentColor = new Color(0.95f, 0.4f, 0.1f)
            },
            new CharacterData
            {
                CharacterId = "shooter_spark",
                DisplayName = "Kira \"Spark\" Chen",
                Class = CharacterData.CharacterClass.Shooter,
                SkillName = "Quick Draw",
                TacticalDescription = "Increases weapon reload and primary/secondary swap speeds by 20% in combat.",
                MeshScenePath = "res://art/characters/AshenCorvus.fbx",
                JacketColor = new Color(0.32f, 0.38f, 0.42f),
                PantsColor = new Color(0.15f, 0.18f, 0.2f),
                AccentColor = new Color(1.0f, 0.85f, 0.2f)
            },
            new CharacterData
            {
                CharacterId = "shooter_flash",
                DisplayName = "Leo \"Flash\" Sterling",
                Class = CharacterData.CharacterClass.Shooter,
                SkillName = "Overclocked Mag",
                TacticalDescription = "Increases magazine capacity across all assault rifles and SMGs by 15%.",
                MeshScenePath = "res://art/characters/AshenCorvus.fbx",
                JacketColor = new Color(0.42f, 0.4f, 0.32f),
                PantsColor = new Color(0.22f, 0.2f, 0.18f),
                AccentColor = new Color(0.9f, 0.75f, 0.25f)
            },
            // 4 Medics
            new CharacterData
            {
                CharacterId = "medic_pulse",
                DisplayName = "Maya \"Pulse\" Lin",
                Class = CharacterData.CharacterClass.Medic,
                SkillName = "Rapid Triage",
                TacticalDescription = "Cuts downed teammate revival time in half during on-foot combat.",
                MeshScenePath = "res://art/characters/AshenCorvus.fbx",
                JacketColor = new Color(0.6f, 0.65f, 0.68f),
                PantsColor = new Color(0.22f, 0.24f, 0.26f),
                AccentColor = new Color(0.9f, 0.15f, 0.2f)
            },
            new CharacterData
            {
                CharacterId = "medic_patch",
                DisplayName = "Gus \"Patch\" Gallagher",
                Class = CharacterData.CharacterClass.Medic,
                SkillName = "Field Mechanic",
                TacticalDescription = "Allows on-foot repair tools to restore small amounts of STV/Buggy HP directly in the field.",
                MeshScenePath = "res://art/characters/AshenCorvus.fbx",
                JacketColor = new Color(0.52f, 0.5f, 0.44f),
                PantsColor = new Color(0.26f, 0.24f, 0.22f),
                AccentColor = new Color(0.85f, 0.25f, 0.15f)
            },
            new CharacterData
            {
                CharacterId = "medic_stim",
                DisplayName = "Dr. Zane \"Stim\" Mercer",
                Class = CharacterData.CharacterClass.Medic,
                SkillName = "Adrenaline Boost",
                TacticalDescription = "Healing a teammate grants both operators a 5-second movement and reload speed buff.",
                MeshScenePath = "res://art/characters/AshenCorvus.fbx",
                JacketColor = new Color(0.48f, 0.55f, 0.52f),
                PantsColor = new Color(0.2f, 0.22f, 0.21f),
                AccentColor = new Color(0.2f, 0.85f, 0.45f)
            },
            new CharacterData
            {
                CharacterId = "medic_anchor",
                DisplayName = "Brody \"Anchor\" Hayes",
                Class = CharacterData.CharacterClass.Medic,
                SkillName = "Guardian Shell",
                TacticalDescription = "Grants a +20 HP personal shield buffer while standing within 5 meters of a teammate or vehicle.",
                MeshScenePath = "res://art/characters/AshenCorvus.fbx",
                JacketColor = new Color(0.45f, 0.48f, 0.52f),
                PantsColor = new Color(0.18f, 0.2f, 0.24f),
                AccentColor = new Color(0.3f, 0.6f, 0.95f)
            }
        };
    }

    public static CharacterData? Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (CharacterData character in Operators)
            if (character.CharacterId == id) return character;
        return null;
    }

    /// <summary>The nickname between the quotes: Cade "Ironsight" Kessler -> IRONSIGHT.</summary>
    public static string Callsign(CharacterData? data)
    {
        if (data == null) return "STANDARD ISSUE";
        string name = data.DisplayName;
        int open = name.IndexOf('"');
        int close = open >= 0 ? name.IndexOf('"', open + 1) : -1;
        return (open >= 0 && close > open ? name.Substring(open + 1, close - open - 1) : name).ToUpperInvariant();
    }

    /// <summary>Cade "Ironsight" Kessler -> Cade Kessler.</summary>
    public static string RealName(CharacterData? data)
    {
        if (data == null) return "No operator";
        string name = data.DisplayName;
        int open = name.IndexOf('"');
        int close = open >= 0 ? name.IndexOf('"', open + 1) : -1;
        if (open < 0 || close <= open) return name;
        return $"{name.Substring(0, open).Trim()} {name.Substring(close + 1).Trim()}".Trim();
    }

    public static string ClassName(CharacterData.CharacterClass value) => value switch
    {
        CharacterData.CharacterClass.Driver => "DRIVER",
        CharacterData.CharacterClass.Shooter => "SHOOTER",
        CharacterData.CharacterClass.Fixer => "FIXER",
        CharacterData.CharacterClass.Operator => "OPERATOR",
        CharacterData.CharacterClass.Medic => "MEDIC",
        CharacterData.CharacterClass.Demolisher => "DEMOLISHER",
        _ => "OPERATOR",
    };

    public static string ClassIcon(CharacterData.CharacterClass value) => value switch
    {
        CharacterData.CharacterClass.Driver => "class-driver",
        CharacterData.CharacterClass.Shooter => "rifle",
        CharacterData.CharacterClass.Fixer => "class-fixer",
        CharacterData.CharacterClass.Operator => "class-operator",
        CharacterData.CharacterClass.Medic => "class-medic",
        CharacterData.CharacterClass.Demolisher => "class-demolisher",
        _ => "user",
    };

    public static string ClassBlurb(CharacterData.CharacterClass value) => value switch
    {
        CharacterData.CharacterClass.Driver => "Keeps the vehicle fast, fed and alive.",
        CharacterData.CharacterClass.Shooter => "Weapons fire, combat marksmanship and vehicular firepower.",
        CharacterData.CharacterClass.Fixer => "Repairs and armours the vehicle on the move.",
        CharacterData.CharacterClass.Operator => "On-foot fighter and objective specialist.",
        CharacterData.CharacterClass.Medic => "Heals the crew and lays cover smoke.",
        CharacterData.CharacterClass.Demolisher => "Charges, blasts and road denial.",
        _ => string.Empty,
    };

    // ---- colours ----------------------------------------------------------------------------

    private static bool IsWhite(Color c) => c.R > 0.98f && c.G > 0.98f && c.B > 0.98f;

    /// <summary>Colours an operator wears before the player touches anything. The roster data
    /// has none yet (every .tres is white), so each class gets a gritty base palette, nudged per
    /// operator so twenty-five people do not look like five.</summary>
    public static void DefaultColors(CharacterData? data, out Color jacket, out Color pants, out Color accent)
    {
        if (data != null && !(IsWhite(data.JacketColor) && IsWhite(data.PantsColor) && IsWhite(data.AccentColor)))
        {
            jacket = data.JacketColor; pants = data.PantsColor; accent = data.AccentColor;
            return;
        }

        (uint j, uint p, uint a) = data?.Class switch
        {
            CharacterData.CharacterClass.Driver => (0x7a5a2eu, 0x2e2a26u, 0xc8582au),
            CharacterData.CharacterClass.Shooter => (0x3a4f3du, 0x242d25u, 0xd47a2au),
            CharacterData.CharacterClass.Fixer => (0x3d5a63u, 0x2b2f31u, 0xb59a3cu),
            CharacterData.CharacterClass.Operator => (0x4b5238u, 0x33352cu, 0x1b1b1du),
            CharacterData.CharacterClass.Medic => (0x8a8578u, 0x3a3d35u, 0xa3231du),
            CharacterData.CharacterClass.Demolisher => (0x5a2a22u, 0x232323u, 0xb59a3cu),
            _ => (0x55583fu, 0x2f312au, 0x1b1b1du),
        };

        int step = 0;
        if (data != null)
        {
            int sum = 0;
            foreach (char ch in data.CharacterId) sum += ch;
            step = (sum % 5) - 2; // -2..2, stable for a given id
        }
        jacket = Shift(Hex(j), step);
        pants = Shift(Hex(p), -step / 2);
        accent = Shift(Hex(a), step);
    }

    /// <summary>The colours to actually show: the player's own if they set any, else the defaults.</summary>
    public static void ResolveColors(CharacterData? data, out Color jacket, out Color pants, out Color accent)
    {
        string id = data?.CharacterId ?? string.Empty;
        if (PlayerProfile.TryGetColors(id, out jacket, out pants, out accent)) return;
        DefaultColors(data, out jacket, out pants, out accent);
    }

    private static Color Hex(uint rgb) => new(
        ((rgb >> 16) & 0xFF) / 255.0f, ((rgb >> 8) & 0xFF) / 255.0f, (rgb & 0xFF) / 255.0f);

    private static Color Shift(Color c, int step)
    {
        if (step == 0) return c;
        c.H = Mathf.PosMod(c.H + step * 0.02f, 1.0f);
        c.V = Mathf.Clamp(c.V * (1.0f + step * 0.09f), 0.05f, 1.0f);
        return c;
    }

    // ---- weapons ----------------------------------------------------------------------------

    public static IReadOnlyList<WeaponData> Weapons
    {
        get
        {
            if (_weapons != null) return _weapons;
            _weapons = new List<WeaponData>();
            var registry = ResourceLoader.Load<WeaponRegistry>(WeaponRegistryPath);
            if (registry == null)
            {
                GD.PushWarning($"OperatorInfo: could not load {WeaponRegistryPath}.");
                return _weapons;
            }
            foreach (WeaponData weapon in registry.Weapons)
                if (weapon != null) _weapons.Add(weapon);
            return _weapons;
        }
    }

    public static WeaponData? FindWeapon(string name)
    {
        foreach (WeaponData weapon in Weapons)
            if (weapon.WeaponName == name) return weapon;
        return null;
    }

    /// <summary>True when the in-match weapon list (WeaponSwitcher.Loadout) contains this weapon.
    /// A weapon that is not in it can be browsed and picked in the lobby, but a match starts you
    /// on the default weapon instead.</summary>
    public static bool IsInMatchLoadout(WeaponData weapon) =>
        Array.IndexOf(WeaponSwitcher.Loadout, weapon.WeaponName) >= 0;

    public static string WeaponIcon(WeaponData weapon)
    {
        string cls = weapon.WeaponClass.Replace(" ", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
        return cls switch
        {
            "assaultrifle" => "rifle",
            "smg" or "pdw" => "wpn-smg",
            "pistol" or "sidearm" => "wpn-pistol",
            "shotgun" => "wpn-shotgun",
            "melee" => "wpn-melee",
            "antivehicle" or "launcher" => "wpn-launcher",
            _ => "rifle",
        };
    }

    public static string FireModeName(WeaponData weapon) => weapon.FireMode switch
    {
        WeaponData.FireModeType.FullAuto => "FULL AUTO",
        WeaponData.FireModeType.SemiAuto => "SEMI AUTO",
        _ => string.Empty,
    };
}
