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

			// Also give the under-construction building a slider, so the range can be
			// pre-configured (e.g. by mods that expose settings before construction)
			// and carried over when the building completes.
			public static void Postfix()
			{
				BuildingDef def = Assets.GetBuildingDef(LogicDuplicantSensorConfig.ID);
				if (def != null && def.BuildingUnderConstruction != null)
					def.BuildingUnderConstruction.AddOrGet<RangeSlider>();
			}
		}

		[HarmonyPatch(typeof(Constructable), "FinishConstruction")]
		public static class Constructable_FinishConstruction_Patch
		{
			public static void Postfix(Constructable __instance)
			{
				RangeSlider source = __instance.GetComponent<RangeSlider>();
				if (source == null)
					return;
				Building building = __instance.GetComponent<Building>();
				if (building == null || building.Def == null)
					return;
				int cell = Grid.PosToCell(__instance.transform.GetLocalPosition());
				GameObject built = Grid.Objects[cell, (int)building.Def.ObjectLayer];
				if (built == null || built == __instance.gameObject)
					return;
				RangeSlider target = built.GetComponent<RangeSlider>();
				if (target != null)
					target.CopyRangeFrom(source);
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
