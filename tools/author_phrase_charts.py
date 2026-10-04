"""Expand an explicitly curated phrase plan into editable chart CSVs.

Not a peak-to-chart generator: every proposed payoff, pattern, side and section
is authored in phrase_plan.json. Band attacks only refine that chosen payoff
within 140ms. Nothing is labelled listening-approved by this tool.
"""
import csv, json, math, argparse
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
def read(path):
    return list(csv.DictReader(path.open(encoding='utf-8-sig')))
def write(path, rows):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open('w', encoding='utf-8', newline='') as f:
        w = csv.DictWriter(f, rows[0].keys()); w.writeheader(); w.writerows(rows)
    path.with_name(path.name+'.import').write_text('[remap]\nimporter="keep"\n')

def main():
    if (ROOT/'data/charts/listening_reviews.csv').exists():
        raise SystemExit('Listening reviews exist: edit current target CSVs, do not regenerate over manual decisions.')
    parser=argparse.ArgumentParser();parser.add_argument('--replace-candidates',action='store_true');args=parser.parse_args()
    plan=json.loads((ROOT/'data/charts/phrase_plan.json').read_text())
    if any((ROOT/'data/charts'/song['filename']).exists() for song in plan) and not args.replace_candidates:
        raise SystemExit('Current charts exist. Edit CSV directly; explicit --replace-candidates is required to regenerate unapproved proposals.')
    timing={r['song_id']:r for r in read(ROOT/'data/balance/song_timing.csv')}
    songs=read(ROOT/'data/balance/songs.csv')
    sections=[];summary=[];interference=[];decisions=[]
    for n, song in enumerate(plan, 1):
        sid=song['song_id']; grid=timing[sid]; pulse=60/float(grid['bpm']); music=60/float(grid['music_bpm']); phase=float(grid['beat_offset_sec'])
        bins=read(Path(f'/private/tmp/bite-v2-song{n}-accents.csv'))
        times=[float(r['time_sec'])-.0025 for r in bins]
        def refine(ideal, band, minimum=0):
            energy=[float(r[band+'_energy']) for r in bins]
            # Distinguish bass and upper percussion attack instead of a generic onset score.
            candidates=[i for i,t in enumerate(times) if abs(t-ideal)<=.14 and t>=minimum and i>=2 and i<len(times)-2]
            if not candidates: raise ValueError(f'{sid}: payoff {ideal} conflicts with preceding resolve; edit phrase plan')
            def score(i):
                attack=max(0,energy[i]-energy[i-2])
                return attack * math.exp(-.5*((times[i]-ideal)/.095)**2)
            best=max(candidates,key=score)
            return times[best],score(best)
        chart=[]
        for section in song['sections']:
            sections.append(dict(song_id=sid,section_id=section['id'],start_time_sec=section['start'],end_time_sec=section['end'],character=section['character'],pacing=section['pacing'],listening_status='pending'))
            for event in section['events']:
                ideal,pattern,side,band,reason=event
                minimum=chart[-1]['target_time_sec']+4*pulse+.36 if chart else 4*pulse
                target,score=refine(ideal,band,minimum)
                if not section['start']<=target<section['end']:raise ValueError(f'{sid}: target outside authored section')
                start=target-4*pulse
                row=dict(song_id=sid,index=len(chart),start_time_sec=f'{start:.12f}',target_time_sec=target,side=side,pattern_id=pattern,game_bpm=float(grid['bpm']),pulse_duration_sec=f'{pulse:.12f}',timing_grid='phrase_curated_attack_candidate',nearest_musical_beat_sec=f'{phase+round((target-phase)/music*4)*music/4:.6f}',optional_section_id=section['id'],optional_notes=f'{reason}; {band}-band attack candidate; listening pending')
                chart.append(row)
                decisions.append(dict(song_id=sid,event_index=row['index'],section_id=section['id'],planned_payoff_sec=ideal,target_time_sec=f'{target:.6f}',attack_band=band,reason=reason,human_listened='false'))
        for row in chart:row['target_time_sec']=f"{row['target_time_sec']:.6f}"
        path=ROOT/'data/charts'/song['filename'];write(path,chart)
        metadata=next(r for r in songs if r['song_id']==sid);metadata['chart_path']='res://'+str(path.relative_to(ROOT));metadata['status']='phrase_curated_listening_pending'
        for ideal,kind,reason in song['interference']:
            target,_=refine(ideal,'low' if kind in ('ship_horn','whale') else 'high')
            interference.append(dict(song_id=sid,type=kind,time_seconds=f'{target:.6f}',optional_notes=reason+'; listening pending'))
        summary.append(dict(song_id=sid,chart_path=metadata['chart_path'],encounter_count=len(chart),musical_bpm=grid['music_bpm'],gameplay_pulse_bpm=grid['bpm'],listening_approved=0,status='candidate_not_final'))
        print(sid,len(chart),'phrase-authored candidates; no density quota')
    write(ROOT/'data/balance/songs.csv',songs)
    write(ROOT/'data/balance/interference.csv',interference)
    write(ROOT/'data/charts/musical_sections.csv',sections)
    write(ROOT/'data/charts/chart_summary.csv',summary)
    write(ROOT/'data/charts/authoring_decisions.csv',decisions)

if __name__=='__main__':main()
