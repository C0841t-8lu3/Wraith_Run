using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// LanDiscoveryManager (Master GDD Section 7 Architecture):
/// Autoload managing automatic Wi-Fi and mobile hotspot lobby detection on port 7777,
/// along with Pure Chase 2v2 Duo Buggy crew invites.
/// </summary>
public partial class LanDiscoveryManager : Node
{
    public const int DiscoveryPort = 7777;
    public static LanDiscoveryManager? Instance { get; private set; }

    public struct DiscoveredLobby
    {
        public string HostAddress;
        public int Port;
        public string Name;
        public string Mode;
        public string Map;
        public int PlayerCount;
        public int MaxPlayers;
        public bool IsHotspot;
    }

    public struct DuoBuggyInvite
    {
        public long SenderPeerId;
        public string SenderName;
        public double Timestamp;
    }

    [Signal] public delegate void LobbiesUpdatedEventHandler();
    [Signal] public delegate void DuoBuggyInviteReceivedEventHandler(long senderPeerId, string senderName);
    [Signal] public delegate void DuoBuggyInviteAcceptedEventHandler(long senderPeerId, long recipientPeerId);

    private readonly List<DiscoveredLobby> _discoveredLobbies = new();
    private readonly List<DuoBuggyInvite> _pendingDuoInvites = new();
    private double _pollTimer;

    public IReadOnlyList<DiscoveredLobby> DiscoveredLobbies => _discoveredLobbies;
    public IReadOnlyList<DuoBuggyInvite> PendingDuoInvites => _pendingDuoInvites;

    public override void _Ready()
    {
        Instance = this;
        LanMenu.HotspotSessionsUpdated += OnHotspotSessionsUpdated;
        LanMenu.StartHotspotDiscovery();
        GD.Print("[LanDiscoveryManager] Initialized on UDP port ", DiscoveryPort);
    }

    public override void _ExitTree()
    {
        LanMenu.HotspotSessionsUpdated -= OnHotspotSessionsUpdated;
        LanMenu.StopHotspotDiscovery();
        if (Instance == this) Instance = null;
    }

    public override void _Process(double delta)
    {
        _pollTimer += delta;
        if (_pollTimer >= 0.5)
        {
            _pollTimer = 0.0;
            LanMenu.PollHotspotDiscovery(0.5);
        }
    }

    private void OnHotspotSessionsUpdated()
    {
        _discoveredLobbies.Clear();
        foreach (var session in LanMenu.DiscoveredSessions.Values)
        {
            _discoveredLobbies.Add(new DiscoveredLobby
            {
                HostAddress = session.Host,
                Port = session.Port,
                Name = session.Name,
                Mode = session.Mode,
                Map = session.Map,
                PlayerCount = session.Players,
                MaxPlayers = 18,
                IsHotspot = session.IsHotspot,
            });
        }
        EmitSignal(SignalName.LobbiesUpdated);
    }

    /// <summary>
    /// Master GDD Section 2.4 & Section 6.2 Tab 1:
    /// Send an invite to form a 2-player Raider Buggy crew (1 Driver + 1 Shooter) in Pure Chase.
    /// </summary>
    public void SendDuoBuggyInvite(long targetPeerId, string targetName)
    {
        GD.Print($"[LanDiscoveryManager] Sending Duo Buggy invite to peer {targetPeerId} ({targetName})");
        // In loopback/solo or LAN, register the invite
        _pendingDuoInvites.Add(new DuoBuggyInvite
        {
            SenderPeerId = Multiplayer.GetUniqueId(),
            SenderName = PlayerAccount.Name,
            Timestamp = Time.GetUnixTimeFromSystem(),
        });
        EmitSignal(SignalName.DuoBuggyInviteReceived, Multiplayer.GetUniqueId(), PlayerAccount.Name);
    }

    public void AcceptDuoBuggyInvite(long senderPeerId)
    {
        _pendingDuoInvites.RemoveAll(i => i.SenderPeerId == senderPeerId);
        GD.Print($"[LanDiscoveryManager] Accepted Duo Buggy invite from peer {senderPeerId}");
        EmitSignal(SignalName.DuoBuggyInviteAccepted, senderPeerId, Multiplayer.GetUniqueId());
    }
}
