"""
SkySaga: Infinite Isles - local ws_host / patch stub server.

Replaces the dead AWS/CloudFront infrastructure the Launcher and SkySaga.exe were
originally configured to talk to. Response formats were reverse-engineered from
the decompiled Launcher.exe and from the client binary.

This one file replaces app_http.py, app_https.py and proxy.py. Those three were
byte-identical apart from their run block, so every route had to be edited twice
and three consoles had to be babysat. Here the app is defined once and the
process starts all three pieces itself:

    127.0.0.1:5080   plain HTTP backend   (thread)
    127.0.0.1:5443   HTTPS backend        (thread)
    0.0.0.0:5001     sniffing proxy       (main thread)

The proxy looks at the first byte of each connection: 0x16 means a TLS
ClientHello and goes to the HTTPS backend, anything else is plain HTTP. That is
what lets the Launcher (plain HTTP) and the game (TLS on this build) share port
5001.

Run with:
    pip install flask
    python server.py

Point Launcher.exe.config's ws_host, client_url and service_status at
http://127.0.0.1:5001 - the proxy sorts out which backend answers.
"""

import asyncio
import datetime
import ipaddress
import json
import os
import lzma
import ssl
import sys
import threading
import uuid
from pathlib import Path

from flask import Flask, Response, jsonify, request
from account_store import AccountStore
from home_islands import HomeIslandManager

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

LISTEN_HOST = "0.0.0.0"
LISTEN_PORT = 5001
GAME_ENTRY_PORT = int(os.environ.get("SKYSAGA_GAME_ENTRY_PORT", "42069"))

HTTP_BACKEND = ("127.0.0.1", 5080)
HTTPS_BACKEND = ("127.0.0.1", 5443)

CERT_FILE = Path(r"C:\SkySagaCerts\server.crt")
KEY_FILE = Path(r"C:\SkySagaCerts\server.key")

# Import existing JSON identities into SQLite once. Keep the host JSON and old
# remote account files untouched for rollback; SQLite is the account store after
# import. Player and world saves remain in their existing locations.
ACCOUNT_FILE = Path(__file__).resolve().parent / "Data" / "account.json"
ACCOUNTS_DIR = ACCOUNT_FILE.parent / "accounts"
ACCOUNT_DB_FILE = ACCOUNT_FILE.parent / "accounts.sqlite3"
DEFAULT_PLAYER_NAME = os.environ.get("SKYSAGA_PLAYER_NAME", "NeUbivashka")


def _load_account():
    if ACCOUNT_FILE.exists():
        account = json.loads(ACCOUNT_FILE.read_text(encoding="utf-8"))
    else:
        account = {}
    changed = False
    for key, default in (("name", DEFAULT_PLAYER_NAME), ("characterUuid", None), ("homeBiome", "Desert")):
        if not account.get(key):
            account[key] = default if default is not None else str(uuid.uuid4())
            changed = True
    if changed:
        ACCOUNT_FILE.parent.mkdir(parents=True, exist_ok=True)
        ACCOUNT_FILE.write_text(json.dumps(account, indent=2), encoding="utf-8")
    return account


ACCOUNT_STORE = AccountStore(ACCOUNT_DB_FILE)
ACCOUNT_STORE.initialize(_load_account, ACCOUNT_FILE, ACCOUNTS_DIR)
ACCOUNT = ACCOUNT_STORE.get_host()
PLAYER_NAME = ACCOUNT["name"]
ACCOUNT_CREATED = ACCOUNT["createdAt"]
HOME_ISLANDS = HomeIslandManager(Path(__file__).resolve().parent)

# The proxy's backend socket port identifies the original TCP peer even for TLS.
# Flask's request.remote_addr is always the loopback backend address here.
_proxy_peers = {}
_peer_accounts = {}
_identity_lock = threading.Lock()


def _peer_ip():
    try:
        backend_port = int(request.environ["REMOTE_PORT"])
    except (KeyError, TypeError, ValueError):
        return None
    with _identity_lock:
        return _proxy_peers.get(backend_port)


def _is_local_peer(peer):
    try:
        return ipaddress.ip_address(peer).is_loopback
    except (ValueError, TypeError):
        return False


def _login_username():
    payload = request.get_json(silent=True)
    if not isinstance(payload, dict):
        payload = request.form.to_dict()

    def find_name(value):
        if not isinstance(value, dict):
            return None
        for key, item in value.items():
            if key.lower() in ("username", "user_name") and isinstance(item, str):
                return item.strip()
        for item in value.values():
            found = find_name(item)
            if found:
                return found
        return None
    name = find_name(payload)
    return name if name and len(name) <= 64 and name.isprintable() else None


def _remote_account(name):
    return ACCOUNT_STORE.get_or_create_remote(name, ACCOUNT["homeBiome"])


def _select_login_account():
    global ACCOUNT, PLAYER_NAME, ACCOUNT_CREATED

    peer = _peer_ip()
    if _is_local_peer(peer):
        name = _login_username()
        if name and name.casefold() != ACCOUNT["name"].casefold():
            # The local launcher owns the host account.  This changes its
            # display name only: character UUID, account id, home-world UUID,
            # port, map and player save remain associated with the same row.
            ACCOUNT = ACCOUNT_STORE.rename_host(name)
            PLAYER_NAME = ACCOUNT["name"]
            ACCOUNT_CREATED = ACCOUNT["createdAt"]
            print(f"[identity] updated host display name to {ACCOUNT['name']!r}; "
                  f"characterUuid={ACCOUNT['characterUuid']}")
        return ACCOUNT
    name = _login_username()
    if not peer or not name:
        payload = request.get_json(silent=True)
        keys = list(payload) if isinstance(payload, dict) else []
        print(f"[identity] login has no usable peer/name (peer={peer}, JSON keys={keys}); keeping previous identity")
        return _account_for_peer(peer)
    account = _remote_account(name)
    with _identity_lock:
        _peer_accounts[peer] = account
    print(f"[identity] peer={peer} name={account['name']} characterUuid={account['characterUuid']}")
    if GAME_ENTRY_PORT == 42069:
        HOME_ISLANDS.start_in_background(ACCOUNT_STORE.get_or_create_home_world(account))
    return account


def _account_for_peer(peer):
    if _is_local_peer(peer):
        return ACCOUNT
    with _identity_lock:
        return _peer_accounts.get(peer)


def _account_for_request():
    peer = _peer_ip()
    account = _account_for_peer(peer)
    if account is None:
        print(f"[identity] no login account for peer={peer}; using legacy host response")
    return account or ACCOUNT


# ---------------------------------------------------------------------------
# Timestamp every stdout line, so our own print() calls line up with werkzeug's
# request log.
# ---------------------------------------------------------------------------
class _TimestampedStdout:
    def __init__(self, inner):
        self._inner = inner
        self._at_line_start = True
        self._lock = threading.Lock()

    def write(self, text):
        if not text:
            return
        if isinstance(text, bytes):
            text = text.decode("utf-8", errors="replace")
        out = []
        with self._lock:
            for chunk in text.splitlines(keepends=True):
                if self._at_line_start and chunk.strip("\n") != "":
                    ts = datetime.datetime.now().strftime("%H:%M:%S.%f")[:-3]
                    out.append(f"[{ts}] ")
                out.append(chunk)
                self._at_line_start = chunk.endswith("\n")
            self._inner.write("".join(out))

    def flush(self):
        self._inner.flush()


sys.stdout = _TimestampedStdout(sys.stdout)

app = Flask(__name__)

# The host character is always present: build 36731 asks characters/_active
# and does not recover correctly from a null active character.


# ---------------------------------------------------------------------------
# /manifest - empty, LZMA_ALONE-compressed BSON patch manifest.
#
# Launcher.Manifest has three optional fields (SubManifests "d", Files "f",
# RequiredDiskSpaceBytes "disk-space"). An empty BSON document deserializes to
# all-null/zero, and Launcher.Patcher.ProducePatchList treats that as "nothing to
# patch" (PatchResult.NoWorkRequired).
# ---------------------------------------------------------------------------
def build_empty_manifest() -> bytes:
    empty_bson = b"\x05\x00\x00\x00\x00"  # 4-byte length + 0x00 terminator
    filters = [{"id": lzma.FILTER_LZMA1, "preset": 9}]
    return lzma.compress(empty_bson, format=lzma.FORMAT_ALONE, filters=filters)


MANIFEST_BYTES = build_empty_manifest()


@app.route("/", defaults={"path": ""})
@app.route("/<path:path>")
def catch_all_get(path):
    if path.endswith("manifest"):
        return Response(MANIFEST_BYTES, mimetype="application/octet-stream")

    if path.endswith("geonode"):
        # Main.GeoNode: UUID, DataCentre, IP, Port. Pings/MedianPing are
        # client-side only and not deserialized from JSON.
        return jsonify([{
            "UUID": str(uuid.uuid4()),
            "DataCentre": "UK",
            "IP": "127.0.0.1",
            "Port": GAME_ENTRY_PORT,
        }])

    # Everything else - server-status file, home page, unknown paths - is
    # answered as "service is online". Main.ServiceStatus.
    return jsonify({"Status": "1", "SubFolder": "", "EndOfGameSubFolder": ""})


# ---------------------------------------------------------------------------
# Authentication
# ---------------------------------------------------------------------------

def _token():
    return {
        "tokenId": "tokenId",
        "refreshingTokenId": "refreshingTokenId",
        "timeout": 999999,
    }


@app.route("/api/authentication/applications/names/login", methods=["POST"])
def alpha10_login():
    _select_login_account()
    print("=== alpha10 login request ===")
    print("Body:", request.get_data(as_text=True))
    return jsonify({"result": _token()}), 200


@app.route("/api/authentication/sgauth/_login", methods=["POST"])
def alpha10_sgauth_login():
    account = _select_login_account()
    print("=== alpha10 sgauth login request ===")
    print("Body:", request.get_data(as_text=True))
    return jsonify({
        "result": {
            "sgUser": "",
            "memberId": "1",
            "username": account["name"] if account else PLAYER_NAME,
            "token": _token(),
        }
    }), 200


@app.route("/api/authentication/credentials/usernames/autologin", methods=["POST"])
def alpha10_autologin():
    print("=== alpha10 autologin request ===")
    return jsonify({"result": _token()}), 200


@app.route("/api/authentication/tokens/refresh", methods=["POST"])
def alpha10_token_refresh():
    print("=== alpha10 token refresh ===")
    return jsonify({"result": _token()}), 200


@app.route("/api/authentication/credentials/usernames/logout", methods=["POST"])
def alpha10_logout():
    print("=== alpha10 logout request ===")
    return jsonify({"result": {}}), 200


@app.route("/api/authentication/sgauth/_convertLauncherToken", methods=["POST"])
def alpha10_convert_launcher_token():
    # The client exchanges the launcher-generated token for a game-session token.
    # This used to fall through to the generic catch-all ({"result": "ok"}), which
    # the client rejects - it retried 5x with backoff then failed with 17002
    # ("Failed to authenticate with login server").
    print("=== alpha10 convert launcher token ===")
    print("Body:", request.get_data(as_text=True))
    return jsonify({"result": _token()}), 200


@app.route("/api/authentication/sgauth/credentials/_login", methods=["POST"])
def login():
    # Main.LoginResult, matched to what bgwLogin_DoWork expects. acceptedVersion
    # MUST equal currentVersion or the launcher will not consider the login flow
    # complete even with a valid token.
    account = _select_login_account()
    return jsonify({
        "token": {
            "tokenId": str(uuid.uuid4()),
            "refreshingTokenId": str(uuid.uuid4()),
            "timeout": 999999,
        },
        "principal": account["name"] if account else PLAYER_NAME,
        "legals": {
            "acceptedVersion": 1,
            "currentVersion": 1,
            "currentTermsAndConditionsUrl": "http://127.0.0.1:5001/tos",
        },
        "accountActions": {
            "actionRequired": False,
            "updatePassword": False,
            "updateCharacterName": False,
            "ticketId": "",
        },
    }), 200


@app.route("/api/authentication/sgauth/_acceptLegals", methods=["POST"])
def accept_legals():
    return login()


# ---------------------------------------------------------------------------
# Characters and account
# ---------------------------------------------------------------------------

def _character():
    account = _account_for_request()
    return {
        "uuid": account["characterUuid"],
        "name": account["name"],
        "homeBiome": account["homeBiome"],
        "positionInList": 0,
    }


@app.route("/api/persistent-record/profanity/blacklist", methods=["POST"])
def alpha10_profanity_blacklist():
    return jsonify({"result": []}), 200


@app.route("/api/persistent-record/characters/list", methods=["GET"])
def alpha10_characters_list():
    if _account_for_request()["characterUuid"] is None:
        # No character yet -> explicit error code, which is what prompts the
        # client to call _create next.
        return jsonify({"Error": {"code": 11001, "message": "", "detail": ""}}), 200
    return jsonify({"result": {"characters": [_character()]}}), 200


@app.route("/api/persistent-record/characters/_active", methods=["GET"])
def alpha10_characters_active():
    # The 2017 builds (Alpha V10 b36731) call this instead of /characters/list
    # (RPC HTTPRPCDownloadActiveCharacter). With no route it fell through to the
    # catch-all GET handler, which returns ServiceStatus-shaped JSON with no
    # "result"/"character" field - and still 200, so it went unnoticed for a long
    # time.
    #
    # The shape is singular "character", not a "characters" array, and the
    # no-character case must be a SUCCESS with character=null, NOT the 11001 error
    # /list uses - the client renders that differently here and treating "no
    # character" as an error blocks it from reaching character creation.
    if _account_for_request()["characterUuid"] is None:
        return jsonify({"result": {"character": None}}), 200
    return jsonify({"result": {"character": _character(), **_character()}}), 200


@app.route("/api/persistent-record/characters/_create", methods=["POST"])
def alpha10_characters_create():
    # One account, one character: creation hands back the existing uuid.
    character_uuid = _account_for_request()["characterUuid"]
    print("=== alpha10 character create ->", character_uuid, "===")
    return jsonify({"result": {"characterUUID": character_uuid}}), 200


@app.route("/GetGUID", methods=["GET"])
def get_guid():
    # Used by SkySaga.Game to fetch the player's real character uuid for
    # ServerInfo.ServerOwnerGuid, so it matches what the client's
    # ClientOwnerComponent expects. A mismatch here is what made home-island
    # locked items refuse placement.
    account = _account_for_request()
    return jsonify({"result": {"GUID": account["characterUuid"], "Name": account["name"]}}), 200


@app.route("/internal/player-identity", methods=["GET"])
def player_identity():
    if not _is_local_peer(_peer_ip()):
        return jsonify({"error": "local server only"}), 403
    peer = request.args.get("peer")
    account = _account_for_peer(peer)
    if account is None:
        return jsonify({"error": "no login for peer"}), 404
    is_host = account is ACCOUNT
    return jsonify({"result": {
        "GUID": account["characterUuid"],
        "Name": account["name"],
        "SaveFile": "player.json" if is_host else f"player_{account['characterUuid']}.json",
    }}), 200


@app.route("/internal/home-island", methods=["GET"])
def home_island_destination():
    """Resolve and start the home island for a connected game character."""
    if not _is_local_peer(_peer_ip()):
        return jsonify({"error": "local server only"}), 403
    account = ACCOUNT_STORE.get_by_character_uuid(request.args.get("characterUuid"))
    if account is None:
        return jsonify({"error": "unknown character"}), 404
    world = ACCOUNT_STORE.get_or_create_home_world(account)
    try:
        HOME_ISLANDS.ensure_running(world)
    except (OSError, RuntimeError) as exc:
        print(f"[home-island] Return transfer unavailable for {account['name']}: {exc}", flush=True)
        return jsonify({"error": "Home island unavailable"}), 503
    return jsonify({"result": {
        "WorldUUID": world["uuid"],
        "ServerUUID": world["serverUuid"],
        "Port": world["port"],
    }}), 200


@app.route("/internal/world-island", methods=["GET"])
def world_island_destination():
    """Resolve and start a published island selected in the world browser."""
    if not _is_local_peer(_peer_ip()):
        return jsonify({"error": "local server only"}), 403
    world = ACCOUNT_STORE.get_home_world_by_uuid(request.args.get("worldUuid"))
    if world is None:
        return jsonify({"error": "unknown world"}), 404
    try:
        HOME_ISLANDS.ensure_running(world)
    except (OSError, RuntimeError) as exc:
        print(f"[world-island] Transfer unavailable for {world['uuid']}: {exc}", flush=True)
        return jsonify({"error": "Island unavailable"}), 503
    return jsonify({"result": {
        "WorldUUID": world["uuid"],
        "ServerUUID": world["serverUuid"],
        "Port": world["port"],
        "OwnerUUID": world["owner"],
        "OwnerName": world["ownerName"],
    }}), 200


@app.route("/api/account/name/checkName", methods=["GET", "POST"])
def alpha10_account_check_name():
    # The character creator checks the chosen name here before it sends RPCSaveCharacterName
    # (GameState_CharacterCreation callback FUN_00810C00). It reads "valid"; otherwise "profane",
    # "containsNotAllowedCharacters", "alreadyExists" and "suggestion" pick the error text.
    print("=== checkName", request.get_data(as_text=True)[:200], "===")
    # FUN_00811360 looks the keys up in the response node at +0x34 and wants JSON bools (node type
    # 6). Whether that node is the root or "result" is not settled, so both carry them.
    verdict = {"valid": True, "profane": False, "containsNotAllowedCharacters": False,
               "alreadyExists": False}
    return jsonify({"result": dict(verdict), **verdict}), 200


@app.route("/api/social-graph/character/profile/batch", methods=["POST"])
def alpha10_character_profile_batch():
    body = request.get_json(silent=True, force=True) or {}
    params = body.get("params", body)
    recipients = params.get("recipients") or [_account_for_request()["characterUuid"]]
    print("=== alpha10 character profile batch", request.get_data(as_text=True), "===", flush=True)
    return jsonify({"result": [_character_profile(uuid_) for uuid_ in recipients]}), 200


def _character_profile(character_uuid):
    account = _account_for_request()
    if character_uuid != account["characterUuid"]:
        account = ACCOUNT_STORE.get_by_character_uuid(character_uuid) or account
    return {
        "characterOnlineData": {
            "characterUuid": character_uuid,
            "characterName": account["name"],
        },
        "counts": {"follows": 0, "followedBy": 0, "friends": 0},
        "accountCreated": ACCOUNT_CREATED,
    }


@app.route("/api/account/get", methods=["POST"])
def alpha10_account_get():
    return jsonify({"result": {"keySubset": {"RESERVED_NAME": _account_for_request()["name"]}}}), 200


# ---------------------------------------------------------------------------
# Loading screen content
# ---------------------------------------------------------------------------

@app.route("/api/persistent-record/loadingscreen/_suggest", methods=["POST"])
def alpha10_loadingscreen_suggest():
    # This is the endpoint that decides the loading screen's background - the
    # picture that changes mid-load in the retail game.
    #
    # It used to answer {"result": {"loadingScreens": []}}, which was a guess. The
    # client never looks for a "loadingScreens" key. Its response handler
    # (FUN_008F78E0) does:
    #
    #     if payload empty                        -> retry in 1000 ms
    #     lookup "question"    -> FUN_008F7F90
    #     lookup "photoBattle" -> FUN_008F7E40
    #     lookup "tip"         -> FUN_008F8570
    #     none of the three    -> this[0x160] = 0, retry in 1000 ms
    #
    # The tip branch walks the children of the "tip" node for two named entries,
    # "tip" and "photo", requires a "name" on each, then reads "text" and an
    # optional "tags". On success it arms FUN_00C62DD0(10000) - the ten second tip
    # display. That is also where the loading screen's 10 s timer comes from.
    #
    # CONFIRMED from the binary: the three section names, the tip/photo/name/text/
    # tags keys, and both timer values.
    #
    # STILL A GUESS: the nesting below and whether "result" is the right envelope
    # here. There is a free indicator - if the client keeps asking once a second,
    # it did not accept the answer; if it goes quiet, it did.
    print("=== alpha10 loading screen content suggest ===")
    print("Body:", request.get_data(as_text=True))

    return jsonify({
        "result": {
            "tip": {
                "tip": {
                    "name": "tip_welcome",
                    "text": "Welcome to SkySaga.",
                    "tags": [],
                },
                "photo": {
                    "name": "loading_concept_01",
                },
            }
        }
    }), 200


@app.route("/api/persistent-record/surveys/_answer", methods=["POST"])
def alpha10_surveys_answer():
    return jsonify({"result": {}}), 200


# ---------------------------------------------------------------------------
# Matchmaking, game conductor, telemetry
# ---------------------------------------------------------------------------

@app.route("/api/matchmaking/userdatacentre/create", methods=["POST"])
def alpha10_matchmaking_create():
    return "", 200


@app.route("/api/binary-storage/photos/_whichIsCooler", methods=["POST"])
def alpha10_binary_storage_cooler():
    return "", 200


@app.route("/api/persistent-record/worlds/listforowner/<owner>", methods=["GET"])
def alpha10_worlds_list_for_owner(owner):
    print("=== worlds/listforowner", owner, "===")
    # My Islands is an account-owned list.  Resolve it through the signed-in
    # proxy identity as well as the URL so one client cannot substitute another
    # character UUID and make that island appear as its own.
    account = _account_for_request()
    try:
        requested_owner = str(uuid.UUID(owner))
    except (TypeError, ValueError, AttributeError):
        return jsonify([]), 200
    if requested_owner != account["characterUuid"]:
        print(f"[my-islands] Ignored owner mismatch for {account['name']}: {requested_owner}")
        return jsonify([]), 200
    world = ACCOUNT_STORE.get_or_create_home_world(account)
    print(f"[my-islands] Listed {account['name']}'s home island {world['uuid']}")
    return jsonify([_home_world(world)]), 200


@app.route("/api/persistent-record/curatedcontent/ugcbrowserworldcontent/_search", methods=["POST"])
def alpha10_world_browser_search():
    body = request.get_json(silent=True) or {}
    if not isinstance(body, dict):
        return jsonify({"error": "Invalid world search"}), 400

    def nonnegative_int(value, default):
        try:
            return max(0, int(value))
        except (TypeError, ValueError):
            return default

    first = nonnegative_int(body.get("first"), 0)
    page_size = min(nonnegative_int(body.get("pageSize"), 12), 100)
    owner_search = str(body.get("ownerNameSearch") or "").strip().casefold()
    name_search = str(body.get("nameSearch") or "").strip().casefold()

    viewer_uuid = _account_for_request()["characterUuid"]
    worlds = [_home_world(record) for record in ACCOUNT_STORE.list_home_worlds()]
    worlds = [world for world in worlds if world["published"]
              and world["owner"] != viewer_uuid and (
        (not owner_search and not name_search)
        or (owner_search and owner_search in world["ownerName"].casefold())
        or (name_search and name_search in world["name"].casefold())
    )]
    result = {
        "totalHotWorldCount": len(worlds),
        "totalWorldCount": len(worlds),
        "hotUgcWorlds": [
            {"id": world["uuid"], "thumbnails": []}
            for world in worlds[first:first + page_size]
        ],
        "promotedUgcWorlds": [],
        "worlds": [world["uuid"] for world in worlds[first:first + page_size]],
    }
    print(f"[world-browser] search owner={owner_search!r} name={name_search!r} "
          f"matched={len(worlds)} returned={len(result['worlds'])}")
    return jsonify({"result": result, **result}), 200


@app.route("/api/binary-storage/photos/_multiLikeGet", methods=["POST"])
def binary_storage_photo_likes():
    return jsonify({"result": {"likes": {}}}), 200


def _empty_batch(field):
    """Return an accepted empty batch using both Alpha 10 response views."""
    values = []
    return jsonify({field: values, "result": {field: values}}), 200


@app.route("/api/persistent-record/world/like/find/batch", methods=["POST"])
@app.route("/api/persistent-record/worlds/visits/multicount", methods=["POST"])
@app.route("/api/persistent-record/worlds/engagements/worlds", methods=["POST"])
@app.route("/api/persistent-record/world/like/character/batch", methods=["POST"])
def alpha10_world_metadata_batch():
    return _empty_batch("worlds")


@app.route("/api/persistent-record/character/favourite/find/batch", methods=["POST"])
def alpha10_world_favourites_batch():
    return _empty_batch("favourite")


@app.route("/api/persistent-record/world/buildpermission/batch", methods=["POST"])
def alpha10_world_permissions_batch():
    return _empty_batch("worldPermission")


@app.route("/api/persistent-record/world/buildpermission/character/batch", methods=["POST"])
def alpha10_character_world_permissions_batch():
    return _empty_batch("characterPermission")


@app.route("/api/persistent-record/worlds/engagements/numberPromotions", methods=["POST"])
def alpha10_world_promotions_batch():
    return _empty_batch("promotions")


@app.route("/api/persistent-record/worlds/engagements/engagement/multiavailable", methods=["POST"])
def alpha10_world_rewards_batch():
    return _empty_batch("ugcIds")


@app.route("/api/social-graph/character/relationship/info", methods=["POST"])
def alpha10_character_relationships_batch():
    return _empty_batch("relationships")


HOME_WORLD_UUID = "6f0d3a1e-5c47-4f92-9a6b-1d84c0e73b25"
SOCIAL_HUB_WORLD_UUID = "b6574a23-64df-4c26-9c31-40b2a485d0fe"

HOME_ISLAND_MAP_SPEC = [
    [
        "Forest",
        0,
        "Home_Island_Adventure",
        "Easy",
        "",
        1337,
        "BiomePalette_Forest_HomeIsland",
        0, 0, 0,
        "Home_Island",
        0,
        "Special",
        "HomeIsland_6x4x6",
        0,
        "Home_Island_World",
        "NoEvent",
    ],
    ["", 0],
    [],
    "",
]

# The client calls worlds/getSpecific after a transfer has already connected to
# the City server.  This record must agree with the live social-hub handshake;
# returning a player's home record here makes the client replace the City title
# and map metadata with "<player>'s Ruins" during the loading sequence.
SOCIAL_HUB_MAP_SPEC = [
    [
        "NPCfriend",
        0,
        "NPC_Friend_Adventure",
        "Easy",
        "",
        1337,
        "BiomePalette_SocialHub",
        0, 0, 0,
        "MidMorning",
        0,
        "Special",
        "MapSize_4x4x4",
        0,
        "City_Social_Centre_Area",
        "NoEvent",
    ],
    ["", 0],
    [],
    "",
]


@app.route("/api/persistent-record/worlds/getSpecific", methods=["POST"])
def alpha10_worlds_get_specific():
    body = request.get_json(silent=True) or {}
    print("=== worlds/getSpecific", body, "===")
    requested_uuid = body.get("uuid")
    if requested_uuid == SOCIAL_HUB_WORLD_UUID:
        world = _social_hub_world()
    else:
        world_record = ACCOUNT_STORE.get_home_world_by_uuid(requested_uuid)
        if world_record is None:
            world_record = dict(ACCOUNT_STORE.get_or_create_home_world(ACCOUNT))
            world_record["uuid"] = requested_uuid or HOME_WORLD_UUID
        world = _home_world(world_record)
    return jsonify({"result": dict(world), **world}), 200


def _home_world(world_record):
    return {
        "uuid": world_record["uuid"],
        "mapSpec": HOME_ISLAND_MAP_SPEC,
        "owner": world_record["owner"],
        "ownerName": world_record["ownerName"],
        "ownerType": "C",
        "name": f"{world_record['ownerName']}'s Ruins",
        "homeWorld": True,
        "published": True,
    }


def _social_hub_world():
    return {
        "uuid": SOCIAL_HUB_WORLD_UUID,
        "mapSpec": SOCIAL_HUB_MAP_SPEC,
        "owner": "",
        "ownerName": "The City of Last Light",
        "ownerType": "C",
        "name": "The City of Last Light",
        "homeWorld": False,
        "published": True,
    }


@app.route("/ping", methods=["GET"])
def ping():
    return "", 200


@app.route("/api/game-conductor/geonode", methods=["GET"])
def alpha10_geonode():
    return jsonify({
        "result": [{
            "uuid": str(uuid.uuid4()),
            "datacentre": "UK",
            "ip": "127.0.0.1",
            "port": 5001,
        }]
    }), 200


@app.route("/api/game-conductor/reserve", methods=["PUT"])
def alpha10_game_conductor_reserve():
    return jsonify({"result": {}}), 200


@app.route("/api/game-conductor/retrieve", methods=["POST"])
def alpha10_game_conductor_retrieve():
    print("=== alpha10 game-conductor retrieve (client wants game server address) ===")
    account = _account_for_request()
    if GAME_ENTRY_PORT == 42069:
        world = ACCOUNT_STORE.get_or_create_home_world(account)
        try:
            HOME_ISLANDS.ensure_running(world)
        except (OSError, RuntimeError) as exc:
            print(f"[home-island] Refusing to route {account['name']}: {exc}", flush=True)
            return jsonify({"error": "Home island unavailable"}), 503
        destination_world = world["uuid"]
        destination_server = world["serverUuid"]
        destination_port = world["port"]
    else:
        # Preserve the explicit social-hub test launcher and its fixed entry port.
        destination_world = str(uuid.uuid4())
        destination_server = str(uuid.uuid4())
        destination_port = GAME_ENTRY_PORT
    return jsonify({
        "result": {
            "retryInMillis": 5000,
            "world": destination_world,
            "ip": "100.76.72.11",
            "port": destination_port,
            "server": destination_server,
        }
    }), 200


@app.route("/api/bi-collector/opensession/_create", methods=["POST"])
@app.route("/api/bi-collector/opensession/_addQueue", methods=["POST"])
def bi_collector(*_args, **_kwargs):
    return jsonify({"result": "ok"}), 200


@app.route("/<path:path>", methods=["POST", "PUT", "DELETE"])
def catch_all_write(path):
    # Generic fallback for anything not implemented yet. Logs the whole request so
    # we can see what the client expects, and answers 200 with a JSON body rather
    # than 404/405 so the client does not choke on the status code.
    print(f"=== Unhandled {request.method} /{path} ===")
    print("Headers:", dict(request.headers))
    print("Body:", request.get_data(as_text=True))
    return jsonify({"result": "ok"}), 200


# ---------------------------------------------------------------------------
# Backends and proxy
# ---------------------------------------------------------------------------

def run_http_backend():
    app.run(host=HTTP_BACKEND[0], port=HTTP_BACKEND[1],
            debug=False, use_reloader=False, threaded=True)


def run_https_backend():
    context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    context.load_cert_chain(str(CERT_FILE), str(KEY_FILE))

    app.run(host=HTTPS_BACKEND[0], port=HTTPS_BACKEND[1],
            debug=False, use_reloader=False, threaded=True, ssl_context=context)


async def pipe(reader: asyncio.StreamReader, writer: asyncio.StreamWriter):
    try:
        while True:
            data = await reader.read(65536)
            if not data:
                break
            writer.write(data)
            await writer.drain()
    except (ConnectionResetError, BrokenPipeError):
        pass
    finally:
        try:
            writer.close()
        except Exception:
            pass


async def handle_client(client_reader: asyncio.StreamReader,
                        client_writer: asyncio.StreamWriter):
    peer = client_writer.get_extra_info("peername")

    try:
        first_byte = await asyncio.wait_for(client_reader.read(1), timeout=10)
    except asyncio.TimeoutError:
        print(f"[proxy] {peer}: timed out waiting for the first byte, closing")
        client_writer.close()
        return

    if not first_byte:
        client_writer.close()
        return

    # 0x16 is the start of a TLS ClientHello. Anything else is plain HTTP text.
    is_tls = first_byte == b"\x16"
    backend = HTTPS_BACKEND if is_tls else HTTP_BACKEND

    print(f"[proxy] {peer}: first byte {first_byte.hex()} -> "
          f"{'HTTPS' if is_tls else 'HTTP'}, forwarding to {backend[0]}:{backend[1]}")

    try:
        backend_reader, backend_writer = await asyncio.open_connection(*backend)
    except ConnectionRefusedError:
        print(f"[proxy] {peer}: backend {backend} refused the connection, closing")
        client_writer.close()
        return

    backend_port = backend_writer.get_extra_info("sockname")[1]
    with _identity_lock:
        _proxy_peers[backend_port] = peer[0]
    try:
        # Forward the byte we already consumed.
        backend_writer.write(first_byte)
        await backend_writer.drain()

        await asyncio.gather(
            pipe(client_reader, backend_writer),
            pipe(backend_reader, client_writer),
        )
    finally:
        with _identity_lock:
            _proxy_peers.pop(backend_port, None)


async def run_proxy():
    server = await asyncio.start_server(handle_client, LISTEN_HOST, LISTEN_PORT)

    print(f"Proxy listening on {LISTEN_HOST}:{LISTEN_PORT}")
    print(f"  HTTP  -> {HTTP_BACKEND[0]}:{HTTP_BACKEND[1]}")
    print(f"  HTTPS -> {HTTPS_BACKEND[0]}:{HTTPS_BACKEND[1]}")

    async with server:
        await server.serve_forever()


def main():
    print("Serving empty manifest:", len(MANIFEST_BYTES), "bytes")

    threading.Thread(target=run_http_backend, daemon=True).start()

    if CERT_FILE.exists() and KEY_FILE.exists():
        threading.Thread(target=run_https_backend, daemon=True).start()
    else:
        # The game needs TLS on this build, so without certs it will fail to
        # connect - but the launcher, which is plain HTTP, still works. Say so
        # rather than dying, since that is a useful half-working state.
        print(f"WARNING: {CERT_FILE} or {KEY_FILE} is missing - HTTPS backend not started.")
        print("         The launcher will work; the game will not be able to connect.")

    try:
        asyncio.run(run_proxy())
    except KeyboardInterrupt:
        print("Shutting down.")


if __name__ == "__main__":
    main()
