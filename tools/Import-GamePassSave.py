r"""Adds a KCD2 .whs save to an Xbox Game Pass install of the game.

Game Pass saves are not files in a folder: they are blobs in an XGameSave
container under %LOCALAPPDATA%\Packages\DeepSilver...\SystemAppData\wgs. This
adds the save to the container 'saves/playline0' the way the game itself adds
a second save to a playline: a new blob file, a new container.<N+1> listing
the old blobs and the new one, and the index entry bumped (sequence, time,
size). Etag and flags are left alone.

  python tools/Import-GamePassSave.py package/save/autosave018.whs kcdmpskip.whs

The second argument is the name the save gets in the game. Give it one of its
own, as above: under its original name (autosave018.whs) the game's own
autosaves can take its place.

The game must be closed and must have been started once with a new game, so
that the container exists. The whole save folder is copied to Downloads first
(KCD2_BACKUP_ROOT to put the backup elsewhere). KCD2_WGS names the save folder
outright when the machine has more than one Xbox account with saves.
Used on one live machine (game v1.5.6.0, 2026-09-30): the save appeared in the
Load menu and loaded.
"""
import os, sys, struct, uuid, shutil, datetime, subprocess

def die(msg):
    print("ABORT:", msg); sys.exit(1)

def find_wgs():
    """The one account folder under wgs that holds a containers.index."""
    root = os.path.join(os.environ["LOCALAPPDATA"], "Packages",
                        "DeepSilver.77536C3FE941_hmv7qcest37me", "SystemAppData", "wgs")
    if not os.path.isdir(root):
        die(f"no Game Pass save folder at {root} -- is the Game Pass game installed and started once?")
    found = [os.path.join(root, n) for n in os.listdir(root)
             if os.path.isfile(os.path.join(root, n, "containers.index"))]
    if len(found) != 1:
        die(f"expected one account folder with saves under {root}, found {len(found)} -- set KCD2_WGS to the right one")
    return found[0]

if len(sys.argv) < 2:
    die("usage: python Import-GamePassSave.py <save.whs> [name-in-the-game]")
WGS = os.environ.get("KCD2_WGS") or find_wgs()
CONTAINER = "saves/playline0"
SRC = sys.argv[1]
if not os.path.isfile(SRC):
    die(f"no such file: {SRC}")
BLOB_NAME = sys.argv[2] if len(sys.argv) > 2 else os.path.basename(SRC)
BACKUP_ROOT = os.environ.get("KCD2_BACKUP_ROOT") or os.path.join(os.environ["USERPROFILE"], "Downloads")

def filetime_now():
    return int((datetime.datetime.now(datetime.timezone.utc)
                - datetime.datetime(1601, 1, 1, tzinfo=datetime.timezone.utc)).total_seconds() * 10**7)

# Always: the game writes its containers from the sequence it holds in memory,
# so a change made under a running game is orphaned or corrupts the playline.
if b"KingdomCome.exe" in subprocess.run(["tasklist"], capture_output=True).stdout:
    die("KingdomCome.exe is running -- close the game first")
data = open(SRC, "rb").read()
if data[:4] != b"\xff\xff\xff\xff" or b"<C_SaveGameDescription" not in data[:64]:
    die("source is not a KCD2 .whs save")

idx_path = os.path.join(WGS, "containers.index")
d = bytearray(open(idx_path, "rb").read())

def s16(o):
    n = struct.unpack_from("<I", d, o)[0]
    return d[o + 4:o + 4 + 2 * n].decode("utf-16-le"), o + 4 + 2 * n

version, count = struct.unpack_from("<II", d, 0)
if version != 14:
    die(f"unexpected index version {version}")
o = 12
_, o = s16(o)
hdr_ft_off = o
o += 8 + 4
_, o = s16(o)
o += 8
entry = None
for _ in range(count):
    name, o = s16(o); _, o = s16(o); _, o = s16(o)
    seq_off = o
    gid = uuid.UUID(bytes_le=bytes(d[o + 5:o + 21])).hex.upper()
    if name == CONTAINER:
        entry = (seq_off, gid)
    o += 5 + 16 + 24
if o != len(d):
    die("index did not parse cleanly")
if entry is None:
    die(f"container {CONTAINER!r} not found -- start a new game once so it exists")
seq_off, gid = entry
seq = d[seq_off]
cdir = os.path.join(WGS, gid)
cfile = os.path.join(cdir, f"container.{seq}")
if not os.path.isfile(cfile):
    die(f"{cfile} missing (index seq={seq})")

c = open(cfile, "rb").read()
cver, n = struct.unpack_from("<II", c, 0)
blobs = []
for i in range(n):
    off = 8 + i * 160
    bname = c[off:off + 128].decode("utf-16-le").rstrip("\0")
    blobs.append((bname, c[off + 128:off + 144], c[off + 144:off + 160]))
if any(b[0].lower() == BLOB_NAME.lower() for b in blobs):
    die(f"{BLOB_NAME} is already in {CONTAINER}")
if len(BLOB_NAME) >= 64:
    die("blob name too long")

new_seq = seq + 1
if new_seq > 255:
    die("container sequence would overflow")      # before anything is written

stamp = datetime.datetime.now().strftime("%Y%m%d-%H%M%S")
backup = os.path.join(BACKUP_ROOT, f"KCD2-GamePass-save-backup-{stamp}")
shutil.copytree(WGS, backup)
print("backup:", backup)

new_guid = uuid.uuid4()
new_file = os.path.join(cdir, new_guid.hex.upper())
with open(new_file, "wb") as f:
    f.write(data)
blobs.append((BLOB_NAME, new_guid.bytes_le, new_guid.bytes_le))

out = bytearray(struct.pack("<II", cver, len(blobs)))
for bname, g1, g2 in blobs:
    out += bname.encode("utf-16-le").ljust(128, b"\0") + g1 + g2
with open(os.path.join(cdir, f"container.{new_seq}"), "wb") as f:
    f.write(out)

total = sum(os.path.getsize(os.path.join(cdir, uuid.UUID(bytes_le=bytes(g2)).hex.upper()))
            for _, _, g2 in blobs)
now = filetime_now()
d[seq_off] = new_seq
struct.pack_into("<Q", d, seq_off + 5 + 16, now)
struct.pack_into("<Q", d, seq_off + 5 + 16 + 16, total)
struct.pack_into("<Q", d, hdr_ft_off, now)
tmp = idx_path + ".tmp"
with open(tmp, "wb") as f:
    f.write(d)
os.replace(tmp, idx_path)
os.remove(cfile)

print(f"added {BLOB_NAME} ({len(data)} bytes) to {CONTAINER}: container.{seq} -> container.{new_seq}, "
      f"blobs={[b[0] for b in blobs]}, size={total}")
