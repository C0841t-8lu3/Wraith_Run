using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Base class for mode controllers governing match flow, objective progression, and mode-specific scoring.
/// Enforces the Master GDD Section 5 Scoring Separation Rule:
/// - Universal Kill-Style Bonuses apply strictly to Team Deathmatch and Deathmatch.
/// - Objective vehicular modes (Convoy Assault and Pure Chase) implement dedicated mode-specific point tables.
/// </summary>
public abstract partial class BaseModeController : Node
{
    [Signal] public delegate void ObjectiveScoredEventHandler(string objectiveName, int team, long peerId, int points);
    [Signal] public delegate void ModeEventAnnouncedEventHandler(string eventName, string description);

    public MatchManager Match => MatchManager.Instance;

    public abstract MatchManager.Mode ModeId { get; }
    public abstract bool UsesUniversalKillStyles { get; }
    public abstract int BaseKillPoints { get; }
    public virtual bool IncrementTeamScoreOnKill => true;
    public virtual bool HandlesClockExpiry => false;

    public virtual void OnMatchStart() { }

    public virtual void OnMatchClockTick(float elapsedSeconds, float timeRemainingSeconds) { }

    /// <summary>
    /// Calculates the points for an individual confirmed kill.
    /// In TDM / DM: stacks universal kill-style bonuses onto the base kill value.
    /// In Convoy Assault & Pure Chase: evaluates dedicated role and vehicle numerics without universal style bonuses.
    /// </summary>
    public virtual int CalculateKillScore(long killerPeerId, long victimPeerId, KillStyle styles, int basePoints, Health? victimHealth)
    {
        if (UsesUniversalKillStyles)
        {
            return KillStyleBonus.Evaluate(basePoints > 0 ? basePoints : BaseKillPoints, styles);
        }

        return BaseKillPoints;
    }

    /// <summary>
    /// Calculates assist points earned by a non-killing damager.
    /// </summary>
    public virtual int CalculateAssistScore(int standardAssistPoints)
    {
        return UsesUniversalKillStyles ? standardAssistPoints : 0;
    }

    /// <summary>
    /// Invoked whenever KillFeed announces a kill to all peers.
    /// </summary>
    public virtual void OnKillAnnounced(long killerPeerId, long victimPeerId, int styleFlags, int basePoints,
        int totalPoints, long assistPeerId, int assistPoints, bool struggles)
    {
    }

    /// <summary>
    /// Invoked when a vehicle takes damage.
    /// </summary>
    public virtual void OnVehicleDamaged(GameVehicle vehicle, float damage, float oldHp, float newHp, long attackerId, bool isHeavy)
    {
    }

    /// <summary>
    /// Invoked when a vehicle hits a defender spike strip.
    /// </summary>
    public virtual void OnSpikeStripHit(GameVehicle vehicle, long attackerId)
    {
    }

    public virtual void Cleanup() { }

    /// <summary>
    /// Helper to look up a player's PlayerScore node in the scene tree.
    /// </summary>
    protected PlayerScore? FindPlayerScore(long peerId)
    {
        SceneTree? tree = GetTree();
        if (tree == null) return null;
        foreach (Node node in tree.GetNodesInGroup("players"))
        {
            if (node is CharacterBody3D body && PlayerIdentity.IdOf(body) == peerId)
            {
                return body.GetNodeOrNull<PlayerScore>("Score");
            }
        }
        return null;
    }

    /// <summary>
    /// Helper to find PlayerMovement component for a peer.
    /// </summary>
    protected PlayerMovement? FindPlayerMovement(Health? victimHealth, long victimPeerId = 0)
    {
        if (victimHealth != null && GodotObject.IsInstanceValid(victimHealth))
        {
            var pm = victimHealth.GetParent() as PlayerMovement;
            if (pm != null) return pm;
        }
        if (victimPeerId > 0)
        {
            SceneTree? tree = GetTree() ?? Engine.GetMainLoop() as SceneTree;
            if (tree != null)
            {
                foreach (Node node in tree.GetNodesInGroup("players"))
                {
                    if (node is CharacterBody3D body && PlayerIdentity.IdOf(body) == victimPeerId)
                    {
                        return body as PlayerMovement;
                    }
                }
            }
        }
        return null;
    }
}
