using System;
using System.Collections.Generic;
using Godot;

public enum CursedEventType
{
    None = 0,
    FloorCollapse = 1,
    PullVortex = 2,
    BlindnessFog = 3,
    MirrorShift = 4,
    GhostPossession = 5,
    DeathKnell = 6
}

/// <summary>
/// Mode 4: Pure Chase Controller (Master GDD Section 2.4 & 5.2).
/// Features:
/// - 2v2 Duo Buggy combat race (4 players: 2 teams of 2 players, 1 Driver + 1 Shooter per Raider Buggy).
/// - 4–5 minute checkpoint race route with locked vehicle roles (Driver steers, Shooter shoots).
/// - Active Cursed Land Events with 8-second visual omen warnings:
///   * Floor Collapse: Roadway drops away.
///   * Pull Vortex: Gravitational vortex drawing vehicles inward.
///   * Blindness Fog: Dense black mist cutting visibility.
///   * Mirror Shift: Flips visual geometry inside the zone.
///   * Ghost Possession: Briefly inverts steering/movement inputs.
///   * Death Knell: Permanently closes off a route sector.
/// - Dedicated objective numerics (Scoring Separation Rule: NO Universal Kill-Style Bonuses):
///   * Damage Rival Buggy: +200 per 25% HP threshold crossed (75%, 50%, 25%, 0%).
///   * Kill Rival Driver: +250 (Individual).
///   * Kill Rival Shooter: +200 (Individual).
/// </summary>
public partial class PureChaseController : BaseModeController
{
    public const int PointsDamageBuggyThreshold = 200;
    public const int PointsKillRivalDriver = 250;
    public const int PointsKillRivalShooter = 200;
    public const int PointsFinishRaceFirst = 500;

    public const float OmenWarningDurationSeconds = 8.0f;
    public const float ActiveEventDurationSeconds = 15.0f;
    public const float EventCooldownSeconds = 20.0f;
    public const int TotalCheckpoints = 5;

    public enum EventPhase
    {
        Idle = 0,
        OmenWarning = 1,
        Active = 2,
        Cooldown = 3
    }

    [Signal] public delegate void OmenWarningStartedEventHandler(int eventType, float omenDurationSeconds);
    [Signal] public delegate void CursedEventActivatedEventHandler(int eventType, float activeDurationSeconds);
    [Signal] public delegate void CursedEventEndedEventHandler(int eventType);
    [Signal] public delegate void RivalBuggyThresholdCrossedEventHandler(int victimTeam, float thresholdPercent, long attackerId, int pointsAwarded);
    [Signal] public delegate void CheckpointPassedEventHandler(int team, int checkpointIndex, float elapsedSeconds);
    [Signal] public delegate void RaceFinishedEventHandler(int winningTeam, float finishTime);

    public override MatchManager.Mode ModeId => MatchManager.Mode.PureChase;
    public override bool UsesUniversalKillStyles => false;
    public override int BaseKillPoints => PointsKillRivalShooter;
    public override bool IncrementTeamScoreOnKill => false;
    public override bool HandlesClockExpiry => true;

    // Cursed Land Events state
    public EventPhase CurrentEventPhase { get; private set; } = EventPhase.Idle;
    public CursedEventType CurrentEvent { get; private set; } = CursedEventType.None;
    public float PhaseTimer { get; private set; }

    public bool IsOmenWarningActive => CurrentEventPhase == EventPhase.OmenWarning;
    public bool IsCursedEventActive => CurrentEventPhase == EventPhase.Active;

    // Active gameplay effects
    public bool IsGhostPossessionActive => IsCursedEventActive && CurrentEvent == CursedEventType.GhostPossession;
    public bool IsBlindnessFogActive => IsCursedEventActive && CurrentEvent == CursedEventType.BlindnessFog;
    public bool IsPullVortexActive => IsCursedEventActive && CurrentEvent == CursedEventType.PullVortex;
    public bool IsMirrorShiftActive => IsCursedEventActive && CurrentEvent == CursedEventType.MirrorShift;
    public bool IsFloorCollapseActive => IsCursedEventActive && CurrentEvent == CursedEventType.FloorCollapse;
    public bool IsDeathKnellActive => IsCursedEventActive && CurrentEvent == CursedEventType.DeathKnell;

    public Vector3 VortexCenter { get; set; } = Vector3.Zero;

    // Checkpoint progression
    public int Team0Checkpoints { get; private set; }
    public int Team1Checkpoints { get; private set; }
    public bool IsRaceFinished { get; private set; }
    public int RaceWinner { get; private set; } = -1;

    // 25% HP Threshold tracking per vehicle: indices 0=75%, 1=50%, 2=25%, 3=0%
    private readonly Dictionary<ulong, bool[]> _thresholdsCrossedPerVehicle = new();
    private readonly List<CursedEventType> _eventRotation = new()
    {
        CursedEventType.FloorCollapse,
        CursedEventType.PullVortex,
        CursedEventType.BlindnessFog,
        CursedEventType.MirrorShift,
        CursedEventType.GhostPossession,
        CursedEventType.DeathKnell
    };
    private int _rotationIndex;

    public override void OnMatchStart()
    {
        CurrentEventPhase = EventPhase.Cooldown;
        PhaseTimer = 10.0f; // Initial grace period before first omen warning
        CurrentEvent = CursedEventType.None;
        Team0Checkpoints = 0;
        Team1Checkpoints = 0;
        IsRaceFinished = false;
        RaceWinner = -1;
        _thresholdsCrossedPerVehicle.Clear();
        _rotationIndex = 0;

        EmitSignal(SignalName.ModeEventAnnounced, "PURE CHASE",
            "2v2 Duo Buggy combat race. Checkpoints and active Cursed Land Events are live!");
    }

    public override void _Process(double delta)
    {
        if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) return;
        float dt = (float)delta;
        TickCursedEvents(dt);
    }

    private void TickCursedEvents(float dt)
    {
        if (PhaseTimer > 0.0f)
        {
            PhaseTimer -= dt;
            if (PhaseTimer <= 0.0f)
            {
                AdvanceEventPhase();
            }
        }
    }

    private void AdvanceEventPhase()
    {
        switch (CurrentEventPhase)
        {
            case EventPhase.Idle or EventPhase.Cooldown:
                CurrentEvent = _eventRotation[_rotationIndex % _eventRotation.Count];
                _rotationIndex++;
                SyncEventPhase((int)EventPhase.OmenWarning, (int)CurrentEvent, OmenWarningDurationSeconds);
                break;

            case EventPhase.OmenWarning:
                SyncEventPhase((int)EventPhase.Active, (int)CurrentEvent, ActiveEventDurationSeconds);
                break;

            case EventPhase.Active:
                SyncEventPhase((int)EventPhase.Cooldown, (int)CurrentEvent, EventCooldownSeconds);
                break;
        }
    }

    private void SyncEventPhase(int phase, int cursedEvent, float timer)
    {
        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
        {
            Rpc(MethodName.RpcSyncEventPhase, phase, cursedEvent, timer);
        }
        ApplyEventPhase((EventPhase)phase, (CursedEventType)cursedEvent, timer);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcSyncEventPhase(int phase, int cursedEvent, float timer)
    {
        ApplyEventPhase((EventPhase)phase, (CursedEventType)cursedEvent, timer);
    }

    private void ApplyEventPhase(EventPhase phase, CursedEventType cursedEvent, float timer)
    {
        CurrentEventPhase = phase;
        CurrentEvent = cursedEvent;
        PhaseTimer = timer;

        switch (CurrentEventPhase)
        {
            case EventPhase.OmenWarning:
                EmitSignal(SignalName.OmenWarningStarted, (int)CurrentEvent, PhaseTimer);
                EmitSignal(SignalName.ModeEventAnnounced, "CURSED OMEN WARNING",
                    $"8s WARNING: {CurrentEvent} is about to manifest!");
                break;
            case EventPhase.Active:
                EmitSignal(SignalName.CursedEventActivated, (int)CurrentEvent, PhaseTimer);
                EmitSignal(SignalName.ModeEventAnnounced, "CURSED EVENT ACTIVE",
                    $"{CurrentEvent} has engulfed the route!");
                break;
            case EventPhase.Cooldown:
                EmitSignal(SignalName.CursedEventEnded, (int)CurrentEvent);
                EmitSignal(SignalName.ModeEventAnnounced, "CURSED EVENT CLEARED",
                    $"{CurrentEvent} subsided. Highway clear for now.");
                break;
        }
    }

    /// <summary>
    /// Explicitly trigger an omen warning for a specific cursed event (used for script testing or race triggers).
    /// </summary>
    public void TriggerOmenWarning(CursedEventType eventType)
    {
        SyncEventPhase((int)EventPhase.OmenWarning, (int)eventType, OmenWarningDurationSeconds);
    }

    /// <summary>
    /// Force-activate a cursed event immediately without waiting for omen warning.
    /// </summary>
    public void ForceActivateEvent(CursedEventType eventType, float duration = ActiveEventDurationSeconds)
    {
        SyncEventPhase((int)EventPhase.Active, (int)eventType, duration);
    }

    public override void OnMatchClockTick(float elapsedSeconds, float timeRemainingSeconds)
    {
        if (IsRaceFinished || !Match.HasTimeLimit) return;
        if (timeRemainingSeconds <= 0.0f)
        {
            int score0 = Match.GetTeamScore(0);
            int score1 = Match.GetTeamScore(1);
            int winner;
            if (score0 != score1) winner = score0 > score1 ? 0 : 1;
            else if (Team0Checkpoints != Team1Checkpoints) winner = Team0Checkpoints > Team1Checkpoints ? 0 : 1;
            else winner = -1;

            CompleteRace(winner);
        }
    }

    /// <summary>
    /// Scoring Separation Rule: Universal kill styles do NOT apply.
    /// Dedicated numerics: Rival Driver kill earns +250; Rival Shooter kill earns +200.
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

        return isDriver ? PointsKillRivalDriver : PointsKillRivalShooter;
    }

    public override int CalculateAssistScore(int standardAssistPoints)
    {
        return 0;
    }

    /// <summary>
    /// Vehicle damage tracking: checks for 25% HP threshold crossings (at 75%, 50%, 25%, 0% HP).
    /// Each threshold crossed awards +200 points to the attacker and attacker's team.
    /// If repaired back above a threshold, the threshold re-arms for subsequent combat.
    /// </summary>
    public override void OnVehicleDamaged(GameVehicle vehicle, float damage, float oldHp, float newHp, long attackerId, bool isHeavy)
    {
        if (IsRaceFinished || attackerId <= 0) return;

        ulong vehicleId = vehicle.GetInstanceId();
        if (!_thresholdsCrossedPerVehicle.TryGetValue(vehicleId, out bool[]? thresholds))
        {
            thresholds = new bool[4]; // 0=75%, 1=50%, 2=25%, 3=0%
            _thresholdsCrossedPerVehicle[vehicleId] = thresholds;
        }

        int victimTeam = Match.GetTeam(vehicle.DriverPeerId > 0 ? vehicle.DriverPeerId : vehicle.PassengerPeerId);
        int attackerTeam = Match.GetTeam(attackerId);

        // Fallback for unoccupied rival vehicle in 2-team pure chase
        if (victimTeam < 0 && attackerTeam >= 0)
        {
            victimTeam = 1 - attackerTeam;
        }

        // Friendly fire on vehicles is not awarded points
        if (victimTeam >= 0 && attackerTeam >= 0 && victimTeam == attackerTeam) return;

        float[] thresholdValues = { 75.0f, 50.0f, 25.0f, 0.0f };
        for (int i = 0; i < 4; i++)
        {
            // Re-arm threshold if vehicle HP was repaired back above it
            if (thresholds[i] && oldHp > thresholdValues[i])
            {
                thresholds[i] = false;
            }

            if (!thresholds[i] && (newHp <= thresholdValues[i] || (i == 3 && vehicle.DamageController.IsWrecked)))
            {
                thresholds[i] = true;
                AwardBuggyThresholdPoints(attackerId, attackerTeam, thresholdValues[i], victimTeam);
            }
        }
    }

    private void AwardBuggyThresholdPoints(long attackerId, int attackerTeam, float thresholdHp, int victimTeam)
    {
        PlayerScore? score = FindPlayerScore(attackerId);
        score?.AwardDirectPoints(PointsDamageBuggyThreshold, $"Rival Buggy Damage ({thresholdHp}% HP)");

        if (attackerTeam >= 0)
        {
            Match.AddTeamScore(attackerTeam, PointsDamageBuggyThreshold, $"Rival Buggy {thresholdHp}% HP");
        }

        EmitSignal(SignalName.RivalBuggyThresholdCrossed, victimTeam, thresholdHp, attackerId, PointsDamageBuggyThreshold);
        EmitSignal(SignalName.ObjectiveScored, $"Rival Buggy {thresholdHp}% HP", attackerTeam, attackerId, PointsDamageBuggyThreshold);
    }

    /// <summary>
    /// Progresses a team through race checkpoints. Checkpoints must be reached sequentially (1, 2, 3, 4, 5).
    /// Returns true if valid checkpoint passed, false if skipped or rejected.
    /// The first buggy to pass TotalCheckpoints wins!
    /// </summary>
    public bool PassCheckpoint(int team, int checkpointIndex)
    {
        if (IsRaceFinished || team < 0 || team >= 2) return false;

        int current = team == 0 ? Team0Checkpoints : Team1Checkpoints;
        if (checkpointIndex != current + 1) return false;

        if (team == 0) Team0Checkpoints = checkpointIndex;
        else Team1Checkpoints = checkpointIndex;

        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
        {
            Rpc(MethodName.RpcSyncRaceProgress, Team0Checkpoints, Team1Checkpoints, IsRaceFinished, RaceWinner);
        }

        EmitSignal(SignalName.CheckpointPassed, team, checkpointIndex, Match.ElapsedSeconds);

        if (checkpointIndex >= TotalCheckpoints)
        {
            CompleteRace(team);
        }
        return true;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RpcSyncRaceProgress(int team0Checkpoints, int team1Checkpoints, bool isFinished, int winner)
    {
        Team0Checkpoints = team0Checkpoints;
        Team1Checkpoints = team1Checkpoints;
        IsRaceFinished = isFinished;
        RaceWinner = winner;
    }

    private void CompleteRace(int winningTeam)
    {
        if (IsRaceFinished) return;
        IsRaceFinished = true;
        RaceWinner = winningTeam;

        if (winningTeam >= 0)
        {
            Match.AddTeamScore(winningTeam, PointsFinishRaceFirst, "Race Finish 1st Place");
        }
        EmitSignal(SignalName.RaceFinished, winningTeam, Match.ElapsedSeconds);
        string announcement = winningTeam >= 0
            ? $"Team {MatchManager.TeamName(winningTeam)} crossed the finish line first!"
            : "Race ended in a tie!";
        EmitSignal(SignalName.ModeEventAnnounced, "RACE FINISHED", announcement);
        Match.EndMatch(winningTeam, winningTeam >= 0 ? "First across checkpoint race finish line" : "Race ended in tie");

        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
        {
            Rpc(MethodName.RpcSyncRaceProgress, Team0Checkpoints, Team1Checkpoints, IsRaceFinished, RaceWinner);
        }
    }
}
