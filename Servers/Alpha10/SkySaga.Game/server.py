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
from photo_store import PhotoStore

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
PHOTO_DB_FILE = ACCOUNT_FILE.parent / "photos.sqlite3"
PHOTOS_DIR = ACCOUNT_FILE.parent / "photos"
PHOTO_STORE = PhotoStore(PHOTO_DB_FILE, PHOTOS_DIR)

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
    try:
        body = request.get_json(silent=True, force=True)
    except Exception:
        body = None
    if not isinstance(body, dict):
        try:
            raw = request.get_data(cache=True)
            if raw:
                import json as _json
                body = _json.loads(raw.decode("utf-8", "ignore"))
        except Exception:
            body = {}
    if not isinstance(body, dict):
        body = {}
    params = body.get("params", body) if isinstance(body, dict) else {}
    if not isinstance(params, dict):
        params = {}

    for source in (body, params, getattr(request, "args", {})):
        for key in ("character", "characterUuid", "characterUUID", "requester", "sender"):
            val = source.get(key)
            if isinstance(val, dict):
                val = val.get("characterUuid") or val.get("uuid") or val.get("id")
            if val and isinstance(val, str):
                acc = ACCOUNT_STORE.get_by_character_uuid(val)
                if acc:
                    return acc

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
    params = body.get("params", body) if isinstance(body, dict) else {}
    recipients = None
    if isinstance(params, dict):
        recipients = params.get("recipients")
    if not recipients and isinstance(body, dict):
        recipients = body.get("recipients")
    if not recipients:
        req = _account_for_request()
        recipients = [req["characterUuid"]] if req else []
    print(f"=== alpha10 character profile batch recipients={recipients} ===", flush=True)
    profiles = [_character_profile(uuid_) for uuid_ in recipients]
    # SkySaga.exe 0x009f1f53 expects root JSON node type == 2 (Array)
    return jsonify(profiles), 200


def _build_online_data(account):
    if not account:
        return {}
    world = ACCOUNT_STORE.get_or_create_home_world(account)
    world_uuid = world.get("uuid", "")
    server_uuid = world.get("serverUuid") or world_uuid or account.get("characterUuid", "")
    biome = account.get("homeBiome", "Desert") or "Desert"
    name = account.get("name", "Explorer")
    char_uuid = account.get("characterUuid", "")

    return {
        "characterUuid": char_uuid,
        "characterName": name,
        "currentWorld": world_uuid,
        "currentWorldBiome": biome,
        "currentWorldAdventure": "home",
        "currentWorldSingleton": True,
        "instanceUuid": server_uuid,
        "worldOwnerUuid": char_uuid,
        "worldOwnerName": name,
        "online": True,
        "isOnline": True,
        "status": "online",
    }


def _character_profile(character_uuid):
    req_account = _account_for_request()
    account = None
    char_str = str(character_uuid).strip().lower() if character_uuid else ""

    if char_str:
        if req_account and str(req_account.get("characterUuid", "")).lower() == char_str:
            account = req_account
        if not account:
            account = ACCOUNT_STORE.get_by_character_uuid(char_str)
        if not account:
            with _identity_lock:
                for peer_acc in _peer_accounts.values():
                    if peer_acc and str(peer_acc.get("characterUuid", "")).lower() == char_str:
                        account = peer_acc
                        break
        if not account and str(ACCOUNT.get("characterUuid", "")).lower() == char_str:
            account = ACCOUNT

    if not account:
        account = req_account or ACCOUNT

    created_at = account.get("createdAt", ACCOUNT_CREATED) if account else ACCOUNT_CREATED
    online_data = _build_online_data(account)

    return {
        "characterOnlineData": online_data,
        "counts": ACCOUNT_STORE.relationship_counts(account),
        "accountCreated": created_at,
        "online": True,
        "isOnline": True,
        "status": "online",
        "instanceUuid": online_data.get("instanceUuid", ""),
    }


@app.route("/api/social-graph/character/profile", methods=["POST"])
def alpha10_character_profile():
    body = request.get_json(silent=True, force=True) or {}
    params = body.get("params", body) if isinstance(body, dict) else {}
    recipient = None
    if isinstance(params, dict):
        recipient = params.get("recipient") or params.get("id") or params.get("characterId")
    if not recipient and isinstance(body, dict):
        recipient = body.get("recipient") or body.get("id") or body.get("characterId")
    if not recipient:
        req = _account_for_request()
        recipient = req["characterUuid"] if req else None
    profile = _character_profile(recipient)
    print(f"=== alpha10 character profile recipient={recipient} name={profile['characterOnlineData']['characterName']} instance={profile['characterOnlineData']['instanceUuid']} ===", flush=True)
    return jsonify({"result": profile, **profile}), 200


@app.route("/api/social-graph/character/relationship/info", methods=["POST"])
def alpha10_character_relationship_info():
    body = request.get_json(silent=True, force=True) or {}
    params = body.get("params", body) if isinstance(body, dict) else {}
    if not isinstance(params, dict):
        params = {}

    req_account = None
    for src in (body, params):
        if isinstance(src, dict):
            c_val = src.get("character")
            if c_val and isinstance(c_val, str):
                req_account = ACCOUNT_STORE.get_by_character_uuid(c_val)
                if req_account:
                    break
    if not req_account:
        req_account = _account_for_request()

    rel_list = None
    if isinstance(params, dict):
        rel_list = params.get("relationships") or params.get("recipients")
    if not rel_list and isinstance(body, dict):
        rel_list = body.get("relationships") or body.get("recipients")
    if not rel_list:
        target = params.get("character") or params.get("recipient") or params.get("id") if isinstance(params, dict) else None
        if target:
            rel_list = [target]
        else:
            rel_list = [req_account["characterUuid"]] if req_account and req_account.get("characterUuid") else []

    if isinstance(rel_list, str):
        rel_list = [rel_list]

    # SkySaga.exe 0x009f21d3 expects root JSON node type == 2 (Array)
    # SkySaga.exe 0x009edfe0 expects each element to be a 10-element array:
    # [ "<character_uuid>", is_friend, is_following, is_followed_by, is_blocked, is_incoming, is_outgoing, is_ignored, bit7, bit8 ]
    result = []
    seen = set()
    for char_uuid in rel_list:
        u_str = str(char_uuid).strip().lower()
        if not u_str or u_str in seen:
            continue
        seen.add(u_str)
        other = ACCOUNT_STORE.get_by_character_uuid(u_str)
        flags = ACCOUNT_STORE.relationship(req_account, other)
        result.append([
            u_str,
            flags["friend"], flags["following"], flags["followedBy"],
            False, flags["incoming"], flags["outgoing"], False, False, False,
        ])

    print(f"=== alpha10 character relationship info for {req_account['name']}: {result} ===", flush=True)
    return jsonify(result), 200


@app.route("/api/social-graph/character/friends/info", methods=["POST"])
def alpha10_character_friends_info():
    # SkySaga.exe 0x90fed0 looks for "characterOnlineData" (array of objects with "characterUuid")
    # and "total" (integer count), triggering 0x5b5500 to update the online notification badge.
    req_account = _account_for_request()
    friends = ACCOUNT_STORE.list_friends(req_account)
    online_data = [_build_online_data(item) for item in friends]
    values = [item["characterUuid"] for item in friends]
    resp = {
        "characterOnlineData": online_data,
        "characters": online_data,
        "total": len(friends),
        "totalHits": len(friends),
        "count": len(friends),
        "friends": values,
    }
    print(f"=== friends/info for {req_account['name']}: {len(friends)} friends ===", flush=True)
    return jsonify({"result": resp, **resp}), 200


@app.route("/api/social-graph/character/follows/info", methods=["POST"])
def alpha10_character_follows_info():
    # SkySaga.exe 0x90fcc0 checks "characterOnlineData" + "total"; 0x910d10 checks "characters" + "count".
    req_account = _account_for_request()
    follows = ACCOUNT_STORE.list_follows(req_account)
    char_list = [_build_online_data(item) for item in follows]
    values = [item["characterUuid"] for item in follows]
    resp = {
        "characters": char_list,
        "characterOnlineData": char_list,
        "count": len(follows),
        "total": len(follows),
        "totalHits": len(follows),
        "follows": values,
    }
    return jsonify({"result": resp, **resp}), 200


@app.route("/api/social-graph/character/followedby/info", methods=["POST"])
def alpha10_character_followedby_info():
    # SkySaga.exe 0x910d10 checks "characters" + "count".
    req_account = _account_for_request()
    followed_by = ACCOUNT_STORE.list_followed_by(req_account)
    char_list = [_build_online_data(item) for item in followed_by]
    values = [item["characterUuid"] for item in followed_by]
    resp = {
        "characters": char_list,
        "characterOnlineData": char_list,
        "count": len(followed_by),
        "total": len(followed_by),
        "totalHits": len(followed_by),
        "followedBy": values,
    }
    return jsonify({"result": resp, **resp}), 200


@app.route("/api/social-graph/character/<char_uuid>/blocked", methods=["GET", "POST"])
@app.route("/api/social-graph/character/blocked", methods=["GET", "POST"])
def alpha10_character_blocked(char_uuid=None):
    # SkySaga.exe 0x910f10 expects an array of blocked objects with "characterUuid".
    return jsonify([]), 200


@app.route("/api/social-graph/character/block", methods=["POST"])
def alpha10_character_block():
    return _social_action_ok()


@app.route("/api/social-graph/character/block/remove", methods=["POST"])
def alpha10_character_block_remove():
    return _social_action_ok()


@app.route("/api/social-graph/character/buildpermission/update", methods=["POST"])
def alpha10_character_buildpermission_update():
    return _social_action_ok()


@app.route("/api/persistent-record/characters/_findWithOnlineInfo", methods=["POST"])
def alpha10_characters_find_with_online_info():
    # SkySaga.exe 0x910d10 expects "characters" array and "count" integer.
    body = request.get_json(silent=True, force=True) or {}
    params = body.get("params", body) if isinstance(body, dict) else {}
    if not isinstance(params, dict):
        params = {}
    include_friends = params.get("includeFriends", True)
    name_starts_with = (params.get("nameStartsWith") or "").strip().lower()

    req_account = _account_for_request()
    friend_ids = set()
    if req_account:
        friends = ACCOUNT_STORE.list_friends(req_account)
        friend_ids = {f["accountId"] for f in friends}

    accounts = ACCOUNT_STORE.list_all_accounts()
    char_list = []
    for acc in accounts:
        if req_account and acc["accountId"] == req_account["accountId"]:
            continue
        if not include_friends and acc["accountId"] in friend_ids:
            continue
        if name_starts_with and not acc["name"].lower().startswith(name_starts_with):
            continue
        char_list.append(_build_online_data(acc))

    resp = {
        "characters": char_list,
        "characterOnlineData": char_list,
        "count": len(char_list),
        "total": len(char_list),
        "totalHits": len(char_list),
    }
    return jsonify({"result": resp, **resp}), 200


def _social_request_params():
    """Read the client action payload without assuming one JSON wrapper.

    The Alpha 10 client uses different payload builders for social screens and
    context-menu actions.  Some deliver a normal object, while others pass a
    bare UUID or a list.  Keep the original data so the target resolver can
    identify the other signed-in character in any of those forms.
    """
    parsed = request.get_json(silent=True, force=True)
    if isinstance(parsed, dict):
        params = parsed.get("params", parsed)
        return parsed, params if isinstance(params, dict) else {}
    if isinstance(parsed, (list, tuple)):
        return {"_values": list(parsed)}, {}
    if isinstance(parsed, str):
        return {"_rawTarget": parsed}, {}
    raw = request.get_data(cache=True, as_text=True).strip()
    return ({"_rawTarget": raw} if raw else {}), {}


def _social_target(body, params):
    """Resolve an action recipient by UUID or display name."""
    candidates = ("recipient", "character", "characterUuid", "characterUUID", "characterId",
                  "target", "targetCharacterUuid", "friendUuid", "friendUUID", "requester",
                  "sender", "player", "id", "name", "characterName", "playerName", "username")
    request_account = _account_for_request()

    for values in (params, body):
        for key in candidates:
            value = values.get(key)
            if isinstance(value, dict):
                value = (value.get("characterUuid") or value.get("uuid") or value.get("id") or
                         value.get("name") or value.get("characterName"))
            if value:
                account = ACCOUNT_STORE.get_by_character_uuid(value)
                if not account and isinstance(value, str):
                    account = ACCOUNT_STORE.get_by_name(value)
                if account and account["accountId"] != request_account["accountId"]:
                    print(f"[social] {request.path} target={account['name']} via {key}")
                    return account

    # Bare UUID, display name, and list payloads emitted by some context-menu actions.
    values_to_check = []

    def collect(value):
        if isinstance(value, str):
            values_to_check.append(value)
        elif isinstance(value, dict):
            for nested in value.values():
                collect(nested)
        elif isinstance(value, (list, tuple)):
            for nested in value:
                collect(nested)

    collect(params)
    collect(body)
    for value in values_to_check:
        account = ACCOUNT_STORE.get_by_character_uuid(value)
        if not account and isinstance(value, str):
            account = ACCOUNT_STORE.get_by_name(value)
        if account and account["accountId"] != request_account["accountId"]:
            print(f"[social] {request.path} target={account['name']} via bare payload")
            return account
    print(f"[social] {request.path} had no resolvable target; payload keys={list(body)}")
    return None


def _social_action_error(message, status=400):
    return jsonify({"error": message, "result": {"ok": False, "error": message}}), status


def _social_action_ok():
    # Mutation RPCs only require a successful object response.  The client
    # refreshes relationship state separately.
    return jsonify({"result": {}, "ok": True}), 200


@app.route("/api/social-graph/friendrequest/incoming", methods=["POST"])
def alpha10_friend_requests_incoming():
    # SkySaga.exe 0x9104c0 expects "results" (array of {"characterUuid": ...}) and "totalHits" (int).
    # When totalHits > 0, 0x5b5500 is called to trigger the HUD notification popup/badge!
    req_account = _account_for_request()
    accounts = ACCOUNT_STORE.list_friend_requests(req_account, incoming=True)
    results = [_build_online_data(item) for item in accounts]
    values = [item["characterUuid"] for item in accounts]
    response = {
        "results": results,
        "totalHits": len(results),
        "total": len(results),
        "count": len(results),
        "friendRequests": values,
        "requests": values,
    }
    print(f"=== friendrequest/incoming for {req_account['name']}: {len(results)} requests ===", flush=True)
    return jsonify({"result": response, **response}), 200


@app.route("/api/social-graph/friendrequest/outgoing", methods=["POST"])
def alpha10_friend_requests_outgoing():
    # SkySaga.exe 0x910ab0 expects "results" and "totalHits" parsed via 0x9111d0.
    req_account = _account_for_request()
    accounts = ACCOUNT_STORE.list_friend_requests(req_account, incoming=False)
    results = [_build_online_data(item) for item in accounts]
    values = [item["characterUuid"] for item in accounts]
    response = {
        "results": results,
        "totalHits": len(results),
        "total": len(results),
        "count": len(results),
        "friendRequests": values,
        "requests": values,
    }
    print(f"=== friendrequest/outgoing for {req_account['name']}: {len(results)} requests ===", flush=True)
    return jsonify({"result": response, **response}), 200


@app.route("/api/social-graph/friendrequest/create", methods=["POST"])
def alpha10_friend_request_create():
    body, params = _social_request_params()
    sender = None
    target = None
    if isinstance(body, dict):
        if "requester" in body and "recipient" in body:
            sender = ACCOUNT_STORE.get_by_character_uuid(body["requester"])
            target = ACCOUNT_STORE.get_by_character_uuid(body["recipient"])
    if sender is None:
        sender = _account_for_request()
    if target is None:
        target = _social_target(body, params)
    if target is None or sender is None:
        return _social_action_error("Unknown friend request recipient", 404)
    if sender["accountId"] == target["accountId"]:
        return _social_action_error("Cannot friend request yourself", 400)
    try:
        ACCOUNT_STORE.create_friend_request(sender, target)
    except ValueError as exc:
        return _social_action_error(str(exc))
    return _social_action_ok()


@app.route("/api/social-graph/friendrequest/cancel", methods=["POST"])
def alpha10_friend_request_cancel():
    body, params = _social_request_params()
    acc1 = None
    acc2 = None
    if isinstance(body, dict):
        if "requester" in body and "recipient" in body:
            acc1 = ACCOUNT_STORE.get_by_character_uuid(body["requester"])
            acc2 = ACCOUNT_STORE.get_by_character_uuid(body["recipient"])
    if not acc1 or not acc2:
        acc1 = _account_for_request()
        acc2 = _social_target(body, params)
    if acc1 and acc2:
        ACCOUNT_STORE.cancel_friend_request(acc1, acc2)
    return _social_action_ok()


@app.route("/api/social-graph/friendrequest/accept", methods=["POST"])
def alpha10_friend_request_accept():
    body, params = _social_request_params()
    acc1 = None
    acc2 = None
    if isinstance(body, dict):
        if "requester" in body and "recipient" in body:
            acc1 = ACCOUNT_STORE.get_by_character_uuid(body["requester"])
            acc2 = ACCOUNT_STORE.get_by_character_uuid(body["recipient"])
    if not acc1 or not acc2:
        acc1 = _account_for_request()
        acc2 = _social_target(body, params)
    if acc1 is None or acc2 is None:
        return _social_action_error("Unknown friend request sender", 404)
    if not ACCOUNT_STORE.accept_friend_request(acc1, acc2):
        return _social_action_error("Friend request was not pending", 409)
    return _social_action_ok()


@app.route("/api/social-graph/friendrequest/reject", methods=["POST"])
def alpha10_friend_request_reject():
    body, params = _social_request_params()
    acc1 = None
    acc2 = None
    if isinstance(body, dict):
        if "requester" in body and "recipient" in body:
            acc1 = ACCOUNT_STORE.get_by_character_uuid(body["requester"])
            acc2 = ACCOUNT_STORE.get_by_character_uuid(body["recipient"])
    if not acc1 or not acc2:
        acc1 = _account_for_request()
        acc2 = _social_target(body, params)
    if acc1 and acc2:
        ACCOUNT_STORE.reject_friend_request(acc1, acc2)
    return _social_action_ok()


@app.route("/api/social-graph/character/unfriend", methods=["POST"])
def alpha10_unfriend():
    body, params = _social_request_params()
    acc1 = None
    acc2 = None
    if isinstance(body, dict):
        if "requester" in body and "recipient" in body:
            acc1 = ACCOUNT_STORE.get_by_character_uuid(body["requester"])
            acc2 = ACCOUNT_STORE.get_by_character_uuid(body["recipient"])
    if not acc1 or not acc2:
        acc1 = _account_for_request()
        acc2 = _social_target(body, params)
    if acc1 and acc2:
        ACCOUNT_STORE.remove_friend(acc1, acc2)
    return _social_action_ok()


@app.route("/api/social-graph/character/follow", methods=["POST"])
def alpha10_follow():
    body, params = _social_request_params()
    follower = None
    followed = None
    if isinstance(body, dict):
        if "requester" in body and "recipient" in body:
            follower = ACCOUNT_STORE.get_by_character_uuid(body["requester"])
            followed = ACCOUNT_STORE.get_by_character_uuid(body["recipient"])
    if follower is None:
        follower = _account_for_request()
    if followed is None:
        followed = _social_target(body, params)
    if follower is None or followed is None:
        return _social_action_error("Unknown player to follow", 404)
    if follower["accountId"] == followed["accountId"]:
        return _social_action_error("Cannot follow yourself", 400)
    try:
        ACCOUNT_STORE.follow(follower, followed)
    except ValueError as exc:
        return _social_action_error(str(exc))
    return _social_action_ok()


@app.route("/api/social-graph/character/unfollow", methods=["POST"])
def alpha10_unfollow():
    body, params = _social_request_params()
    follower = None
    followed = None
    if isinstance(body, dict):
        if "requester" in body and "recipient" in body:
            follower = ACCOUNT_STORE.get_by_character_uuid(body["requester"])
            followed = ACCOUNT_STORE.get_by_character_uuid(body["recipient"])
    if follower is None:
        follower = _account_for_request()
    if followed is None:
        followed = _social_target(body, params)
    if follower and followed:
        ACCOUNT_STORE.unfollow(follower, followed)
    return _social_action_ok()


@app.route("/api/persistent-record/groups/info", methods=["GET", "POST"])
def alpha10_groups_info():
    """Return a typed empty group collection until the UDP group protocol is complete.

    The former catch-all returned the string ``ok`` here, which is not a
    collection and leaves the Groups page with an invalid response shape.
    """
    result = {"groups": [], "total": 0, "totalHits": 0}
    return jsonify({"result": result, **result}), 200


@app.route("/api/social-graph/guilds/members/list/info", methods=["GET", "POST"])
def alpha10_guild_members_list_info():
    # SkySaga.exe 0x00a73357 expects characterOnlineData and members
    resp = {
        "characterOnlineData": [],
        "members": [],
        "total": 0,
        "count": 0,
    }
    return jsonify({"result": resp, **resp}), 200


@app.route("/api/online-presence/onlineCharacterHandler", methods=["GET", "POST"])
def alpha10_online_character_handler():
    return jsonify({"status": "ok"}), 200


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


def _photo_metadata(record, host_url=None):
    if not host_url:
        http_scheme = "https" if request.is_secure else "http"
        host_url = f"{http_scheme}://{request.host}"
    photo_uuid = record["uuid"]
    md5_hash = record["hash"]
    img_url = f"{host_url}/api/binary-storage/photos/{photo_uuid}/image"

    file_info = {
        "url": img_url,
        "hash": md5_hash,
    }

    return {
        "uuid": photo_uuid,
        "photoUUID": photo_uuid,
        "photo": photo_uuid,
        "name": record.get("caption") or f"Photo {photo_uuid[:8]}",
        "thumbnail": file_info,
        "scaled": file_info,
        "file": file_info,
        "large": file_info,
        "gallery": True,
        "likes": record.get("likes", 0),
        "avatarPhoto": bool(record.get("is_avatar", 0)),
        "created": record.get("created_at") or "",
        "creationDate": record.get("created_at") or "",
        "access": {
            "visiblePublic": bool(record.get("visible_public", 1)),
            "visibleFriends": bool(record.get("visible_friends", 1)),
        },
        "game": {
            "won": 0,
            "lost": 0,
            "thumbsTotal": record.get("likes", 0),
        },
        "privateData": {
            "owner": record["owner_uuid"],
            "ownerName": record["owner_name"],
            "visiblePublic": bool(record.get("visible_public", 1)),
            "visibleFriends": bool(record.get("visible_friends", 1)),
            "avatarPhoto": bool(record.get("is_avatar", 0)),
            "likes": record.get("likes", 0),
        },
        "metaData": {
            "owner": record["owner_uuid"],
            "created": record.get("created_at") or "",
            "worldUUID": record.get("world_uuid", ""),
            "biome": record.get("biome", "Forest"),
            "adventure": record.get("adventure", "home"),
            "seed": 1337,
            "paletteName": record.get("palette_name", "Forest"),
            "caption": record.get("caption", ""),
            "avatarPhoto": bool(record.get("is_avatar", 0)),
            "tags": [],
        }
    }


@app.route("/api/binary-storage/photos/_whichIsCooler", methods=["POST"])
def alpha10_binary_storage_cooler():
    photos = PHOTO_STORE.list_public_photos()
    if len(photos) >= 2:
        return jsonify({
            "result": {
                "results": [photos[0]["uuid"], photos[1]["uuid"]],
                "battleToken": "battle_token_1"
            }
        }), 200
    return jsonify({"result": {"results": []}}), 200


@app.route("/api/persistent-record/worlds/homeworldforowner/<owner>", methods=["GET"])
def alpha10_worlds_homeworld_for_owner(owner):
    print("=== worlds/homeworldforowner", owner, "===")
    try:
        requested_owner = str(uuid.UUID(owner))
    except (TypeError, ValueError, AttributeError):
        return jsonify({}), 404
    owner_account = ACCOUNT_STORE.get_by_character_uuid(requested_owner)
    if not owner_account:
        owner_account = _account_for_request()
    world = ACCOUNT_STORE.get_or_create_home_world(owner_account)
    hw = _home_world(world)
    return jsonify({"result": hw, **hw}), 200


@app.route("/api/persistent-record/worlds/listforowner/<owner>", methods=["GET"])
def alpha10_worlds_list_for_owner(owner):
    print("=== worlds/listforowner", owner, "===")
    try:
        requested_owner = str(uuid.UUID(owner))
    except (TypeError, ValueError, AttributeError):
        return jsonify([]), 200
    owner_account = ACCOUNT_STORE.get_by_character_uuid(requested_owner)
    if not owner_account:
        req = _account_for_request()
        if req and str(req.get("characterUuid", "")).lower() == requested_owner.lower():
            owner_account = req
        else:
            owner_account = req
    world = ACCOUNT_STORE.get_or_create_home_world(owner_account)
    print(f"[islands] Listed {owner_account['name']}'s home island {world['uuid']} for owner={owner}")
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

    worlds = [_home_world(record) for record in ACCOUNT_STORE.list_home_worlds()]
    worlds = [world for world in worlds if world.get("published", True) and (
        (not owner_search and not name_search)
        or (owner_search and owner_search in world.get("ownerName", "").casefold())
        or (name_search and name_search in world.get("name", "").casefold())
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


@app.route("/api/persistent-record/curatedcontent/dsscontent/_search", methods=["POST"])
def alpha10_featured_ugc_islands():
    """Answer the Destination Selection featured-island request with an empty world list.

    SkySaga V10 identifies this RPC as HTTPRPCGetFeaturedUGCIslands and looks
    for a 'worlds' key in the root JSON response object.
    """
    result = {"worlds": []}
    return jsonify({"result": result, **result}), 200


@app.route("/api/persistent-record/worlds/multiGet", methods=["POST"])
def alpha10_worlds_multi_get():
    """Return the complete browser records requested for a page of islands.

    The V10 client identifies this call as HTTPRPCGetGenericIslandList and
    requires a JSON array of world card objects (matching HTTPRPCGetHomeIslandList).
    """
    body = request.get_json(silent=True) or {}
    if isinstance(body, list):
        requested_ids = body
    elif isinstance(body, dict):
        requested_ids = body.get("ids", [])
    else:
        requested_ids = []

    if not isinstance(requested_ids, list):
        requested_ids = []

    known_worlds = {
        world["uuid"]: world
        for world in ACCOUNT_STORE.list_home_worlds()
    }
    cards = []
    for world_id in requested_ids:
        wid_str = str(world_id)
        world = known_worlds.get(wid_str) or ACCOUNT_STORE.get_home_world_by_uuid(wid_str)
        if world is not None:
            cards.append(_home_world(world))

    print(f"[world-browser] multiGet requested={len(requested_ids)} returned={len(cards)}")
    return jsonify(cards), 200


@app.route("/api/binary-storage/photos/<photo_uuid>/_upload", methods=["POST"])
def alpha10_photo_upload(photo_uuid):
    account = _account_for_request()
    owner_uuid = account["characterUuid"]
    owner_name = account["name"]

    file_bytes = b""
    if "photo" in request.files:
        file_bytes = request.files["photo"].read()
    elif request.data:
        file_bytes = request.data

    world_uuid = ""
    home_world = ACCOUNT_STORE.get_or_create_home_world(account)
    if home_world:
        world_uuid = home_world.get("uuid", "")

    record = PHOTO_STORE.save_photo_file(
        photo_uuid=photo_uuid,
        image_bytes=file_bytes,
        owner_uuid=owner_uuid,
        owner_name=owner_name,
        world_uuid=world_uuid,
    )

    PHOTO_STORE.set_avatar(owner_uuid, photo_uuid)
    record["is_avatar"] = 1

    print(f"[photo-upload] Uploaded {photo_uuid} ({len(file_bytes)} bytes) by {owner_name} ({owner_uuid}) - set as avatar")
    meta = _photo_metadata(record)
    return jsonify({"result": meta, **meta}), 200


@app.route("/api/binary-storage/photos/<photo_uuid>/access", methods=["POST"])
def alpha10_photo_access(photo_uuid):
    body = request.get_json(silent=True, force=True) or {}
    params = body.get("params", body)
    vis_public = params.get("visiblePublic", True)
    vis_friends = params.get("visibleFriends", True)
    PHOTO_STORE.update_access(photo_uuid, vis_public, vis_friends)
    print(f"[photo-access] Updated access for {photo_uuid}: public={vis_public}, friends={vis_friends}")
    return jsonify({"result": "OK"}), 200


@app.route("/api/binary-storage/photos/<photo_uuid>", methods=["GET"])
def alpha10_photo_get(photo_uuid):
    record = PHOTO_STORE.get_photo(photo_uuid)
    if not record:
        return jsonify({"error": "Photo not found"}), 404
    meta = _photo_metadata(record)
    return jsonify({"result": meta, **meta}), 200


@app.route("/api/binary-storage/photos/<photo_uuid>/image", methods=["GET"])
def alpha10_photo_download(photo_uuid):
    file_path = PHOTO_STORE.photos_dir / f"{photo_uuid}.jpg"
    if not file_path.exists():
        return Response("Photo not found", status=404)
    data = file_path.read_bytes()
    return Response(data, mimetype="image/jpeg", headers={
        "Content-Type": "image/jpeg",
        "Content-Length": str(len(data)),
        "Cache-Control": "no-cache, must-revalidate"
    })


@app.route("/api/binary-storage/photos/<photo_uuid>", methods=["DELETE", "POST"])
@app.route("/api/binary-storage/photos/<photo_uuid>/_delete", methods=["DELETE", "POST"])
def alpha10_photo_delete(photo_uuid):
    account = _account_for_request()
    owner_uuid = account.get("characterUuid") if isinstance(account, dict) else None
    PHOTO_STORE.delete_photo(photo_uuid, owner_uuid)
    print(f"[photo-delete] Photo {photo_uuid} deleted (requester={owner_uuid})")
    return jsonify({"result": "OK"}), 200


@app.route("/api/binary-storage/photos/_searchAvatarByCharacterId", methods=["POST"])
def alpha10_photo_search_avatar():
    body = request.get_json(silent=True, force=True) or {}
    params = body.get("params", body) if isinstance(body, dict) else {}
    char_id = None
    if isinstance(params, dict):
        char_id = params.get("id") or params.get("characterId") or params.get("recipient") or params.get("characterUuid")
    if not char_id and isinstance(body, dict):
        char_id = body.get("id") or body.get("characterId") or body.get("recipient") or body.get("characterUuid")
    if not char_id:
        req = _account_for_request()
        char_id = req["characterUuid"] if req else None
    avatar = PHOTO_STORE.get_avatar(char_id) if char_id else None
    if avatar:
        results = [_photo_metadata(avatar)]
    else:
        results = []
    print(f"[photo-avatar] Avatar for {char_id}: found={bool(avatar)}, photo={avatar['uuid'] if avatar else None}")
    return jsonify({"result": {"results": results}, "results": results}), 200


def _get_page_range(body, params, default_size=24):
    first_val = None
    if isinstance(params, dict):
        first_val = params.get("pageStart") if "pageStart" in params else params.get("first")
    if first_val is None and isinstance(body, dict):
        first_val = body.get("pageStart") if "pageStart" in body else body.get("first")
    try:
        first = max(0, int(first_val))
    except (TypeError, ValueError):
        first = 0

    size_val = None
    if isinstance(params, dict):
        size_val = params.get("pageSize")
    if size_val is None and isinstance(body, dict):
        size_val = body.get("pageSize")
    try:
        page_size = max(1, int(size_val))
    except (TypeError, ValueError):
        page_size = default_size

    return first, page_size


@app.route("/api/binary-storage/photos/_searchByCharacterAndWorldId", methods=["POST"])
def alpha10_photo_search_by_char_and_world():
    body = request.get_json(silent=True, force=True) or {}
    params = body.get("params", body) if isinstance(body, dict) else {}
    char_id = params.get("characterId") or params.get("characterUUID") or params.get("ownerId")
    world_id = params.get("worldId") or params.get("worldUUID")
    first, page_size = _get_page_range(body, params, default_size=24)

    all_photos = PHOTO_STORE.list_photos_by_character_and_world(char_id, world_id)
    page_photos = all_photos[first:first + page_size]
    results = [_photo_metadata(p) for p in page_photos]
    total_hits = max(len(all_photos), first + len(results))
    print(f"[photo-search] char={char_id} world={world_id} first={first} pageSize={page_size} total={total_hits} returned={len(results)}")
    res = {"results": results, "totalHits": total_hits}
    return jsonify({"result": res, **res}), 200


@app.route("/api/persistent-record/curatedcontent/ugcbrowserphotocontent/_search", methods=["POST"])
def alpha10_ugc_browser_photos_search():
    body = request.get_json(silent=True, force=True) or {}
    params = body.get("params", body) if isinstance(body, dict) else {}
    first, page_size = _get_page_range(body, params, default_size=24)
    sort = str(params.get("sort", "hot")).lower()

    all_photos = PHOTO_STORE.list_public_photos(sort=sort)
    page_photos = all_photos[first:first + page_size]

    entries = [
        {
            "photo": p["uuid"],
            "likes": p["likes"],
            "creationDate": p["created_at"],
            "ownerName": p["owner_name"],
        }
        for p in page_photos
    ]

    # In SkySaga client (0x00a34d00 & 0x00a355a0), the promoted photo vector capacity is
    # permanently 0 (never resized). Returning any items in promotedUgcPhotos causes an
    # assertion failure cmp index, 0 at 0x00a355c1. It MUST ALWAYS be empty [].
    result = {
        "totalHotPhotoCount": len(all_photos),
        "hotUgcPhotos": entries,
        "promotedUgcPhotos": [],
    }
    print(f"[photo-browser] UGC Gallery search: sort={sort} total={len(all_photos)}, first={first}, page={len(entries)}")
    return jsonify({"result": result, **result}), 200


@app.route("/api/binary-storage/photos/_multiLikeGet", methods=["POST"])
def binary_storage_photo_likes():
    body = request.get_json(silent=True, force=True) or {}
    params = body.get("params", body) if isinstance(body, dict) else {}
    ids = params.get("ids", [])
    likes_dict = {photo_id: (PHOTO_STORE.get_photo(photo_id)["likes"] if PHOTO_STORE.get_photo(photo_id) else 0) for photo_id in ids}
    return jsonify({"result": {"likes": likes_dict}, "likes": likes_dict}), 200


@app.route("/api/persistent-record/likes/_multiIsLiked", methods=["POST"])
def alpha10_multi_is_liked():
    body = request.get_json(silent=True, force=True) or {}
    params = body.get("params", body) if isinstance(body, dict) else {}
    char_id = params.get("characterId") or _account_for_request()["characterUuid"]
    ids = params.get("ids", [])
    liked_map = PHOTO_STORE.multi_is_liked(ids, char_id)
    # The client checks "likes" in 0x00a8b470 and "isPlayerLiked" in other places
    res = {
        "likes": liked_map,
        "isLiked": liked_map,
        "isPlayerLiked": liked_map,
    }
    return jsonify({"result": res, **res, **liked_map}), 200


@app.route("/api/binary-storage/photos/_like", methods=["POST"])
def alpha10_photo_like():
    body = request.get_json(silent=True, force=True) or {}
    params = body.get("params", body) if isinstance(body, dict) else {}
    photo_uuid = params.get("id") or params.get("photoUUID")
    char_uuid = params.get("characterId") or _account_for_request()["characterUuid"]
    total = 0
    if photo_uuid:
        PHOTO_STORE.like_photo(photo_uuid, char_uuid)
        photo = PHOTO_STORE.get_photo(photo_uuid)
        total = photo["likes"] if photo else 0
    result = {"id": photo_uuid, "total": total}
    return jsonify({"result": result, **result}), 200


@app.route("/api/binary-storage/photos/_unlike", methods=["POST"])
def alpha10_photo_unlike():
    body = request.get_json(silent=True, force=True) or {}
    params = body.get("params", body) if isinstance(body, dict) else {}
    photo_uuid = params.get("id") or params.get("photoUUID")
    char_uuid = params.get("characterId") or _account_for_request()["characterUuid"]
    total = 0
    if photo_uuid:
        PHOTO_STORE.unlike_photo(photo_uuid, char_uuid)
        photo = PHOTO_STORE.get_photo(photo_uuid)
        total = photo["likes"] if photo else 0
    result = {"id": photo_uuid, "total": total}
    return jsonify({"result": result, **result}), 200


@app.route("/api/persistent-record/curatedcontent/ugcbrowserblueprintcontent/_search", methods=["POST"])
def alpha10_ugc_browser_blueprints_search():
    result = {
        "totalHotBlueprintCount": 0,
        "hotUgcBlueprints": [],
    }
    return jsonify({"result": result, **result}), 200


@app.route("/api/binary-storage/photos/_searchByName", methods=["POST"])
def alpha10_photo_search_by_name():
    body = request.get_json(silent=True, force=True) or {}
    params = body.get("params", body) if isinstance(body, dict) else {}
    name = str(params.get("name", "")).strip().casefold()
    first, page_size = _get_page_range(body, params, default_size=24)

    all_photos = PHOTO_STORE.list_public_photos()
    matches = [p for p in all_photos if not name or name in (p.get("caption") or "").casefold()]
    page_photos = matches[first:first + page_size]
    results = [_photo_metadata(p) for p in page_photos]
    total_hits = max(len(matches), first + len(results))
    res = {"results": results, "totalHits": total_hits}
    return jsonify({"result": res, **res}), 200


@app.route("/api/binary-storage/photos/_searchRandomByWorldId", methods=["POST"])
def alpha10_photo_search_random_by_world():
    body = request.get_json(silent=True, force=True) or {}
    params = body.get("params", body) if isinstance(body, dict) else {}
    world_id = params.get("worldId") or params.get("worldUUID")
    photos = PHOTO_STORE.list_photos_by_character_and_world(world_uuid=world_id) if world_id else PHOTO_STORE.list_public_photos()
    if photos:
        import random
        chosen = random.choice(photos)
        meta = _photo_metadata(chosen)
        return jsonify({"result": meta, **meta}), 200
    return jsonify({"result": {}, "results": []}), 200


@app.route("/api/binary-storage/photos/_searchByMapSpec", methods=["POST"])
def alpha10_photo_search_by_mapspec():
    body = request.get_json(silent=True, force=True) or {}
    params = body.get("params", body) if isinstance(body, dict) else {}
    char_id = params.get("characterId") or params.get("characterUUID") or params.get("ownerId")
    first, page_size = _get_page_range(body, params, default_size=24)

    all_photos = PHOTO_STORE.list_photos_by_character_and_world(char_id) if char_id else PHOTO_STORE.list_public_photos()
    page_photos = all_photos[first:first + page_size]
    results = [_photo_metadata(p) for p in page_photos]
    total_hits = max(len(all_photos), first + len(results))
    res = {"results": results, "totalHits": total_hits}
    return jsonify({"result": res, **res}), 200


@app.route("/api/binary-storage/photos/_whichIsCoolerOutcome", methods=["POST"])
def alpha10_photo_which_is_cooler_outcome():
    return jsonify({"result": "OK"}), 200


@app.route("/api/binary-storage/photos/_report", methods=["POST"])
def alpha10_photo_report():
    return jsonify({"result": "OK"}), 200


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


# Duplicate relationship route removed; handled by alpha10_character_relationship_info


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
    owner_name = world_record.get("ownerName") or world_record.get("name") or "Player"
    world_name = world_record.get("name") or f"{owner_name}'s Ruins"
    return {
        "uuid": world_record["uuid"],
        "mapSpec": HOME_ISLAND_MAP_SPEC,
        "owner": world_record["owner"],
        "ownerName": owner_name,
        "ownerType": "C",
        "name": world_name,
        "homeWorld": True,
        "published": True,
        "deleted": False,
        "blueprint": False,
        "created": int(world_record.get("created_at") or 0),
        "updated": int(world_record.get("created_at") or 0),
        "publishId": 0,
        "likes": int(world_record.get("likes") or 0),
        "visits": int(world_record.get("visits") or 0),
        "photoIds": [],
        "engagement": "",
        "tagCrcs": [],
    }


def _social_hub_world():
    return {
        "uuid": SOCIAL_HUB_WORLD_UUID,
        "mapSpec": SOCIAL_HUB_MAP_SPEC,
        "owner": "00000000-0000-0000-0000-000000000000",
        "ownerName": "The City of Last Light",
        "ownerType": "C",
        "name": "The City of Last Light",
        "homeWorld": False,
        "published": True,
        "deleted": False,
        "blueprint": False,
        "created": 0,
        "updated": 0,
        "publishId": 0,
        "likes": 0,
        "visits": 0,
        "photoIds": [],
        "engagement": "",
        "tagCrcs": [],
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
