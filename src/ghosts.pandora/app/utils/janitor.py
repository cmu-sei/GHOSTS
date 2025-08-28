import os, time, threading, heapq
import logging
logger = logging.getLogger(__name__)

def _dir_size_bytes(root: str) -> int:
    total = 0
    for p, _, files in os.walk(root):
        for f in files:
            try:
                total += os.path.getsize(os.path.join(p, f))
            except FileNotFoundError:
                pass
    return total

def _list_files_with_atime(root: str):
    """Yield (atime, size, path) for every regular file under root."""
    for p, _, files in os.walk(root):
        for f in files:
            path = os.path.join(p, f)
            try:
                st = os.stat(path)
                yield (st.st_atime or st.st_mtime, st.st_size, path)
            except FileNotFoundError:
                continue

def prune_cache(root: str, max_bytes: int, min_age_seconds: int) -> dict:
    """
    Delete files under `root` that are:
      1) older than `min_age_seconds` (by atime/mtime), and then
      2) if size still > max_bytes, evict oldest-first until within budget.
    Returns a summary dict you can log.
    """
    now = time.time()
    deleted = 0
    freed = 0

    # First pass: age-based delete
    victims = []
    for atime, size, path in _list_files_with_atime(root):
        if now - atime > min_age_seconds:
            try:
                os.remove(path)
                deleted += 1
                freed += size
            except FileNotFoundError:
                pass
            except IsADirectoryError:
                pass

    # Second pass: LRU size trim
    cur = _dir_size_bytes(root)
    if cur > max_bytes:
        # Build min-heap by atime (oldest evicted first)
        heap = list(_list_files_with_atime(root))
        heapq.heapify(heap)
        while cur > max_bytes and heap:
            atime, size, path = heapq.heappop(heap)
            try:
                os.remove(path)
                deleted += 1
                cur -= size
                freed += size
            except FileNotFoundError:
                pass
            except IsADirectoryError:
                pass

    return {"root": root, "deleted": deleted, "freed_bytes": freed}

def _janitor_loop(
    roots,
    max_bytes_per_root: int = 8 * 1024 * 1024 * 1024,   # 8 GiB per cache dir
    min_age_seconds: int = 3600,                         # don’t touch files < 1h old
    interval_seconds: int = 900                          # run every 15 minutes
):
    logger.info(f"Starting cache janitor for: {roots}")
    for r in roots:
        try:
            summary = prune_cache(r, max_bytes_per_root, min_age_seconds)
            # Replace with your logger if available
            print(f"[janitor] {summary}")
        except Exception as e:
            print(f"[janitor] error pruning {r}: {e}")
    time.sleep(interval_seconds)

def start_cache_janitor():
    roots = []
    for env in ("TORCH_HOME","TORCHINDUCTOR_CACHE_DIR","TRITON_CACHE_DIR",
                "HF_HOME","TRANSFORMERS_CACHE","HF_DATASETS_CACHE","CUDA_CACHE_PATH"):
        logger.info(f"Checking environment variable: {env}")
        p = os.environ.get(env)
        if p:
            roots.append(p)
    # de-dup and keep only those that exist
    roots = sorted({r for r in roots if os.path.isdir(r)})
    if not roots:
        return
    t = threading.Thread(target=_janitor_loop, args=(roots,), daemon=True)
    t.start()