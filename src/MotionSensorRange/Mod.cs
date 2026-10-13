using HarmonyLib;
using Isochronous;
using KMod;

namespace MotionSensorRange
{
	public sealed class MotionSensorRangeMod : UserMod2
	{
		public override void OnLoad(Harmony harmony)
		{
			base.OnLoad(harmony);
			Translations.Register(harmony, typeof(ModStrings));
			Debug.Log("[MotionSensorRange] Loaded version " + typeof(MotionSensorRangeMod).Assembly.GetName().Version);
		}
	}
}
