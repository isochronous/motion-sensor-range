using HarmonyLib;
using UnityEngine;

namespace MotionSensorRange
{
	public static class Patches
	{
		[HarmonyPatch(typeof(GeneratedBuildings), nameof(GeneratedBuildings.LoadGeneratedBuildings))]
		public static class GeneratedBuildings_LoadGeneratedBuildings_Patch
		{
			public static void Prefix()
			{
				Strings.Add(RangeSlider.TitleKey, RangeSlider.Title);
				Strings.Add(RangeSlider.TooltipKey, RangeSlider.Tooltip);
			}
		}

		[HarmonyPatch(typeof(LogicDuplicantSensorConfig), nameof(LogicDuplicantSensorConfig.DoPostConfigureComplete))]
		public static class LogicDuplicantSensorConfig_DoPostConfigureComplete_Patch
		{
			public static void Postfix(GameObject go)
			{
				go.AddOrGet<RangeSlider>();
			}
		}
	}
}
