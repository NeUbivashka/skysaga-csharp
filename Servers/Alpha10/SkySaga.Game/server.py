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
import json
import os
import lzma
import ssl
import sys
import threading
import uuid
from pathlib import Path

from flask import Flask, Response, jsonify, request

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

LISTEN_HOST = "0.0.0.0"
LISTEN_PORT = 5001

HTTP_BACKEND = ("127.0.0.1", 5080)
HTTPS_BACKEND = ("127.0.0.1", 5443)

CERT_FILE = Path(r"C:\SkySagaCerts\server.crt")
KEY_FILE = Path(r"C:\SkySagaCerts\server.key")

# The single local account. Created once and kept in Data/account.json (not in git), so the
# character keeps the same uuid across restarts. SKYSAGA_PLAYER_NAME overrides the name only
# when the file is first created; edit the file to rename later.
ACCOUNT_FILE = Path(__file__).resolve().parent / "Data" / "account.json"
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


ACCOUNT = _load_account()
PLAYER_NAME = ACCOUNT["name"]
ACCOUNT_CREATED = int(ACCOUNT_FILE.stat().st_ctime)


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

# The account's character. Always present: the 36731 client never calls _create (it asks
# characters/_active, and with character=null it walked into the world with characterUUID ""
# and the world owner GUID did not match the player).
_state = {"character_uuid": ACCOUNT["characterUuid"]}


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
            "Port": 42069,
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
    print("=== alpha10 login request ===")
    print("Body:", request.get_data(as_text=True))
    return jsonify({"result": _token()}), 200


@app.route("/api/authentication/sgauth/_login", methods=["POST"])
def alpha10_sgauth_login():
    print("=== alpha10 sgauth login request ===")
    print("Body:", request.get_data(as_text=True))
    return jsonify({
        "result": {
            "sgUser": "",
            "memberId": "1",
            "username": PLAYER_NAME,
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
    return jsonify({
        "token": {
            "tokenId": str(uuid.uuid4()),
            "refreshingTokenId": str(uuid.uuid4()),
            "timeout": 999999,
        },
        "principal": PLAYER_NAME,
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
    return {
        "uuid": _state["character_uuid"],
        "name": PLAYER_NAME,
        "homeBiome": ACCOUNT["homeBiome"],
        "positionInList": 0,
    }


@app.route("/api/persistent-record/profanity/blacklist", methods=["POST"])
def alpha10_profanity_blacklist():
    return jsonify({"result": []}), 200


@app.route("/api/persistent-record/characters/list", methods=["GET"])
def alpha10_characters_list():
    if _state["character_uuid"] is None:
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
    if _state["character_uuid"] is None:
        return jsonify({"result": {"character": None}}), 200
    return jsonify({"result": {"character": _character(), **_character()}}), 200


@app.route("/api/persistent-record/characters/_create", methods=["POST"])
def alpha10_characters_create():
    # One account, one character: creation hands back the existing uuid.
    print("=== alpha10 character create ->", _state["character_uuid"], "===")
    return jsonify({"result": {"characterUUID": _state["character_uuid"]}}), 200


@app.route("/GetGUID", methods=["GET"])
def get_guid():
    # Used by SkySaga.Game to fetch the player's real character uuid for
    # ServerInfo.ServerOwnerGuid, so it matches what the client's
    # ClientOwnerComponent expects. A mismatch here is what made home-island
    # locked items refuse placement.
    return jsonify({
        "result": {"GUID": _state["character_uuid"], "Name": PLAYER_NAME}
    }), 200


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
    recipients = params.get("recipients") or [_state["character_uuid"]]
    print("=== alpha10 character profile batch", request.get_data(as_text=True), "===", flush=True)
    return jsonify({"result": [_character_profile(uuid_) for uuid_ in recipients]}), 200


def _character_profile(character_uuid):
    return {
        "characterOnlineData": {
            "characterUuid": character_uuid,
            "characterName": PLAYER_NAME,
        },
        "counts": {"follows": 0, "followedBy": 0, "friends": 0},
        "accountCreated": ACCOUNT_CREATED,
    }


@app.route("/api/account/get", methods=["POST"])
def alpha10_account_get():
    return jsonify({"result": {"keySubset": {"RESERVED_NAME": PLAYER_NAME}}}), 200


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


@app.route("/api/binary-storage/photos/_multiLikeGet", methods=["POST"])
def binary_storage_photo_likes():
    return jsonify({"result": {"likes": {}}}), 200


@app.route("/api/persistent-record/worlds/getSpecific", methods=["POST"])
def alpha10_worlds_get_specific():
    # The HUD island title reads the world record's owner name. Field names come from the
    # client's world record type (uuid, owner, ownerName, ownerType, name, homeWorld, ...).
    body = request.get_json(silent=True) or {}
    print("=== worlds/getSpecific", body, "===")
    world = {
        "uuid": body.get("uuid", ""),
        "owner": _state["character_uuid"],
        "ownerName": PLAYER_NAME,
        "homeWorld": True,
        "published": False,
        "deleted": False,
    }
    return jsonify({"result": {"world": dict(world), **world}}), 200


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
    return jsonify({
        "result": {
            "retryInMillis": 5000,
            "world": str(uuid.uuid4()),
            "ip": "127.0.0.1",
            "port": 42069,
            "server": str(uuid.uuid4()),
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

    # Forward the byte we already consumed.
    backend_writer.write(first_byte)
    await backend_writer.drain()

    await asyncio.gather(
        pipe(client_reader, backend_writer),
        pipe(backend_reader, client_writer),
    )


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
