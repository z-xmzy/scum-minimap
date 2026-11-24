"""Builds files.pack and files.txt for incremental updates.

The file set matches what SCUM-MiniMap.iss installs, with paths relative to the installation root:
app/** (without app/data and app/temp), dist/**, 使用说明.md, 启动 SCUM 地图.cmd and 许可与出处.txt.
files.pack is the files' raw-deflate streams back to back; files.txt gives each file's SHA256, size
and byte range so the app can fetch only what changed with HTTP range requests.

usage: python -I build_update_pack.py <package dir> <license file> <version> <output dir>
"""
import hashlib, os, sys, zlib

EXCLUDED_DIRS = {'app/data', 'app/temp'}
EXCLUDED_SUFFIXES = ('.update.exe', '.partial', '.previous')


def collect(package, license_file):
    files = []
    for top in ('app', 'dist'):
        base = os.path.join(package, top)
        for dirpath, dirnames, filenames in os.walk(base):
            rel_dir = os.path.relpath(dirpath, package).replace(os.sep, '/')
            dirnames[:] = sorted(d for d in dirnames if (rel_dir + '/' + d) not in EXCLUDED_DIRS)
            for name in sorted(filenames):
                if name.endswith(EXCLUDED_SUFFIXES):
                    continue
                files.append((rel_dir + '/' + name, os.path.join(dirpath, name)))
    files.append(('使用说明.md', os.path.join(package, '使用说明.md')))
    files.append(('启动 SCUM 地图.cmd', os.path.join(package, '启动 SCUM 地图.cmd')))
    files.append(('许可与出处.txt', license_file))
    files.sort(key=lambda f: f[0])
    return files


def main():
    package, license_file, version, out = sys.argv[1:5]
    os.makedirs(out, exist_ok=True)
    pack_path = os.path.join(out, 'files.pack')
    lines = ['schema=1', 'version=' + version]
    entries = []
    offset = 0
    with open(pack_path, 'wb') as pack:
        for rel, path in collect(package, license_file):
            sha = hashlib.sha256()
            comp = zlib.compressobj(9, zlib.DEFLATED, -15)
            size = 0
            start = offset
            with open(path, 'rb') as f:
                while True:
                    chunk = f.read(1 << 20)
                    if not chunk:
                        break
                    size += len(chunk)
                    sha.update(chunk)
                    data = comp.compress(chunk)
                    pack.write(data)
                    offset += len(data)
            data = comp.flush()
            pack.write(data)
            offset += len(data)
            entries.append('file=%s|%d|%d|%d|%s' % (sha.hexdigest(), size, start, offset - start, rel))
    lines.append('pack-size=%d' % offset)
    lines.extend(entries)
    with open(os.path.join(out, 'files.txt'), 'w', encoding='utf-8', newline='\n') as f:
        f.write('\n'.join(lines) + '\n')
    print('files.pack: %d files, %.1f MB' % (len(entries), offset / 1048576.0))


if __name__ == '__main__':
    main()
