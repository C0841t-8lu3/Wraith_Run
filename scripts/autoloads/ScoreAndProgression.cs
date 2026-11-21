using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// ScoreAndProgression (Master GDD Section 1, Section 5, and Section 7 Architecture):
/// Manages live match scoring, enforces the Scoring Separation Rule between TDM/DM Universal Style Bonuses
/// and Convoy Assault/Pure Chase dedicated numerics, and tracks XP, Rank Leveling (1-55+), and CP currency.
/// </summary>
public partial class ScoreAndProgression : Node
{
    public static ScoreAndProgression? Instance { get; private set; }

    [Signal] public delegate void ScoreAwardedEventHandler(long peerId, int points, string reason, bool isStyleBonus);
    [Signal] public delegate void XpGainedEventHandler(long peerId, int xpAmount, int totalXp, int currentLevel);
    [Signal] public delegate void LevelUpEventHandler(long peerId, int newLevel, string rankTitle);
    [Signal] public delegate void CpAwardedEventHandler(long peerId, int cpAmount, int totalCp);

    // Rank milestones (1 to 55+)
    public const int MaxStandardRank = 55;
    public const int BaseXpPerRank = 1000;

    private int _localXp;
    private int _localCp;

    public int LocalXp => _localXp;
    public int LocalCp => _localCp;
    public int LocalRank => CalculateRank(_localXp);
    public string LocalRankTitle => GetRankTitle(LocalRank);

    public override void _Ready()
    {
        Instance = this;
        // Load existing career XP/CP if available
        if (PlayerAccount.Current?.Stats != null)
        {
            _localXp = (int)(PlayerAccount.Current.Stats.Kills * 100);
            _localCp = _localXp / 10;
        }
        GD.Print($"[ScoreAndProgression] Initialized. Current Rank: {LocalRank} ({LocalRankTitle}), XP: {_localXp}, CP: {_localCp}");
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public static int CalculateRank(int totalXp)
    {
        int rank = 1 + (totalXp / BaseXpPerRank);
        return rank;
    }

    public static string GetRankTitle(int rank)
    {
        if (rank < 5) return "Recruit";
        if (rank < 15) return "Operator";
        if (rank < 25) return "Specialist";
        if (rank < 35) return "Sergeant";
        if (rank < 45) return "Commander";
        if (rank < 55) return "Wraith Vanguard";
        return "Wraith Elite (Master)";
    }

    /// <summary>
    /// Master GDD Section 5: Scoring Separation Rule.
    /// Returns true if the mode utilizes Universal Kill-Style Bonuses (TDM & DM).
    /// </summary>
    public static bool UsesUniversalStyleBonuses(MatchManager.Mode mode)
    {
        return mode == MatchManager.Mode.TeamDeathmatch || mode == MatchManager.Mode.Deathmatch;
    }

    /// <summary>
    /// Master GDD Section 5.1: Universal Kill-Style Bonus Table (TDM & DM only).
    /// </summary>
    public int AwardUniversalStyleKill(long peerId, bool isHeadshot, bool isHipFire, bool is360NoScope, bool isMultiKill, bool onVergeOfDeath)
    {
        if (!UsesUniversalStyleBonuses(MatchManager.Instance?.GameMode ?? MatchManager.Mode.TeamDeathmatch))
        {
            GD.PushWarning("[ScoreAndProgression] Attempted to award Universal Style Bonus in an objective vehicle mode!");
            return 0;
        }

        int totalScore = KillStyleBonus.DefaultBaseKillPoints; // +100
        EmitScore(peerId, KillStyleBonus.DefaultBaseKillPoints, "Base Elimination", false);

        if (isHeadshot)
        {
            totalScore += KillStyleBonus.HeadshotBonus; // +150
            EmitScore(peerId, KillStyleBonus.HeadshotBonus, "Headshot", true);
        }
        if (isHipFire)
        {
            totalScore += KillStyleBonus.HipFireBonus; // +125
            EmitScore(peerId, KillStyleBonus.HipFireBonus, "Hip-Fire Kill", true);
        }
        if (is360NoScope)
        {
            totalScore += KillStyleBonus.NoScope360Bonus; // +175
            EmitScore(peerId, KillStyleBonus.NoScope360Bonus, "360 No-Scope", true);
        }
        if (isMultiKill)
        {
            totalScore += KillStyleBonus.MultiKillBonus; // +200
            EmitScore(peerId, KillStyleBonus.MultiKillBonus, "One-Shot Multi-Kill", true);
        }
        if (onVergeOfDeath)
        {
            totalScore += KillStyleBonus.VergeOfDeathBonus; // +120
            EmitScore(peerId, KillStyleBonus.VergeOfDeathBonus, "On Verge of Death", true);
        }

        AwardXp(peerId, totalScore);
        AwardCp(peerId, totalScore / 10);
        return totalScore;
    }

    /// <summary>
    /// Master GDD Section 5.2: Convoy Assault Dedicated Mode Numerics.
    /// </summary>
    public void AwardConvoyAssaultScore(long peerId, string action)
    {
        int points = action switch
        {
            "EnemyKill" => 150,
            "DriverKill" => 250,
            "SpikeStripHit" => 275,
            "Corridor1Km" => 100,
            "TargetCheckpoint" => 200,
            "DestroySTV" => 500,
            "RoundWin" => 500,
            _ => 100
        };

        EmitScore(peerId, points, $"Convoy: {action}", false);
        AwardXp(peerId, points);
        AwardCp(peerId, points / 10);
    }

    /// <summary>
    /// Master GDD Section 5.2: Pure Chase Dedicated Mode Numerics.
    /// </summary>
    public void AwardPureChaseScore(long peerId, string action)
    {
        int points = action switch
        {
            "BuggyThresholdDamage" => 200, // per 25% HP crossed
            "DriverKill" => 250,
            "ShooterKill" => 200,
            _ => 100
        };

        EmitScore(peerId, points, $"Pure Chase: {action}", false);
        AwardXp(peerId, points);
        AwardCp(peerId, points / 10);
    }

    private void EmitScore(long peerId, int points, string reason, bool isStyleBonus)
    {
        EmitSignal(SignalName.ScoreAwarded, peerId, points, reason, isStyleBonus);
    }

    public void AwardXp(long peerId, int amount)
    {
        if (amount <= 0) return;
        int priorRank = LocalRank;
        _localXp += amount;
        int newRank = LocalRank;

        EmitSignal(SignalName.XpGained, peerId, amount, _localXp, newRank);

        if (newRank > priorRank)
        {
            EmitSignal(SignalName.LevelUp, peerId, newRank, LocalRankTitle);
        }
    }

    public void AwardCp(long peerId, int amount)
    {
        if (amount <= 0) return;
        _localCp += amount;
        EmitSignal(SignalName.CpAwarded, peerId, amount, _localCp);
    }
}
