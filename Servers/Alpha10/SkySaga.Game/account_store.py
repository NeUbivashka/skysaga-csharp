"""SQLite persistence for Alpha V10 character accounts.

Legacy JSON accounts are imported once. The files are left untouched so an
existing installation can be backed up or rolled back without changing saves.
"""

import hashlib
import json
import sqlite3
import time
import uuid
from contextlib import closing
from pathlib import Path


class AccountStore:
    SCHEMA_VERSION = 6
    MAX_HOME_WORLDS = 8
    # 42069 is the primary home world and 42070-42074 are the fixed City and
    # adventure listeners from Launch Two Worlds.bat.  Player-owned islands
    # must never claim those ports.
    HOME_WORLD_PORT_MIN = 42100
    HOME_WORLD_PORT_MAX = 42299
    HOME_WORLD_UUID = "6f0d3a1e-5c47-4f92-9a6b-1d84c0e73b25"
    HOME_SERVER_UUID = "b3e94c07-2a15-4d68-8f31-7c05a9be4610"
    WORLD_NAMESPACE = uuid.UUID("eef872a9-4785-42c4-83b4-4e1ce956998f")

    def __init__(self, path: Path):
        self.path = path

    def _connect(self):
        connection = sqlite3.connect(self.path, timeout=10)
        connection.row_factory = sqlite3.Row
        connection.execute("PRAGMA busy_timeout=10000")
        connection.execute("PRAGMA foreign_keys=ON")
        return connection

    @staticmethod
    def _account(row):
        if row is None:
            return None
        return {
            "accountId": row["account_id"],
            "kind": row["kind"],
            "name": row["name"],
            "characterUuid": row["character_uuid"],
            "homeBiome": row["home_biome"],
            "createdAt": row["created_at"],
        }

    @staticmethod
    def _import_account(connection, account, kind, created_at):
        name = account["name"]
        character_uuid = str(uuid.UUID(account["characterUuid"]))
        if not isinstance(name, str) or not name:
            raise ValueError(f"Legacy {kind} account has no name")
        connection.execute(
            """INSERT INTO accounts
               (account_id, kind, name_key, name, character_uuid, home_biome, created_at)
               VALUES (?, ?, ?, ?, ?, ?, ?)""",
            (str(uuid.uuid4()), kind, name.casefold(), name, character_uuid,
             account.get("homeBiome") or "Desert", created_at),
        )

    def initialize(self, legacy_host_loader, legacy_host_path: Path, legacy_remote_dir: Path):
        self.path.parent.mkdir(parents=True, exist_ok=True)
        with closing(self._connect()) as connection:
            connection.execute("PRAGMA journal_mode=WAL")
            version = connection.execute("PRAGMA user_version").fetchone()[0]
            if version and version < self.SCHEMA_VERSION:
                backup_path = self.path.with_name(
                    f"{self.path.stem}.pre-port-reservation-v{version}.sqlite3"
                )
                if not backup_path.exists():
                    with sqlite3.connect(backup_path) as backup:
                        connection.backup(backup)
                    print(f"[home-island] Backed up account database to {backup_path.name}")
            with connection:
                connection.execute("BEGIN IMMEDIATE")
                if version > self.SCHEMA_VERSION:
                    raise RuntimeError(f"Account database version {version} is newer than this server")
                connection.execute(
                    """CREATE TABLE IF NOT EXISTS accounts (
                       account_id TEXT PRIMARY KEY,
                       kind TEXT NOT NULL CHECK (kind IN ('host', 'remote')),
                       name_key TEXT NOT NULL,
                       name TEXT NOT NULL,
                       character_uuid TEXT NOT NULL UNIQUE,
                       home_biome TEXT NOT NULL,
                       created_at INTEGER NOT NULL,
                       UNIQUE (kind, name_key)
                    )"""
                )
                if version == 0:
                    host = legacy_host_loader()
                    self._import_account(connection, host, "host", int(legacy_host_path.stat().st_ctime))
                    if legacy_remote_dir.exists():
                        for path in sorted(legacy_remote_dir.glob("*.json")):
                            remote = json.loads(path.read_text(encoding="utf-8"))
                            name = remote["name"]
                            expected = hashlib.sha256(name.casefold().encode("utf-8")).hexdigest()
                            if path.stem != expected:
                                raise ValueError(f"Legacy account file name does not match its username: {path}")
                            self._import_account(connection, remote, "remote", int(path.stat().st_ctime))
                if connection.execute("SELECT 1 FROM accounts WHERE kind = 'host'").fetchone() is None:
                    raise RuntimeError("Account database has no host account; refusing to create a replacement")
                if version < 4:
                    old_worlds = connection.execute(
                        "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'home_worlds'"
                    ).fetchone() is not None
                    if old_worlds:
                        connection.execute("ALTER TABLE home_worlds RENAME TO home_worlds_v3")

                    connection.execute(
                        """CREATE TABLE home_worlds (
                           world_uuid TEXT PRIMARY KEY,
                           account_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
                           server_uuid TEXT NOT NULL UNIQUE,
                           game_port INTEGER NOT NULL UNIQUE,
                           map_file TEXT NOT NULL UNIQUE,
                           slot_index INTEGER NOT NULL CHECK (slot_index >= 0),
                           is_primary INTEGER NOT NULL CHECK (is_primary IN (0, 1)),
                           name TEXT,
                           published INTEGER NOT NULL DEFAULT 1 CHECK (published IN (0, 1)),
                           biome_index INTEGER,
                           region_index INTEGER,
                           adventure_index INTEGER,
                           palette_index INTEGER,
                           terrain_block INTEGER,
                           created_at INTEGER NOT NULL,
                           UNIQUE (account_id, slot_index)
                        )"""
                    )
                    if old_worlds:
                        connection.execute(
                            """INSERT INTO home_worlds
                               (world_uuid, account_id, server_uuid, game_port, map_file,
                                slot_index, is_primary, name, published, created_at)
                               SELECT world_uuid, account_id, server_uuid, game_port, map_file,
                                      0, 1, NULL, 1, ?
                               FROM home_worlds_v3""",
                            (int(time.time()),),
                        )
                        connection.execute("DROP TABLE home_worlds_v3")
                else:
                    connection.execute(
                        """CREATE TABLE IF NOT EXISTS home_worlds (
                           world_uuid TEXT PRIMARY KEY,
                           account_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
                           server_uuid TEXT NOT NULL UNIQUE,
                           game_port INTEGER NOT NULL UNIQUE,
                           map_file TEXT NOT NULL UNIQUE,
                           slot_index INTEGER NOT NULL CHECK (slot_index >= 0),
                           is_primary INTEGER NOT NULL CHECK (is_primary IN (0, 1)),
                           name TEXT,
                           published INTEGER NOT NULL DEFAULT 1 CHECK (published IN (0, 1)),
                           biome_index INTEGER,
                           region_index INTEGER,
                           adventure_index INTEGER,
                           palette_index INTEGER,
                           terrain_block INTEGER,
                           created_at INTEGER NOT NULL,
                           UNIQUE (account_id, slot_index)
                        )"""
                    )
                if version < 5:
                    columns = {
                        row["name"] for row in connection.execute("PRAGMA table_info(home_worlds)")
                    }
                    for column in (
                        "biome_index", "region_index", "adventure_index",
                        "palette_index", "terrain_block",
                    ):
                        if column not in columns:
                            connection.execute(f"ALTER TABLE home_worlds ADD COLUMN {column} INTEGER")
                connection.execute(
                    """CREATE UNIQUE INDEX IF NOT EXISTS one_primary_home_world_per_account
                       ON home_worlds(account_id) WHERE is_primary = 1"""
                )
                # Social records are keyed by the internal account id, never by a
                # client supplied display name or IP address.  This keeps a
                # relationship stable when a player changes machines or joins via
                # the proxy.
                connection.execute(
                    """CREATE TABLE IF NOT EXISTS friend_requests (
                       requester_account_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
                       recipient_account_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
                       created_at INTEGER NOT NULL,
                       PRIMARY KEY (requester_account_id, recipient_account_id),
                       CHECK (requester_account_id <> recipient_account_id)
                    )"""
                )
                connection.execute(
                    """CREATE TABLE IF NOT EXISTS friendships (
                       first_account_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
                       second_account_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
                       created_at INTEGER NOT NULL,
                       PRIMARY KEY (first_account_id, second_account_id),
                       CHECK (first_account_id < second_account_id)
                    )"""
                )
                connection.execute(
                    """CREATE TABLE IF NOT EXISTS follows (
                       follower_account_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
                       followed_account_id TEXT NOT NULL REFERENCES accounts(account_id) ON DELETE CASCADE,
                       created_at INTEGER NOT NULL,
                       PRIMARY KEY (follower_account_id, followed_account_id),
                       CHECK (follower_account_id <> followed_account_id)
                    )"""
                )
                host = connection.execute("SELECT account_id FROM accounts WHERE kind = 'host'").fetchone()
                connection.execute(
                    """INSERT OR IGNORE INTO home_worlds
                       (world_uuid, account_id, server_uuid, game_port, map_file,
                        slot_index, is_primary, name, published, created_at)
                       VALUES (?, ?, ?, 42069, 'TestZoo.json', 0, 1, NULL, 1, ?)""",
                    (self.HOME_WORLD_UUID, host["account_id"], self.HOME_SERVER_UUID,
                     int(time.time())),
                )
                if version < 6:
                    self._migrate_reserved_home_world_ports(connection)
                connection.execute(f"PRAGMA user_version={self.SCHEMA_VERSION}")

    def get_host(self):
        with closing(self._connect()) as connection:
            row = connection.execute("SELECT * FROM accounts WHERE kind = 'host'").fetchone()
        if row is None:
            raise RuntimeError("Account database has no host account")
        return self._account(row)

    def rename_host(self, name):
        """Update only the host display name, preserving its identity and home world."""
        if not isinstance(name, str):
            raise ValueError("Host name must be text")
        name = name.strip()
        if not name or len(name) > 64 or not name.isprintable():
            raise ValueError("Host name is invalid")

        with closing(self._connect()) as connection:
            with connection:
                connection.execute("BEGIN IMMEDIATE")
                connection.execute(
                    "UPDATE accounts SET name_key = ?, name = ? WHERE kind = 'host'",
                    (name.casefold(), name),
                )
                row = connection.execute(
                    "SELECT * FROM accounts WHERE kind = 'host'"
                ).fetchone()
        if row is None:
            raise RuntimeError("Account database has no host account")
        return self._account(row)

    def get_or_create_remote(self, name, home_biome):
        name_key = name.casefold()
        with closing(self._connect()) as connection:
            with connection:
                connection.execute("BEGIN IMMEDIATE")
                row = connection.execute(
                    "SELECT * FROM accounts WHERE kind = 'remote' AND name_key = ?", (name_key,)
                ).fetchone()
                if row is None:
                    connection.execute(
                        """INSERT INTO accounts
                           (account_id, kind, name_key, name, character_uuid, home_biome, created_at)
                           VALUES (?, 'remote', ?, ?, ?, ?, ?)""",
                        (str(uuid.uuid4()), name_key, name, str(uuid.uuid4()), home_biome, int(time.time())),
                    )
                    row = connection.execute(
                        "SELECT * FROM accounts WHERE kind = 'remote' AND name_key = ?", (name_key,)
                    ).fetchone()
        return self._account(row)

    def get_by_character_uuid(self, character_uuid):
        if not character_uuid:
            return None
        try:
            parsed_uuid = str(uuid.UUID(str(character_uuid).strip()))
        except (TypeError, ValueError, AttributeError):
            parsed_uuid = str(character_uuid).strip().lower()
        uuid_no_hyphen = parsed_uuid.replace("-", "")
        with closing(self._connect()) as connection:
            row = connection.execute("""
                SELECT * FROM accounts 
                WHERE LOWER(character_uuid) = ? OR LOWER(REPLACE(character_uuid, '-', '')) = ?
            """, (parsed_uuid, uuid_no_hyphen)).fetchone()
        return self._account(row)

    def get_by_name(self, name):
        if not name or not isinstance(name, str):
            return None
        name_key = name.strip().casefold()
        with closing(self._connect()) as connection:
            row = connection.execute(
                "SELECT * FROM accounts WHERE name_key = ?", (name_key,)
            ).fetchone()
        return self._account(row)

    def list_all_accounts(self):
        with closing(self._connect()) as connection:
            rows = connection.execute(
                "SELECT * FROM accounts ORDER BY name_key"
            ).fetchall()
        return self._social_accounts(rows)

    def get_or_create_home_world(self, account):
        with closing(self._connect()) as connection:
            with connection:
                connection.execute("BEGIN IMMEDIATE")
                row = connection.execute(
                    "SELECT * FROM home_worlds WHERE account_id = ? AND is_primary = 1",
                    (account["accountId"],)
                ).fetchone()
                if row is None:
                    if account["kind"] != "remote":
                        raise RuntimeError("Host home world is missing")
                    used_ports = {item[0] for item in connection.execute(
                        "SELECT game_port FROM home_worlds"
                    )}
                    port = self._next_home_world_port(connection)
                    if port is None:
                        raise RuntimeError("No home-island game ports remain")
                    character_uuid = account["characterUuid"]
                    connection.execute(
                        """INSERT INTO home_worlds
                           (world_uuid, account_id, server_uuid, game_port, map_file,
                            slot_index, is_primary, name, published, created_at)
                           VALUES (?, ?, ?, ?, ?, 0, 1, NULL, 1, ?)""",
                        (str(uuid.uuid5(self.WORLD_NAMESPACE, "world:" + character_uuid)),
                         account["accountId"],
                         str(uuid.uuid5(self.WORLD_NAMESPACE, "server:" + character_uuid)),
                         port, f"home_{character_uuid}.json", int(time.time())),
                    )
                    row = connection.execute(
                        "SELECT * FROM home_worlds WHERE account_id = ? AND is_primary = 1",
                        (account["accountId"],)
                    ).fetchone()
        return self._world(row, account)

    @classmethod
    def _next_home_world_port(cls, connection):
        used_ports = {item[0] for item in connection.execute("SELECT game_port FROM home_worlds")}
        return next((candidate for candidate in range(cls.HOME_WORLD_PORT_MIN, cls.HOME_WORLD_PORT_MAX + 1)
                     if candidate not in used_ports), None)

    def move_home_world_to_next_port(self, world_uuid):
        """Atomically reserve a different home-world port after an OS-level collision."""
        with closing(self._connect()) as connection:
            with connection:
                connection.execute("BEGIN IMMEDIATE")
                row = connection.execute(
                    "SELECT game_port FROM home_worlds WHERE world_uuid = ?",
                    (str(world_uuid),),
                ).fetchone()
                if row is None:
                    raise RuntimeError(f"Home world {world_uuid} is not registered")

                port = self._next_home_world_port(connection)
                if port is None:
                    raise RuntimeError("No safe home-island game ports remain")
                connection.execute(
                    "UPDATE home_worlds SET game_port = ? WHERE world_uuid = ?",
                    (port, str(world_uuid)),
                )
        print(f"[home-island] Reassigned {world_uuid} from UDP {row['game_port']} to UDP {port}")
        return port

    @classmethod
    def _migrate_reserved_home_world_ports(cls, connection):
        """Move persisted non-host islands away from the fixed world listeners."""
        rows = connection.execute(
            """SELECT world_uuid, game_port FROM home_worlds
               WHERE game_port BETWEEN 42070 AND 42074
               ORDER BY created_at, world_uuid"""
        ).fetchall()
        for row in rows:
            replacement = cls._next_home_world_port(connection)
            if replacement is None:
                raise RuntimeError("No safe home-island game ports remain")
            connection.execute(
                "UPDATE home_worlds SET game_port = ? WHERE world_uuid = ?",
                (replacement, row["world_uuid"]),
            )
            print(f"[home-island] Moved {row['world_uuid']} from UDP {row['game_port']} to UDP {replacement}")

    def create_extra_home_world(self, account, name=None):
        """Create an additional island without changing the account's primary island."""
        with closing(self._connect()) as connection:
            with connection:
                connection.execute("BEGIN IMMEDIATE")
                rows = connection.execute(
                    "SELECT * FROM home_worlds WHERE account_id = ? ORDER BY slot_index",
                    (account["accountId"],),
                ).fetchall()
                if len(rows) >= self.MAX_HOME_WORLDS:
                    raise ValueError(f"An account can have at most {self.MAX_HOME_WORLDS} islands")

                used_slots = {row["slot_index"] for row in rows}
                slot = next(index for index in range(self.MAX_HOME_WORLDS) if index not in used_slots)
                if slot == 0:
                    raise RuntimeError("Primary home island is missing")

                port = self._next_home_world_port(connection)
                if port is None:
                    raise RuntimeError("No home-island game ports remain")

                character_uuid = account["characterUuid"]
                world_uuid = str(uuid.uuid5(
                    self.WORLD_NAMESPACE, f"world:{character_uuid}:slot:{slot}"
                ))
                server_uuid = str(uuid.uuid5(
                    self.WORLD_NAMESPACE, f"server:{character_uuid}:slot:{slot}"
                ))
                world_name = str(name).strip() if isinstance(name, str) else ""
                if not world_name:
                    world_name = f"{account['name']}'s Island {slot + 1}"
                if len(world_name) > 64 or not world_name.isprintable():
                    raise ValueError("Island name is invalid")

                connection.execute(
                    """INSERT INTO home_worlds
                       (world_uuid, account_id, server_uuid, game_port, map_file,
                        slot_index, is_primary, name, published, created_at)
                       VALUES (?, ?, ?, ?, ?, ?, 0, ?, 1, ?)""",
                    (world_uuid, account["accountId"], server_uuid, port,
                     f"home_{character_uuid}_island_{slot}.json", slot, world_name,
                     int(time.time())),
                )
                row = connection.execute(
                    "SELECT * FROM home_worlds WHERE world_uuid = ?", (world_uuid,)
                ).fetchone()
        return self._world(row, account)

    def list_home_worlds_for_account(self, account):
        with closing(self._connect()) as connection:
            rows = connection.execute(
                "SELECT * FROM home_worlds WHERE account_id = ? ORDER BY slot_index",
                (account["accountId"],),
            ).fetchall()
        return [self._world(row, account) for row in rows]

    @staticmethod
    def _world(row, account):
        return {
            "uuid": row["world_uuid"],
            "serverUuid": row["server_uuid"],
            "port": row["game_port"],
            "mapFile": row["map_file"],
            "owner": account["characterUuid"],
            "ownerName": account["name"],
            "ownerBiome": account.get("homeBiome", "Desert"),
            "kind": account["kind"],
            "slot": row["slot_index"],
            "primary": bool(row["is_primary"]),
            "name": row["name"],
            "published": bool(row["published"]),
            "biomeIndex": row["biome_index"],
            "regionIndex": row["region_index"],
            "adventureIndex": row["adventure_index"],
            "paletteIndex": row["palette_index"],
            "terrainBlock": row["terrain_block"],
            "created_at": row["created_at"],
        }

    def get_home_world_by_uuid(self, world_uuid):
        try:
            world_uuid = str(uuid.UUID(world_uuid))
        except (TypeError, ValueError, AttributeError):
            return None
        with closing(self._connect()) as connection:
            row = connection.execute(
                """SELECT home_worlds.*, accounts.kind,
                          accounts.name AS account_name,
                          accounts.home_biome AS account_home_biome,
                          accounts.character_uuid
                   FROM home_worlds JOIN accounts USING (account_id)
                   WHERE home_worlds.world_uuid = ?""", (world_uuid,)
            ).fetchone()
        if row is None:
            return None
        return self._world(row, {"kind": row["kind"], "name": row["account_name"],
                                 "homeBiome": row["account_home_biome"],
                                 "characterUuid": row["character_uuid"]})

    def list_home_worlds(self):
        with closing(self._connect()) as connection:
            rows = connection.execute(
                """SELECT home_worlds.*, accounts.kind,
                          accounts.name AS account_name,
                          accounts.home_biome AS account_home_biome,
                          accounts.character_uuid
                   FROM home_worlds JOIN accounts USING (account_id)
                   ORDER BY accounts.name_key, home_worlds.slot_index"""
            ).fetchall()
        return [self._world(row, {"kind": row["kind"], "name": row["account_name"],
                                  "homeBiome": row["account_home_biome"],
                                  "characterUuid": row["character_uuid"]}) for row in rows]

    @staticmethod
    def _friend_pair(first_account_id, second_account_id):
        if first_account_id == second_account_id:
            raise ValueError("An account cannot have a relationship with itself")
        return tuple(sorted((first_account_id, second_account_id)))

    @staticmethod
    def _social_accounts(rows):
        return [AccountStore._account(row) for row in rows]

    def relationship(self, account, other):
        """Return the relationship flags consumed by relationship/info."""
        if not account or not other or account["accountId"] == other["accountId"]:
            return {"friend": False, "following": False, "followedBy": False,
                    "incoming": False, "outgoing": False}
        first, second = self._friend_pair(account["accountId"], other["accountId"])
        with closing(self._connect()) as connection:
            friend = connection.execute(
                "SELECT 1 FROM friendships WHERE first_account_id = ? AND second_account_id = ?",
                (first, second),
            ).fetchone() is not None
            following = connection.execute(
                "SELECT 1 FROM follows WHERE follower_account_id = ? AND followed_account_id = ?",
                (account["accountId"], other["accountId"]),
            ).fetchone() is not None
            followed_by = connection.execute(
                "SELECT 1 FROM follows WHERE follower_account_id = ? AND followed_account_id = ?",
                (other["accountId"], account["accountId"]),
            ).fetchone() is not None
            incoming = connection.execute(
                "SELECT 1 FROM friend_requests WHERE requester_account_id = ? AND recipient_account_id = ?",
                (other["accountId"], account["accountId"]),
            ).fetchone() is not None
            outgoing = connection.execute(
                "SELECT 1 FROM friend_requests WHERE requester_account_id = ? AND recipient_account_id = ?",
                (account["accountId"], other["accountId"]),
            ).fetchone() is not None
        return {"friend": friend, "following": following, "followedBy": followed_by,
                "incoming": incoming, "outgoing": outgoing}

    def relationship_counts(self, account):
        if not account:
            return {"follows": 0, "followedBy": 0, "friends": 0}
        account_id = account["accountId"]
        with closing(self._connect()) as connection:
            follows = connection.execute(
                "SELECT COUNT(*) FROM follows WHERE follower_account_id = ?", (account_id,)
            ).fetchone()[0]
            followed_by = connection.execute(
                "SELECT COUNT(*) FROM follows WHERE followed_account_id = ?", (account_id,)
            ).fetchone()[0]
            friends = connection.execute(
                "SELECT COUNT(*) FROM friendships WHERE first_account_id = ? OR second_account_id = ?",
                (account_id, account_id),
            ).fetchone()[0]
        return {"follows": follows, "followedBy": followed_by, "friends": friends}

    def create_friend_request(self, requester, recipient):
        if requester["accountId"] == recipient["accountId"]:
            raise ValueError("Cannot add yourself as a friend")
        first, second = self._friend_pair(requester["accountId"], recipient["accountId"])
        with closing(self._connect()) as connection:
            with connection:
                connection.execute("BEGIN IMMEDIATE")
                if connection.execute(
                    "SELECT 1 FROM friendships WHERE first_account_id = ? AND second_account_id = ?",
                    (first, second),
                ).fetchone() is None:
                    connection.execute(
                        "INSERT OR IGNORE INTO friend_requests "
                        "(requester_account_id, recipient_account_id, created_at) VALUES (?, ?, ?)",
                        (requester["accountId"], recipient["accountId"], int(time.time())),
                    )

    def cancel_friend_request(self, requester, recipient):
        with closing(self._connect()) as connection:
            with connection:
                connection.execute(
                    """DELETE FROM friend_requests
                       WHERE requester_account_id = ? AND recipient_account_id = ?""",
                    (requester["accountId"], recipient["accountId"]),
                )

    def accept_friend_request(self, recipient, requester):
        first, second = self._friend_pair(recipient["accountId"], requester["accountId"])
        with closing(self._connect()) as connection:
            with connection:
                connection.execute("BEGIN IMMEDIATE")
                pending = connection.execute(
                    """SELECT 1 FROM friend_requests
                       WHERE requester_account_id = ? AND recipient_account_id = ?""",
                    (requester["accountId"], recipient["accountId"]),
                ).fetchone()
                if pending is None:
                    return False
                connection.execute(
                    "INSERT OR IGNORE INTO friendships (first_account_id, second_account_id, created_at) VALUES (?, ?, ?)",
                    (first, second, int(time.time())),
                )
                connection.execute(
                    """DELETE FROM friend_requests
                       WHERE (requester_account_id = ? AND recipient_account_id = ?)
                          OR (requester_account_id = ? AND recipient_account_id = ?)""",
                    (requester["accountId"], recipient["accountId"],
                     recipient["accountId"], requester["accountId"]),
                )
        return True

    def reject_friend_request(self, recipient, requester):
        with closing(self._connect()) as connection:
            with connection:
                connection.execute(
                    """DELETE FROM friend_requests
                       WHERE requester_account_id = ? AND recipient_account_id = ?""",
                    (requester["accountId"], recipient["accountId"]),
                )

    def remove_friend(self, account, other):
        first, second = self._friend_pair(account["accountId"], other["accountId"])
        with closing(self._connect()) as connection:
            with connection:
                connection.execute(
                    "DELETE FROM friendships WHERE first_account_id = ? AND second_account_id = ?", (first, second)
                )

    def follow(self, follower, followed):
        if follower["accountId"] == followed["accountId"]:
            raise ValueError("Cannot follow yourself")
        with closing(self._connect()) as connection:
            with connection:
                connection.execute(
                    "INSERT OR IGNORE INTO follows (follower_account_id, followed_account_id, created_at) VALUES (?, ?, ?)",
                    (follower["accountId"], followed["accountId"], int(time.time())),
                )

    def unfollow(self, follower, followed):
        with closing(self._connect()) as connection:
            with connection:
                connection.execute(
                    "DELETE FROM follows WHERE follower_account_id = ? AND followed_account_id = ?",
                    (follower["accountId"], followed["accountId"]),
                )

    def list_friends(self, account):
        account_id = account["accountId"]
        with closing(self._connect()) as connection:
            rows = connection.execute(
                """SELECT accounts.* FROM friendships
                   JOIN accounts ON accounts.account_id = CASE
                       WHEN friendships.first_account_id = ? THEN friendships.second_account_id
                       ELSE friendships.first_account_id END
                   WHERE friendships.first_account_id = ? OR friendships.second_account_id = ?
                   ORDER BY accounts.name_key""",
                (account_id, account_id, account_id),
            ).fetchall()
        return self._social_accounts(rows)

    def list_follows(self, account):
        with closing(self._connect()) as connection:
            rows = connection.execute(
                """SELECT accounts.* FROM follows JOIN accounts ON accounts.account_id = follows.followed_account_id
                   WHERE follows.follower_account_id = ? ORDER BY accounts.name_key""",
                (account["accountId"],),
            ).fetchall()
        return self._social_accounts(rows)

    def list_followed_by(self, account):
        with closing(self._connect()) as connection:
            rows = connection.execute(
                """SELECT accounts.* FROM follows JOIN accounts ON accounts.account_id = follows.follower_account_id
                   WHERE follows.followed_account_id = ? ORDER BY accounts.name_key""",
                (account["accountId"],),
            ).fetchall()
        return self._social_accounts(rows)

    def list_friend_requests(self, account, incoming):
        column = "recipient_account_id" if incoming else "requester_account_id"
        other = "requester_account_id" if incoming else "recipient_account_id"
        with closing(self._connect()) as connection:
            rows = connection.execute(
                f"""SELECT accounts.* FROM friend_requests
                    JOIN accounts ON accounts.account_id = friend_requests.{other}
                    WHERE friend_requests.{column} = ? ORDER BY accounts.name_key""",
                (account["accountId"],),
            ).fetchall()
        return self._social_accounts(rows)
