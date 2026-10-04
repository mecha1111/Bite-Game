"""Run actual Godot regression scenes sequentially; each test uses an isolated save."""
import argparse, json, subprocess, time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
p=argparse.ArgumentParser();p.add_argument('--godot',required=True);p.add_argument('--full-songs',action='store_true');p.add_argument('--presentation-songs',action='store_true',help='Independent full-song real CoreAudio checks without a window');p.add_argument('--only',help='Comma-separated test labels');p.add_argument('--report',default='regression-results.json');p.add_argument('--output',default='artifacts/cleanup');a=p.parse_args()
out=ROOT/a.output;out.mkdir(parents=True,exist_ok=True)
tests=[('build-policy','build_policy_checks',[]),('progress-controls','progress_controls_checks',[]),('UI-update','presentation_update_checks',[]),('stage-FX','stage_presentation_checks',[]),('tutorial-mastery','tutorial_mastery_checks',[]),('satiety-sections','satiety_section_checks',[]),('stage-completion','stage_completion_checks',[]),('tutorial-vision','tutorial_vision_checks',['--tutorial-debug']),('tutorial-performance','tutorial_performance_checks',['--tutorial-debug']),('tutorial-ux','tutorial_ux_checks',[]),('tutorial-state','tutorial_state_checks',['--tutorial-debug']),('tutorial','tutorial_checks',[]),('performance','cleanup_performance_checks',[]),('flow','gameplay_checks',[]),('anti-mash','anti_mash_checks',[]),('rhythm','rhythm_verification',['--verify']),('menu','processed_menu_checks',[]),('processed-audio','processed_audio_checks',['--data-only']),('catch-audio','catch_audio_checks',[]),('EX','ex_stage_checks',[]),('polish','polish_checks',[]),('settings','settings_context_checks',[]),('result','result_presentation_checks',[]),('water','water_lifecycle_checks',[]),('skeleton','water_skeleton_checks',[]),('presentation','presentation_correction_checks',[]),('song-chart','song_chart_checks',[]),('runtime','runtime_wiring_audit',[])]
if a.full_songs:tests.append(('full-songs',None,['--musical-audit-driver']))
if a.presentation_songs:tests.extend((f'full-song-{i}',None,['--musical-audit-driver',f'--audit-song={i}']) for i in range(1,5))
if a.only:tests=[t for t in tests if t[0] in set(a.only.split(','))]
results=[]
for label,scene,args in tests:
    log=out/(label+'.log');cmd=[a.godot,'--path',str(ROOT),'--disable-vsync','--max-fps','120']
    if label in ('tutorial-ux','tutorial-state','anti-mash') or label.startswith('full-song-'):cmd+=['--headless','--audio-driver','CoreAudio']
    if scene:cmd+=['--scene',f'res://game/debug/{scene}.tscn']
    if args:cmd+=['--']+args
    print('RUN',label,flush=True);start=time.monotonic()
    with log.open('w') as f:
        try:code=subprocess.run(cmd,stdout=f,stderr=subprocess.STDOUT,timeout=960 if label=="tutorial" else 740 if scene is None else 230).returncode
        except subprocess.TimeoutExpired:code=124
    text=log.read_text();passed=code==0 and 'ERROR:' not in text
    results.append(dict(test=label,exit=code,passed=passed,seconds=round(time.monotonic()-start,2),log=str(log.relative_to(ROOT))))
    (out/a.report).write_text(json.dumps(results,indent=2));print('PASS' if passed else 'FAIL',label,results[-1]['seconds'],flush=True)
print('FINISHED',sum(x['passed'] for x in results),'/',len(results),flush=True)
raise SystemExit(0 if all(x['passed'] for x in results) else 1)
