using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Custom Game Lobby tab (Master GDD & prompt.md Bug 3):
/// 3-column tactical layout:
/// - Left Column: Host Configuration (Start Match, OptionButtons for Mode & Map, Duration pills, Max Players, Markers, Bots)
/// - Center Column: Stacked Team Containers (Albert vs Einstein / Attackers vs Defenders, live count headers, calling cards, crown, 3-dot host menu)
/// - Right Column: Social & Invites (Discovered players invite list, lobby chat box with Send message... bar)
/// </summary>
public partial class CustomLobbyTab : Control
{
    private VBoxContainer _rootContainer = null!;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        _rootContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _rootContainer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_rootContainer);

        BuildLobby();

        if (MatchManager.Instance != null)
        {
            MatchManager.Instance.LobbyChanged += OnLobbyChanged;
        }
    }

    public override void _ExitTree()
    {
        if (MatchManager.Instance != null)
        {
            MatchManager.Instance.LobbyChanged -= OnLobbyChanged;
        }
    }

    private void OnLobbyChanged()
    {
        BuildLobby();
    }

    public void BuildLobby()
    {
        foreach (Node child in _rootContainer.GetChildren())
        {
            child.QueueFree();
        }

        if (HubTablet.Instance != null)
        {
            HubTablet.Instance.BuildLobbyForTest(_rootContainer);
        }
    }
}
