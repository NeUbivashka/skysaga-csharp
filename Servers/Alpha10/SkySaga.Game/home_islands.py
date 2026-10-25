"""Start an isolated Alpha V10 game-server process for each remote home island."""

import atexit
import os
import shutil
import socket
import subprocess
import threading
import time
from pathlib import Path


class HomeIslandManager:
    def __init__(self, project_dir: Path, account_store=None):
        self.bin_dir = project_dir / "bin" / "Debug" / "net10.0"
        self.account_store = account_store
        self._lock = threading.Lock()
        self._processes = {}
        self._pending = set()
        atexit.register(self.stop)

    def start_in_background(self, world):
        if world["kind"] == "host" and world.get("primary", False):
            return
        world_uuid = world["uuid"]
        with self._lock:
            if world_uuid in self._pending:
                return
            self._pending.add(world_uuid)
        threading.Thread(target=self._start, args=(world,), daemon=True).start()

    def _start(self, world):
        try:
            self.ensure_running(world)
        except Exception as exc:
            print(f"[home-island] Could not start {world['uuid']}: {exc}", flush=True)
        finally:
            with self._lock:
                self._pending.discard(world["uuid"])

    def ensure_running(self, world):
        if world["kind"] == "host" and world.get("primary", False):
            return world["port"]

        with self._lock:
            world_uuid = world["uuid"]
            process = self._processes.get(world_uuid)
            if process is not None and process.poll() is None:
                return world["port"]
            self._processes.pop(world_uuid, None)

            executable = self.bin_dir / "SkySaga.Game.exe"
            template = self.bin_dir / "Data" / "TestZoo.json"
            island_map = self.bin_dir / "Data" / world["mapFile"]
            if not executable.is_file() or not template.is_file():
                raise RuntimeError(f"Missing built game server or island template in {self.bin_dir}")

            if not island_map.exists():
                temporary_map = island_map.with_suffix(".json.tmp")
                shutil.copyfile(template, temporary_map)
                os.replace(temporary_map, island_map)

            # Keep saved world ports unique in SQLite, and also account for
            # unrelated local processes that may already own a reserved port.
            # The replacement is persisted before its endpoint is returned to
            # the caller, so the transfer packet and listener use one port.
            port_attempts = 0
            while True:
                with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as probe:
                    try:
                        probe.bind(("0.0.0.0", world["port"]))
                        break
                    except OSError as exc:
                        if self.account_store is None or port_attempts >= 199:
                            raise RuntimeError(
                                f"UDP port {world['port']} is already occupied and no replacement was found"
                            ) from exc
                        previous_port = world["port"]
                        world["port"] = self.account_store.move_home_world_to_next_port(world_uuid)
                        port_attempts += 1
                        print(f"[home-island] UDP {previous_port} is busy; retrying world {world_uuid} "
                              f"on UDP {world['port']}", flush=True)

            environment = os.environ.copy()
            environment.update({
                "SKYSAGA_WORLD": "home",
                "SKYSAGA_MAP": world["mapFile"],
                "SKYSAGA_ADVENTURE": "home",
                "SKYSAGA_GAME_PORT": str(world["port"]),
                "SKYSAGA_SOCIAL_HUB_PORT": os.environ.get("SKYSAGA_SOCIAL_HUB_PORT", "42070"),
                "SKYSAGA_FTUE_PORT": os.environ.get("SKYSAGA_FTUE_PORT", "42071"),
                "SKYSAGA_MASTERY_FOREST_PORT": os.environ.get("SKYSAGA_MASTERY_FOREST_PORT", "42072"),
                "SKYSAGA_MASTERY_DESERT_PORT": os.environ.get("SKYSAGA_MASTERY_DESERT_PORT", "42073"),
                "SKYSAGA_MASTERY_JUNGLE_PORT": os.environ.get("SKYSAGA_MASTERY_JUNGLE_PORT", "42074"),
                "SKYSAGA_HOME_WORLD_UUID": world_uuid,
                "SKYSAGA_HOME_SERVER_UUID": world["serverUuid"],
                "SKYSAGA_HOME_OWNER_UUID": world["owner"],
                "SKYSAGA_HOME_OWNER_NAME": world["ownerName"],
                "SKYSAGA_INSTANCE_LOG": f"server-home-{world_uuid}.log",
                "SKYSAGA_COMPLETE_CHALLENGES": "1",
                "SKYSAGA_DATA2_UNIFORM": "fd",
            })
            for variable, field in (
                ("SKYSAGA_BIOME_INDEX", "biomeIndex"),
                ("SKYSAGA_REGION_INDEX", "regionIndex"),
                ("SKYSAGA_ADVENTURE_INDEX", "adventureIndex"),
                ("SKYSAGA_PALETTE", "paletteIndex"),
                ("SKYSAGA_TERRAIN_BLOCK", "terrainBlock"),
            ):
                value = world.get(field)
                if value is not None:
                    environment[variable] = str(value)

            error_log = self.bin_dir / f"server-home-{world_uuid}.stderr.log"
            log_path = self.bin_dir / environment["SKYSAGA_INSTANCE_LOG"]
            previous_log_time = log_path.stat().st_mtime_ns if log_path.exists() else None
            with error_log.open("ab") as errors:
                process = subprocess.Popen(
                    [str(executable)], cwd=self.bin_dir, env=environment,
                    stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                    stderr=errors, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0),
                )
            self._processes[world_uuid] = process

            deadline = time.monotonic() + 25
            while time.monotonic() < deadline:
                if process.poll() is not None:
                    self._processes.pop(world_uuid, None)
                    raise RuntimeError(f"Game server exited with code {process.returncode}; see {error_log}")
                if log_path.exists() and log_path.stat().st_mtime_ns != previous_log_time:
                    log = log_path.read_text(encoding="utf-8", errors="replace")
                    if "Server started successfully" in log:
                        print(f"[home-island] {world['ownerName']} ready on UDP {world['port']} "
                              f"(world {world_uuid})", flush=True)
                        return world["port"]
                    if "Failed to start server" in log:
                        break
                time.sleep(0.1)

            process.terminate()
            self._processes.pop(world_uuid, None)
            raise RuntimeError(f"Game server did not become ready; see {log_path}")

    def stop(self):
        with self._lock:
            processes = list(self._processes.values())
            self._processes.clear()
            for process in processes:
                if process.poll() is None:
                    process.terminate()
        for process in processes:
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)
