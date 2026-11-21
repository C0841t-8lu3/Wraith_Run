public static class WeaponAttachmentEffectIds
{
	/// <summary>Multiplier on ADS aim transition speed (>1.0 = faster ADS transition).</summary>
	public const string AdsSpeedMult = "AdsSpeedMult";

	/// <summary>Multiplier on weapon swap/switch speed (>1.0 = faster weapon swap, lowers cooldown).</summary>
	public const string SwitchSpeedMult = "SwitchSpeedMult";

	/// <summary>Multiplier on weapon reload speed (>1.0 = faster reload, lowers reload time).</summary>
	public const string ReloadSpeedMult = "ReloadSpeedMult";

	/// <summary>Multiplier on vertical recoil kick (<1.0 = less upward barrel kick).</summary>
	public const string VerticalRecoilMult = "VerticalRecoilMult";

	/// <summary>Multiplier on horizontal recoil kick (<1.0 = less horizontal drift).</summary>
	public const string HorizontalRecoilMult = "HorizontalRecoilMult";

	/// <summary>Multiplier on bullet / pellet cone spread (<1.0 = tighter accuracy).</summary>
	public const string BulletSpreadMult = "BulletSpreadMult";

	/// <summary>Multiplier on procedural viewmodel visual recoil kick (<1.0 = less screen jump).</summary>
	public const string VisualKickMult = "VisualKickMult";

	/// <summary>Signed delta on magazine capacity (+round count added to magazine).</summary>
	public const string AmmoCapacityDelta = "AmmoCapacityDelta";

	/// <summary>Multiplier on magazine capacity (>1.0 = larger magazine size).</summary>
	public const string AmmoCapacityMult = "AmmoCapacityMult";
}
