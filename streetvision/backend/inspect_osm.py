
import json
from pathlib import Path

path = Path("outputs/osm_world.json")

with path.open("r", encoding="utf-8") as file:
    data = json.load(file)

print("Location:", data["location"]["name"])
print("Roads:", len(data["roads"]))
print("Buildings:", len(data["buildings"]))

print("\nFirst road:")
if data["roads"]:
    print(json.dumps(data["roads"][0], indent=2)[:1000])

print("\nFirst building:")
if data["buildings"]:
    print(json.dumps(data["buildings"][0], indent=2)[:1500])

print("\nBuildings with at least 3 footprint points:",
      sum(
          len(building["coordinates"]) >= 3
          for building in data["buildings"]
      ))
