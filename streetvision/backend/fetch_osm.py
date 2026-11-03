
import json
import math
import time
from pathlib import Path

import requests

BASE_DIR = Path(__file__).resolve().parent
OUTPUT_DIR = BASE_DIR / "outputs"
OUTPUT_PATH = OUTPUT_DIR / "osm_world.json"

# Selected location to reconstruct.
PLACE_QUERY = "Trafalgar Square, London, UK"

# A small initial area, roughly a few hundred metres across.
HALF_SIZE_METRES = 180

USER_AGENT = "STREETVISION/0.1 (contact: b.hunumunt0820231@arts.ac.uk)"

NOMINATIM_URL = "https://nominatim.openstreetmap.org/search"
OVERPASS_URL = "https://overpass-api.de/api/interpreter"


def get_location():
    response = requests.get(
        NOMINATIM_URL,
        params={
            "q": PLACE_QUERY,
            "format": "jsonv2",
            "limit": 1,
        },
        headers={"User-Agent": USER_AGENT},
        timeout=30,
    )
    response.raise_for_status()

    results = response.json()

    if not results:
        raise RuntimeError(f"Location not found: {PLACE_QUERY}")

    result = results[0]

    return {
        "name": result["display_name"],
        "latitude": float(result["lat"]),
        "longitude": float(result["lon"]),
    }


def make_bbox(latitude, longitude, half_size_metres):
    # Approximate conversion from metres to geographic degrees.
    lat_delta = half_size_metres / 111_320.0

    lon_scale = 111_320.0 * math.cos(math.radians(latitude))
    lon_delta = half_size_metres / max(lon_scale, 1.0)

    south = latitude - lat_delta
    north = latitude + lat_delta
    west = longitude - lon_delta
    east = longitude + lon_delta

    return south, west, north, east


def query_osm(bbox):
    south, west, north, east = bbox

    query = f"""
    [out:json][timeout:40];
    (
      way["highway"]({south},{west},{north},{east});
      way["building"]({south},{west},{north},{east});
    );
    out body geom;
    """

    response = requests.post(
        OVERPASS_URL,
        data={"data": query},
        headers={"User-Agent": USER_AGENT},
        timeout=60,
    )
    response.raise_for_status()

    return response.json()



def to_local_coordinates(geometry, origin_lat, origin_lon):
    coordinates = []

    for point in geometry:
        lat = point["lat"]
        lon = point["lon"]

        x = (
            lon - origin_lon
        ) * 111320.0 * math.cos(math.radians(origin_lat))

        z = (lat - origin_lat) * 111320.0

        coordinates.append({
            "x": round(x, 2),
            "z": round(z, 2)
        })

    return coordinates



def convert_elements(elements, origin_lat, origin_lon):
    roads = []
    buildings = []

    for element in elements:
        if element.get("type") != "way":
            continue

        geometry = element.get("geometry", [])

        if len(geometry) < 2:
            continue

        
        coordinates = to_local_coordinates(
            element["geometry"],
            origin_lat,
            origin_lon
        )


        tags = element.get("tags", {})

        if "highway" in tags:
            roads.append({
                "osm_id": element["id"],
                "highway": tags["highway"],
                "name": tags.get("name", ""),
                "coordinates": coordinates,
            })

        if "building" in tags:
            # Building ways should form closed outlines.
            if len(coordinates) >= 4 and coordinates[0] == coordinates[-1]:
                coordinates = coordinates[:-1]

            if len(coordinates) >= 3:
                buildings.append({
                    "osm_id": element["id"],
                    "building": tags["building"],
                    "levels": tags.get("building:levels"),
                    "name": tags.get("name", ""),
                    "coordinates": coordinates,
                })

    return roads, buildings


def main():
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    print(f"Resolving location: {PLACE_QUERY}")
    location = get_location()

    latitude = location["latitude"]
    longitude = location["longitude"]

    print(f"Resolved coordinates: {latitude}, {longitude}")

    bbox = make_bbox(
        latitude,
        longitude,
        HALF_SIZE_METRES,
    )

    print("Requesting roads and building footprints from OSM...")
    osm_data = query_osm(bbox)

    roads, buildings = convert_elements(
        osm_data.get("elements", []),
        latitude,
        longitude,
    )

    result = {
        "schema_version": 1,
        "source": "OpenStreetMap",
        "attribution": "© OpenStreetMap contributors",
        "location": location,
        "origin": {
            "latitude": latitude,
            "longitude": longitude,
        },
        "half_size_metres": HALF_SIZE_METRES,
        "roads": roads,
        "buildings": buildings,
    }

    OUTPUT_PATH.write_text(
        json.dumps(result, indent=2),
        encoding="utf-8",
    )

    print("\nOSM extraction complete.")
    print(f"Road ways: {len(roads)}")
    print(f"Building footprints: {len(buildings)}")
    print(f"Saved to: {OUTPUT_PATH}")

    if not roads:
        print("Warning: no roads were returned for this area.")

    if not buildings:
        print("Warning: no building footprints were returned.")


if __name__ == "__main__":
    main()
