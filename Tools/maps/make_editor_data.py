import json
import os
import re

from targets import BUILD, GAME, GEODATA, ROOT

SERVICE_ENTITIES = {"Player", "TimeOfDay"}
DATA_TAG = '<script id="skysaga-data">'
DEFAULT_SPAWN_PARAMS = "GenericFeatureSpawner"


def category(entity):
    components = set((entity.get("client") or {}).get("components", {}))
    if entity["Name"] in SERVICE_ENTITIES or "clientresourcepickupcomponent" in components:
        return "service"
    if not components & {"transformcomponent", "smoothedtransformcomponent"}:
        return "service"
    if "clientnpccomponent" in components:
        return "npc"
    if components & {"clientenemyaicomponent", "characterstatecomponent"}:
        return "creature"
    if "clientdevicecomponent" in components:
        return "device"
    return "object"


def read_lines(path):
    with open(path, encoding="utf-8") as f:
        return [line.split() for line in f if line.strip() and not line.startswith("#")]


def main():
    data = os.path.join(GAME, "Data")
    blocks = {int(parts[0]): parts[-1] for parts in read_lines(os.path.join(data, "VoxelStats.txt"))}

    cells = {}
    for parts in read_lines(os.path.join(data, "Devices.txt")):
        if len(parts) == 8 and parts[1] not in cells:
            cells[parts[1]] = [[int(v) for v in cell.split(",")] for cell in parts[7].split(";")]

    with open(os.path.join(data, "Entities.json"), encoding="utf-8") as f:
        entities = json.load(f)["Entities"]
    placeable = sorted(
        ({"name": e["Name"], "category": category(e)} for e in entities),
        key=lambda e: e["name"].lower(),
    )
    for e in placeable:
        if e["category"] == "object" and e["name"] in cells:
            e["category"] = "device"

    with open(GEODATA, encoding="utf-8") as f:
        geo = json.load(f)
    sizes = {}
    for size in geo["MapSizes"]:
        match = re.search(r"(\d+)x(\d+)x(\d+)$", size["MapSize"])
        if match:
            sizes.setdefault(tuple(map(int, match.groups())), []).append(size["Name"])
    map_sizes = [{"x": x, "y": y, "z": z, "names": ", ".join(names)} for (x, y, z), names in sorted(sizes.items())]

    out = {
        "build": BUILD,
        "blocks": blocks,
        "entities": [e for e in placeable if e["category"] != "service"],
        "serviceEntities": [e["name"] for e in placeable if e["category"] == "service"],
        "deviceCells": dict(sorted(cells.items())),
        "mapSizes": map_sizes,
        "spawnParams": [{"name": s["Name"], "home": s["HomeRange"], "roam": s["MaxDistanceFromSpawnPosition"]}
                        for s in sorted(geo["SpawnParams"], key=lambda s: s["Name"].lower())],
        "defaultSpawnParams": DEFAULT_SPAWN_PARAMS,
    }

    path = os.path.join(ROOT, "Tools", "terrain-editor", "index.html")
    with open(path, encoding="utf-8", newline="") as f:
        html = f.read()
    start = html.index(DATA_TAG) + len(DATA_TAG)
    end = html.index("</script>", start)
    nl = "\r\n" if "\r\n" in html else "\n"
    payload = json.dumps(out, separators=(",", ":")).replace("</", "<\\/")
    html = html[:start] + nl + "window.SKYSAGA_DATA = " + payload + ";" + nl + html[end:]
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(html)

    counts = {}
    for e in out["entities"]:
        counts[e["category"]] = counts.get(e["category"], 0) + 1
    print(f"{len(blocks)} blocks, entities {counts}, {len(out['serviceEntities'])} service, "
          f"{len(cells)} device footprints, {len(map_sizes)} map sizes -> {path}")


if __name__ == "__main__":
    main()
