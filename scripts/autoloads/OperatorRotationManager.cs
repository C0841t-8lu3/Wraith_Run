using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Manages the 12-Operator rotation and 4-round role lock rules for Convoy Assault (Master GDD Section 2.3 & 3).
/// In Convoy Assault (7-round Best-of-7 format):
/// - Every player selects from the pool of 12 operators (4 Drivers, 4 Shooters, 4 Medics).
/// - Mandatory rotation: A player cannot pick the same operator as the immediate previous round.
/// - 4-Round Role Cap: Each role class is capped at 4 rounds maximum per match. Once 4 rounds are played
///   in a role class, that entire row is locked out for the rest of the match.
/// </summary>
public partial class OperatorRotationManager : Node
{
    public static OperatorRotationManager? Instance { get; private set; }

    public const int MaxRoleRounds = 4;
    public const int MaxMatchRounds = 7;
    public const int WinsToWinMatch = 4;

    [Signal] public delegate void OperatorSelectedEventHandler(string operatorId);
    [Signal] public delegate void RoleCountsChangedEventHandler();
    [Signal] public delegate void RoundAdvancedEventHandler(int currentRound);
    [Signal] public delegate void SeriesFinishedEventHandler(int winningTeam);

    public int CurrentRound { get; private set; } = 1;
    public string CurrentOperatorId { get; private set; } = "driver_apex";
    public string? PreviousRoundOperatorId { get; private set; }

    public int Team0RoundWins { get; private set; }
    public int Team1RoundWins { get; private set; }
    public bool IsMatchFinished => Team0RoundWins >= WinsToWinMatch || Team1RoundWins >= WinsToWinMatch;
    public int WinningTeam => Team0RoundWins >= WinsToWinMatch ? 0 : (Team1RoundWins >= WinsToWinMatch ? 1 : -1);

    /// <summary>Teams swap sides each round (Round 1 Team 0 attacks, Round 2 Team 1 attacks, etc.).</summary>
    public int AttackingTeam => (CurrentRound % 2 == 1) ? 0 : 1;
    public int DefendingTeam => (CurrentRound % 2 == 1) ? 1 : 0;
    public bool IsAttacker(int team) => team == AttackingTeam;

    private int _driverRounds;
    private int _shooterRounds;
    private int _medicRounds;

    public int DriverRounds => _driverRounds;
    public int ShooterRounds => _shooterRounds;
    public int MedicRounds => _medicRounds;

    private readonly List<(int Round, string OperatorId, CharacterData.CharacterClass Role)> _roundHistory = new();
    public IReadOnlyList<(int Round, string OperatorId, CharacterData.CharacterClass Role)> RoundHistory => _roundHistory;

    public override void _Ready()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public int GetRoleRounds(CharacterData.CharacterClass role) => role switch
    {
        CharacterData.CharacterClass.Driver => _driverRounds,
        CharacterData.CharacterClass.Shooter or CharacterData.CharacterClass.Operator => _shooterRounds,
        CharacterData.CharacterClass.Medic => _medicRounds,
        _ => 0,
    };

    public bool IsRoleLocked(CharacterData.CharacterClass role)
    {
        return GetRoleRounds(role) >= MaxRoleRounds;
    }

    public bool IsOperatorLocked(string operatorId, out string lockReason)
    {
        CharacterData? data = OperatorInfo.Find(operatorId);
        if (data == null)
        {
            lockReason = "Unknown operator";
            return true;
        }

        // Previous round repeat lock
        if (!string.IsNullOrEmpty(PreviousRoundOperatorId) && operatorId == PreviousRoundOperatorId)
        {
            lockReason = "Locked: Cannot repeat operator from previous round";
            return true;
        }

        // 4-Round role cap check
        if (IsRoleLocked(data.Class))
        {
            lockReason = $"Role Locked: 4-round cap reached for {OperatorInfo.ClassName(data.Class)}s";
            return true;
        }

        lockReason = string.Empty;
        return false;
    }

    public bool TrySelectOperator(string operatorId, out string failureReason)
    {
        if (IsOperatorLocked(operatorId, out failureReason))
            return false;

        CurrentOperatorId = operatorId;
        PlayerProfile.OperatorId = operatorId;
        EmitSignal(SignalName.OperatorSelected, operatorId);
        return true;
    }

    public void RecordRoundCompleted(string operatorId, int roundWinningTeam = -1)
    {
        CharacterData? data = OperatorInfo.Find(operatorId);
        CharacterData.CharacterClass role = data?.Class ?? CharacterData.CharacterClass.Driver;

        switch (role)
        {
            case CharacterData.CharacterClass.Driver:
                _driverRounds = Mathf.Min(_driverRounds + 1, MaxRoleRounds);
                break;
            case CharacterData.CharacterClass.Shooter or CharacterData.CharacterClass.Operator:
                _shooterRounds = Mathf.Min(_shooterRounds + 1, MaxRoleRounds);
                break;
            case CharacterData.CharacterClass.Medic:
                _medicRounds = Mathf.Min(_medicRounds + 1, MaxRoleRounds);
                break;
        }

        if (roundWinningTeam == 0) Team0RoundWins++;
        else if (roundWinningTeam == 1) Team1RoundWins++;

        _roundHistory.Add((CurrentRound, operatorId, role));
        PreviousRoundOperatorId = operatorId;
        CurrentRound = Mathf.Min(CurrentRound + 1, MaxMatchRounds);

        EmitSignal(SignalName.RoleCountsChanged);
        EmitSignal(SignalName.RoundAdvanced, CurrentRound);

        if (IsMatchFinished)
        {
            EmitSignal(SignalName.SeriesFinished, WinningTeam);
        }
    }

    public void ResetMatch()
    {
        CurrentRound = 1;
        _driverRounds = 0;
        _shooterRounds = 0;
        _medicRounds = 0;
        Team0RoundWins = 0;
        Team1RoundWins = 0;
        PreviousRoundOperatorId = null;
        _roundHistory.Clear();
        PlayerProfile.OperatorId = "driver_apex";
        EmitSignal(SignalName.RoleCountsChanged);
        EmitSignal(SignalName.RoundAdvanced, CurrentRound);
    }
}
