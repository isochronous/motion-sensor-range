using System;
using System.Reflection;
using HarmonyLib;
using KSerialization;
using UnityEngine;

#pragma warning disable 649, 169 // [MyCmpGet]/[MyCmpAdd] fields are handled by the game via reflection

namespace MotionSensorRange
{
	/// <summary>
	/// Adds a range slider side screen to the Duplicant Motion Sensor and applies the
	/// chosen range to the sensor's detection logic, partitioner registration and
	/// range visualizer.
	/// </summary>
	[SerializationConfig(MemberSerialization.OptIn)]
	public sealed class RangeSlider : KMonoBehaviour, IIntSliderControl
	{
		public const int MinRange = 2;
		public const int MaxRange = 16;
		public const int DefaultRange = 4;

		public const string TitleKey = "STRINGS.UI.UISIDESCREENS.MOTION_SENSOR_RANGE_SIDE_SCREEN.TITLE";
		public const string Title = "Range";
		public const string TooltipKey = "STRINGS.UI.UISIDESCREENS.MOTION_SENSOR_RANGE_SIDE_SCREEN.TOOLTIP";
		public const string Tooltip = "Set how far away the sensor can detect Duplicants";

		// LogicDuplicantSensor keeps its scene partitioner state in private members;
		// these are re-applied whenever the range changes (names verified against U59-744825).
		private static readonly FieldInfo ExtentsField = AccessTools.Field(typeof(LogicDuplicantSensor), "pickupableExtents");
		private static readonly FieldInfo EntryField = AccessTools.Field(typeof(LogicDuplicantSensor), "pickupablesChangedEntry");
		private static readonly FieldInfo DirtyField = AccessTools.Field(typeof(LogicDuplicantSensor), "pickupablesDirty");
		private static readonly MethodInfo OnPickupablesChangedMethod = AccessTools.Method(typeof(LogicDuplicantSensor), "OnPickupablesChanged");
		private static readonly MethodInfo RefreshReachableCellsMethod = AccessTools.Method(typeof(LogicDuplicantSensor), "RefreshReachableCells");

		private static readonly EventSystem.IntraObjectHandler<RangeSlider> OnCopySettingsDelegate =
			new EventSystem.IntraObjectHandler<RangeSlider>(delegate(RangeSlider component, object data)
			{
				component.OnCopySettings(data);
			});

		[Serialize]
		private int range = DefaultRange;

		// Opts the motion sensor into the vanilla copy-settings system (the vanilla
		// building has nothing copyable, so it lacks this component).
		[MyCmpAdd]
		private CopyBuildingSettings copyBuildingSettings;

		[MyCmpGet]
		private LogicDuplicantSensor sensor;

		[MyCmpGet]
		private RangeVisualizer visualizer;

		[MyCmpGet]
		private Rotatable rotatable;

		public string SliderTitleKey => TitleKey;

		public string SliderUnits => string.Empty;

		public int SliderDecimalPlaces(int index) => 0;

		public float GetSliderMin(int index) => MinRange;

		public float GetSliderMax(int index) => MaxRange;

		public float GetSliderValue(int index) => range;

		public string GetSliderTooltipKey(int index) => TooltipKey;

		public string GetSliderTooltip(int index) => $"Detects Duplicants within {range} tiles";

		public void SetSliderValue(float value, int index)
		{
			// The sensor's scan area is only symmetric for even ranges
			// (it spans range/2 tiles to each side), so snap to even values.
			int newRange = Mathf.Clamp(Mathf.RoundToInt(value / 2f) * 2, MinRange, MaxRange);
			if (newRange == range)
				return;

			range = newRange;
			ApplyRange();
		}

		protected override void OnPrefabInit()
		{
			base.OnPrefabInit();
			Subscribe((int)GameHashes.CopySettings, OnCopySettingsDelegate);
		}

		protected override void OnSpawn()
		{
			base.OnSpawn();
			// LogicDuplicantSensor.OnSpawn has already registered with the default
			// range at this point; re-apply the deserialized value.
			ApplyRange();
		}

		private void OnCopySettings(object data)
		{
			GameObject sourceGo = data as GameObject;
			if (sourceGo != null)
				CopyRangeFrom(sourceGo.GetComponent<RangeSlider>());
		}

		/// <summary>
		/// Takes the range from another slider (copy-settings tool, or the
		/// under-construction building when construction completes). Safe before
		/// spawn: OnSpawn applies the stored value.
		/// </summary>
		internal void CopyRangeFrom(RangeSlider source)
		{
			if (source == null || source.range == range)
				return;
			range = source.range;
			if (isSpawned)
				ApplyRange();
		}

		private void ApplyRange()
		{
			if (sensor == null || GameScenePartitioner.Instance == null)
				return;

			sensor.pickupRange = range;

			// Mirror the extents math from LogicDuplicantSensor.OnSpawn.
			Vector2I xy = Grid.CellToXY(this.NaturalBuildingCell());
			int cell = Grid.XYToCell(xy.x, xy.y + range / 2);
			CellOffset offset = new CellOffset(0, range / 2);
			if ((bool)rotatable)
			{
				offset = rotatable.GetRotatedCellOffset(offset);
				if (Grid.IsCellOffsetValid(this.NaturalBuildingCell(), offset))
					cell = Grid.OffsetCell(this.NaturalBuildingCell(), offset);
			}
			Extents extents = new Extents(cell, range / 2);
			ExtentsField.SetValue(sensor, extents);

			var entry = (HandleVector<int>.Handle)EntryField.GetValue(sensor);
			GameScenePartitioner.Instance.Free(ref entry);
			var callback = (Action<object>)Delegate.CreateDelegate(typeof(Action<object>), sensor, OnPickupablesChangedMethod);
			EntryField.SetValue(sensor, GameScenePartitioner.Instance.Add(
				"DuplicantSensor.PickupablesChanged", gameObject, extents,
				GameScenePartitioner.Instance.pickupablesChangedLayer, callback));

			DirtyField.SetValue(sensor, true);
			RefreshReachableCellsMethod.Invoke(sensor, null);

			if (visualizer != null)
			{
				visualizer.RangeMin = new Vector2I(-range / 2, 0);
				visualizer.RangeMax = new Vector2I(range / 2, range);
			}
		}
	}
}
