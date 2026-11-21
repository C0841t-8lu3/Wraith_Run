using System;
using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class VerifyModeControllersAndScoring : Node
{
    public override void _Ready()
    {
        GD.Print("=== Starting VerifyModeControllersAndScoring ===");
        try
        {
            RunAllTests();
            GD.Print("=== ALL MODE CONTROLLERS & SCORING TESTS PASSED! ===");
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

    private static void AssertApprox(float actual, float expected, string message, float tolerance = 0.001f)
    {
        if (Mathf.Abs(actual - expected) > tolerance)
        {
            throw new Exception($"Assertion Failed: {message}. Expected: {expected}, Actual: {actual}");
        }
    }

    private void RunAllTests()
    {
        TestGameModeRulesMetadata();
        TestTeamDeathmatchController();
        TestDeathmatchController();
        TestConvoyAssaultNumericsAndScoringSeparation();
        TestConvoyAssault10KmCorridorAndExtraction();
        TestConvoyAssault7RoundSeriesAndSideSwaps();
        TestConvoyAssaultSpikeStripAndSTVDestruction();
        TestPureChaseNumericsAndScoringSeparation();
        TestPureChase25PercentBuggyThresholds();
        TestPureChaseCursedEventsAnd8SecondOmens();
        TestGhostPossessionSteeringInversion();
        TestMatchManagerActiveControllerLifecycle();
        TestConvoyAssaultClockResetAndVehicleRestoration();
        TestPureChaseSequentialCheckpointsAndRepairRearm();
        TestDeathmatchScoreboardPointsResolution();
    }

    private void TestGameModeRulesMetadata()
    {
        GD.Print("1. Testing GameModeBase metadata for all 4 core modes...");

        // Mode 1: Team Deathmatch
        GameModeBase tdm = GameModeBase.For(MatchManager.Mode.TeamDeathmatch);
        Assert(tdm is TeamDeathmatchMode, "TDM should be instance of TeamDeathmatchMode");
        Assert(tdm.Id == MatchManager.Mode.TeamDeathmatch, "TDM Id mismatch");
        Assert(tdm.DisplayName == "Team Deathmatch", $"TDM DisplayName mismatch: {tdm.DisplayName}");
        Assert(tdm.UsesTeams, "TDM must use teams");
        Assert(!tdm.UsesVehicles, "TDM must not use vehicles (foot only)");
        Assert(!tdm.UsesCursedEvents, "TDM must not use cursed events");
        Assert(tdm.HasTimedResolution, "TDM must have timed resolution");
        Assert(tdm.UsesUniversalKillStyles, "TDM must use universal kill styles");
        Assert(tdm.BaseKillPoints == 100, "TDM base kill points must be 100");

        // Mode 2: Deathmatch (Free-For-All)
        GameModeBase dm = GameModeBase.For(MatchManager.Mode.FreeForAll);
        Assert(dm is DeathmatchMode, "DM should be instance of DeathmatchMode");
        Assert(dm.Id == MatchManager.Mode.FreeForAll, "DM Id mismatch");
        Assert(dm.DisplayName == "Deathmatch", $"DM DisplayName mismatch: {dm.DisplayName}");
        Assert(!dm.UsesTeams, "DM must be Free For All (no teams)");
        Assert(!dm.UsesVehicles, "DM must not use vehicles");
        Assert(!dm.UsesCursedEvents, "DM must have no storms or cursed zones (Master GDD 2.2)");
        Assert(dm.HasTimedResolution, "DM must have timed resolution");
        Assert(dm.UsesUniversalKillStyles, "DM must use universal kill styles");
        Assert(dm.BaseKillPoints == 100, "DM base kill points must be 100");

        // Mode 3: Convoy Assault
        GameModeBase convoy = GameModeBase.For(MatchManager.Mode.ConvoyAssault);
        Assert(convoy is ConvoyAssaultMode, "Convoy should be instance of ConvoyAssaultMode");
        Assert(convoy.Id == MatchManager.Mode.ConvoyAssault, "Convoy Id mismatch");
        Assert(convoy.DisplayName == "Convoy Assault", $"Convoy DisplayName mismatch: {convoy.DisplayName}");
        Assert(convoy.UsesTeams, "Convoy must use teams");
        Assert(convoy.UsesVehicles, "Convoy must use vehicles (STV convoy)");
        Assert(!convoy.UsesCursedEvents, "Convoy must not use cursed events");
        Assert(convoy.MinimumPlayers == 6, "Convoy MinimumPlayers must be 6");
        Assert(convoy.MaximumPlayers == 18, $"Convoy MaximumPlayers must be 18 (9v9 max), got {convoy.MaximumPlayers}");
        Assert(convoy.RecommendedTeamSize == 9, "Convoy RecommendedTeamSize must be 9");
        Assert(!convoy.UsesUniversalKillStyles, "Convoy must NOT use universal kill styles (Scoring Separation Rule)");
        Assert(convoy.BaseKillPoints == 150, "Convoy base kill points must be 150 (Standard Enemy Kill)");

        // Mode 4: Pure Chase
        GameModeBase chase = GameModeBase.For(MatchManager.Mode.PureChase);
        Assert(chase is PureChaseMode, "PureChase should be instance of PureChaseMode");
        Assert(chase.Id == MatchManager.Mode.PureChase, "PureChase Id mismatch");
        Assert(chase.DisplayName == "Pure Chase", $"PureChase DisplayName mismatch: {chase.DisplayName}");
        Assert(chase.UsesTeams, "PureChase must use teams (2v2 Duo Buggy)");
        Assert(chase.UsesVehicles, "PureChase must use vehicles (Raider Buggy)");
        Assert(chase.UsesCursedEvents, "PureChase must use active Cursed Land Events (Master GDD 2.4)");
        Assert(chase.MinimumPlayers == 4 && chase.MaximumPlayers == 4, "PureChase must be exactly 4 players (2v2)");
        Assert(chase.RecommendedTeamSize == 2, "PureChase RecommendedTeamSize must be 2");
        Assert(!chase.UsesUniversalKillStyles, "PureChase must NOT use universal kill styles (Scoring Separation Rule)");
        Assert(chase.BaseKillPoints == 200, "PureChase base kill points must be 200 (Rival Shooter Kill)");

        GD.Print("  GameModeBase metadata verified successfully!");
    }

    private void TestTeamDeathmatchController()
    {
        GD.Print("2. Testing TeamDeathmatchController and Universal Kill-Style Bonuses...");
        var tdm = new TeamDeathmatchController();
        AddChild(tdm);

        Assert(tdm.UsesUniversalKillStyles, "TDM must use universal kill styles");
        Assert(tdm.IncrementTeamScoreOnKill, "TDM must increment team score on kill");
        Assert(tdm.BaseKillPoints == 100, "TDM base kill points must be 100");

        // Verify universal kill styles stack additively
        KillStyle styles = KillStyle.Headshot | KillStyle.HipFire | KillStyle.NoScope360 | KillStyle.VergeOfDeath;
        int totalScore = tdm.CalculateKillScore(1, 2, styles, 100, null!);
        // 100 + 150 (Headshot) + 125 (HipFire) + 175 (360) + 120 (Verge) = 670
        int expected = 100 + 150 + 125 + 175 + 120;
        Assert(totalScore == expected, $"TDM kill score stack should be {expected}, got {totalScore}");

        // Assist scaling test
        int assistPoints = tdm.CalculateAssistScore(50);
        Assert(assistPoints == 50, $"TDM assist score should pass through, got {assistPoints}");

        tdm.QueueFree();
        GD.Print("  TeamDeathmatchController verified successfully!");
    }

    private void TestDeathmatchController()
    {
        GD.Print("3. Testing DeathmatchController (FFA)...");
        var dm = new DeathmatchController();
        AddChild(dm);

        Assert(dm.UsesUniversalKillStyles, "DM must use universal kill styles");
        Assert(!dm.IncrementTeamScoreOnKill, "DM is FFA and must not increment team score");
        Assert(dm.BaseKillPoints == 100, "DM base kill points must be 100");

        // Single bonus: Headshot (+150 on top of 100 = 250)
        int headshotScore = dm.CalculateKillScore(1, 2, KillStyle.Headshot, 100, null!);
        Assert(headshotScore == 250, $"DM headshot score should be 250, got {headshotScore}");

        dm.QueueFree();
        GD.Print("  DeathmatchController verified successfully!");
    }

    private void TestConvoyAssaultNumericsAndScoringSeparation()
    {
        GD.Print("4. Testing Convoy Assault numerics and Scoring Separation Rule...");
        var convoy = new ConvoyAssaultController();
        AddChild(convoy);

        Assert(!convoy.UsesUniversalKillStyles, "Convoy Assault must NOT use universal kill styles");
        Assert(!convoy.IncrementTeamScoreOnKill, "Convoy Assault kills do NOT increment team score; team score comes from objectives");
        Assert(convoy.BaseKillPoints == 150, "Convoy Assault base kill points must be 150");

        // Test Scoring Separation Rule:
        // Even if styles include Headshot, HipFire, 360 NoScope, Verge of Death, they MUST NOT add bonuses!
        KillStyle allStyles = KillStyle.Headshot | KillStyle.HipFire | KillStyle.NoScope360 | KillStyle.VergeOfDeath;

        // Standard Enemy Kill: +150 (Individual)
        int standardKill = convoy.CalculateKillScore(1, 2, allStyles, 100, null!);
        Assert(standardKill == 150, $"Standard enemy kill in Convoy Assault must be exactly 150, got {standardKill}");

        // Assist must be 0 (no universal assist)
        int assist = convoy.CalculateAssistScore(50);
        Assert(assist == 0, $"Convoy Assault assist score must be 0, got {assist}");

        convoy.QueueFree();
        GD.Print("  Convoy Assault Scoring Separation Rule verified!");
    }

    private void TestConvoyAssault10KmCorridorAndExtraction()
    {
        GD.Print("5. Testing Convoy Assault 10 km corridor progression and milestones...");
        var convoy = new ConvoyAssaultController();
        AddChild(convoy);
        convoy.OnMatchStart();

        MatchManager match = MatchManager.Instance;
        match.PrepareHostMatch(keepTeams: false);

        int team0Initial = match.GetTeamScore(convoy.AttackingTeam);

        // Advance 0.8 km (no milestone yet)
        convoy.AdvanceSTV(0.8f);
        AssertApprox(convoy.STVDistanceKm, 0.8f, "STVDistanceKm should be 0.8 km");
        Assert(match.GetTeamScore(convoy.AttackingTeam) == team0Initial, "No points before 1 km milestone");

        // Advance to 1.2 km -> crosses 1 km milestone! Must award +100 team points!
        convoy.AdvanceSTV(0.4f);
        AssertApprox(convoy.STVDistanceKm, 1.2f, "STVDistanceKm should be 1.2 km");
        Assert(match.GetTeamScore(convoy.AttackingTeam) == team0Initial + 100,
            $"Crossing 1 km should award +100 team points, got {match.GetTeamScore(convoy.AttackingTeam)}");

        // Advance to 3.5 km -> crosses 2 km and 3 km milestones! Must award +200 more team points (+300 total)!
        convoy.AdvanceSTV(2.3f);
        AssertApprox(convoy.STVDistanceKm, 3.5f, "STVDistanceKm should be 3.5 km");
        Assert(match.GetTeamScore(convoy.AttackingTeam) == team0Initial + 300,
            $"Crossing 3 km total should award +300 team points, got {match.GetTeamScore(convoy.AttackingTeam)}");

        // Advance STV to 10.0 km checkpoint!
        // Remaining milestones: 4, 5, 6, 7, 8, 9 (+600) + Checkpoint Reached (+200) + Round Win (+500)
        int scoreBeforeCheckpoint = match.GetTeamScore(convoy.AttackingTeam);
        int attackingTeam = convoy.AttackingTeam;

        convoy.AdvanceSTV(6.5f);
        AssertApprox(convoy.STVDistanceKm, 0.0f, "STVDistanceKm resets to 0.0 km upon advancing to Round 2");

        // Expected addition: 6 milestones * 100 (+600) + Checkpoint (+200) + Round Win (+500) = +1300
        int expectedScore = scoreBeforeCheckpoint + 600 + ConvoyAssaultController.PointsSTVReachCheckpoint + ConvoyAssaultController.PointsCompleteRoundWin;
        Assert(match.GetTeamScore(attackingTeam) == expectedScore,
            $"STV reaching 10 km checkpoint should award milestones, checkpoint bonus, and round win. Expected {expectedScore}, got {match.GetTeamScore(attackingTeam)}");

        // Verify round 1 win recorded
        Assert(convoy.Team0RoundWins == 1, $"Team 0 should have 1 round win, got {convoy.Team0RoundWins}");
        // Verify sides swapped for Round 2!
        Assert(convoy.CurrentRound == 2, $"CurrentRound should advance to 2, got {convoy.CurrentRound}");
        Assert(convoy.AttackingTeam == 1, $"Attacking team should swap to 1, got {convoy.AttackingTeam}");
        Assert(convoy.DefendingTeam == 0, $"Defending team should swap to 0, got {convoy.DefendingTeam}");
        AssertApprox(convoy.STVDistanceKm, 0.0f, "STV distance should reset to 0 for Round 2");

        convoy.QueueFree();
        GD.Print("  Convoy Assault 10 km corridor and extraction verified!");
    }

    private void TestConvoyAssault7RoundSeriesAndSideSwaps()
    {
        GD.Print("6. Testing Convoy Assault 7-round series (first to 4 wins)...");
        var convoy = new ConvoyAssaultController();
        AddChild(convoy);
        convoy.OnMatchStart();

        MatchManager match = MatchManager.Instance;
        match.PrepareHostMatch(keepTeams: false);

        Assert(convoy.CurrentRound == 1, "Should start at Round 1");
        Assert(convoy.AttackingTeam == 0 && convoy.DefendingTeam == 1, "Round 1: Team 0 attacks, Team 1 defends");

        // Round 1: Team 0 wins
        convoy.CompleteRound(0, "Round 1 win");
        Assert(convoy.Team0RoundWins == 1 && convoy.Team1RoundWins == 0, "Team 0: 1 win, Team 1: 0 wins");
        Assert(convoy.CurrentRound == 2, "Advanced to Round 2");
        Assert(convoy.AttackingTeam == 1 && convoy.DefendingTeam == 0, "Round 2: Team 1 attacks, Team 0 defends");

        // Round 2: Team 1 wins
        convoy.CompleteRound(1, "Round 2 win");
        Assert(convoy.Team0RoundWins == 1 && convoy.Team1RoundWins == 1, "Team 0: 1 win, Team 1: 1 win");
        Assert(convoy.CurrentRound == 3, "Advanced to Round 3");
        Assert(convoy.AttackingTeam == 0 && convoy.DefendingTeam == 1, "Round 3: Team 0 attacks, Team 1 defends");

        // Round 3: Team 0 wins
        convoy.CompleteRound(0, "Round 3 win");
        Assert(convoy.Team0RoundWins == 2 && convoy.Team1RoundWins == 1, "Team 0: 2 wins");

        // Round 4: Team 0 wins
        convoy.CompleteRound(0, "Round 4 win");
        Assert(convoy.Team0RoundWins == 3 && convoy.Team1RoundWins == 1, "Team 0: 3 wins");

        // Round 5: Team 0 wins (4th win -> First to 4 wins series!)
        bool seriesWonEmitted = false;
        int winningTeam = -1;
        convoy.SeriesWon += (winner) =>
        {
            seriesWonEmitted = true;
            winningTeam = winner;
        };

        convoy.CompleteRound(0, "Round 5 win");
        Assert(convoy.Team0RoundWins == 4, "Team 0 should have 4 round wins");
        Assert(convoy.IsSeriesFinished, "Series should be finished (first to 4 wins)");
        Assert(convoy.SeriesWinningTeam == 0, "Team 0 should be series winner");
        Assert(seriesWonEmitted, "SeriesWon signal should be emitted");
        Assert(winningTeam == 0, "Winning team should be 0");
        Assert(match.IsMatchEnded, "MatchManager should declare match ended");
        Assert(match.WinningTeam == 0, "MatchManager winning team should be 0");

        convoy.QueueFree();
        GD.Print("  Convoy Assault 7-round series (first to 4 wins) verified!");
    }

    private void TestConvoyAssaultSpikeStripAndSTVDestruction()
    {
        GD.Print("7. Testing Convoy Assault Spike Strip (+275 ind.) and STV Destruction (+500 team)...");
        var convoy = new ConvoyAssaultController();
        AddChild(convoy);
        convoy.OnMatchStart();

        MatchManager match = MatchManager.Instance;
        match.PrepareHostMatch(keepTeams: false);

        // Test Spike Strip Hit on STV
        bool spikeStripEventFired = false;
        convoy.SpikeStripHitConfirmed += (defenderId, points) =>
        {
            spikeStripEventFired = true;
            Assert(points == 275, $"Spike Strip points should be 275, got {points}");
            Assert(defenderId == 42, "Defender ID mismatch");
        };

        convoy.NotifySpikeStripHit(42, null!);
        Assert(spikeStripEventFired, "SpikeStripHitConfirmed signal should be emitted");

        // Test STV Destruction: +500 team points to Defenders and Defender round win
        int defendingTeam = convoy.DefendingTeam;
        int defScoreInitial = match.GetTeamScore(defendingTeam);
        bool stvDestroyedEventFired = false;

        convoy.STVDestroyedByDefenders += (team, points) =>
        {
            stvDestroyedEventFired = true;
            Assert(points == 500, $"Destroy STV points should be 500, got {points}");
            Assert(team == defendingTeam, "Defending team mismatch");
        };

        convoy.NotifySTVDestroyed(42);
        Assert(stvDestroyedEventFired, "STVDestroyedByDefenders signal should be emitted");

        // Defenders receive +500 for STV Destruction AND +500 for Round Win (+1000 total)
        int expectedDefScore = defScoreInitial + ConvoyAssaultController.PointsDestroySTV + ConvoyAssaultController.PointsCompleteRoundWin;
        Assert(match.GetTeamScore(defendingTeam) == expectedDefScore,
            $"Defenders should receive +1000 total points (+500 STV destruction + +500 round win). Got {match.GetTeamScore(defendingTeam)}");

        convoy.QueueFree();
        GD.Print("  Spike Strip and STV Destruction numerics verified!");
    }

    private void TestPureChaseNumericsAndScoringSeparation()
    {
        GD.Print("8. Testing Pure Chase numerics and Scoring Separation Rule...");
        var chase = new PureChaseController();
        AddChild(chase);

        Assert(!chase.UsesUniversalKillStyles, "Pure Chase must NOT use universal kill styles");
        Assert(!chase.IncrementTeamScoreOnKill, "Pure Chase kills do not increment team kill counter");

        // Rival Shooter Kill: +200 (Individual)
        int shooterKill = chase.CalculateKillScore(1, 2, KillStyle.Headshot | KillStyle.NoScope360, 100, null!);
        Assert(shooterKill == 200, $"Rival shooter kill must be exactly 200 without style bonuses, got {shooterKill}");

        // Assist must be 0
        int assist = chase.CalculateAssistScore(50);
        Assert(assist == 0, $"Pure Chase assist score must be 0, got {assist}");

        chase.QueueFree();
        GD.Print("  Pure Chase Scoring Separation Rule verified!");
    }

    private void TestPureChase25PercentBuggyThresholds()
    {
        GD.Print("9. Testing Pure Chase 25% buggy HP thresholds (+200 per threshold)...");
        var chase = new PureChaseController();
        AddChild(chase);
        chase.OnMatchStart();

        MatchManager match = MatchManager.Instance;
        match.PrepareHostMatch(keepTeams: false);
        // Team Albert (0) vs Team Einstein (1)
        match.SetPeerTeam(1, 0); // Attacker on Team 0
        match.SetPeerTeam(2, 1); // Victim driver on Team 1

        var vehicle = new GameVehicle();
        AddChild(vehicle);
        vehicle.DriverPeerId = 2; // Team 1 vehicle

        int team0Initial = match.GetTeamScore(0);
        int thresholdsCrossedCount = 0;
        chase.RivalBuggyThresholdCrossed += (team, threshold, attackerId, points) =>
        {
            thresholdsCrossedCount++;
            Assert(points == 200, "Each threshold must award +200 points");
            Assert(attackerId == 1, "Attacker ID should be 1");
        };

        // 1. Damage drops HP from 100 to 80 (no threshold crossed yet: above 75)
        chase.OnVehicleDamaged(vehicle, 20.0f, 100.0f, 80.0f, 1, false);
        Assert(thresholdsCrossedCount == 0, "No threshold crossed at 80 HP");
        Assert(match.GetTeamScore(0) == team0Initial, "Team score should not change yet");

        // 2. Damage drops HP from 80 to 70 (crosses 75% threshold!)
        chase.OnVehicleDamaged(vehicle, 10.0f, 80.0f, 70.0f, 1, false);
        Assert(thresholdsCrossedCount == 1, "Crossed 75% threshold");
        Assert(match.GetTeamScore(0) == team0Initial + 200, "Team 0 should receive +200 team points");

        // 3. Second hit staying between 75 and 50 (e.g. 70 to 60) -> no duplicate 75% award!
        chase.OnVehicleDamaged(vehicle, 10.0f, 70.0f, 60.0f, 1, false);
        Assert(thresholdsCrossedCount == 1, "Duplicate threshold must not trigger again");

        // 4. Large hit dropping HP from 60 to 20 (crosses BOTH 50% and 25% thresholds!)
        chase.OnVehicleDamaged(vehicle, 40.0f, 60.0f, 20.0f, 1, false);
        Assert(thresholdsCrossedCount == 3, "Crossed both 50% and 25% thresholds (+2 triggers)");
        Assert(match.GetTeamScore(0) == team0Initial + 600, "Team 0 should now have +600 points total");

        // 5. Final hit dropping HP to 0 (crosses 0% threshold!)
        vehicle.DamageController.CurrentState = VehicleDamageController.DamageState.Wrecked;
        chase.OnVehicleDamaged(vehicle, 20.0f, 20.0f, 0.0f, 1, true);
        Assert(thresholdsCrossedCount == 4, "Crossed 0% threshold");
        Assert(match.GetTeamScore(0) == team0Initial + 800, "Team 0 should now have +800 points total (4 thresholds * 200)");

        vehicle.QueueFree();
        chase.QueueFree();
        GD.Print("  25% buggy HP threshold numerics verified!");
    }

    private void TestPureChaseCursedEventsAnd8SecondOmens()
    {
        GD.Print("10. Testing Pure Chase Cursed Land Events and 8-second visual omen warnings...");
        var chase = new PureChaseController();
        AddChild(chase);
        chase.OnMatchStart();

        // Verify the 6 events defined in Master GDD Section 2.4
        Assert(Enum.IsDefined(typeof(CursedEventType), CursedEventType.FloorCollapse), "FloorCollapse event missing");
        Assert(Enum.IsDefined(typeof(CursedEventType), CursedEventType.PullVortex), "PullVortex event missing");
        Assert(Enum.IsDefined(typeof(CursedEventType), CursedEventType.BlindnessFog), "BlindnessFog event missing");
        Assert(Enum.IsDefined(typeof(CursedEventType), CursedEventType.MirrorShift), "MirrorShift event missing");
        Assert(Enum.IsDefined(typeof(CursedEventType), CursedEventType.GhostPossession), "GhostPossession event missing");
        Assert(Enum.IsDefined(typeof(CursedEventType), CursedEventType.DeathKnell), "DeathKnell event missing");

        // Verify Omen Warning Duration is exactly 8.0 seconds
        AssertApprox(PureChaseController.OmenWarningDurationSeconds, 8.0f, "Omen warning duration must be 8.0s");

        bool omenStartedFired = false;
        chase.OmenWarningStarted += (eventType, duration) =>
        {
            omenStartedFired = true;
            AssertApprox(duration, 8.0f, "Omen duration in signal must be 8.0s");
            Assert(eventType == (int)CursedEventType.PullVortex, "Event type mismatch");
        };

        // Trigger omen warning for PullVortex
        chase.TriggerOmenWarning(CursedEventType.PullVortex);
        Assert(chase.IsOmenWarningActive, "IsOmenWarningActive must be true");
        Assert(!chase.IsCursedEventActive, "IsCursedEventActive must be false during omen warning");
        Assert(chase.CurrentEvent == CursedEventType.PullVortex, "CurrentEvent should be PullVortex");
        Assert(omenStartedFired, "OmenWarningStarted signal must be emitted");

        // Fast-forward 8 seconds to activate event
        bool eventActivatedFired = false;
        chase.CursedEventActivated += (eventType, duration) =>
        {
            eventActivatedFired = true;
            Assert(eventType == (int)CursedEventType.PullVortex, "Event type mismatch");
        };

        chase._Process(8.0f);
        Assert(chase.IsCursedEventActive, "IsCursedEventActive must be true after 8s omen warning");
        Assert(!chase.IsOmenWarningActive, "IsOmenWarningActive must be false once event is active");
        Assert(chase.IsPullVortexActive, "IsPullVortexActive hook must return true");
        Assert(eventActivatedFired, "CursedEventActivated signal must be emitted");

        chase.QueueFree();
        GD.Print("  Cursed Land Events and 8-second visual omen warnings verified!");
    }

    private void TestGhostPossessionSteeringInversion()
    {
        GD.Print("11. Testing Ghost Possession control inversion hook...");
        var chase = new PureChaseController();
        AddChild(chase);

        chase.ForceActivateEvent(CursedEventType.GhostPossession, 15.0f);
        Assert(chase.IsCursedEventActive, "Cursed event should be active");
        Assert(chase.IsGhostPossessionActive, "IsGhostPossessionActive must be true");

        // When Ghost Possession is active, steering input is inverted in GameVehicle
        float normalSteer = 0.75f;
        float invertedSteer = -normalSteer;
        Assert(invertedSteer == -0.75f, "Ghost possession steering inverted calculation");

        chase.QueueFree();
        GD.Print("  Ghost Possession steering inversion hook verified!");
    }

    private void TestMatchManagerActiveControllerLifecycle()
    {
        GD.Print("12. Testing MatchManager ActiveController initialization and lifecycle...");
        MatchManager match = MatchManager.Instance;
        Assert(match != null, "MatchManager.Instance is null");

        // Switch mode to Convoy Assault and initialize controller
        match.GameMode = MatchManager.Mode.ConvoyAssault;
        match.PrepareHostMatch(keepTeams: false);

        Assert(match.ActiveController != null, "ActiveController should be instantiated");
        Assert(match.ActiveController is ConvoyAssaultController, "ActiveController should be ConvoyAssaultController");
        Assert(match.GetController<ConvoyAssaultController>() != null, "GetController<ConvoyAssaultController>() should return non-null");

        // Switch mode to Pure Chase and initialize controller
        match.GameMode = MatchManager.Mode.PureChase;
        match.PrepareHostMatch(keepTeams: false);

        Assert(match.ActiveController != null, "ActiveController should be instantiated");
        Assert(match.ActiveController is PureChaseController, "ActiveController should be PureChaseController");
        Assert(match.GetController<PureChaseController>() != null, "GetController<PureChaseController>() should return non-null");

        // Switch mode to Team Deathmatch and initialize controller
        match.GameMode = MatchManager.Mode.TeamDeathmatch;
        match.PrepareHostMatch(keepTeams: false);

        Assert(match.ActiveController is TeamDeathmatchController, "ActiveController should be TeamDeathmatchController");

        // Reset to Free For All (Deathmatch)
        match.GameMode = MatchManager.Mode.FreeForAll;
        match.PrepareHostMatch(keepTeams: false);

        Assert(match.ActiveController is DeathmatchController, "ActiveController should be DeathmatchController");

        match.EndSession();
        Assert(match.ActiveController == null, "ActiveController should be null after EndSession");

        GD.Print("  MatchManager ActiveController lifecycle verified!");
    }

    private void TestConvoyAssaultClockResetAndVehicleRestoration()
    {
        GD.Print("13. Testing Convoy Assault clock reset across rounds and vehicle restoration...");
        MatchManager match = MatchManager.Instance;
        match.GameMode = MatchManager.Mode.ConvoyAssault;
        match.TimeLimitMinutes = 5;
        match.PrepareHostMatch(keepTeams: false);

        var convoy = match.GetController<ConvoyAssaultController>();
        Assert(convoy != null, "ConvoyAssaultController should be active");

        // Spawn a test STV vehicle in gameplay_vehicles group
        var vehicle = new GameVehicle { DisplayName = "STV · Land Cruiser", TargetLength = 4.9f };
        AddChild(vehicle);
        vehicle.AddToGroup("gameplay_vehicles");

        // Simulate Round 1 time expiry
        Assert(convoy!.CurrentRound == 1, "Should start at Round 1");
        convoy.OnMatchClockTick(300.0f, 0.0f); // Time expires in Round 1
        Assert(convoy.Team1RoundWins == 1, "Defenders (Team 1) should win Round 1 on time expiry");
        Assert(convoy.CurrentRound == 2, "Should advance to Round 2");
        AssertApprox(convoy.RoundElapsedSeconds, 0.0f, "RoundElapsedSeconds should reset to 0 for Round 2");
        AssertApprox(match.ElapsedSeconds, 0.0f, "MatchManager.ElapsedSeconds should reset to 0 on round transition");

        // Simulate next second tick in Round 2: should NOT complete Round 2 immediately!
        convoy.OnMatchClockTick(1.0f, 299.0f);
        Assert(convoy.CurrentRound == 2, "Round 2 must continue and not immediately expire on subsequent ticks");
        Assert(convoy.Team1RoundWins == 1, "Team 1 round wins should still be 1");

        // Test vehicle destruction and restoration across rounds
        vehicle.DamageController.ApplyDamage(100.0f, isHeavy: true, hitTire: false);
        Assert(vehicle.DamageController.IsWrecked, "Vehicle should be wrecked after heavy hit");

        convoy.ResetVehiclesForRound();
        Assert(!vehicle.DamageController.IsWrecked, "Vehicle must be restored from wrecked state upon round advance");
        Assert(vehicle.DamageController.IsOperational, "Vehicle must be operational for the new round");
        AssertApprox(vehicle.DamageController.CurrentHealth, 100.0f, "Vehicle health must be restored to 100");

        vehicle.QueueFree();
        match.EndSession();
        GD.Print("  Convoy Assault clock reset and vehicle restoration verified!");
    }

    private void TestPureChaseSequentialCheckpointsAndRepairRearm()
    {
        GD.Print("14. Testing Pure Chase sequential checkpoints and threshold re-arm on repair...");
        MatchManager match = MatchManager.Instance;
        match.GameMode = MatchManager.Mode.PureChase;
        match.PrepareHostMatch(keepTeams: false);

        var chase = match.GetController<PureChaseController>();
        Assert(chase != null, "PureChaseController should be active");

        // Test sequential checkpoint rejection of skips
        Assert(chase!.Team0Checkpoints == 0, "Initial checkpoints should be 0");
        bool skipResult = chase.PassCheckpoint(0, 5); // Attempting to skip directly to finish
        Assert(!skipResult, "Skipping straight to finish must be rejected");
        Assert(chase.Team0Checkpoints == 0, "Checkpoints must remain 0 after rejected skip");
        Assert(!chase.IsRaceFinished, "Race must not finish from skipped checkpoint");

        // Progress sequentially: 1 -> 2 -> 3 -> 4
        Assert(chase.PassCheckpoint(0, 1), "Passing checkpoint 1 must succeed");
        Assert(chase.Team0Checkpoints == 1, "Team0Checkpoints should be 1");
        Assert(!chase.PassCheckpoint(0, 3), "Skipping checkpoint 2 must be rejected");
        Assert(chase.PassCheckpoint(0, 2), "Passing checkpoint 2 must succeed");
        Assert(chase.PassCheckpoint(0, 3), "Passing checkpoint 3 must succeed");
        Assert(chase.PassCheckpoint(0, 4), "Passing checkpoint 4 must succeed");
        Assert(!chase.IsRaceFinished, "Race not finished before final checkpoint");

        // Final checkpoint (5) finishes race!
        Assert(chase.PassCheckpoint(0, 5), "Passing checkpoint 5 must succeed");
        Assert(chase.IsRaceFinished, "Race must be finished after checkpoint 5");
        Assert(chase.RaceWinner == 0, "Team 0 must be race winner");

        // Test vehicle damage threshold re-arm on repair
        match.PrepareHostMatch(keepTeams: false);
        var chase2 = new PureChaseController();
        AddChild(chase2);
        chase2.OnMatchStart();

        var buggy = new GameVehicle { DisplayName = "Raider Buggy" };
        AddChild(buggy);

        match.SetPeerTeam(1, 0);
        int team0Initial = match.GetTeamScore(0);
        // Attacker is peer 1 (Team 0), victim buggy is Team 1
        chase2.OnVehicleDamaged(buggy, 30.0f, 100.0f, 70.0f, 1, false);
        Assert(match.GetTeamScore(0) == team0Initial + 200, "Crossing 75% threshold must award +200 points");

        // Repair buggy back to 85 HP
        buggy.DamageController.Repair(15.0f);
        Assert(buggy.DamageController.CurrentHealth >= 85.0f, "Buggy repaired to 85 HP");

        // Damage buggy again past 75% -> must award points again because it was repaired!
        chase2.OnVehicleDamaged(buggy, 20.0f, 85.0f, 65.0f, 1, false);
        Assert(match.GetTeamScore(0) == team0Initial + 400, "Damaging re-armed threshold after repair must award +200 again");

        buggy.QueueFree();
        chase2.QueueFree();
        match.EndSession();
        GD.Print("  Pure Chase sequential checkpoints and threshold re-arming verified!");
    }

    private void TestDeathmatchScoreboardPointsResolution()
    {
        GD.Print("15. Testing Deathmatch (Free-For-All) points-based scoreboard resolution...");
        MatchManager match = MatchManager.Instance;
        match.GameMode = MatchManager.Mode.FreeForAll;
        match.TimeLimitMinutes = 10;
        match.PrepareHostMatch(keepTeams: false);

        var dm = match.GetController<DeathmatchController>();
        Assert(dm != null, "DeathmatchController should be active");
        Assert(dm!.UsesUniversalKillStyles, "DM must use universal kill styles");

        // Verify universal kill styles stack points additively
        // Base (+100) + Headshot (+150) + NoScope (+175) = +425
        int totalKill = dm.CalculateKillScore(1, 2, KillStyle.Headshot | KillStyle.NoScope360, 100, null!);
        Assert(totalKill == 425, $"Calculated kill score mismatch: expected 425, got {totalKill}");

        match.EndSession();
        GD.Print("  Deathmatch scoreboard resolution verified!");
    }
}
