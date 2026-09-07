# Motion Sensor Range

An [Oxygen Not Included](https://www.klei.com/games/oxygen-not-included) mod that makes the range of the **Duplicant Motion Sensor** configurable via a slider in the building's side screen (2–16 tiles, default 4 — the vanilla range).

Inspired by [Cairath's Configurable Motion Sensor Range](https://github.com/Cairath/ONI-Mods/tree/master/src/ConfigurableMotionSensorRange), which crashes on current game versions (`TypeLoadException: VTable setup of type RangeSwitcher failed` — the game's `ISliderControl` interface changed shape). This is a from-scratch rewrite against the current game (U59+, all DLCs).

## How it works

- A `RangeSlider` component (implementing the game's current `IIntSliderControl`) is added to the motion sensor prefab via a Harmony postfix on `LogicDuplicantSensorConfig.DoPostConfigureComplete`, giving the building an int slider side screen.
- When the slider changes (or a saved value is loaded), the mod sets `LogicDuplicantSensor.pickupRange`, rebuilds the sensor's scene-partitioner registration to match the new detection area, and resizes the `RangeVisualizer` overlay.
- The chosen range is saved with the building (`KSerialization`).
- Ranges snap to even numbers because the sensor's scan area is only symmetric for even ranges.

## Building

Requires the .NET SDK (8+). Shared build configuration lives in the [oni-mods-common](https://github.com/isochronous/oni-mods-common) submodule, so clone with `--recurse-submodules` (or run `git submodule update --init`). The game DLLs are referenced directly from the game install; override the path if yours differs:

```
dotnet build src/MotionSensorRange -c Release -p:GameFolder="<path-to>\OxygenNotIncluded"
```

A successful build automatically deploys `MotionSensorRange.dll`, `mod.yaml` and `mod_info.yaml` to `Documents\Klei\OxygenNotIncluded\mods\local\MotionSensorRange` (disable with `-p:ModDeployFolder=none`).
