using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Save;
using Gamejam2.Settings;
using Gamejam2.Tutorial;
using Gamejam2.Rhythm;
namespace Gamejam2.Debugging;
public partial class TutorialPerformanceChecks:Node
{
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    private void Check(bool ok,string name){if(!ok)throw new Exception(name);GD.Print("PASS "+name);}
    private async Task Run()
    {
        try
        {
            SaveStore.Initialize("/private/tmp/bite-tutorial-perf-"+OS.GetProcessId()+".cfg");ProgressService.Initialize();SettingsService.Initialize();SettingsService.InputOffsetSeconds=SettingsService.VisualOffsetSeconds=0;SettingsService.SetResolution(2);GetWindow().GrabFocus();
            double nodes=Performance.GetMonitor(Performance.Monitor.ObjectNodeCount),orphans=Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
            var game=GD.Load<PackedScene>("res://game/tutorial/TutorialGameplay.tscn").Instantiate<TutorialGameplayScreen>();game.IsReplay=true;AddChild(game);Check(game.DebugJump("FinalExam"),"heavy wave-only tutorial checkpoint starts");
            var samples=new List<double>();double previous=Time.GetTicksUsec()/1000d,lastCatch=0,maxTransition=0,maxDrift=0;int underruns=0,frame=0;
            ulong end=Time.GetTicksMsec()+25000;
            while(Time.GetTicksMsec()<end)
            {
                if(game.Rhythm.IsFocusPaused)GetWindow().GrabFocus();
                double t=game.Rhythm.SongTimeSeconds;
                // Dense PERFECT visual workload does not commit inputs or award mastery.
                if(t<lastCatch)lastCatch=t;
                if(t-lastCatch>.3){game.Shark.Bite(t);game.Juice.Judgment(RhythmJudgment.Perfect,t,1);lastCatch=t;}
                maxTransition=Math.Max(maxTransition,game.LastPracticeTransitionMilliseconds);underruns=Math.Max(underruns,game.AudioCues.UnderrunResyncs);
                if(game.Rhythm.IsClockAdvancing&&game.Music.GetPlaybackPosition()>0)
                {
                    double heard=game.Music.GetPlaybackPosition()+AudioServer.GetTimeSinceLastMix()-AudioServer.GetOutputLatency();
                    // Ignore the new output request's initial latency window.
                    if(t>game.SegmentStart+.2)maxDrift=Math.Max(maxDrift,Math.Abs(heard-t));
                }
                int loops=game.MusicCheckpointRestarts,gc0=GC.CollectionCount(0),gc1=GC.CollectionCount(1),gc2=GC.CollectionCount(2);
                await Frame();double now=Time.GetTicksUsec()/1000d;double ms=now-previous;
                if(frame++>30){samples.Add(ms);if(ms>25)GD.Print($"SPIKE frame={frame} time={t:F3} interval={ms:F3}ms loops={loops}->{game.MusicCheckpointRestarts} GC={gc0}->{GC.CollectionCount(0)}/{gc1}->{GC.CollectionCount(1)}/{gc2}->{GC.CollectionCount(2)}");}previous=now;
            }
            samples.Sort();
            GD.Print($"PROFILE tutorial frames={samples.Count}, p50={samples[samples.Count/2]:F3}ms, p95={samples[(int)(samples.Count*.95)]:F3}ms, p99={samples[(int)(samples.Count*.99)]:F3}ms, max={samples[^1]:F3}ms, maxTransition={maxTransition:F3}ms, playbackRequest={game.Rhythm.LastPlaybackRequestMilliseconds:F3}ms, maxClockDrift={maxDrift*1000:F3}ms, SFXunderruns={underruns}");
            Check(game.MusicCheckpointRestarts==1,"only natural song-end retry used in heavy practice");Check(underruns==0,"tutorial heavy practice has no SFX underrun");Check(maxDrift<.05,"tutorial music and authoritative clock stay within 50ms");
            game.StopGameplay();game.QueueFree();for(int i=0;i<8;i++)await Frame();
            Check(Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)==nodes&&Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount)==orphans,"tutorial waves/debris/audio nodes return to baseline");
            GD.Print("TUTORIAL PERFORMANCE VERIFIED");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
