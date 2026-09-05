using HarmonyLib;
using KMod;

namespace MotionSensorRange
{
	public sealed class MotionSensorRangeMod : UserMod2
	{
		public override void OnLoad(Harmony harmony)
		{
			base.OnLoad(harmony);
			Debug.Log("[MotionSensorRange] Loaded version " + typeof(MotionSensorRangeMod).Assembly.GetName().Version);
		}
	}
}
