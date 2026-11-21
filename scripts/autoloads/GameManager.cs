using Godot;
using System;

/// <summary>
/// GameManager (Master GDD Section 7 Architecture):
/// Autoload that manages the Hub vs. Match loop, enforces 3s / 9s psychological respawn delays,
/// and coordinates seamless returns to the 3D Warehouse Hub.
/// </summary>
public partial class GameManager : Node
{
    public const string WarehouseHubScenePath = "res://scenes/hub/WarehouseHub.tscn";
    public const string LegacyHubScenePath = "res://scenes/Hub.tscn";

    public static GameManager? Instance { get; private set; }

    [Signal] public delegate void MatchStateChangedEventHandler(bool inMatch, string modeName);
    [Signal] public delegate void PlayerRespawnScheduledEventHandler(long peerId, float delaySeconds);

    public bool IsInMatch => !HubState.Active;
    public bool IsInHub => HubState.Active;

    public override void _Ready()
    {
        Instance = this;
        GD.Print("[GameManager] Initialized. Hub scene: ", WarehouseHubScenePath);
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Master GDD Section 1 & Section 7:
    /// Standard modes enforce 3-second respawn delay; Convoy Assault enforces punitive 9-second respawn delay.
    /// </summary>
    public static float GetRespawnDelaySeconds(MatchManager.Mode mode)
    {
        return mode == MatchManager.Mode.ConvoyAssault ? 9.0f : 3.0f;
    }

    /// <summary>
    /// Master GDD Section 6.1:
    /// Finishing or leaving any match returns all players straight back to the live Warehouse Hub.
    /// </summary>
    public void ReturnToWarehouseHub()
    {
        SceneTree tree = GetTree();
        GameUiState.Reset();
        if (!OS.HasFeature("mobile")) Input.MouseMode = Input.MouseModeEnum.Visible;

        CareerTracker.Instance?.FinishMatch();
        MatchStats.Instance?.Reset();

        string target = ResourceLoader.Exists(WarehouseHubScenePath) ? WarehouseHubScenePath : LegacyHubScenePath;
        Error err = tree.ChangeSceneToFile(target);
        if (err != Error.Ok)
        {
            GD.PrintErr($"[GameManager] Failed to return to Warehouse Hub ({target}): {err}");
        }
        else
        {
            GD.Print($"[GameManager] Returned to Warehouse Hub ({target})");
        }
    }
}
