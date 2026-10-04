"""Recompute starts/grid metadata in the actual per-song runtime charts; never move targets."""
import csv
from pathlib import Path
root=Path(__file__).resolve().parents[1]
def read(p):return list(csv.DictReader(p.open(encoding='utf-8-sig')))
timing={r['song_id']:r for r in read(root/'data/balance/song_timing.csv')}
outputs=[]
for song in read(root/'data/balance/songs.csv'):
    path=root/song['chart_path'].replace('res://','');rows=read(path);grid=timing[song['song_id']];beat=60/float(grid['bpm']);phase=float(grid['beat_offset_sec']);music_beat=60/float(grid['music_bpm']);previous=-100
    for row in rows:
        target=float(row['target_time_sec']);start=target-4*beat
        if start<0 or start<previous+.25-1e-9:raise ValueError(f'Prey overlap/negative start: {song["song_id"]}, {start}->{target}')
        row['start_time_sec']=f'{start:.12f}';
        if 'game_bpm' in row:row['game_bpm']=grid['bpm']
        if 'pulse_duration_sec' in row:row['pulse_duration_sec']=f'{beat:.12f}'
        row['nearest_musical_beat_sec']=f'{phase+round((target-phase)/music_beat*4)*music_beat/4:.6f}';previous=target
    outputs.append((path,rows))
for path,rows in outputs:
    with path.open('w',encoding='utf-8',newline='') as f:
        w=csv.DictWriter(f,rows[0].keys());w.writeheader();w.writerows(rows)
print('Derived',sum(len(r) for p,r in outputs),'starts in actual runtime files; targets unchanged.')
