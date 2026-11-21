using Godot;
using System;

/// <summary>
/// A weapon attachment from the 3D Gunsmith configurator (Optics, Muzzle, Underbarrel,
/// Laser/Flashlight, Magazine, Stock) with its associated stat modifiers.
/// </summary>
[GlobalClass]
public partial class AttachmentData : Resource
{
	public enum AttachmentSlot { Optics, Muzzle, Underbarrel, Laser, Magazine, Stock }

	[Export] public string AttachmentId { get; set; } = string.Empty;
	[Export] public string AttachmentName { get; set; } = string.Empty;
	[Export] public AttachmentSlot Slot { get; set; } = AttachmentSlot.Optics;
	[Export] public StatModifier[] Modifiers { get; set; } = Array.Empty<StatModifier>();
	[Export(PropertyHint.File, "*.glb,*.gltf,*.tscn")] public string ModelPath { get; set; } = string.Empty;

	public float GetModifier(string effectId, float defaultValue)
	{
		if (Modifiers == null) return defaultValue;
		bool isMult = effectId.EndsWith("Mult");
		bool found = false;
		float total = isMult ? 1.0f : 0.0f;
		foreach (var mod in Modifiers)
		{
			if (mod != null && mod.EffectId == effectId)
			{
				if (isMult)
					total *= mod.Value;
				else
					total += mod.Value;
				found = true;
			}
		}
		return found ? total : defaultValue;
	}
}
