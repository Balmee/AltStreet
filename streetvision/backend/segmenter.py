
import json
import time
from pathlib import Path

import numpy as np
import torch
import torch.nn.functional as F
from PIL import Image, ImageColor
from transformers import (
    SegformerForSemanticSegmentation,
    SegformerImageProcessor,
)

MODEL_ID = "nvidia/segformer-b0-finetuned-ade-512-512"

BASE_DIR = Path(__file__).resolve().parent
IMAGE_PATH = BASE_DIR / "assets" / "reference_images" / "street.jpeg"
OUTPUT_DIR = BASE_DIR / "outputs"

# Use the RTX GPU when CUDA is available.
DEVICE = torch.device("cuda" if torch.cuda.is_available() else "cpu")


def make_palette(num_classes: int) -> np.ndarray:
    """Create a stable colour for each predicted class."""
    palette = np.zeros((num_classes, 3), dtype=np.uint8)

    for class_id in range(num_classes):
        # Deterministic colours, independent of Python's hash seed.
        r = (class_id * 67 + 31) % 256
        g = (class_id * 131 + 73) % 256
        b = (class_id * 197 + 109) % 256
        palette[class_id] = [r, g, b]

    return palette


def main():
    if not IMAGE_PATH.is_file():
        raise FileNotFoundError(
            f"Add a photograph at: {IMAGE_PATH}"
        )

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    print(f"Device: {DEVICE}")
    print(f"Loading model: {MODEL_ID}")

    # The first run downloads the model files.
    processor = SegformerImageProcessor.from_pretrained(MODEL_ID)
    model = SegformerForSemanticSegmentation.from_pretrained(
        MODEL_ID
    )
    model.to(DEVICE)
    model.eval()

    image = Image.open(IMAGE_PATH).convert("RGB")
    width, height = image.size

    inputs = processor(images=image, return_tensors="pt")
    inputs = {
        key: value.to(DEVICE)
        for key, value in inputs.items()
    }

    # Exclude model-loading time from inference timing.
    if DEVICE.type == "cuda":
        torch.cuda.synchronize()

    start = time.perf_counter()

    with torch.inference_mode():
        outputs = model(**inputs)

        # Resize class scores to the source image resolution.
        logits = F.interpolate(
            outputs.logits,
            size=(height, width),
            mode="bilinear",
            align_corners=False,
        )

        predicted_ids = logits.argmax(dim=1)[0]

    if DEVICE.type == "cuda":
        torch.cuda.synchronize()

    inference_seconds = time.perf_counter() - start

    # Copy the prediction map to CPU for image and JSON processing.
    mask = predicted_ids.cpu().numpy().astype(np.uint8)

    num_classes = model.config.num_labels
    palette = make_palette(num_classes)

    colour_mask = Image.fromarray(palette[mask], mode="RGB")
    colour_mask.save(OUTPUT_DIR / "segmentation.png")

    # Blend the colour mask over the original photograph.
    overlay = Image.blend(image, colour_mask, alpha=0.50)
    overlay.save(OUTPUT_DIR / "overlay.png")

    # Count predicted pixels for every class.
    counts = np.bincount(
        mask.ravel(),
        minlength=num_classes,
    )
    total_pixels = int(mask.size)

    id2label = model.config.id2label
    class_percentages = {}

    for class_id, count in enumerate(counts):
        if count == 0:
            continue

        label = id2label.get(
            class_id,
            id2label.get(str(class_id), str(class_id)),
        )

        class_percentages[label] = round(
            100.0 * int(count) / total_pixels,
            3,
        )

    # Sort largest predicted pixel proportions first.
    class_percentages = dict(
        sorted(
            class_percentages.items(),
            key=lambda item: item[1],
            reverse=True,
        )
    )

    profile = {
        "model": MODEL_ID,
        "device": str(DEVICE),
        "source_image": IMAGE_PATH.name,
        "image_width": width,
        "image_height": height,
        "inference_seconds": round(inference_seconds, 4),
        "class_pixel_percentages": class_percentages,
    }

    profile_path = OUTPUT_DIR / "scene_profile.json"
    profile_path.write_text(
        json.dumps(profile, indent=2),
        encoding="utf-8",
    )

    print("\nTop predicted classes:")
    for label, percentage in list(class_percentages.items())[:10]:
        print(f"  {label}: {percentage:.3f}%")

    print(f"\nInference time: {inference_seconds:.3f} seconds")
    print(f"Segmentation: {OUTPUT_DIR / 'segmentation.png'}")
    print(f"Overlay:      {OUTPUT_DIR / 'overlay.png'}")
    print(f"Scene profile:{profile_path}")
    print("\nSegmentation completed successfully.")


if __name__ == "__main__":
    main()
