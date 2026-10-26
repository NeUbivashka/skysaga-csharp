"""
Persistent storage for player photos, likes, and avatar associations in SkySaga Alpha 10.
"""

import datetime
import hashlib
import sqlite3
import threading
from pathlib import Path


class PhotoStore:
    def __init__(self, db_path: Path, photos_dir: Path):
        self.db_path = Path(db_path)
        self.photos_dir = Path(photos_dir)
        self._lock = threading.Lock()
        self.init_db()

    def _connect(self):
        conn = sqlite3.connect(str(self.db_path), check_same_thread=False)
        conn.row_factory = sqlite3.Row
        return conn

    def init_db(self):
        self.photos_dir.mkdir(parents=True, exist_ok=True)
        self.db_path.parent.mkdir(parents=True, exist_ok=True)
        with self._lock, self._connect() as conn:
            conn.execute("""
                CREATE TABLE IF NOT EXISTS photos (
                    uuid TEXT PRIMARY KEY,
                    owner_uuid TEXT NOT NULL,
                    owner_name TEXT NOT NULL,
                    world_uuid TEXT NOT NULL DEFAULT '',
                    biome TEXT NOT NULL DEFAULT 'Forest',
                    adventure TEXT NOT NULL DEFAULT 'home',
                    palette_name TEXT NOT NULL DEFAULT 'Forest',
                    caption TEXT NOT NULL DEFAULT '',
                    is_avatar INTEGER NOT NULL DEFAULT 0,
                    visible_public INTEGER NOT NULL DEFAULT 1,
                    visible_friends INTEGER NOT NULL DEFAULT 1,
                    likes INTEGER NOT NULL DEFAULT 0,
                    hash TEXT NOT NULL DEFAULT '',
                    file_size INTEGER NOT NULL DEFAULT 0,
                    file_path TEXT NOT NULL DEFAULT '',
                    created_at TEXT NOT NULL
                )
            """)
            conn.execute("""
                CREATE TABLE IF NOT EXISTS photo_likes (
                    photo_uuid TEXT NOT NULL,
                    character_uuid TEXT NOT NULL,
                    PRIMARY KEY(photo_uuid, character_uuid)
                )
            """)
            conn.commit()

    def save_photo_file(self, photo_uuid: str, image_bytes: bytes, owner_uuid: str, owner_name: str,
                        world_uuid: str = "", biome: str = "Forest", adventure: str = "home",
                        palette_name: str = "Forest", caption: str = "", is_avatar: int = 1) -> dict:
        file_path = self.photos_dir / f"{photo_uuid}.jpg"
        file_path.write_bytes(image_bytes)
        md5_hash = hashlib.md5(image_bytes).hexdigest()
        now = datetime.datetime.utcnow().isoformat() + "Z"

        record = {
            "uuid": photo_uuid,
            "owner_uuid": owner_uuid,
            "owner_name": owner_name,
            "world_uuid": world_uuid or "",
            "biome": biome or "Forest",
            "adventure": adventure or "home",
            "palette_name": palette_name or "Forest",
            "caption": caption or "",
            "is_avatar": is_avatar,
            "visible_public": 1,
            "visible_friends": 1,
            "likes": 0,
            "hash": md5_hash,
            "file_size": len(image_bytes),
            "file_path": str(file_path),
            "created_at": now
        }

        with self._lock, self._connect() as conn:
            if is_avatar and owner_uuid:
                owner_clean = str(owner_uuid).strip().lower()
                owner_no_hyphen = owner_clean.replace("-", "")
                conn.execute("""
                    UPDATE photos SET is_avatar = 0
                    WHERE LOWER(owner_uuid) = ? OR LOWER(REPLACE(owner_uuid, '-', '')) = ?
                """, (owner_clean, owner_no_hyphen))

            conn.execute("""
                INSERT OR REPLACE INTO photos (
                    uuid, owner_uuid, owner_name, world_uuid, biome, adventure,
                    palette_name, caption, is_avatar, visible_public, visible_friends,
                    likes, hash, file_size, file_path, created_at
                ) VALUES (
                    :uuid, :owner_uuid, :owner_name, :world_uuid, :biome, :adventure,
                    :palette_name, :caption, :is_avatar, :visible_public, :visible_friends,
                    :likes, :hash, :file_size, :file_path, :created_at
                )
            """, record)
            conn.commit()

        return record

    def get_photo(self, photo_uuid: str):
        photo_clean = str(photo_uuid).strip().lower()
        photo_no_hyphen = photo_clean.replace("-", "")
        with self._lock, self._connect() as conn:
            row = conn.execute("""
                SELECT * FROM photos
                WHERE LOWER(uuid) = ? OR LOWER(REPLACE(uuid, '-', '')) = ?
            """, (photo_clean, photo_no_hyphen)).fetchone()
            return dict(row) if row else None

    def update_access(self, photo_uuid: str, visible_public: bool, visible_friends: bool):
        photo_clean = str(photo_uuid).strip().lower()
        photo_no_hyphen = photo_clean.replace("-", "")
        with self._lock, self._connect() as conn:
            conn.execute("""
                UPDATE photos SET visible_public = ?, visible_friends = ?
                WHERE LOWER(uuid) = ? OR LOWER(REPLACE(uuid, '-', '')) = ?
            """, (1 if visible_public else 0, 1 if visible_friends else 0, photo_clean, photo_no_hyphen))
            conn.commit()

    def set_avatar(self, owner_uuid: str, photo_uuid: str):
        owner_clean = str(owner_uuid).strip().lower()
        owner_no_hyphen = owner_clean.replace("-", "")
        photo_clean = str(photo_uuid).strip().lower()
        photo_no_hyphen = photo_clean.replace("-", "")
        with self._lock, self._connect() as conn:
            conn.execute("""
                UPDATE photos SET is_avatar = 0
                WHERE LOWER(owner_uuid) = ? OR LOWER(REPLACE(owner_uuid, '-', '')) = ?
            """, (owner_clean, owner_no_hyphen))
            conn.execute("""
                UPDATE photos SET is_avatar = 1
                WHERE LOWER(uuid) = ? OR LOWER(REPLACE(uuid, '-', '')) = ?
            """, (photo_clean, photo_no_hyphen))
            conn.commit()

    def get_avatar(self, owner_uuid: str):
        if not owner_uuid:
            return None
        owner_clean = str(owner_uuid).strip().lower()
        owner_no_hyphen = owner_clean.replace("-", "")
        with self._lock, self._connect() as conn:
            row = conn.execute("""
                SELECT * FROM photos
                WHERE (LOWER(owner_uuid) = ? OR LOWER(REPLACE(owner_uuid, '-', '')) = ?)
                  AND is_avatar = 1
                ORDER BY created_at DESC LIMIT 1
            """, (owner_clean, owner_no_hyphen)).fetchone()
            if not row:
                # Fallback to the latest photo by this owner if no photo is explicitly marked avatar
                row = conn.execute("""
                    SELECT * FROM photos
                    WHERE LOWER(owner_uuid) = ? OR LOWER(REPLACE(owner_uuid, '-', '')) = ?
                    ORDER BY created_at DESC LIMIT 1
                """, (owner_clean, owner_no_hyphen)).fetchone()
                if row:
                    conn.execute("""
                        UPDATE photos SET is_avatar = 1 WHERE uuid = ?
                    """, (row["uuid"],))
                    conn.commit()
            return dict(row) if row else None

    def list_public_photos(self, sort: str = "hot"):
        with self._lock, self._connect() as conn:
            if sort == "recent":
                rows = conn.execute("SELECT * FROM photos WHERE visible_public = 1 ORDER BY created_at DESC").fetchall()
            else:
                rows = conn.execute("SELECT * FROM photos WHERE visible_public = 1 ORDER BY likes DESC, created_at DESC").fetchall()
            return [dict(r) for r in rows]

    def list_photos_by_character_and_world(self, character_uuid: str = None, world_uuid: str = None):
        with self._lock, self._connect() as conn:
            query = "SELECT * FROM photos WHERE 1=1"
            params = []
            if character_uuid:
                query += " AND owner_uuid = ?"
                params.append(character_uuid)
            if world_uuid:
                query += " AND world_uuid = ?"
                params.append(world_uuid)
            query += " ORDER BY created_at DESC"
            rows = conn.execute(query, params).fetchall()
            return [dict(r) for r in rows]

    def like_photo(self, photo_uuid: str, character_uuid: str):
        with self._lock, self._connect() as conn:
            try:
                conn.execute("INSERT INTO photo_likes (photo_uuid, character_uuid) VALUES (?, ?)",
                             (photo_uuid, character_uuid))
                conn.execute("UPDATE photos SET likes = likes + 1 WHERE uuid = ?", (photo_uuid,))
                conn.commit()
                return True
            except sqlite3.IntegrityError:
                return False

    def unlike_photo(self, photo_uuid: str, character_uuid: str):
        with self._lock, self._connect() as conn:
            cur = conn.execute("DELETE FROM photo_likes WHERE photo_uuid = ? AND character_uuid = ?",
                               (photo_uuid, character_uuid))
            if cur.rowcount > 0:
                conn.execute("UPDATE photos SET likes = MAX(0, likes - 1) WHERE uuid = ?", (photo_uuid,))
                conn.commit()
                return True
            return False

    def is_liked(self, photo_uuid: str, character_uuid: str) -> bool:
        with self._lock, self._connect() as conn:
            row = conn.execute("SELECT 1 FROM photo_likes WHERE photo_uuid = ? AND character_uuid = ?",
                               (photo_uuid, character_uuid)).fetchone()
            return bool(row)

    def multi_is_liked(self, photo_uuids: list, character_uuid: str) -> dict:
        if not photo_uuids or not character_uuid:
            return {}
        with self._lock, self._connect() as conn:
            placeholders = ",".join("?" for _ in photo_uuids)
            rows = conn.execute(f"""
                SELECT photo_uuid FROM photo_likes WHERE character_uuid = ? AND photo_uuid IN ({placeholders})
            """, [character_uuid] + list(photo_uuids)).fetchall()
            liked_set = {r["photo_uuid"] for r in rows}
            return {uuid_: (uuid_ in liked_set) for uuid_ in photo_uuids}

    def delete_photo(self, photo_uuid: str, owner_uuid: str = None):
        photo_uuid = str(photo_uuid).strip().lower()
        uuid_no_hyphen = photo_uuid.replace("-", "")
        with self._lock, self._connect() as conn:
            # Check owner before deleting
            row = conn.execute("""
                SELECT owner_uuid, is_avatar FROM photos
                WHERE LOWER(uuid) = ? OR LOWER(REPLACE(uuid, '-', '')) = ?
            """, (photo_uuid, uuid_no_hyphen)).fetchone()
            detected_owner = row["owner_uuid"] if row else owner_uuid

            if owner_uuid:
                owner_clean = str(owner_uuid).strip().lower()
                owner_no_hyphen = owner_clean.replace("-", "")
                res = conn.execute("""
                    DELETE FROM photos
                    WHERE (LOWER(uuid) = ? OR LOWER(REPLACE(uuid, '-', '')) = ?)
                      AND (LOWER(owner_uuid) = ? OR LOWER(REPLACE(owner_uuid, '-', '')) = ?)
                """, (photo_uuid, uuid_no_hyphen, owner_clean, owner_no_hyphen))
                if res.rowcount == 0:
                    conn.execute("""
                        DELETE FROM photos
                        WHERE LOWER(uuid) = ? OR LOWER(REPLACE(uuid, '-', '')) = ?
                    """, (photo_uuid, uuid_no_hyphen))
            else:
                conn.execute("""
                    DELETE FROM photos
                    WHERE LOWER(uuid) = ? OR LOWER(REPLACE(uuid, '-', '')) = ?
                """, (photo_uuid, uuid_no_hyphen))
            conn.execute("""
                DELETE FROM photo_likes
                WHERE LOWER(photo_uuid) = ? OR LOWER(REPLACE(photo_uuid, '-', '')) = ?
            """, (photo_uuid, uuid_no_hyphen))

            # Promote latest remaining photo to avatar if no avatar exists for owner
            if detected_owner:
                owner_c = str(detected_owner).strip().lower()
                owner_nh = owner_c.replace("-", "")
                has_avatar = conn.execute("""
                    SELECT 1 FROM photos
                    WHERE (LOWER(owner_uuid) = ? OR LOWER(REPLACE(owner_uuid, '-', '')) = ?)
                      AND is_avatar = 1
                """, (owner_c, owner_nh)).fetchone()
                if not has_avatar:
                    latest = conn.execute("""
                        SELECT uuid FROM photos
                        WHERE LOWER(owner_uuid) = ? OR LOWER(REPLACE(owner_uuid, '-', '')) = ?
                        ORDER BY created_at DESC LIMIT 1
                    """, (owner_c, owner_nh)).fetchone()
                    if latest:
                        conn.execute("UPDATE photos SET is_avatar = 1 WHERE uuid = ?", (latest["uuid"],))

            conn.commit()
        for f in [self.photos_dir / f"{photo_uuid}.jpg", self.photos_dir / f"{uuid_no_hyphen}.jpg"]:
            if f.exists():
                try:
                    f.unlink()
                except OSError:
                    pass
