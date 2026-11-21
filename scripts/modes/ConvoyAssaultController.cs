using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Mode 3: Convoy Assault Controller (Master GDD Section 2.3, 3 & 5.2).
/// Features:
/// - Asymmetric tactical extraction mode supporting up to 9v9 (18 players max).
/// - 7-round series (First team to 4 wins wins the match, no draws).
/// - Teams swap sides each round (Attackers vs. Defenders).
/// - 10 km extraction corridor (STV advances towards high-security territory).
/// - 12-operator rotation with 4-round role cap (integrated with OperatorRotationManager).
/// - 9s respawn delay (enforced via Health.ConvoyAssaultRespawnDelaySeconds).
/// - Dedicated objective numerics (Scoring Separation Rule: NO Universal Kill-Style Bonuses):
///   * Standard Enemy Kill: +150 (Individual)
///   * Kill Active Driver: +250 (Individual)
///   * Spike Strip Hits STV: +275 (Individual)
///   * STV Advances 1 km along corridor: +100 (Team)
///   * STV Reaches Target Checkpoint (10 km): +200 (Team)
///   * Destroy the STV: +500 (Team)
///   * Complete 1 Round Win: +500 (Team)
/// </summary>
public partial class ConvoyAssaultController : BaseModeController
{
    public const int PointsStandardEnemyKill = 150;
    public const int PointsKillActiveDriver = 250;
    public const int PointsSpikeStripHitSTV = 275;
    public const int PointsSTVAdvance1Km = 100;
    public const int PointsSTVReachCheckpoint = 200;
    public const int PointsDestroySTV = 500;
    public const int PointsCompleteRoundWin = 500;

    public const float TotalCorridorKm = 10.0f;
    public const int MaxMatchRounds = 7;
    public const int WinsToWinMatch = 4;

    [Signal] public delegate void RoundStartedEventHandler(int roundNumber, int attackingTeam, int defendingTeam);
    [Signal] public delegate void RoundCompletedEventHandler(int roundNumber, int winningTeam, string reason);
    [Signal] public delegate void SeriesWonEventHandler(int winningTeam);
    [Signal] public delegate void STVProgressUpdatedEventHandler(float currentKm, float totalKm);
    [Signal] public delegate void STVMilestonePassedEventHandler(int milestoneKm, int attackingTeam, int pointsAwarded);
    [Signal] public delegate void STVCheckpointSecuredEventHandler(int attackingTeam, int pointsAwarded);
    [Signal] public delegate void STVDestroyedByDefendersEventHandler(int defendingTeam, int pointsAwarded);
    [Signal] public delegate void SpikeStripHitConfirmedEventHandler(long defenderPeerId, int pointsAwarded);

    public override MatchManager.Mode ModeId => MatchManager.Mode.ConvoyAssault;
    public override bool UsesUniversalKillStyles => false;
    public override int BaseKillPoints => PointsStandardEnemyKill;
    public override bool IncrementTeamScoreOnKill => false;
    public override bool HandlesClockExpiry => true;

    public int CurrentRound { get; private set; } = 1;
    public int Team0RoundWins { get; private set; }
    public int Team1RoundWins { get; private set; }
    public bool IsSeriesFinished => Team0RoundWins >= WinsToWinMatch || Team1RoundWins >= WinsToWinMatch;
    public int SeriesWinningTeam => Team0RoundWins >= WinsToWinMatch ? 0 : (Team1RoundWins >= WinsToWinMatch ? 1 : -1);

    /// <summary>Teams swap sides each round: Round 1 Team 0 attacks, Round 2 Team 1 attacks, etc.</summary>
    public int AttackingTeam => (CurrentRound % 2 == 1) ? 0 : 1;
    public int DefendingTeam => (CurrentRound % 2 == 1) ? 1 : 0;
    public bool IsAttacking(int team) => team == AttackingTeam;
    public bool IsDefending(int team) => team == DefendingTeam;

    public float STVDistanceKm { get; private set; }
    public float RoundElapsedSeconds { get; private set; }
    private int _lastAwardedMilestoneKm;
    private bool _roundResolved;

    public override void OnMatchStart()
    {
        CurrentRound = 1;
        Team0RoundWins = 0;
        Team1RoundWins = 0;
        _lastAwardedMilestoneKm = 0;
        STVDistanceKm = 0.0f;
        RoundElapsedSeconds = 0.0f;
        _roundResolved = false;

        OperatorRotationManager.Instance?.ResetMatch();

        EmitSignal(SignalName.ModeEventAnnounced, "CONVOY ASSAULT",
            $"Round 1 / 7: Team {MatchManager.TeamName(AttackingTeam)} Escorts STV · Team {MatchManager.TeamName(DefendingTeam)} Defends.");
        EmitSignal(SignalName.RoundStarted, CurrentRound, AttackingTeam, DefendingTeam);
        EmitSignal(SignalName.STVProgressUpdated, STVDistanceKm, TotalCorridorKm);
    }

    /// <summary>
    /// Scoring Separation Rule: Universal kill styles do NOT apply.
    /// Dedicated numerics: Active Driver kill earns +250; Standard enemy kill earns +150.
    /// </summary>
    public override int CalculateKillScore(long killerPeerId, long victimPeerId, KillStyle styles, int basePoints, Health? victimHealth)
    {
        PlayerMovement? movement = FindPlayerMovement(victimHealth, victimPeerId);
        bool isDriver = movement?.IsVehicleDriver ?? false;

        if (!isDriver && victimPeerId > 0)
        {
            SceneTree? tree = GetTree() ?? Engine.GetMainLoop() as SceneTree;
            if (tree != null)
            {
                foreach (Node node in tree.GetNodesInGroup("gameplay_vehicles"))
                {
                    if (node is GameVehicle v && v.LastDriverPeerId == victimPeerId)
                    {
                        isDriver = true;
                        break;
                    }
                }
            }
        }

        int points = isDriver ? PointsKillActiveDriver : PointsStandardEnemyKill;
        return points;
    }

    public override int CalculateAssistScore(int standardAssistPoints)
    {
        // Convoy Assault uses dedicated objective point tables without universal assist stacking.
        return 0;
    }

    /// <summary>
    /// Advances STV along the 10 km corridor. Awards +100 team points per 1 km milestone,
    /// and +200 team points plus an Attacker round win upon reaching 10.0 km.
    /// </summary>
    public void AdvanceSTV(float deltaKm)
    {
        if (_roundResolved || deltaKm <= 0.0f) return;

        // Check if active STV in the corridor is disabled or destroyed
        SceneTree? tree = GetTree() ?? Engine.GetMainLoop() as SceneTree;
        if (tree != null)
        {
            foreach (Node node in tree.GetNodesInGroup("gameplay_vehicles"))
            {
                if (node is GameVehicle v && (v.DisplayName.Contains("STV") || v.DisplayName.Contains("Land Cruiser") || v.TargetLength > 4.5f))
                {
                    if (v.DamageController.IsWrecked || v.DamageController.IsDisabled)
                        return; // Disabled or wrecked STV cannot advance along corridor
                }
            }
        }

        STVDistanceKm = Mathf.Clamp(STVDistanceKm + deltaKm, 0.0f, TotalCorridorKm);
        EmitSignal(SignalName.STVProgressUpdated, STVDistanceKm, TotalCorridorKm);

        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
        {
            Rpc(MethodName.RpcSyncRoundState, CurrentRound, Team0RoundWins, Team1RoundWins, STVDistanceKm, _roundResolved);
        }

        int currentMilestone = (int)Math.Floor(STVDistanceKm);
        while (_lastAwardedMilestoneKm < currentMilestone && _lastAwardedMilestoneKm < (int)TotalCorridorKm)
        {
            _lastAwardedMilestoneKm++;
            if (_lastAwardedMilestoneKm < (int)TotalCorridorKm)
            {
                // Award +100 team points for each intermediate 1 km milestone
                Match.AddTeamScore(AttackingTeam, PointsSTVAdvance1Km, $"STV Advance {_lastAwardedMilestoneKm}km");
                EmitSignal(SignalName.STVMilestonePassed, _lastAwardedMilestoneKm, AttackingTeam, PointsSTVAdvance1Km);
                EmitSignal(SignalName.ObjectiveScored, $"STV Advance {_lastAwardedMilestoneKm}km", AttackingTeam, 0, PointsSTVAdvance1Km);
            }
        }

        if (STVDistanceKm >= TotalCorridorKm && !_roundResolved)
        {
            // STV reaches target checkpoint at 10 km: +200 team points and attacker round win!
            Match.AddTeamScore(AttackingTeam, PointsSTVReachCheckpoint, "STV Checkpoint Reached");
            EmitSignal(SignalName.STVCheckpointSecured, AttackingTeam, PointsSTVReachCheckpoint);
            EmitSignal(SignalName.ObjectiveScored, "STV Checkpoint Reached", AttackingTeam, 0, PointsSTVReachCheckpoint);
            CompleteRound(AttackingTeam, "STV reached target extraction checkpoint");
        }
    }

    /// <summary>
    /// Triggered when the STV vehicle is destroyed by defenders.
    /// Awards +500 team points to defenders and grants them the round win.
    /// </summary>
    public void NotifySTVDestroyed(long attackerPeerId)
    {
        if (_roundResolved) return;

        Match.AddTeamScore(DefendingTeam, PointsDestroySTV, "STV Destroyed");
        EmitSignal(SignalName.STVDestroyedByDefenders, DefendingTeam, PointsDestroySTV);
        EmitSignal(SignalName.ObjectiveScored, "STV Destroyed", DefendingTeam, attackerPeerId, PointsDestroySTV);
        CompleteRound(DefendingTeam, "Defenders destroyed the STV");
    }

    /// <summary>
    /// Triggered when the STV vehicle rolls over a defender spike strip.
    /// Awards +275 individual points to the defending player.
    /// </summary>
    public void NotifySpikeStripHit(long defenderPeerId, GameVehicle vehicle)
    {
        if (defenderPeerId <= 0) return;

        PlayerScore? score = FindPlayerScore(defenderPeerId);
        score?.AwardDirectPoints(PointsSpikeStripHitSTV, "Spike Strip hit STV");

        EmitSignal(SignalName.SpikeStripHitConfirmed, defenderPeerId, PointsSpikeStripHitSTV);
        EmitSignal(SignalName.ObjectiveScored, "Spike Strip STV Hit", DefendingTeam, defenderPeerId, PointsSpikeStripHitSTV);
    }

    public override void OnVehicleDamaged(GameVehicle vehicle, float damage, float oldHp, float newHp, long attackerId, bool isHeavy)
    {
        if (_roundResolved) return;

        // Check if STV was wrecked
        bool isSTV = vehicle.DisplayName.Contains("STV") || vehicle.DisplayName.Contains("Land Cruiser") || vehicle.TargetLength > 4.5f;
        if (isSTV && vehicle.DamageController.IsWrecked)
        {
            NotifySTVDestroyed(attackerId);
        }
    }

    public override void OnSpikeStripHit(GameVehicle vehicle, long attackerId)
    {
        bool isSTV = vehicle.DisplayName.Contains("STV") || vehicle.DisplayName.Contains("Land Cruiser") || vehicle.TargetLength > 4.5f;
        if (isSTV)
        {
            NotifySpikeStripHit(attackerId, vehicle);
        }
    }

    public override void OnMatchClockTick(float elapsedSeconds, float timeRemainingSeconds)
    {
        if (_roundResolved || !Match.HasTimeLimit) return;
        RoundElapsedSeconds += 1.0f;

        // If round clock expires before STV reaches 10 km checkpoint, defenders win!
        if (timeRemainingSeconds <= 0.0f || (Match.HasTimeLimit && RoundElapsedSeconds >= Match.TimeLimitMinutes * 60.0f))
        {
            CompleteRound(DefendingTeam, "Time expired before STV reached extraction checkpoint");
        }
    }

    /// <summary>
    /// Completes the active round, awards +500 team points, records operator rotation,
    /// swaps attacking/defending sides, and evaluates series win condition (first to 4 wins).
    /// </summary>
    public void CompleteRound(int winningTeam, string reason)
    {
        if (_roundResolved) return;
        _roundResolved = true;

        if (winningTeam == 0) Team0RoundWins++;
        else if (winningTeam == 1) Team1RoundWins++;

        // Award +500 team points for round win
        Match.AddTeamScore(winningTeam, PointsCompleteRoundWin, "Round Win");
        EmitSignal(SignalName.ObjectiveScored, "Round Win", winningTeam, 0, PointsCompleteRoundWin);

        // Update OperatorRotationManager
        OperatorRotationManager.Instance?.RecordRoundCompleted(PlayerProfile.OperatorId, winningTeam);

        EmitSignal(SignalName.RoundCompleted, CurrentRound, winningTeam, reason);

        // Series finish check: first team to 4 wins finishes match
        if (IsSeriesFinished)
        {
            int seriesWinner = SeriesWinningTeam;
            EmitSignal(SignalName.SeriesWon, seriesWinner);
            EmitSignal(SignalName.ModeEventAnnounced, "SERIES FINISHED",
                $"Team {MatchManager.TeamName(seriesWinner)} won the series ({Team0RoundWins} - {Team1RoundWins})!");
            Match.EndMatch(seriesWinner, $"Convoy Assault Series Won: {Team0RoundWins} - {Team1RoundWins}");
            return;
        }

        // Advance to next round if under 7 rounds
        if (CurrentRound < MaxMatchRounds)
        {
            AdvanceToNextRound();
        }
        else
        {
            // Max rounds reached: team with highest wins wins
            int seriesWinner = Team0RoundWins > Team1RoundWins ? 0 : (Team1RoundWins > Team0RoundWins ? 1 : -1);
            Match.EndMatch(seriesWinner, $"Max rounds reached ({Team0RoundWins} - {Team1RoundWins})");
        }
    }

    private void AdvanceToNextRound()
    {
        CurrentRound++;
        _roundResolved = false;
        _lastAwardedMilestoneKm = 0;
        STVDistanceKm = 0.0f;
        RoundElapsedSeconds = 0.0f;
        Match.ResetClock();
        ResetVehiclesForRound();

        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
        {
            Rpc(MethodName.RpcSyncRoundState, CurrentRound, Team0RoundWins, Team1RoundWins, STVDistanceKm, _roundResolved);
        }

        EmitSignal(SignalName.ModeEventAnnounced, "SIDES SWAPPED",
            $"Round {CurrentRound} / {MaxMatchRounds}: Team {MatchManager.TeamName(AttackingTeam)} Attacks · Team {MatchManager.TeamName(DefendingTeam)} Defends.");
        EmitSignal(SignalName.RoundStarted, CurrentRound, AttackingTeam, DefendingTeam);
        EmitSignal(SignalName.STVProgressUpdated, STVDistanceKm, TotalCorridorKm);
    }

    public void ResetVehiclesForRound()
    {
        SceneTree? tree = GetTree() ?? Engine.GetMainLoop() as SceneTree;
        if (tree == null) return;
        foreach (Node node in tree.GetNodesInGroup("gameplay_vehicles"))
        {
            if (node is GameVehicle v)
            {
                v.ResetVehicle();
            }
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcSyncRoundState(int currentRound, int team0Wins, int team1Wins, float stvDistanceKm, bool roundResolved)
    {
        CurrentRound = currentRound;
        Team0RoundWins = team0Wins;
        Team1RoundWins = team1Wins;
        STVDistanceKm = stvDistanceKm;
        _roundResolved = roundResolved;
        EmitSignal(SignalName.STVProgressUpdated, STVDistanceKm, TotalCorridorKm);
    }
}
