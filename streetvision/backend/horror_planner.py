
import json
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent
INPUT_PATH = BASE_DIR / "outputs" / "scene_profile.json"
OUTPUT_PATH = BASE_DIR / "outputs" / "transformation_plan.json"


def clamp(value: float, minimum: float, maximum: float) -> float:
    return max(minimum, min(value, maximum))


def get_class_percentage(
    percentages: dict,
    *possible_names: str,
) -> float:
    """Find a class percentage without depending on label casing."""
    normalised = {
        str(name).strip().casefold(): float(value)
        for name, value in percentages.items()
    }

    for name in possible_names:
        if name.casefold() in normalised:
            return normalised[name.casefold()]

    return 0.0


def main():
    if not INPUT_PATH.exists():
        raise FileNotFoundError(
            f"Scene profile not found: {INPUT_PATH}\n"
            "Run segmenter.py first."
        )

    profile = json.loads(INPUT_PATH.read_text(encoding="utf-8"))
    percentages = profile["class_pixel_percentages"]

    building = get_class_percentage(
        percentages, "building"
    ) / 100.0

    wall = get_class_percentage(
        percentages, "wall"
    ) / 100.0

    sky = get_class_percentage(
        percentages, "sky"
    ) / 100.0

    sidewalk = get_class_percentage(
        percentages, "sidewalk", "pavement"
    ) / 100.0

    road = get_class_percentage(
        percentages, "road", "street"
    ) / 100.0

    vegetation = sum(
        get_class_percentage(percentages, name)
        for name in ("tree", "grass", "plant", "flower")
    ) / 100.0

    # Derived visual cues. These are heuristics, not physical
    # measurements of the real location.
    enclosure = clamp(
        0.65 * building + 0.35 * wall,
        0.0,
        1.0,
    )

    horror_intensity = clamp(
        0.50 + 0.25 * building + 0.10 * sidewalk,
        0.0,
        1.0,
    )

    fog_density = clamp(
        0.015 + 0.045 * sky + 0.020 * enclosure,
        0.01,
        0.08,
    )

    vegetation_overgrowth = clamp(
        0.15 + 1.50 * vegetation,
        0.15,
        0.80,
    )

    flicker_probability = clamp(
        0.10 + 0.25 * (building + sidewalk),
        0.10,
        0.60,
    )

    # Stable parameters make repeated runs comparable.
    plan = {
        "schema_version": 1,
        "source": {
            "model": profile.get("model", "unknown"),
            "source_image": profile.get("source_image", "unknown"),
        },
        "method": "segmentation_informed_rules",
        "visual_profile": {
            "building_coverage": round(building, 4),
            "wall_coverage": round(wall, 4),
            "sky_coverage": round(sky, 4),
            "sidewalk_coverage": round(sidewalk, 4),
            "road_coverage": round(road, 4),
            "vegetation_coverage": round(vegetation, 4),
            "enclosure": round(enclosure, 4),
        },
        "transformation": {
            "horror_intensity": round(horror_intensity, 4),
            "fog_density": round(fog_density, 4),
            "exposure_adjustment": round(
                -0.4 - 0.6 * sky, 4
            ),
            "saturation_multiplier": 0.55,
            "vegetation_overgrowth": round(
                vegetation_overgrowth, 4
            ),
            "streetlight_flicker_probability": round(
                flicker_probability, 4
            ),
            "ambient_audio_profile": "urban_wind_low",
            "material_profile": "weathered_cold",
            "lighting_profile": "psychological_horror",
        },
        "unity_notes": [
            "Preserve original road and building geometry.",
            "Apply fog and lighting through scene settings.",
            "Use overgrowth only where suitable placement points exist.",
            "Use flicker probability as an effect trigger probability, "
            "not a guaranteed flicker on every light.",
        ],
    }

    OUTPUT_PATH.write_text(
        json.dumps(plan, indent=2),
        encoding="utf-8",
    )

    print("Horror transformation plan created.")
    print(f"Input:  {INPUT_PATH}")
    print(f"Output: {OUTPUT_PATH}")
    print("\nTransformation parameters:")

    for key, value in plan["transformation"].items():
        print(f"  {key}: {value}")


if __name__ == "__main__":
    main()
