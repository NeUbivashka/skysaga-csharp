import os

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")

TARGETS = {
    "alpha10": ("36731", "Alpha10"),
    "beta1": ("38328", "Beta1"),
}

BUILD, SERVERS = TARGETS[os.environ.get("SKYSAGA_TARGET", "beta1").lower()]
BUNDLE = os.path.join(ROOT, "Bundled", BUILD)
GEODATA = os.path.join(BUNDLE, "GeoData.json")
GAME = os.path.join(ROOT, "Servers", SERVERS, "SkySaga.Game")
