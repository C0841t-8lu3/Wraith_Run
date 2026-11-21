using System;
using Godot;

/// <summary>
/// Mode 1: Team Deathmatch (Albert vs. Einstein) Controller (Master GDD Section 2.1 & 5.1).
/// Features:
/// - Host-assigned rosters between Team Albert (Blue) and Team Einstein (Orange).
/// - Classic high-tempo arena combat on foot.
/// - Universal Kill-Style Bonuses apply additively with no cap:
///   +100 Base Kill, +150 Headshot, +125 Hip-Fire, +175 360 No-Scope, +120 Verge of Death, +200 Multi-Kill, +25 Min Assist.
/// - Team kill tallies tracked for final match resolution when time limit expires.
/// </summary>
public partial class TeamDeathmatchController : BaseModeController
{
    public override MatchManager.Mode ModeId => MatchManager.Mode.TeamDeathmatch;
    public override bool UsesUniversalKillStyles => true;
    public override int BaseKillPoints => KillStyleBonus.DefaultBaseKillPoints;
    public override bool IncrementTeamScoreOnKill => true;

    [Signal] public delegate void TeamScoreUpdatedEventHandler(int team, int score);

    public override void OnMatchStart()
    {
        EmitSignal(SignalName.ModeEventAnnounced, "TEAM DEATHMATCH", "Eliminate rivals. Universal kill-style bonuses active.");
    }

    public override int CalculateKillScore(long killerPeerId, long victimPeerId, KillStyle styles, int basePoints, Health? victimHealth)
    {
        int baseVal = basePoints > 0 ? basePoints : BaseKillPoints;
        return KillStyleBonus.Evaluate(baseVal, styles);
    }

    public override int CalculateAssistScore(int standardAssistPoints)
    {
        return standardAssistPoints;
    }

    public override void OnKillAnnounced(long killerPeerId, long victimPeerId, int styleFlags, int basePoints,
        int totalPoints, long assistPeerId, int assistPoints, bool struggles)
    {
        int killerTeam = Match.GetTeam(killerPeerId);
        int victimTeam = Match.GetTeam(victimPeerId);

        if (killerTeam >= 0 && killerTeam != victimTeam)
        {
            EmitSignal(SignalName.TeamScoreUpdated, killerTeam, Match.GetTeamScore(killerTeam));
        }
    }
}
