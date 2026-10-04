using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Rhythm;
using Gamejam2.Tutorial;
using Gamejam2.Startup;
using Gamejam2.Calibration;
using Gamejam2.Save;
using Gamejam2.Settings;
namespace Gamejam2.Debugging;
/// <summary>Only the requested A-G samples and ten-phrase direction check. Never plays full songs.</summary>
public partial class GameplayCorrectionChecks : Node
{
    private void Check(bool value,string message){if(!value)throw new Exception(message);GD.Print("PASS "+message);}
    private async Task Frames(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Remove(GameplayScreen g){g.StopGameplay();g.QueueFree();await Frames();}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task CameraOnly()
    {
        GetWindow().GrabFocus();SaveStore.Initialize("/private/tmp/bite-camera-only.cfg");SettingsService.Initialize();
        var g=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();g.SongId="song_3";AddChild(g);await Frames();g.Rhythm.PauseSong();g.SetProcess(false);
        var e=g.Presentation.Events.First(e=>e.Type=="FOLLOW_WAVE"&&e.Priority==85);double at=e.Time+e.Duration*.5;
        g.Signals.Present(at);g.Presentation.Present(at);
        Check(g.Presentation.CameraZoom>1.08&&g.Presentation.CameraOffset.Length()>50,"F corrected source close-up keeps visible framing");
        GD.Print($"CAMERA zoom={g.Presentation.CameraZoom} pan={g.Presentation.CameraOffset} mouth={g.Juice.MouthAnchor.GlobalPosition}");
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng("res://artifacts/release-config/correction-wave-closeup.png");await Remove(g);GetTree().Quit();
    }
    private async Task Run()
    {
        try
        {
            if(OS.GetCmdlineUserArgs().Contains("--camera-only")){await CameraOnly();return;}
            GetWindow().GrabFocus();SaveStore.Initialize("/private/tmp/bite-correction-"+Time.GetTicksUsec()+".cfg");ProgressService.Initialize();SettingsService.Initialize();
            var tutorial=GD.Load<PackedScene>("res://game/tutorial/TutorialGameplay.tscn").Instantiate<TutorialGameplayScreen>();AddChild(tutorial);await Frames();
            int before=tutorial.Satiety.CreateResult().Perfect+tutorial.Satiety.CreateResult().Miss;
            tutorial._Input(new InputEventKey{Pressed=true,PhysicalKeycode=Key.Space});
            tutorial._Input(new InputEventAction{Action="rhythm_input",Pressed=true});
            Check(tutorial.IsDemonstrating&&!tutorial.PracticeInputEnabled&&tutorial.Satiety.EmptyBites==0&&tutorial.Satiety.CreateResult().Perfect+tutorial.Satiety.CreateResult().Miss==before,"A demo ignores Space and shared physical Bite action");await Remove(tutorial);
            var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath="/private/tmp/bite-first-flow-"+Time.GetTicksUsec()+".cfg";AddChild(router);await Frames();
            typeof(SceneRouter).GetMethod("StartRequested",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(router,null);await Frames();
            Check(!SettingsService.CalibrationCompleted&&router.ScreenHost!.GetChildren().Any(n=>n is CalibrationScreen),"B clean Start enters Calibration before Tutorial");
            typeof(SceneRouter).GetMethod("CalibrationCompleted",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(router,null);await Frames();
            Check(router.ScreenHost!.GetChildren().Any(n=>n is TutorialGameplayScreen),"B Calibration completion enters enabled Tutorial");
            foreach(var g in router.ScreenHost.GetChildren().OfType<GameplayScreen>())g.StopGameplay();router.QueueFree();await Frames();
            var data=GameplayData.Load("song_3");var phrase=data.Phrases.First(p=>p.TargetEvents.Length==3);
            var chart=new RhythmChart{SongEndTimeSeconds=2};
            foreach(var t in phrase.TargetEvents)chart.Events.Add(new(){TimeSeconds=.08+t.TargetTime-phrase.TargetSeconds,CueId="rapid",TargetOrdinal=t.Ordinal,EventType=RhythmEventType.InputTarget});
            var player=new AudioStreamPlayer();AddChild(player);var rhythm=new RhythmController{MusicPlayer=player,Chart=chart,EnableDebugLogging=false};AddChild(rhythm);
            int judged=0;rhythm.JudgmentResolved+=r=>{Check(r.Judgment==RhythmJudgment.Perfect,"C authored rapid target remains PERFECT");judged++;};Check(rhythm.StartSong(),"C start same-clock authored three-hit phrase");
            foreach(var target in chart.Events)
            {
                while(rhythm.SongTimeSeconds<target.TimeSeconds)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                int old=judged;Check(rhythm.TryCommitPrey("rapid")&&judged==old+1,"C one Bite resolves exactly one target");Check(!rhythm.TryCommitPrey("rapid")&&judged==old+1,"C duplicate press cannot repair or steal next rapid target");
            }
            Check(judged==3,"C all three authored response hits complete");rhythm.StopSong();rhythm.QueueFree();player.QueueFree();await Frames();
            var gameplay=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();gameplay.SongId="song_3";AddChild(gameplay);await Frames();gameplay.Rhythm.PauseSong();gameplay.SetProcess(false);
            var slot=gameplay.Signals.LeftLane.Slots[0];slot.Activate(WaveKind.Medium,1,0,0);slot.Activate(WaveKind.Medium,1,.05,1);slot.Present(.1);gameplay.Signals.ResolveEncounter(0,.1);slot.Present(.12);
            Check(slot.ActivePulses.Count(w=>w.Visible)>=2,"D independently expanding rings coexist after resolution");
            var state=new SatietyState(data);
            foreach(var t in data.Phrases.SelectMany(p=>p.TargetEvents).OrderBy(t=>t.TargetTime)){state.Advance(t.TargetTime);state.Resolve(new(RhythmJudgment.Perfect,0,new(){TimeSeconds=t.TargetTime}));}
            state.StopDrainAfterFinalTarget(data.LastCatchOpportunityTime);double final=state.Value;state.Advance(data.DurationSeconds);
            Check(final>0&&Math.Abs(state.Value-final)<1e-8&&data.DrainProfile!.MultiplierAt(data.DurationSeconds-.001)==0,"E final resolved target stops hunger throughout actual audio outro");
            var hud=new Control[]{gameplay.Gauge,gameplay.Combo,gameplay.Feedback.GetParent<Control>(),gameplay.SettingsButton.GetParent<Control>()};var rects=hud.Select(c=>c.GetGlobalRect()).ToArray();
            var focus=gameplay.Presentation.Events.First(e=>e.Type=="FOLLOW_WAVE"&&e.Priority==85);double time=focus.Time+focus.Duration*.5;gameplay.Signals.Present(time);gameplay.Presentation.Present(time);await Frames();gameplay.Presentation.Present(time);
            Check(gameplay.Presentation.CameraOffset.Length()>50&&gameplay.Presentation.CameraZoom>1.08,"F wave close-up visibly pans and zooms");
            Check(hud.Select((c,i)=>c.GetGlobalRect().IsEqualApprox(rects[i])).All(v=>v),"F HUD coordinates stay fixed");
            if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("res://artifacts/release-config/correction-wave-closeup.png");}
            gameplay.Interference.Trigger("sardine",time+2);gameplay.Interference.Present(time+3);gameplay.Presentation.Present(time+3);
            Check(gameplay.Interference.GetEffect("sardine").IsActive&&gameplay.Interference.GetEffect("sardine").Vortex!.Strength>0,"G non-Ship-Horn vortex works");
            Check(data.Phrases.Take(10).Zip(data.Phrases.Skip(1).Take(9),(a,b)=>a.FromLeft!=b.FromLeft).All(v=>v)&&phrase.TargetEvents.Length==3,"ten phrases strictly alternate; rapid response stays one side/phrase");
            foreach(var id in new[]{"song_1","song_2","song_3","song_4","custom_shark_pool","custom_part_of_your_world","custom_under_the_sea"})
            {var d=GameplayData.Load(id);Check(d.Phrases.Take(10).Zip(d.Phrases.Skip(1).Take(9),(a,b)=>a.FromLeft!=b.FromLeft).All(v=>v),"direction sequence "+id);}
            await Remove(gameplay);GD.Print("REQUESTED A-G AND SIDE SEQUENCE PASSED; stopping.");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
