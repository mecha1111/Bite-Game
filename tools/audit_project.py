"""Read-only dependency inventory. Heuristic reachability never authorizes deletion."""
from pathlib import Path
import collections, csv, hashlib, io, json, re, unicodedata
ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/cleanup'
TEXT = {'.cs', '.tscn', '.tres', '.gdshader', '.csv', '.py', '.md', '.txt', '.godot', '.json', '.gd'}
def main():
    files = {p.relative_to(ROOT).as_posix(): p for p in ROOT.rglob('*') if p.is_file() and not any(part.startswith('.') or part == '__pycache__' for part in p.relative_to(ROOT).parts) and not p.is_relative_to(OUT)}
    canonical = {unicodedata.normalize('NFC', k): k for k in files}
    texts = {k: p.read_text(errors='replace') for k, p in files.items() if p.suffix in TEXT}
    classes = {m.group(1): k for k, t in texts.items() if k.endswith('.cs') for m in re.finditer(r'\b(?:class|struct|enum|record(?:\s+struct)?)\s+(\w+)', t)}
    edges, incoming = collections.defaultdict(set), collections.defaultdict(set)
    missing, optional_outputs = [], []
    for k, t in texts.items():
        paths = re.findall(r'["\']\*?(res://[^"\'\r\n]+)["\']', t)
        if k.endswith('.csv'):
            paths += [v for row in csv.reader(io.StringIO(t)) for v in row if v.startswith('res://')]
        for path in paths:
            if '{' in path: continue  # explicitly expanded below
            target = canonical.get(unicodedata.normalize('NFC', path[6:]))
            if target:
                edges[k].add(target); incoming[target].add(k)
            elif k == 'game/debug/ChartAudition.cs' and path == 'res://data/charts/listening_reviews.csv':
                optional_outputs.append({'owner': k, 'path': path, 'reason': 'created only after listener approval'})
            elif k.startswith('game/') or k == 'project.godot':
                missing.append({'owner': k, 'path': path})
        if k.endswith('.cs'):
            for word in set(re.findall(r'\b\w+\b', t)):
                if word in classes and classes[word] != k:
                    if k == 'game/startup/SceneRouter.cs' and classes[word].startswith('game/debug/'): continue
                    edges[k].add(classes[word]); incoming[classes[word]].add(k)
    # Actual dynamic loader in GameplayEnvironment.ApplyProfile.
    for p in ROOT.glob('game/gameplay/water_profiles/*.tres'):
        k = p.relative_to(ROOT).as_posix(); edges['game/gameplay/GameplayEnvironment.cs'].add(k); incoming[k].add('game/gameplay/GameplayEnvironment.cs (dynamic song_id)')
    def walk(seeds):
        seen, todo = set(), list(seeds)
        while todo:
            k = todo.pop()
            if k not in seen: seen.add(k); todo.extend(edges[k] - seen)
        return seen
    runtime = walk(['project.godot', 'default_bus_layout.tres'])
    direct = set(edges['project.godot'])
    # Debug drivers are opt-in entry points, not evidence that their historical assets are player dependencies.
    dev = walk(k for k in files if k.startswith(('game/debug/', 'tools/')))
    rows = []
    for k, p in sorted(files.items()):
        if p.suffix in {'.import', '.uid', '.pyc'}: continue
        category = 'A' if k in direct else 'B' if k in runtime else 'E'
        role = 'runtime' if k in runtime else 'development' if k in dev or k.startswith(('docs/', 'artifacts/', 'data/audio/')) else 'unverified'
        rows.append(dict(path=k, type=p.suffix, bytes=p.stat().st_size, category=category, role=role, references=sorted(incoming[k]), sha256=hashlib.sha256(p.read_bytes()).hexdigest()))
    OUT.mkdir(exist_ok=True)
    (OUT/'inventory.json').write_text(json.dumps(rows, ensure_ascii=False, indent=2))
    (OUT/'references.json').write_text(json.dumps(dict(missing=missing, optional_outputs=optional_outputs, runtime=sorted(runtime)), ensure_ascii=False, indent=2))
    print(json.dumps(dict(files=len(rows), counts=dict(collections.Counter(r['type'] for r in rows)), missing=missing), ensure_ascii=False))
if __name__ == '__main__': main()
