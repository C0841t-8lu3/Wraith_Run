using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Autoload (see project.godot, same pattern as KillFeed/SettingsManager) that owns everything a
/// Team Deathmatch match needs beyond what a single player already tracks for itself: which of the
/// two teams each peer is on, the running team score, the match clock, the Friendly Fire and Team
/// Indicators toggles, and the score/time limit the host picked on the "Play Locally" lobby screen.
///
/// Free-For-All (the existing "Deathmatch" mode) is untouched by any of this: GameMode defaults to
/// FreeForAll and every method here is a no-op (or returns a neutral value) while it stays that
/// way, so the mode that shipped before this file existed behaves exactly as it did before.
///
/// Team assignment follows the same authority model as everything else in this codebase: only the
/// server decides (see AssignTeamAndBroadcast, called from GameWorld), then broadcasts the decision
/// to every peer - including itself - through one RPC, so every peer's local PeerTeams dictionary
/// ends up identical without any client ever computing the assignment itself.
///
/// Team score works the other way around: it is never sent over the network directly. Every peer
/// already receives the same KillFeed.KillAnnounced broadcast (see KillFeed.cs) and, thanks to the
/// paragraph above, already agrees on every player's team, so every peer's own tally lands on the
/// same number independently - one less thing to keep in sync by hand, and one less RPC per kill.
/// </summary>
public partial class MatchManager : Node
{
    public enum Mode
    {
        TeamDeathmatch = 0,
        Deathmatch = 1,
        FreeForAll = Deathmatch,
        Default = TeamDeathmatch,
        ConvoyAssault = 2,
        PureChase = 3,
    }

    public enum ArenaMap { Prison = 0, Ashfields = 1, Depot = 2, Lab = 3, Warehouse = 4, DefaultMap = 5, ConvoyCorridor10km = 6, PureChaseRoute = 7 }

    public static readonly ArenaMap[] SelectableMaps =
    {
        ArenaMap.Ashfields, ArenaMap.Warehouse, ArenaMap.ConvoyCorridor10km, ArenaMap.PureChaseRoute
    };

    public ArenaMap SelectedMap = ArenaMap.Ashfields;
    public string MapScenePath => SelectedMap switch
    {
        ArenaMap.Ashfields => "res://scenes/maps/Ashfields.tscn",
        ArenaMap.Warehouse => "res://scenes/maps/DefaultWarehouse.tscn",
        ArenaMap.DefaultMap => "res://scenes/maps/DefaultWarehouse.tscn",
        ArenaMap.ConvoyCorridor10km => "res://scenes/maps/ConvoyCorridor10km.tscn",
        ArenaMap.PureChaseRoute => "res://scenes/maps/PureChaseRoute.tscn",
        _ => "res://scenes/maps/Ashfields.tscn",
    };
    public string MapDisplayName => SelectedMap switch
    {
        ArenaMap.Ashfields => "Ashfields",
        ArenaMap.Warehouse => "Warehouse",
        ArenaMap.DefaultMap => "Warehouse",
        ArenaMap.ConvoyCorridor10km => "Convoy Corridor (10 km)",
        ArenaMap.PureChaseRoute => "Pure Chase Highway",
        _ => "Ashfields",
    };
    public static string MapName(ArenaMap map) => map switch
    {
        ArenaMap.Ashfields => "Ashfields",
        ArenaMap.Warehouse => "Warehouse",
        ArenaMap.DefaultMap => "Warehouse",
        ArenaMap.ConvoyCorridor10km => "Convoy Corridor (10 km)",
        ArenaMap.PureChaseRoute => "Pure Chase Highway",
        _ => "Ashfields",
    };

    public static readonly Mode[] SelectableModes =
    {
        Mode.TeamDeathmatch, Mode.Deathmatch, Mode.ConvoyAssault, Mode.PureChase
    };

    private Mode _cachedRulesMode = (Mode)(-1);
    private GameModeBase? _cachedRules;
    public GameModeBase ModeRules
    {
        get
        {
            if (_cachedRules == null || _cachedRulesMode != GameMode)
            {
                _cachedRules = GameModeBase.For(GameMode);
                _cachedRulesMode = GameMode;
            }
            return _cachedRules;
        }
    }
    public bool IsTeamMode => GameMode == Mode.TeamDeathmatch || ModeRules.UsesTeams;

    public BaseModeController? ActiveController { get; private set; }
    public T? GetController<T>() where T : BaseModeController => ActiveController as T;

    public void InitializeModeController()
    {
        if (ActiveController != null && GodotObject.IsInstanceValid(ActiveController))
        {
            ActiveController.Cleanup();
            ActiveController.QueueFree();
            ActiveController = null;
        }

        ActiveController = ModeRules.CreateModeController();
        ActiveController.Name = "ActiveModeController";
        AddChild(ActiveController);
        ActiveController.OnMatchStart();
    }

    private const int TeamCount = 2;
    public static readonly string[] TeamNames = { "ALBERT", "EINSTEIN" };
    public static readonly Color[] TeamColors =
    {
        new(0.30f, 0.60f, 1.00f), // Team Albert - blue
        new(1.00f, 0.55f, 0.20f), // Team Einstein - orange
    };

    [Signal] public delegate void MatchEndedEventHandler(int winningTeam, string reason);
    [Signal] public delegate void LobbyChangedEventHandler();
    [Signal] public delegate void MatchStartingEventHandler();
    /// <summary>Server only: a peer's copy of the hub is up and can be told about its teammates.</summary>
    [Signal] public delegate void HubReadyReportedEventHandler(long peerId);
    /// <summary>Client only: the host's hub is up - report in again if yours is.</summary>
    [Signal] public delegate void HubReportRequestedEventHandler();

    public static MatchManager Instance { get; private set; } = null!;

    // ---- Host-configured, host-authoritative match rules --------------------------------------
    // Written directly by LanMenu's lobby screen while the host is still picking settings - the
    // same "the screen writes straight into the autoload's fields" pattern SettingsManager's own
    // screen already uses - then carried to every joining client by ReceiveHostConfig below.
    public Mode GameMode = Mode.TeamDeathmatch;
    public bool FriendlyFire;
    public bool TeamIndicators = true;
    /// <summary>How long a match runs, in minutes. There is no score limit: every match is decided by the clock.</summary>
    public int TimeLimitMinutes = 10;

    // ---- Bots (host-only: bots are simulated by the host and reach everyone else as ordinary players) ----
    /// <summary>How many bots GameWorld spawns when the match starts. 0 = none. Set by LanMenu.</summary>
    public int BotCount;
    /// <summary>Selected total match roster, including the local host/player.</summary>
    public int LobbyPlayerCount = 6;
    public BotDifficulty BotLevel = BotDifficulty.Normal;
    /// <summary>Lets bots ease off a little if a person is being crushed (see BotDirector).</summary>
    public bool AdaptiveBots = true;

    private readonly Dictionary<long, int> _peerTeams = new();
    private readonly Dictionary<long, bool> _lobbyReady = new();
    private readonly int[] _teamScores = new int[TeamCount];
    private float _elapsedSeconds;
    private bool _matchEnded;
    public bool HostConfigReceived { get; private set; }
    // True from PrepareHostMatch/PrepareClientMatch (the lobby -> match handoff) until EndSession
    // (leaving to the main menu). The clock only runs while this is true, so it never counts up
    // while the player is sitting in a menu.
    private bool _sessionActive;

    public bool IsMatchEnded => _matchEnded;
    public int WinningTeam { get; private set; } = -1;

    public bool LocalPlayerReady => _lobbyReady.TryGetValue(Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 1, out bool ready) && ready;
    public bool CanHostStartMatch
    {
        get
        {
            if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer()) return false;
            int humans = Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetPeers().Length + 1 : 1;
            if (humans + BotCount < ModeRules.MinimumPlayers) return false;
            if (GameMode == Mode.PureChase && humans + BotCount != ModeRules.MaximumPlayers) return false;
            if (Multiplayer.HasMultiplayerPeer())
            {
                foreach (int peerId in Multiplayer.GetPeers())
                    if (!_lobbyReady.TryGetValue(peerId, out bool ready) || !ready) return false;
            }
            return true;
        }
    }

    /// <summary>Everyone currently in the lobby (host included), lowest id first.</summary>
    public long[] LobbyPeerIds()
    {
        var ids = new List<long>(_lobbyReady.Keys);
        ids.Sort();
        return ids.ToArray();
    }

    public bool IsPeerReady(long peerId) => _lobbyReady.TryGetValue(peerId, out bool ready) && ready;

    /// <summary>Host only: re-send the rules (mode, map, limits) to everyone already connected, after
    /// the host changed one of them while people are standing in the hub.</summary>
    public void BroadcastHostConfig()
    {
        if (!Multiplayer.HasMultiplayerPeer() || !Multiplayer.IsServer()) return;
        Rpc(MethodName.ReceiveHostConfig, (int)GameMode, FriendlyFire, TeamIndicators,
            TimeLimitMinutes, (int)SelectedMap, LobbyPlayerCount, _elapsedSeconds, BotCount);
    }

    public string LobbyReadySummary
    {
        get
        {
            if (_lobbyReady.Count == 0) return "Waiting for lobby connection…";
            var peerIds = new List<long>(_lobbyReady.Keys);
            peerIds.Sort();
            var lines = new List<string>(peerIds.Count);
            foreach (long peerId in peerIds)
            {
                string who = peerId == 1 ? "HOST" : $"PLAYER {peerId}";
                bool ready = _lobbyReady[peerId];
                lines.Add($"{who}   {(ready ? "READY" : "NOT READY")}");
            }
            return string.Join("\n", lines);
        }
    }

    /// <summary>Seconds since this match began, in every game mode (Free-For-All used to have no clock
    /// at all). On a joining client it starts from the host's clock (see ReceiveHostConfig), so both
    /// sides show roughly the same time. Read by the Tab scoreboard and the objectives screen.</summary>
    public float ElapsedSeconds => _elapsedSeconds;

    /// <summary>Every match is timed: the clock is the only way one ends (there is no score limit).</summary>
    public bool HasTimeLimit => TimeLimitMinutes > 0;

    /// <summary>Seconds left on the clock, or -1 when this mode has no time limit.</summary>
    public float TimeRemainingSeconds =>
        HasTimeLimit ? Mathf.Max(0.0f, TimeLimitMinutes * 60.0f - _elapsedSeconds) : -1.0f;

    private Timer? _matchClockTimer;

    public override void _Ready()
    {
        Instance = this;
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        KillFeed? killFeed = GetNodeOrNull<KillFeed>("/root/KillFeed");
        if (killFeed != null) killFeed.KillAnnounced += OnKillAnnounced;

        _matchClockTimer = new Timer
        {
            Name = "MatchClockTimer",
            WaitTime = 1.0f,
            Autostart = true,
            OneShot = false,
        };
        _matchClockTimer.Timeout += OnMatchClockTick;
        AddChild(_matchClockTimer);
    }

    public override void _ExitTree()
    {
        Multiplayer.PeerConnected -= OnPeerConnected;
        Multiplayer.PeerDisconnected -= OnPeerDisconnected;
        Multiplayer.ConnectedToServer -= OnConnectedToServer;
        if (_matchClockTimer != null)
        {
            _matchClockTimer.Timeout -= OnMatchClockTick;
        }
    }

    private void OnMatchClockTick()
    {
        if (!_sessionActive || _matchEnded) return;
        _elapsedSeconds += 1.0f;
        ActiveController?.OnMatchClockTick(_elapsedSeconds, TimeRemainingSeconds);
        if (ActiveController != null && ActiveController.HandlesClockExpiry) return;
        // The time limit is the only thing that ends a match: whoever is ahead when the clock runs out wins.
        if (!HasTimeLimit) return;
        if (_elapsedSeconds < TimeLimitMinutes * 60.0f) return;

        int winner = IsTeamMode
            ? (_teamScores[0] == _teamScores[1] ? -1 : (_teamScores[0] > _teamScores[1] ? 0 : 1))
            : FindFfaWinner();
        EndMatch(winner, "time");
    }

    public override void _Process(double delta)
    {
        // Clock tick handled via MatchClockTimer node to prevent frame-rate drift
    }

    // -------------------------------------------------------------------------------------------
    // Lobby -> match handoff
    // -------------------------------------------------------------------------------------------

    /// <summary>Called by LanMenu right before LanSession.StartHost. GameMode/FriendlyFire/
    /// TeamIndicators/TimeLimitMinutes are whatever the host just picked on the lobby
    /// screen and are left alone here; everything that belongs to one match run is reset so a
    /// second match started later in the same app session (once a return-to-lobby flow exists)
    /// never inherits the previous match's teams or score.</summary>
    public void PrepareHostMatch(bool keepTeams = false)
    {
        // The hub assigns teams while people stand in the warehouse; the match must use those same
        // teams, so the lobby -> match handoff (ReceiveMatchStart) keeps them.
        if (!keepTeams) _peerTeams.Clear();
        _lobbyReady.Clear();
        _teamScores[0] = 0;
        _teamScores[1] = 0;
        _elapsedSeconds = 0.0f;
        _matchEnded = false;
        WinningTeam = -1;
        _sessionActive = true;
        HostConfigReceived = true;
        MatchStats.Instance?.Reset();
        InitializeModeController();
    }

    /// <summary>GameWorld calls this when it loads. Through the normal lobby flow the clock is already
    /// running (PrepareHostMatch/PrepareClientMatch started it), so this changes nothing there; it only
    /// matters when GameWorld.tscn is run directly from the editor for single-instance testing, where
    /// no lobby ran and the scoreboard clock would otherwise sit at 00:00 forever.</summary>
    public void EnsureSessionActive()
    {
        _sessionActive = true;
        if (ActiveController == null) InitializeModeController();
    }

    /// <summary>Called when the player leaves a running match for the main menu (see GameSession).
    /// Stops the clock; the next PrepareHostMatch/PrepareClientMatch starts everything fresh.</summary>
    public void EndSession()
    {
        _sessionActive = false;
        HostConfigReceived = false;
        _peerTeams.Clear();
        _lobbyReady.Clear();
        if (ActiveController != null && GodotObject.IsInstanceValid(ActiveController))
        {
            ActiveController.Cleanup();
            ActiveController.QueueFree();
            ActiveController = null;
        }
    }

    /// <summary>Called by LanMenu right before LanSession.Join. The real rules arrive moments later
    /// via ReceiveHostConfig once the connection is up; this only guarantees a clean slate rather
    /// than stale state left over from a previous match in the same app session.</summary>
    public void PrepareClientMatch()
    {
        GameMode = Mode.TeamDeathmatch;
        FriendlyFire = false;
        TeamIndicators = true;
        BotCount = 0; // only the host ever spawns bots
        PrepareHostMatch();
        HostConfigReceived = false;
        _sessionActive = false;
    }

    public void BeginHostLobby()
    {
        _sessionActive = false;
        _elapsedSeconds = 0.0f;
        _lobbyReady.Clear();
        _lobbyReady[1] = true;
        foreach (int peerId in Multiplayer.GetPeers()) _lobbyReady[peerId] = false;
        BroadcastLobbyState();
        // Team modes: the host takes a side before anyone else connects, so the split stays balanced.
        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer()) AssignTeamAndBroadcast(1);
    }

    /// <summary>Everyone is back in the hub after a match: clear the finished match's clock, score and
    /// result but keep the connection, the host's rules and the teams. The host stays ready and
    /// everyone else has to press READY again for the next match.</summary>
    public void ResetForHub()
    {
        RollBotNamePlan();
        _teamScores[0] = 0;
        _teamScores[1] = 0;
        _elapsedSeconds = 0.0f;
        _matchEnded = false;
        WinningTeam = -1;
        _sessionActive = false;
        MatchStats.Instance?.Reset();
        if (ActiveController != null && GodotObject.IsInstanceValid(ActiveController))
        {
            ActiveController.Cleanup();
            ActiveController.QueueFree();
            ActiveController = null;
        }
        if (!Multiplayer.HasMultiplayerPeer()) return;
        if (Multiplayer.IsServer())
        {
            _lobbyReady.Clear();
            _lobbyReady[1] = true;
            foreach (int peerId in Multiplayer.GetPeers()) _lobbyReady[peerId] = false;
            BroadcastLobbyState();
        }
    }

    // ---- hub handshake ------------------------------------------------------------------------
    // These live here (an autoload every peer always has) rather than on the hub scene, because after
    // a match people reach the hub at different moments: a message sent to a hub that is not loaded
    // yet would be an "RPC node not found" error and the person would never be spawned.

    public void ReportHubReadyToHost()
    {
        if (!Multiplayer.HasMultiplayerPeer() || Multiplayer.IsServer()) return;
        RpcId(1, MethodName.ReportHubReady);
    }

    public void AskPeersToReportHub()
    {
        if (!Multiplayer.HasMultiplayerPeer() || !Multiplayer.IsServer()) return;
        Rpc(MethodName.AskHubReport);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReportHubReady()
    {
        if (!Multiplayer.IsServer()) return;
        long sender = Multiplayer.GetRemoteSenderId();
        if (sender > 1) EmitSignal(SignalName.HubReadyReported, sender);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void AskHubReport() => EmitSignal(SignalName.HubReportRequested);

    public void SetLocalReady(bool ready)
    {
        if (!Multiplayer.HasMultiplayerPeer()) return;
        if (Multiplayer.IsServer())
        {
            _lobbyReady[Multiplayer.GetUniqueId()] = true;
            BroadcastLobbyState();
        }
        else RpcId(1, MethodName.SubmitReadyState, ready);
    }

    public void StartMatchWhenReady()
    {
        if (!CanHostStartMatch || IsMatchEnded) return;
        Rpc(MethodName.ReceiveMatchStart);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void SubmitReadyState(bool ready)
    {
        if (!Multiplayer.IsServer()) return;
        long sender = Multiplayer.GetRemoteSenderId();
        if (sender <= 1 || !_lobbyReady.ContainsKey(sender)) return;
        _lobbyReady[sender] = ready;
        BroadcastLobbyState();
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void RequestLobbySnapshot()
    {
        if (!Multiplayer.IsServer()) return;
        long sender = Multiplayer.GetRemoteSenderId();
        SendLobbyStateTo((int)sender);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveLobbySnapshot(long[] peerIds, int[] readyStates)
    {
        _lobbyReady.Clear();
        int count = Math.Min(peerIds.Length, readyStates.Length);
        for (int i = 0; i < count; i++) _lobbyReady[peerIds[i]] = readyStates[i] != 0;
        EmitSignal(SignalName.LobbyChanged);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveMatchStart()
    {
        if (!Multiplayer.IsServer() && !HostConfigReceived) return;
        PrepareHostMatch(keepTeams: true);
        EmitSignal(SignalName.MatchStarting);
    }

    private void BroadcastLobbyState()
    {
        var peerIds = new long[_lobbyReady.Count];
        var readyStates = new int[_lobbyReady.Count];
        int index = 0;
        foreach (KeyValuePair<long, bool> entry in _lobbyReady)
        {
            peerIds[index] = entry.Key;
            readyStates[index] = entry.Value ? 1 : 0;
            index++;
        }
        Rpc(new StringName(nameof(ReceiveLobbySnapshot)), peerIds, readyStates);
    }

    private void SendLobbyStateTo(int peerId)
    {
        var peerIds = new long[_lobbyReady.Count];
        var readyStates = new int[_lobbyReady.Count];
        int index = 0;
        foreach (KeyValuePair<long, bool> entry in _lobbyReady)
        {
            peerIds[index] = entry.Key;
            readyStates[index] = entry.Value ? 1 : 0;
            index++;
        }
        RpcId(peerId, new StringName(nameof(ReceiveLobbySnapshot)), peerIds, readyStates);
    }

    private void OnConnectedToServer() => RpcId(1, MethodName.RequestLobbySnapshot);

    // -------------------------------------------------------------------------------------------
    // Team assignment - the server decides, everyone receives the same broadcast
    // -------------------------------------------------------------------------------------------

    /// <summary>Server-only (a no-op on a client, harmless if ever called on one). GameWorld calls
    /// this for its own peer (id 1) exactly the way it self-spawns peer 1 in _Ready, since peer 1
    /// never raises Multiplayer.PeerConnected for itself; every other peer reaches this through
    /// OnPeerConnected below instead. A no-op outside Team Deathmatch, same as everything else
    /// here.</summary>
    public void AssignTeamAndBroadcast(long peerId)
    {
        if (!IsTeamMode) return;
        // Already placed (in the hub, before the match): keep the side they have.
        if (_peerTeams.ContainsKey(peerId)) return;

        int countAlpha = 0, countBravo = 0;
        foreach (int assignedTeam in _peerTeams.Values)
        {
            if (assignedTeam == 0) countAlpha++;
            else if (assignedTeam == 1) countBravo++;
        }
        int team = countAlpha <= countBravo ? 0 : 1;

        if (!Multiplayer.HasMultiplayerPeer()) { ApplyTeamAssignment(peerId, team); return; }
        Rpc(MethodName.ReceiveTeamAssignment, peerId, team);
    }

    private void OnPeerConnected(long peerId)
    {
        if (!Multiplayer.IsServer()) return;
        if (Multiplayer.GetPeers().Length + 1 > ModeRules.MaximumPlayers)
        {
            GD.PushWarning($"Rejected peer {peerId}: {ModeRules.DisplayName} supports at most {ModeRules.MaximumPlayers} players.");
            Multiplayer.MultiplayerPeer?.DisconnectPeer((int)peerId);
            return;
        }
        _lobbyReady[peerId] = false;
        // Reliable + targeted at exactly the peer that just connected: every other peer already
        // has the right values, this one has whatever GameMode/FriendlyFire/etc. defaulted to
        // when its own copy of this autoload was created (see PrepareClientMatch).
        RpcId(peerId, MethodName.ReceiveHostConfig, (int)GameMode, FriendlyFire, TeamIndicators,
            TimeLimitMinutes, (int)SelectedMap, LobbyPlayerCount, _elapsedSeconds, BotCount);
        // Teach the newcomer everyone who is already here (the host, other people, bots). The broadcast
        // below only carries the newcomer's own team, so without this a late joiner would not know who
        // is on which side.
        foreach (KeyValuePair<long, int> entry in _peerTeams)
            if (entry.Key != peerId) RpcId(peerId, MethodName.ReceiveTeamAssignment, entry.Key, entry.Value);
        AssignTeamAndBroadcast(peerId);
        BroadcastLobbyState();
    }

    private void OnPeerDisconnected(long peerId)
    {
        _peerTeams.Remove(peerId);
        if (Multiplayer.IsServer() && _lobbyReady.Remove(peerId)) BroadcastLobbyState();
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveTeamAssignment(long peerId, int team) => ApplyTeamAssignment(peerId, team);

    private void ApplyTeamAssignment(long peerId, int team)
    {
        _peerTeams[peerId] = team;
        EmitSignal(SignalName.LobbyChanged);
    }

    /// <summary>Server only: switch a player's team and broadcast the change.</summary>
    public void SetPeerTeam(long peerId, int team)
    {
        if (!Multiplayer.IsServer()) return;
        ApplyTeamAssignment(peerId, team);
        if (Multiplayer.HasMultiplayerPeer())
        {
            Rpc(MethodName.ReceiveTeamAssignment, peerId, team);
            BroadcastLobbyState();
        }
        HubController.Instance?.RefreshVisibility();
        EmitSignal(SignalName.LobbyChanged);
    }

    /// <summary>Server only: kick a player from the server.</summary>
    public void KickPlayer(long peerId)
    {
        if (!Multiplayer.IsServer() || peerId == 1) return;
        _lobbyReady.Remove(peerId);
        _peerTeams.Remove(peerId);
        if (Multiplayer.HasMultiplayerPeer())
        {
            Multiplayer.MultiplayerPeer?.DisconnectPeer((int)peerId);
            BroadcastLobbyState();
        }
        HubController.Instance?.RefreshVisibility();
        EmitSignal(SignalName.LobbyChanged);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveHostConfig(int gameMode, bool friendlyFire, bool teamIndicators,
        int timeLimitMinutes, int arenaMap, int lobbyPlayerCount, float hostElapsedSeconds, int botCount = 0)
    {
        GameMode = System.Enum.IsDefined(typeof(Mode), gameMode) ? (Mode)gameMode : Mode.Default;
        FriendlyFire = friendlyFire;
        TeamIndicators = teamIndicators;
        TimeLimitMinutes = Math.Clamp(timeLimitMinutes, 1, 240);
        SelectedMap = System.Enum.IsDefined(typeof(ArenaMap), arenaMap) ? (ArenaMap)arenaMap : ArenaMap.Ashfields;
        if (SelectedMap is ArenaMap.Prison or ArenaMap.Depot or ArenaMap.Lab) SelectedMap = ArenaMap.Ashfields;
        LobbyPlayerCount = Math.Clamp(lobbyPlayerCount, ModeRules.MinimumPlayers, ModeRules.MaximumPlayers);
        _elapsedSeconds = Mathf.Clamp(hostElapsedSeconds, 0.0f, 86400.0f);
        BotCount = Math.Clamp(botCount, 0, 15);
        HostConfigReceived = true;
        _matchEnded = false;
        WinningTeam = -1;
        EmitSignal(SignalName.LobbyChanged);
    }

    // ---- Bot names ----
    // Picked ahead of the match so the set-up screen can list exactly the bots that will spawn. A new draw is made
    // after every match (ResetForHub) so the next one is not the same crowd.
    private readonly List<string> _botNamePlan = new();

    /// <summary>The name the bot with this index (0-based) will have in the next match.</summary>
    public string PlannedBotName(int index)
    {
        if (_botNamePlan.Count == 0) RollBotNamePlan();
        return index >= 0 && index < _botNamePlan.Count ? _botNamePlan[index] : $"Bot{index + 1}";
    }

    private void RollBotNamePlan()
    {
        _botNamePlan.Clear();
        _botNamePlan.AddRange(BotRoster.NamePool);
        var rng = new RandomNumberGenerator();
        rng.Randomize();
        for (int i = _botNamePlan.Count - 1; i > 0; i--)
        {
            int j = rng.RandiRange(0, i);
            (_botNamePlan[i], _botNamePlan[j]) = (_botNamePlan[j], _botNamePlan[i]);
        }
    }

    /// <summary>Resets the match/round clock, called at match start and on round transitions in round-based modes.</summary>
    public void ResetClock()
    {
        _elapsedSeconds = 0.0f;
        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
        {
            Rpc(MethodName.ReceiveResetClock);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveResetClock()
    {
        _elapsedSeconds = 0.0f;
    }

    private int FindFfaWinner()
    {
        var tree = GetTree();
        if (tree == null) return -1;
        List<ScoreboardData.Row> rows = ScoreboardData.Collect(tree);
        if (rows.Count == 0) return -1;

        ScoreboardData.Row top = rows[0];
        if (top.Points <= 0 && top.Kills <= 0) return -1;

        if (rows.Count > 1 && rows[1].Points == top.Points && rows[1].Kills == top.Kills && rows[1].Deaths == top.Deaths)
        {
            return -1;
        }

        return (int)top.PeerId;
    }

    /// <summary>-1 when the peer has not been assigned a team yet (or GameMode is FreeForAll, which
    /// never assigns one at all).</summary>
    public int GetTeam(long peerId) => _peerTeams.TryGetValue(peerId, out int team) ? team : -1;

    /// <summary>True only while a Team Deathmatch match is running and both peers are known members
    /// of the same team. Shared by WeaponSwitcher's shooter-side pre-check and Health's
    /// victim-authoritative check so the two can never disagree about who is friendly.</summary>
    public bool AreTeammates(long peerA, long peerB)
    {
        if (!IsTeamMode) return false;
        int teamA = GetTeam(peerA);
        return teamA >= 0 && teamA == GetTeam(peerB);
    }

    // -------------------------------------------------------------------------------------------
    // Team score / match end
    // -------------------------------------------------------------------------------------------

    public int GetTeamScore(int team) => team == 0 || team == 1 ? _teamScores[team] : 0;

    public static string TeamName(int team)
    {
        if (Instance != null && Instance.GameMode == Mode.ConvoyAssault)
        {
            if (Instance.ActiveController is ConvoyAssaultController convoy)
            {
                return team == convoy.AttackingTeam ? "ATTACKERS" : (team == convoy.DefendingTeam ? "DEFENDERS" : "UNASSIGNED");
            }
            return team == 0 ? "ATTACKERS" : (team == 1 ? "DEFENDERS" : "UNASSIGNED");
        }
        return team == 0 ? TeamNames[0] : team == 1 ? TeamNames[1] : "UNASSIGNED";
    }

    public static Color TeamColor(int team) => team == 0 ? TeamColors[0] : team == 1 ? TeamColors[1] : Colors.White;

    /// <summary>Every peer receives the identical KillFeed broadcast (see KillFeed.cs) and already
    /// agrees on every player's team, so every peer's tally increments on the exact same kills in
    /// the exact same order - no extra network traffic needed just to keep the team score in sync.</summary>
    private void OnKillAnnounced(long killerPeerId, long victimPeerId, int styleFlags, int basePoints,
        int totalPoints, long assistPeerId, int assistPoints, bool struggles)
    {
        if (!IsTeamMode || _matchEnded) return;

        int killerTeam = GetTeam(killerPeerId);
        // An unassigned killer shouldn't happen once a match is running; a team-kill only reaches
        // here at all when Friendly Fire is on. Either way no team's score should move - crediting
        // a team for killing its own member would be wrong twice.
        if (killerTeam < 0 || killerTeam == GetTeam(victimPeerId)) return;

        if (ActiveController == null || ActiveController.IncrementTeamScoreOnKill)
        {
            _teamScores[killerTeam]++;
        }

        ActiveController?.OnKillAnnounced(killerPeerId, victimPeerId, styleFlags, basePoints,
            totalPoints, assistPeerId, assistPoints, struggles);
    }

    /// <summary>Awards objective points to a team score and synchronizes to clients.</summary>
    public void AddTeamScore(int team, int points, string reason = "")
    {
        if (team < 0 || team >= TeamCount || points == 0 || _matchEnded) return;
        _teamScores[team] += points;
        if (Multiplayer.HasMultiplayerPeer() && Multiplayer.IsServer())
        {
            Rpc(MethodName.ReceiveTeamScores, _teamScores[0], _teamScores[1]);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    public void ReceiveTeamScores(int score0, int score1)
    {
        _teamScores[0] = score0;
        _teamScores[1] = score1;
    }

    public void EndMatch(int winningTeam, string reason)
    {
        if (_matchEnded) return;
        _matchEnded = true;
        _sessionActive = false;
        WinningTeam = winningTeam;
        EmitSignal(SignalName.MatchEnded, winningTeam, reason);
    }
}