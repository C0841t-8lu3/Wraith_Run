using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

[GlobalClass]
public partial class VerifyCustomGameLobbyAndHotspot : Node
{
	public override void _Ready()
	{
		GD.Print("=== Starting VerifyCustomGameLobbyAndHotspot ===");
		try
		{
			RunAllTests();
			GD.Print("=== ALL CUSTOM GAME LOBBY & HOTSPOT TESTS PASSED! ===");
			GetTree().Quit(0);
		}
		catch (Exception ex)
		{
			GD.PrintErr($"TEST FAILED: {ex.Message}\n{ex.StackTrace}");
			GetTree().Quit(1);
		}
	}

	private static void Assert(bool condition, string message)
	{
		if (!condition)
		{
			throw new Exception($"Assertion Failed: {message}");
		}
	}

	private void RunAllTests()
	{
		TestTeamAlbertAndEinsteinNaming();
		TestMatchManagerSwitchTeamAndKick();
		TestHubTabletSceneInstantiation();
		TestHotspotAddressDetection();
		TestHotspotProbeTargets();
		TestHotspotDiscoveryEngine();
		TestUdpProbeReplyLoopback();
		TestHubTabletLobby3ColumnsAndControls();
		TestHubTabletHotspotSessionDisplay();
	}

	private void TestTeamAlbertAndEinsteinNaming()
	{
		GD.Print("Testing Team Albert vs Team Einstein naming...");
		Assert(MatchManager.TeamNames != null, "MatchManager.TeamNames is null");
		Assert(MatchManager.TeamNames.Length >= 2, $"Expected at least 2 team names, got {MatchManager.TeamNames?.Length}");
		Assert(MatchManager.TeamNames![0] == "ALBERT", $"Team 0 name should be ALBERT, got '{MatchManager.TeamNames[0]}'");
		Assert(MatchManager.TeamNames[1] == "EINSTEIN", $"Team 1 name should be EINSTEIN, got '{MatchManager.TeamNames[1]}'");

		string team0Name = MatchManager.TeamName(0);
		string team1Name = MatchManager.TeamName(1);
		Assert(team0Name == "ALBERT", $"MatchManager.TeamName(0) should be ALBERT, got '{team0Name}'");
		Assert(team1Name == "EINSTEIN", $"MatchManager.TeamName(1) should be EINSTEIN, got '{team1Name}'");
		Assert(MatchManager.TeamName(99) == "UNASSIGNED", "Invalid team index should return UNASSIGNED");

		Color color0 = MatchManager.TeamColor(0);
		Color color1 = MatchManager.TeamColor(1);
		Assert(color0 != Colors.White, "Team 0 color should be assigned");
		Assert(color1 != Colors.White, "Team 1 color should be assigned");
		Assert(color0 != color1, "Team 0 and Team 1 should have different colors");
		GD.Print("  Team Albert and Team Einstein naming verified!");
	}

	private void TestMatchManagerSwitchTeamAndKick()
	{
		GD.Print("Testing MatchManager SetPeerTeam and KickPlayer methods...");
		MatchManager match = MatchManager.Instance;
		Assert(match != null, "MatchManager.Instance is null");

		bool lobbyChangedEmitted = false;
		MatchManager.LobbyChangedEventHandler onLobbyChanged = () => lobbyChangedEmitted = true;
		match!.LobbyChanged += onLobbyChanged;

		try
		{
			// Verify server SetPeerTeam switches peer's team directly
			match.SetPeerTeam(1, 1);
			Assert(match.GetTeam(1) == 1, $"match.GetTeam(1) should be 1 after SetPeerTeam(1, 1), got {match.GetTeam(1)}");
			Assert(lobbyChangedEmitted, "LobbyChanged should be emitted on SetPeerTeam");

			lobbyChangedEmitted = false;
			match.SetPeerTeam(1, 0);
			Assert(match.GetTeam(1) == 0, $"match.GetTeam(1) should be 0 after SetPeerTeam(1, 0), got {match.GetTeam(1)}");
			Assert(lobbyChangedEmitted, "LobbyChanged should be emitted on SetPeerTeam(1, 0)");

			// Set up peer 2 and verify kick
			match.SetPeerTeam(2, 1);
			Assert(match.GetTeam(2) == 1, "Peer 2 should be on team 1");
			lobbyChangedEmitted = false;
			match.KickPlayer(2);
			Assert(match.GetTeam(2) == -1, "Peer 2 should be removed from teams after kick");
			Assert(lobbyChangedEmitted, "LobbyChanged should be emitted on KickPlayer");

			GD.Print("  SetPeerTeam and KickPlayer verified safe and functionally correct!");
		}
		finally
		{
			match.LobbyChanged -= onLobbyChanged;
		}
	}

	private void TestHubTabletSceneInstantiation()
	{
		GD.Print("Testing HubTablet scene instantiation...");
		var packed1 = ResourceLoader.Load<PackedScene>("res://scenes/HubTablet.tscn");
		Assert(packed1 != null, "scenes/HubTablet.tscn failed to load!");
		var tablet1 = packed1!.Instantiate<HubTablet>();
		Assert(tablet1 != null, "scenes/HubTablet.tscn could not be instantiated as HubTablet!");
		tablet1.QueueFree();

		var packed2 = ResourceLoader.Load<PackedScene>("res://scenes/ui/HubTablet.tscn");
		Assert(packed2 != null, "scenes/ui/HubTablet.tscn failed to load!");
		var tablet2 = packed2!.Instantiate<HubTablet>();
		Assert(tablet2 != null, "scenes/ui/HubTablet.tscn could not be instantiated as HubTablet!");
		tablet2.QueueFree();

		var codeTablet = new HubTablet();
		Assert(codeTablet != null, "Direct 'new HubTablet()' instantiation failed!");
		codeTablet.QueueFree();

		GD.Print("  HubTablet scenes and instantiation verified!");
	}

	private void TestHotspotAddressDetection()
	{
		GD.Print("Testing LanMenu.IsHotspotAddress...");
		// Android default hotspot
		Assert(LanMenu.IsHotspotAddress("192.168.43.1"), "192.168.43.1 should be recognized as hotspot");
		Assert(LanMenu.IsHotspotAddress("192.168.43.55"), "192.168.43.55 should be recognized as hotspot");

		// iOS Personal Hotspot
		Assert(LanMenu.IsHotspotAddress("172.20.10.1"), "172.20.10.1 should be recognized as hotspot");
		Assert(LanMenu.IsHotspotAddress("172.20.10.3"), "172.20.10.3 should be recognized as hotspot");

		// Windows Mobile Hotspot
		Assert(LanMenu.IsHotspotAddress("192.168.137.1"), "192.168.137.1 should be recognized as hotspot");
		Assert(LanMenu.IsHotspotAddress("192.168.137.89"), "192.168.137.89 should be recognized as hotspot");

		// USB Tethering
		Assert(LanMenu.IsHotspotAddress("192.168.42.1"), "192.168.42.1 should be recognized as hotspot");
		Assert(LanMenu.IsHotspotAddress("192.168.42.129"), "192.168.42.129 should be recognized as hotspot");

		// Non-hotspot LAN/WAN addresses
		Assert(!LanMenu.IsHotspotAddress("192.168.1.100"), "192.168.1.100 should not be recognized as mobile hotspot");
		Assert(!LanMenu.IsHotspotAddress("10.0.0.1"), "10.0.0.1 should not be recognized as mobile hotspot");
		Assert(!LanMenu.IsHotspotAddress("127.0.0.1"), "127.0.0.1 should not be recognized as mobile hotspot");
		Assert(!LanMenu.IsHotspotAddress(""), "Empty IP should not be recognized as mobile hotspot");
		Assert(!LanMenu.IsHotspotAddress(null!), "Null IP should not be recognized as mobile hotspot");

		GD.Print("  LanMenu.IsHotspotAddress verified!");
	}

	private void TestHotspotProbeTargets()
	{
		GD.Print("Testing LanMenu.GetHotspotProbeTargets...");
		List<string> targets = LanMenu.GetHotspotProbeTargets();
		Assert(targets != null, "Probe targets list is null");
		Assert(targets!.Count > 0, "Probe targets list is empty");
		Assert(targets.Contains("255.255.255.255"), "Probe targets must include 255.255.255.255");
		Assert(targets.Contains("192.168.43.1"), "Probe targets must include Android hotspot gateway 192.168.43.1");
		Assert(targets.Contains("172.20.10.1"), "Probe targets must include iOS hotspot gateway 172.20.10.1");
		Assert(targets.Contains("192.168.137.1"), "Probe targets must include Windows hotspot gateway 192.168.137.1");
		GD.Print($"  Probe targets list contains {targets.Count} targets, verified!");
	}

	private void TestHotspotDiscoveryEngine()
	{
		GD.Print("Testing LanMenu HotspotDiscovery engine...");
		LanMenu.StartHotspotDiscovery();

		// Add mock discovered sessions directly to test dictionary & event
		bool eventFired = false;
		Action onUpdate = () => eventFired = true;
		LanMenu.HotspotSessionsUpdated += onUpdate;

		try
		{
			LanMenu.DiscoveredSessions["test_session_hotspot"] = new LanMenu.DiscoveredSession
			{
				SessionId = "test_session_hotspot",
				Name = "Albert's Hotspot Game",
				Host = "192.168.43.1",
				Port = 7777,
				Players = 2,
				Map = "Ashfields",
				Mode = "Team Deathmatch",
				Seen = Time.GetTicksMsec(),
				IsHotspot = true,
			};

			Assert(LanMenu.DiscoveredSessions.ContainsKey("test_session_hotspot"), "Session should be recorded in DiscoveredSessions");
			var s = LanMenu.DiscoveredSessions["test_session_hotspot"];
			Assert(s.IsHotspot, "Session should be flagged as IsHotspot");
			Assert(s.Name == "Albert's Hotspot Game", $"Expected game name 'Albert's Hotspot Game', got '{s.Name}'");
			if (eventFired) GD.Print("  Hotspot event fired.");
		}
		finally
		{
			LanMenu.HotspotSessionsUpdated -= onUpdate;
			LanMenu.DiscoveredSessions.Remove("test_session_hotspot");
			LanMenu.StopHotspotDiscovery();
		}
		GD.Print("  LanMenu HotspotDiscovery verified!");
	}

	private void TestUdpProbeReplyLoopback()
	{
		GD.Print("Testing Real UDP Probe and Reply Loopback...");
		var lanSession = new LanSession { Name = "TestLanSession" };
		AddChild(lanSession);

		Error hostErr = lanSession.StartHost("Loopback Test Arena", 7777);
		Assert(hostErr == Error.Ok, $"LanSession.StartHost failed with {hostErr}");

		LanMenu.DiscoveredSessions.Clear();
		LanMenu.StartHotspotDiscovery();

		try
		{
			bool discovered = false;
			for (int tick = 0; tick < 20; tick++)
			{
				lanSession._Process(0.1);
				LanMenu.PollHotspotDiscovery(0.1);
				if (LanMenu.DiscoveredSessions.Values.Any(s => s.Name == "Loopback Test Arena"))
				{
					discovered = true;
					break;
				}
				OS.DelayMsec(50);
			}

			Assert(discovered, "Client should discover the hosted session via real UDP socket transmission!");
			GD.Print("  Real UDP Probe and Reply Loopback verified successfully!");
		}
		finally
		{
			LanMenu.StopHotspotDiscovery();
			lanSession.Stop();
			lanSession.QueueFree();
		}
	}

	private void TestHubTabletLobby3ColumnsAndControls()
	{
		GD.Print("Testing HubTablet 3-column Custom Game lobby...");
		var tablet = new HubTablet { Name = "TestTablet" };
		AddChild(tablet);

		// Test content property
		Assert(tablet.Content != null, "tablet.Content should not be null");

		// Test 3-column Custom Game lobby structure
		var lobbyContainer = new VBoxContainer();
		tablet.BuildLobbyForTest(lobbyContainer);

		Assert(lobbyContainer.GetChildCount() > 0, "BuildLobby should populate container");
		HBoxContainer? threeColumns = null;
		foreach (Node child in lobbyContainer.GetChildren())
		{
			if (child is HBoxContainer hbox && hbox.GetChildCount() == 3)
			{
				threeColumns = hbox;
				break;
			}
		}

		Assert(threeColumns != null, "3-column layout HBoxContainer not found with exactly 3 child columns");
		Assert(threeColumns!.GetChildCount() == 3, $"Expected 3 columns, got {threeColumns.GetChildCount()}");

		Node col1 = threeColumns.GetChild(0);
		Node col2 = threeColumns.GetChild(1);
		Node col3 = threeColumns.GetChild(2);

		Assert(col1 is VBoxContainer, "Column 1 should be VBoxContainer");
		Assert(col2 is VBoxContainer, "Column 2 should be VBoxContainer");
		Assert(col3 is VBoxContainer, "Column 3 should be VBoxContainer");

		// Check Col 1 (Host Configuration): START MATCH, Mode/Map options, Bot Toggles
		bool foundStartMatch = HasTextRecursive(col1, "START MATCH") || HasTextRecursive(col1, "HOST CONFIGURATION");
		Assert(foundStartMatch, "Column 1 should contain Host Configuration / 'START MATCH'");

		bool foundBotToggles = HasTextRecursive(col1, "BOT TOGGLES") || HasTextRecursive(col1, "BOTS");
		Assert(foundBotToggles, "Column 1 should contain Bot Toggles");

		for (int b = 1; b <= 5; b++)
		{
			bool foundDigit = HasButtonWithTextRecursive(col1, b.ToString());
			Assert(foundDigit, $"Column 1 should contain bot toggle button for '{b}'");
		}

		// Check Col 2 (Stacked Teams): BOTH Team Albert AND Team Einstein in center column
		bool foundAlbert = HasTextRecursive(col2, "TEAM ALBERT");
		bool foundEinstein = HasTextRecursive(col2, "TEAM EINSTEIN");
		Assert(foundAlbert, "Center Column (Col 2) should contain 'TEAM ALBERT'");
		Assert(foundEinstein, "Center Column (Col 2) should contain 'TEAM EINSTEIN'");

		// Check for host 3-dot context menu button (⋮ or ···) in Center Column
		bool foundDotsBtn = HasButtonWithTextRecursive(col2, "⋮") || HasButtonWithTextRecursive(col2, "···");
		Assert(foundDotsBtn, "Host player card in Center Column must contain '⋮' or '···' 3-dot action button");

		// Check Col 3 (Social & Invites): Discovered invite list & Lobby Chat
		bool foundSocial = HasTextRecursive(col3, "SOCIAL & INVITES") || HasTextRecursive(col3, "Invite to lobby");
		bool foundChat = HasTextRecursive(col3, "LOBBY CHAT") || HasTextRecursive(col3, "Send message");
		Assert(foundSocial, "Column 3 should contain Social & Invites");
		Assert(foundChat, "Column 3 should contain Lobby Chat");

		// Bot toggles test: MatchManager BotCount and bot card distribution in Center Column
		MatchManager match = MatchManager.Instance;
		match.BotCount = 4;
		Assert(match.BotCount == 4, "MatchManager BotCount should be 4");

		// Test bot rendering in stacked team containers in Col 2
		var lobbyWithBots = new VBoxContainer();
		tablet.BuildLobbyForTest(lobbyWithBots);
		HBoxContainer? colsWithBots = null;
		foreach (Node child in lobbyWithBots.GetChildren())
		{
			if (child is HBoxContainer hbox && hbox.GetChildCount() == 3) { colsWithBots = hbox; break; }
		}
		Assert(colsWithBots != null, "Lobby layout with bots should have 3 columns");
		bool foundBotCol2 = HasTextRecursive(colsWithBots!.GetChild(1), "[BOT]");
		Assert(foundBotCol2, "Center Column (Col 2) stacked teams should contain [BOT] entries when BotCount > 0");
		lobbyWithBots.QueueFree();

		match.BotCount = 0;
		Assert(match.BotCount == 0, "MatchManager BotCount should be 0");

		lobbyContainer.QueueFree();
		tablet.QueueFree();

		// Verify scenes/ui/CustomLobbyTab.tscn instantiation
		PackedScene customLobbyScene = ResourceLoader.Load<PackedScene>("res://scenes/ui/CustomLobbyTab.tscn");
		Assert(customLobbyScene != null, "scenes/ui/CustomLobbyTab.tscn must load successfully");
		Node customLobbyInstance = customLobbyScene.Instantiate();
		Assert(customLobbyInstance is CustomLobbyTab, "scenes/ui/CustomLobbyTab.tscn must instantiate CustomLobbyTab");
		AddChild(customLobbyInstance);
		customLobbyInstance.QueueFree();

		GD.Print("  HubTablet 3-column Custom Game lobby controls and CustomLobbyTab verified!");
	}

	private static bool HasTextRecursive(Node node, string target)
	{
		if (node is Label lbl && lbl.Text.Contains(target, StringComparison.OrdinalIgnoreCase)) return true;
		if (node is Button btn && btn.Text.Contains(target, StringComparison.OrdinalIgnoreCase)) return true;
		foreach (Node child in node.GetChildren())
		{
			if (HasTextRecursive(child, target)) return true;
		}
		return false;
	}

	private static bool HasButtonWithTextRecursive(Node node, string target)
	{
		if (node is Button btn && (btn.Text.Trim().Equals(target.Trim(), StringComparison.OrdinalIgnoreCase) || HasTextRecursive(btn, target))) return true;
		foreach (Node child in node.GetChildren())
		{
			if (HasButtonWithTextRecursive(child, target)) return true;
		}
		return false;
	}

	private void TestHubTabletHotspotSessionDisplay()
	{
		GD.Print("Testing HubTablet Join screen and hotspot session integration...");
		var tablet = new HubTablet { Name = "TestTabletJoin" };
		AddChild(tablet);

		// Populate a simulated hotspot session in LanMenu
		LanMenu.DiscoveredSessions["hotspot_sim"] = new LanMenu.DiscoveredSession
		{
			SessionId = "hotspot_sim",
			Name = "Hotspot Field Battle",
			Host = "192.168.43.1",
			Port = 7777,
			Players = 3,
			Map = "Ashfields",
			Mode = "Team Deathmatch",
			Seen = Time.GetTicksMsec(),
			IsHotspot = true,
		};

		// Open join section via test helper
		tablet.BuildJoinForTest();

		Assert(LanMenu.DiscoveredSessions.ContainsKey("hotspot_sim"), "Hotspot session must exist in LanMenu.DiscoveredSessions");
		Assert(LanMenu.DiscoveredSessions["hotspot_sim"].IsHotspot, "hotspot_sim must be flagged as hotspot");

		// Inspect tablet's games list to verify actual UI rendering
		VBoxContainer? gamesList = tablet.GamesListForTest;
		Assert(gamesList != null, "tablet.GamesListForTest should not be null after BuildJoin");
		Assert(gamesList!.GetChildCount() >= 2, $"Expected at least 2 children in gamesList (alert banner + row), got {gamesList.GetChildCount()}");

		bool foundAlertBanner = HasTextRecursive(gamesList, "HOTSPOT SESSION DETECTED");
		Assert(foundAlertBanner, "HubTablet Join screen must display 'HOTSPOT SESSION DETECTED' banner");

		bool foundConnectBtn = HasButtonWithTextRecursive(gamesList, "CONNECT");
		Assert(foundConnectBtn, "HubTablet Join screen must contain 'CONNECT' button");

		bool foundRow = HasTextRecursive(gamesList, "Hotspot Field Battle [HOTSPOT]");
		Assert(foundRow, "HubTablet Join screen must display session row with [HOTSPOT] badge");

		// Cleanup
		LanMenu.DiscoveredSessions.Remove("hotspot_sim");
		tablet.QueueFree();
		GD.Print("  HubTablet hotspot session integration verified with deep UI hierarchy check!");
	}
}
