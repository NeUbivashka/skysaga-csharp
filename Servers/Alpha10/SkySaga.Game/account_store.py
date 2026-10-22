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
    SCHEMA_VERSION = 2
    HOME_WORLD_UUID = "6f0d3a1e-5c47-4f92-9a6b-1d84c0e73b25"
    HOME_SERVER_UUID = "b3e94c07-2a15-4d68-8f31-7c05a9be4610"
    WORLD_NAMESPACE = uuid.UUID("eef872a9-4785-42c4-83b4-4e1ce956998f")

    def __init__(self, path: Path):
        self.path = path

    def _connect(self):
        connection = sqlite3.connect(self.path, timeout=10)
        connection.row_factory = sqlite3.Row
        connection.execute("PRAGMA busy_timeout=10000")
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
            with connection:
                connection.execute("BEGIN IMMEDIATE")
                version = connection.execute("PRAGMA user_version").fetchone()[0]
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
                connection.execute(
                    """CREATE TABLE IF NOT EXISTS home_worlds (
                       account_id TEXT PRIMARY KEY REFERENCES accounts(account_id),
                       world_uuid TEXT NOT NULL UNIQUE,
                       server_uuid TEXT NOT NULL UNIQUE,
                       game_port INTEGER NOT NULL UNIQUE,
                       map_file TEXT NOT NULL UNIQUE
                    )"""
                )
                host = connection.execute("SELECT account_id FROM accounts WHERE kind = 'host'").fetchone()
                connection.execute(
                    """INSERT OR IGNORE INTO home_worlds
                       (account_id, world_uuid, server_uuid, game_port, map_file)
                       VALUES (?, ?, ?, 42069, 'TestZoo.json')""",
                    (host["account_id"], self.HOME_WORLD_UUID, self.HOME_SERVER_UUID),
                )
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
        try:
            character_uuid = str(uuid.UUID(character_uuid))
        except (TypeError, ValueError, AttributeError):
            return None
        with closing(self._connect()) as connection:
            row = connection.execute(
                "SELECT * FROM accounts WHERE character_uuid = ?", (character_uuid,)
            ).fetchone()
        return self._account(row)

    def get_or_create_home_world(self, account):
        with closing(self._connect()) as connection:
            with connection:
                connection.execute("BEGIN IMMEDIATE")
                row = connection.execute(
                    "SELECT * FROM home_worlds WHERE account_id = ?", (account["accountId"],)
                ).fetchone()
                if row is None:
                    if account["kind"] != "remote":
                        raise RuntimeError("Host home world is missing")
                    used_ports = {item[0] for item in connection.execute(
                        "SELECT game_port FROM home_worlds"
                    )}
                    port = next((candidate for candidate in range(42071, 42171)
                                 if candidate not in used_ports), None)
                    if port is None:
                        raise RuntimeError("No home-island game ports remain")
                    character_uuid = account["characterUuid"]
                    connection.execute(
                        """INSERT INTO home_worlds
                           (account_id, world_uuid, server_uuid, game_port, map_file)
                           VALUES (?, ?, ?, ?, ?)""",
                        (account["accountId"],
                         str(uuid.uuid5(self.WORLD_NAMESPACE, "world:" + character_uuid)),
                         str(uuid.uuid5(self.WORLD_NAMESPACE, "server:" + character_uuid)),
                         port, f"home_{character_uuid}.json"),
                    )
                    row = connection.execute(
                        "SELECT * FROM home_worlds WHERE account_id = ?", (account["accountId"],)
                    ).fetchone()
        return self._world(row, account)

    @staticmethod
    def _world(row, account):
        return {
            "uuid": row["world_uuid"],
            "serverUuid": row["server_uuid"],
            "port": row["game_port"],
            "mapFile": row["map_file"],
            "owner": account["characterUuid"],
            "ownerName": account["name"],
            "kind": account["kind"],
        }

    def get_home_world_by_uuid(self, world_uuid):
        try:
            world_uuid = str(uuid.UUID(world_uuid))
        except (TypeError, ValueError, AttributeError):
            return None
        with closing(self._connect()) as connection:
            row = connection.execute(
                """SELECT home_worlds.*, accounts.kind, accounts.name, accounts.character_uuid
                   FROM home_worlds JOIN accounts USING (account_id)
                   WHERE home_worlds.world_uuid = ?""", (world_uuid,)
            ).fetchone()
        if row is None:
            return None
        return self._world(row, {"kind": row["kind"], "name": row["name"],
                                 "characterUuid": row["character_uuid"]})

    def list_home_worlds(self):
        with closing(self._connect()) as connection:
            rows = connection.execute(
                """SELECT home_worlds.*, accounts.kind, accounts.name, accounts.character_uuid
                   FROM home_worlds JOIN accounts USING (account_id)
                   ORDER BY accounts.name_key, home_worlds.world_uuid"""
            ).fetchall()
        return [self._world(row, {"kind": row["kind"], "name": row["name"],
                                  "characterUuid": row["character_uuid"]}) for row in rows]
