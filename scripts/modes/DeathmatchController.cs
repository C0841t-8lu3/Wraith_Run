using System;
using Godot;

/// <summary>
/// Mode 2: Deathmatch (Free-For-All) Controller (Master GDD Section 2.2 & 5.1).
/// Features:
/// - Pure, no-gimmick free-for-all combat on default arena settings.
/// - No storms, cursed zones, or environmental distractions.
/// - Universal Kill-Style Bonuses apply additively with no cap:
///   +100 Base Kill, +150 Headshot, +125 Hip-Fire, +175 360 No-Scope, +120 Verge of Death, +200 Multi-Kill, +25 Min Assist.
/// - Individual scoreboard kills and total points decide match victory.
/// </summary>
public partial class DeathmatchController : BaseModeController
{
    public override MatchManager.Mode ModeId => MatchManager.Mode.Deathmatch;
    public override bool UsesUniversalKillStyles => true;
    public override int BaseKillPoints => KillStyleBonus.DefaultBaseKillPoints;
    public override bool IncrementTeamScoreOnKill => false;

    public override void OnMatchStart()
    {
        EmitSignal(SignalName.ModeEventAnnounced, "DEATHMATCH (FFA)", "Free-for-all arena combat. Universal kill-style bonuses active.");
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
}
