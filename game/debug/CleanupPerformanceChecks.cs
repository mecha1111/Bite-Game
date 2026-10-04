using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Save;
using Gamejam2.Settings;
using Gamejam2.Rhythm;
namespace Gamejam2.Debugging;
/// <summary>Repeatable rendered workload, allocation microbenchmark and scene lifecycle regression.</summary>
public partial class CleanupPerformanceChecks : Node
{
    private Task Wait(double seconds)=>WaitInternal(seconds);
    private async Task WaitInternal(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private void Check(bool ok,string label){if(!ok)throw new Exception(label);GD.Print("PASS "+label);}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            SaveStore.Initialize("/private/tmp/bite-performance-"+OS.GetProcessId()+".cfg");ProgressService.Initialize();SettingsService.Initialize();SettingsService.InputOffsetSeconds=SettingsService.VisualOffsetSeconds=0;SettingsService.SetResolution(2);GetWindow().GrabFocus();
            var wave=GD.Load<PackedScene>("res://game/gameplay/WavePulse.tscn").Instantiate<WavePulse>();AddChild(wave);wave.Position=new Vector2(120,440);wave.Begin(WaveKind.Large,0);
            for(int i=0;i<100;i++)wave.PresentAge(.5);
            long before=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
            for(int i=0;i<10000;i++)wave.PresentAge(.5);
            watch.Stop();long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            GD.Print($"PROFILE WavePulse 10000 updates: {watch.Elapsed.TotalMilliseconds:F3}ms, managed bytes={allocated}");
            wave.QueueFree();await Wait(.1);
            var ui=GetNode<Gamejam2.Audio.UiAudio>("/root/UiAudio");int baselineBindings=ui.BindingCount;
            long baselineNodes=(long)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);
            long baselineOrphans=(long)Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
            var samples=new List<double>();long maxNodes=0;double cpu=0;long frameAlloc=0;
            for(int run=0;run<3;run++)
            {
                var game=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();game.SongId=run==2?"song_4":"song_1";AddChild(game);game.SetProcessInput(false);await Wait(.2);
                Check(game.RunState==GameplayScreen.GameplayRunState.Playing,"retry current scene starts "+run);
                int frame=0;double last=Time.GetTicksUsec()/1e6;
                while(frame++<360)
                {
                    if(game.Rhythm.IsFocusPaused)GetWindow().GrabFocus();
                    double t=game.Rhythm.SongTimeSeconds;
                    if(frame%90==1)
                    {
                        foreach(string type in new[]{"ship_horn","fishing_float","whale","sardine"})game.Interference.Trigger(type,t);
                        game.Signals.LeftLane.Activate(4,WaveKind.Large,2,t);
                        game.Juice.Judgment(RhythmJudgment.Perfect,t,frame);
                    }
                    if(frame>30)
                    {
                        double now=Time.GetTicksUsec()/1e6;samples.Add((now-last)*1000);
                        maxNodes=Math.Max(maxNodes,(long)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount));
                        cpu+=Performance.GetMonitor(Performance.Monitor.TimeProcess)*1000;
                    }
                    last=Time.GetTicksUsec()/1e6;
                    long start=GC.GetAllocatedBytesForCurrentThread();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);frameAlloc+=GC.GetAllocatedBytesForCurrentThread()-start;
                }
                GD.Print($"PROFILE run {run}: SFX underruns={game.AudioCues.UnderrunResyncs}, wave nodes={game.FindChildren("WavePulse","",true,false).Count}");
                Check(game.AudioCues.UnderrunResyncs==0,"heavy workload does not underrun SFX "+run);
                game.StopGameplay();game.QueueFree();await Wait(.25);
                long nodes=(long)Performance.GetMonitor(Performance.Monitor.ObjectNodeCount),orphans=(long)Performance.GetMonitor(Performance.Monitor.ObjectOrphanNodeCount);
                Check(ui.BindingCount==baselineBindings,"UI audio binding IDs return to baseline "+run);
                Check(nodes==baselineNodes&&orphans==baselineOrphans,"scene/wave/debris/audio nodes return to baseline "+run+" nodes="+nodes+" orphans="+orphans);
            }
            samples.Sort();GD.Print($"PROFILE heavy rendered frames={samples.Count}, p50={samples[samples.Count/2]:F3}ms, p95={samples[(int)(samples.Count*.95)]:F3}ms, p99={samples[(int)(samples.Count*.99)]:F3}ms, max={samples[^1]:F3}ms, avgCPU={cpu/samples.Count:F3}ms, peakNodes={maxNodes}, frameManagedBytes={frameAlloc}");
            GD.Print("CLEANUP PERFORMANCE VERIFIED");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
