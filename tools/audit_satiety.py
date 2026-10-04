#!/usr/bin/env python3
"""Current chart + authored drain + schedule-only safety. Never modifies musical targets."""
import bisect, csv, json, pathlib, re
ROOT=pathlib.Path(__file__).resolve().parents[1]
def rows(path):return list(csv.DictReader((ROOT/path.replace('res://','')).open()))
balance=rows('data/balance/satiety.csv')[0];judgment=rows('data/balance/judgment.csv')[0];policy=rows('data/balance/bite_policy.csv')[0]
start,maximum,threshold,base=[float(balance[k]) for k in ('starting_satiety','max_satiety','clear_threshold','base_fish_recovery')]
grades=['perfect','good','bad','miss'];rewards=[base*float(judgment[g+'_multiplier']) for g in grades]
drain_policy=rows('data/balance/satiety_drain_policy.csv')[0]
gap_threshold=float(drain_policy['no_food_gap_seconds']);safety_cap=float(drain_policy['no_food_multiplier_cap'])
late=float(re.search(r'LateSeconds = ([\d.]+)',(ROOT/'game/rhythm/InputCaptureWindows.tres').read_text()).group(1))
def distribution(n):
    weights=[.2,.6,.15,.05];counts=[int(n*w) for w in weights]
    for k in sorted(range(4),key=lambda k:n*weights[k]-counts[k],reverse=True)[:n-sum(counts)]:counts[k]+=1
    used,result=[0]*4,[]
    for i in range(n):
        k=max((k for k in range(4) if used[k]<counts[k]),key=lambda k:(i+1)*counts[k]/n-used[k]);result.append(k);used[k]+=1
    return result,dict(zip(grades,counts))
def profile(song,chart):
    duration=float(song['duration_seconds']);authored=[dict(start=float(r['start_time_sec']),end=float(r['end_time_sec']),multiplier=float(r['drain_multiplier']),type=r['section_type']) for r in rows('data/balance/satiety_sections.csv') if r['song_id']==song['song_id']]
    gaps=[];previous=0
    for p in chart:
        a=float(p['start_time_sec'])
        if a>previous:gaps.append(dict(start=previous,end=a))
        previous=float(p['target_time_sec'])+late
    if previous<duration:gaps.append(dict(start=previous,end=duration))
    safe=[g for g in gaps if g['end']-g['start']>=gap_threshold-1e-9]
    cuts=sorted({0,duration}|{t for s in authored for t in (s['start'],s['end'])}|{t for g in safe for t in (g['start'],g['end'])})
    spans=[];prefix=[0];ends=[]
    for a,b in zip(cuts,cuts[1:]):
        mid=(a+b)/2;s=next(s for s in authored if s['start']<=mid<s['end']);protected=any(g['start']<=mid<g['end'] for g in safe);multiplier=min(s['multiplier'],safety_cap) if protected else s['multiplier']
        spans.append(dict(start=a,end=b,multiplier=multiplier,type=s['type'],safety_applied=protected));ends.append(b);prefix.append(prefix[-1]+(b-a)*multiplier)
    def integral(t):
        t=max(0,min(t,duration));i=min(bisect.bisect_right(ends,t),len(spans)-1);s=spans[i];return prefix[i]+(t-s['start'])*s['multiplier']
    def depleted_at(a,b,amount):
        for s in spans:
            x=max(a,s['start']);y=min(b,s['end']);part=max(0,y-x)*s['multiplier']
            if s['multiplier']>0 and amount<=part:return x+amount/s['multiplier']
            amount-=part
            if y>=b:break
        return b
    return authored,gaps,spans,integral,depleted_at
def simulate(times,duration,drain,sequence,integral,depleted_at):
    value,last,minimum,overflow,penalty,streak,death=start,0,start,0,0,0,None
    for t,k in zip(times,sequence):
        before=value;value-=drain*(integral(t)-integral(last));minimum=min(minimum,value)
        if value<=0:death=depleted_at(last,t,before/drain);value=0;break
        added=value+rewards[k];overflow+=max(0,added-maximum);value=min(maximum,added);streak=streak+1 if k==3 else 0
        cost=0 if streak<int(policy['miss_streak_penalty_start']) else float(policy['miss_streak_penalty_2'] if streak==2 else policy['miss_streak_penalty_3_plus'])
        penalty+=cost;value=max(0,value-cost);last=t
        if value<=0:death=t;break
    if death is None:
        before=value;value-=drain*(integral(duration)-integral(last));minimum=min(minimum,value)
        if value<=0:death=depleted_at(last,duration,before/drain);value=0
    return dict(final_satiety=value,minimum_satiety=max(0,minimum),overflow_recovery=overflow,miss_streak_penalty=penalty,game_over_seconds=death,cleared=death is None and value>=threshold)
def main():
    report=[]
    for song in rows('data/balance/songs.csv'):
        duration=float(song['duration_seconds']);drain=float(song['drain_per_3_seconds'])/3;chart=rows(song['chart_path']);times=[float(p['target_time_sec']) for p in chart];n=len(times)
        authored,gaps,spans,integral,depleted_at=profile(song,chart);mixed,counts=distribution(n);perfect=[0]*n
        def run(seq):return simulate(times,duration,drain,seq,integral,depleted_at)
        pairs=[]
        for i in range(n):
            for j in range(i+1,n):seq=perfect.copy();seq[i]=seq[j]=3;pairs.append(run(seq))
        worst=min(pairs,key=lambda p:p['final_satiety']);longest=max(gaps,key=lambda g:g['end']-g['start'])
        gapspans=[s for s in spans if s['start']>=longest['start'] and s['end']<=longest['end']]
        opportunity_times=[0]+times+[duration];all_catch_gaps=[dict(start=a,end=b,duration=b-a) for a,b in zip(opportunity_times,opportunity_times[1:])]
        result=dict(song_id=song['song_id'],name=song['display_name'],duration_seconds=duration,prey_count=n,authored_section_duration={kind:sum(s['end']-s['start'] for s in authored if s['type']==kind) for kind in ['active','low_activity','break']},drain_per_second=drain,effective_seconds=integral(duration),total_song_drain=drain*integral(duration),perfect_total_recovery=n*rewards[0],nominal_all_perfect_final=start+n*rewards[0]-drain*integral(duration),all_perfect=run(perfect),mixed_distribution=counts,mixed=run(mixed),clustered_misses=run([k for k in mixed if k!=3]+[3]*counts['miss']),bad_miss_heavy=run([2 if i%10<7 else 3 for i in range(n)]),worst_two_misses=worst,longest_scheduled_foodless_gap={**longest,'duration':longest['end']-longest['start'],'safety_cap':safety_cap,'effective_multipliers':sorted({s['multiplier'] for s in gapspans})},longest_catch_opportunity_gap=max(all_catch_gaps,key=lambda g:g['duration']),catch_gaps_at_least_3_seconds=[g for g in all_catch_gaps if g['duration']>=3],scheduled_foodless_gaps=[{**g,'duration':g['end']-g['start'],'safety_applied':g['end']-g['start']>=gap_threshold-1e-9} for g in gaps],compiled_spans=spans)
        assert 950<=result['all_perfect']['final_satiety']<=1100,song['song_id']+' AP target'
        assert result['mixed']['cleared'],song['song_id']+' mixed impossible'
        assert worst['cleared'],song['song_id']+' two mistakes doomed'
        report.append(result)
    out=ROOT/'artifacts/satiety-sections';out.mkdir(exist_ok=True,parents=True);(out/'satiety-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print('song | seconds | prey | active/low/rest sec | base drain/s | effective drain | AP | mixed | longest scheduled gap | multiplier')
    for r in report:
        d=r['authored_section_duration'];g=r['longest_scheduled_foodless_gap'];print(f"{r['song_id']} | {r['duration_seconds']:.6f} | {r['prey_count']} | {d['active']:.3f}/{d['low_activity']:.3f}/{d['break']:.3f} | {r['drain_per_second']:.6f} | {r['total_song_drain']:.3f} | {r['all_perfect']['final_satiety']:.3f} | {r['mixed']['final_satiety']:.3f} | {g['duration']:.3f} | {g['effective_multipliers']}")
if __name__=='__main__':main()
