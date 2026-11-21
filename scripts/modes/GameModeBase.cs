using Godot;

/// <summary>Shared, data-driven contract for every selectable match mode.</summary>
public abstract partial class GameModeBase : RefCounted
{
    public abstract MatchManager.Mode Id { get; }
    public abstract string DisplayName { get; }
    public abstract string LobbyDescription { get; }
    public virtual string PlayerFormat => UsesTeams ? $"2 teams · {RecommendedTeamSize} players per squad" : "Free for all";
    public abstract int BaseKillPoints { get; }
    public virtual bool UsesTeams => false;
    public virtual bool UsesVehicles => true;
    public virtual bool UsesCursedEvents => false;
    public virtual bool HasTimedResolution => false;
    public virtual bool UsesUniversalKillStyles => false;
    public virtual int RecommendedTeamSize => 2;
    public virtual int MinimumPlayers => 2;
    public virtual int MaximumPlayers => 16;

    public virtual BaseModeController CreateModeController() => new TeamDeathmatchController();

    public virtual int CalculateKillPoints(long killerPeerId, long victimPeerId, KillStyle styles, int basePoints, bool isDriver, bool isShooter)
    {
        if (UsesUniversalKillStyles)
        {
            return KillStyleBonus.Evaluate(basePoints > 0 ? basePoints : BaseKillPoints, styles);
        }
        return BaseKillPoints;
    }

    public virtual int CalculateAssistPoints(int baseAssist) => UsesUniversalKillStyles ? baseAssist : 0;

    public static GameModeBase For(MatchManager.Mode mode) => mode switch
    {
        MatchManager.Mode.TeamDeathmatch => new TeamDeathmatchMode(),
        MatchManager.Mode.Deathmatch => new DeathmatchMode(),
        MatchManager.Mode.ConvoyAssault => new ConvoyAssaultMode(),
        MatchManager.Mode.PureChase => new PureChaseMode(),
        _ => new TeamDeathmatchMode(),
    };
}

/// <summary>
/// Mode 2: Deathmatch (Free-For-All) — Pure arena combat, no storms or cursed zones (Master GDD Section 2.2).
/// Universal Kill-Style Bonuses apply.
/// </summary>
public partial class DeathmatchMode : GameModeBase
{
    public override MatchManager.Mode Id => MatchManager.Mode.Deathmatch;
    public override string DisplayName => "Deathmatch";
    public override string LobbyDescription => "Pure, no-gimmick free-for-all combat on default arena settings. There are no storms, cursed zones, or environmental distractions—every operator fights strictly for themselves until the match timer or kill cap is reached.";
    public override string PlayerFormat => "Free for all · 4–12 players";
    public override int BaseKillPoints => KillStyleBonus.DefaultBaseKillPoints;
    public override bool UsesTeams => false;
    public override bool UsesVehicles => false;
    public override bool UsesCursedEvents => false;
    public override bool HasTimedResolution => true;
    public override bool UsesUniversalKillStyles => true;
    public override int MinimumPlayers => 2;
    public override int MaximumPlayers => 12;
    public override int RecommendedTeamSize => 1;

    public override BaseModeController CreateModeController() => new DeathmatchController();
}

/// <summary>Backward-compatible alias for DefaultMode.</summary>
public partial class DefaultMode : DeathmatchMode
{
}

/// <summary>
/// Mode 4: Pure Chase (2v2 Duo Buggy Combat Race) — 4–5 min checkpoint route with active Cursed Land Events and 8s omen warnings (Master GDD Section 2.4 & 5.2).
/// Dedicated objective numerics for damaging rival buggy and eliminating rival crew.
/// </summary>
public partial class PureChaseMode : GameModeBase
{
    public override MatchManager.Mode Id => MatchManager.Mode.PureChase;
    public override string DisplayName => "Pure Chase";
    public override string LobbyDescription => "2v2 Duo Buggy combat race across a 4–5 minute checkpoint route. Active Cursed Land Events with 8-second visual omen warnings. Dedicated objective numerics for damaging rival buggy and eliminating rival crew.";
    public override string PlayerFormat => "2 teams · 2 players each · exactly 4 total";
    public override int BaseKillPoints => PureChaseController.PointsKillRivalShooter;
    public override bool UsesTeams => true;
    public override bool UsesVehicles => true;
    public override bool UsesCursedEvents => true;
    public override bool HasTimedResolution => true;
    public override bool UsesUniversalKillStyles => false;
    public override int RecommendedTeamSize => 2;
    public override int MinimumPlayers => 4;
    public override int MaximumPlayers => 4;

    public override BaseModeController CreateModeController() => new PureChaseController();

    public override int CalculateKillPoints(long killerPeerId, long victimPeerId, KillStyle styles, int basePoints, bool isDriver, bool isShooter) =>
        isDriver ? PureChaseController.PointsKillRivalDriver : PureChaseController.PointsKillRivalShooter;
}

/// <summary>
/// Mode 1: Team Deathmatch (Albert vs. Einstein) — Host-assigned rosters, classic high-tempo arena combat (Master GDD Section 2.1 & 5.1).
/// Universal Kill-Style Bonuses apply.
/// </summary>
public partial class TeamDeathmatchMode : GameModeBase
{
    public override MatchManager.Mode Id => MatchManager.Mode.TeamDeathmatch;
    public override string DisplayName => "Team Deathmatch";
    public override string LobbyDescription => "Albert against Einstein on foot. Host-assigned rosters, classic high-tempo arena combat. Universal Kill-Style Bonuses apply. The team with the most points when time runs out wins.";
    public override string PlayerFormat => "2 teams · Albert vs Einstein";
    public override int BaseKillPoints => KillStyleBonus.DefaultBaseKillPoints;
    public override bool UsesTeams => true;
    public override bool UsesVehicles => false;
    public override bool UsesCursedEvents => false;
    public override bool HasTimedResolution => true;
    public override bool UsesUniversalKillStyles => true;
    public override int RecommendedTeamSize => 4;
    public override int MinimumPlayers => 2;
    public override int MaximumPlayers => 16;

    public override BaseModeController CreateModeController() => new TeamDeathmatchController();
}

/// <summary>
/// Mode 3: Convoy Assault — 9v9 max, 7 rounds, first to 4 wins, 10 km corridor, 12-operator rotation with 4-round role cap (Master GDD Section 2.3 & 5.2).
/// Dedicated objective numerics.
/// </summary>
public partial class ConvoyAssaultMode : GameModeBase
{
    public override MatchManager.Mode Id => MatchManager.Mode.ConvoyAssault;
    public override string DisplayName => "Convoy Assault";
    public override string LobbyDescription => "Asymmetric 9v9 tactical extraction across a 10 km corridor. 7-round series (first to 4 wins), sides swap each round. 12-operator rotation with 4-round role cap. Dedicated objective numerics: STV Advance, Checkpoints, Spike Strips, and STV Destruction.";
    public override string PlayerFormat => "2 teams · up to 9v9 (18 players max)";
    public override int BaseKillPoints => ConvoyAssaultController.PointsStandardEnemyKill;
    public override bool UsesTeams => true;
    public override bool UsesVehicles => true;
    public override bool UsesCursedEvents => false;
    public override bool HasTimedResolution => true;
    public override bool UsesUniversalKillStyles => false;
    public override int RecommendedTeamSize => 9;
    public override int MinimumPlayers => 6;
    public override int MaximumPlayers => 18;

    public override BaseModeController CreateModeController() => new ConvoyAssaultController();

    public override int CalculateKillPoints(long killerPeerId, long victimPeerId, KillStyle styles, int basePoints, bool isDriver, bool isShooter) =>
        isDriver ? ConvoyAssaultController.PointsKillActiveDriver : ConvoyAssaultController.PointsStandardEnemyKill;
}
