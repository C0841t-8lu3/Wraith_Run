using Godot;
using System;

/// <summary>
/// BotController (Master GDD Section 6.2, 7 & entities/BotController.tscn):
/// Dedicated AI controller node spawned only when Enable Bots = Yes.
/// Supports 1-5 Difficulty scaling (aim accuracy, reaction time, tactical decision-making).
/// </summary>
public partial class BotController : Node
{
    [Export(PropertyHint.Range, "1,5,1")]
    public int Difficulty { get; set; } = 3;

    public BotBrain? Brain { get; private set; }
    public CharacterBody3D? ControlledPlayer { get; private set; }

    public override void _Ready()
    {
        ControlledPlayer = GetParent() as CharacterBody3D;
        if (ControlledPlayer != null)
        {
            AttachAiBrain(ControlledPlayer);
        }
    }

    public void SetDifficultyLevel(int level)
    {
        Difficulty = Math.Clamp(level, 1, 5);
        if (ControlledPlayer != null && Brain != null)
        {
            ApplyDifficultyParameters();
        }
    }

    private void AttachAiBrain(CharacterBody3D player)
    {
        Brain = player.GetNodeOrNull<BotBrain>("BotBrain");
        if (Brain == null)
        {
            Brain = new BotBrain { Name = "BotBrain" };
            player.AddChild(Brain);
        }
        ApplyDifficultyParameters();
    }

    private void ApplyDifficultyParameters()
    {
        // Difficulty 1: Novice, slow reaction, wide aim spread
        // Difficulty 3: Normal, standard human pacing
        // Difficulty 5: Veteran/Elite, instant lock, aggressive tactics
        if (MatchManager.Instance != null)
        {
            MatchManager.Instance.BotLevel = (BotDifficulty)Math.Clamp(Difficulty - 1, 0, 4);
        }
    }
}
