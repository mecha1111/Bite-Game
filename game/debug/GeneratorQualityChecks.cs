using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Godot;
using Gamejam2.CustomGenerator;
using Gamejam2.Data;
using Gamejam2.Gameplay;
using Gamejam2.Rhythm;
using Gamejam2.Save;
using Gamejam2.Startup;
using Gamejam2.Lobby;
namespace Gamejam2.Debugging;
/// <summary>Music evidence and runtime parity checks. Deletes only maps this fixture creates.</summary>
public partial class GeneratorQualityChecks : Node
{
    private SceneRouter? _router;private string? _createdId;
    private void Check(bool value,string name){if(!value)throw new Exception(name);GD.Print("PASS "+name);}
    private async Task Frames(){GetWindow().GrabFocus();for(int i=0;i<4;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private Analysis Fixture(double duration,Attack[] attacks,StructureChange[] changes)=>new(duration,70,280,0,attacks,new[]{new MusicSection(0,duration,1,"active")},Array.Empty<double>(),Array.Empty<double>(),new[]{70.0,140,280},Array.Empty<EnergyPoint>(),changes);
    private void NumericChecks()
    {
        var patterns=PatternModel.Load();
        var competing=Fixture(20,new[]{new Attack(4,2,1,1),new Attack(5,10,8,1),new Attack(8,2,1,1),new Attack(12,3,1,3)},Array.Empty<StructureChange>());
        var picked=ChartGenerator.Compose(competing,patterns);
        Check(picked.Any(t=>t.Time==5)&&picked.All(t=>t.Time!=4),"strong musical payoff wins over earlier weaker conflicting attack");
        Check(picked.All(t=>competing.Attacks.Any(a=>a.Time==t.Time)),"no metronome-created targets");
        Check(picked.Select((t,i)=>t.Left==(i%2==0)).All(v=>v),"single-target phrases alternate L/R");
        Check(picked.Zip(picked.Skip(1),(a,b)=>b.Start-a.Time>=.30-1e-8).All(v=>v),"one prey at a time with readable lead-in");
        var attacks=new[]{new Attack(30,4,5,1),new Attack(60,3,1,4),new Attack(85,6,8,1)};
        var music=Fixture(100,attacks,new[]{new StructureChange(30,"drop",3),new StructureChange(60,"section",2),new StructureChange(85,"drop",4)});
        var targets=attacks.Select((a,i)=>new GeneratedTarget(a.Time,a.Time-1.2,"m1",i%2==0,a.Strength){AccentClass="section_impact"}).ToArray();
        var before=targets.Select(t=>t.Time).ToArray();var interference=InterferenceGenerator.Compose(music,targets);var camera=CameraGenerator.Compose(music,targets,interference);
        Check(interference.Length>0&&camera.Length>0,"low BPM confidence does not erase valid structural interference/camera");
        Check(interference.All(e=>e.Time>=28),"opening stays free of interference");
        Check(interference.Where(e=>e.Time<80).GroupBy(e=>e.Time).All(g=>g.Count()==1),"middle uses single interference");
        Check(interference.GroupBy(e=>e.Time).All(g=>g.Count()<=2)&&interference.Any(e=>e.Reason=="limited_climax_pair"),"only late structural climax permits a limited pair");
        Check(targets.Select(t=>t.Time).SequenceEqual(before),"interference and camera never move TargetTime");
        var cameraMusic=Fixture(140,Array.Empty<Attack>(),new[]{new StructureChange(20,"drop",4),new StructureChange(50,"buildup",3),new StructureChange(80,"section",2),new StructureChange(110,"release",1)});
        var cameraTargets=new[]{20.0,50,80,110}.Select((t,i)=>new GeneratedTarget(t,t-1.2,"m1",i%2==0,3)).ToArray();
        var shots=CameraGenerator.Compose(cameraMusic,cameraTargets,Array.Empty<GeneratedInterference>());
        Check(new[]{"SHARK_CLOSEUP","SIDE_PAN","SECTION_TRANSITION","PULLBACK"}.All(type=>shots.Any(s=>s.Type==type)),"camera selects shot by structural role");
        Check(shots.All(s=>s.Duration>s.Hold&&s.Hold>0)&&shots.Zip(shots.Skip(1),(a,b)=>b.Time>=a.Time+a.Duration+7).All(v=>v),"sparse MOVE HOLD RETURN with no per-prey camera alternation");
        // Two locally synthesized acoustic structures: quiet bridge is real silence, not a missing grid slot.
        const int rate=11025;var samples=new float[rate*48];var random=new Random(42);
        for(double time=2;time<47;time+=time<32?.5:.25)
        {
            if(time>=20&&time<28)continue;
            bool low=((int)(time*2)%4)==0;for(int k=0;k<rate/12;k++)
            {int index=(int)(time*rate)+k;if(index>=samples.Length)break;double tone=low?Math.Sin(2*Math.PI*80*k/rate):(random.NextDouble()*2-1);samples[index]+=(float)(tone*Math.Exp(-k/(rate*.018))*(time>=32?.8:.4));}
        }
        var analyzed=MusicAnalysis.Analyze(samples,48);var generated=ChartGenerator.Compose(analyzed,patterns);
        Check(analyzed.PulseChoices.Length>=3&&analyzed.PulseChoices.Any(c=>c.Bpm==analyzed.PulseBpm),"compares half/double-time pulse candidates");
        Check(analyzed.Bpm>=115&&analyzed.Bpm<=125,"measured 120 BPM acoustic fixture does not collapse to minimum BPM");
        Check(generated.All(t=>analyzed.Attacks.Any(a=>a.Time==t.Time))&&generated.All(t=>t.Time<20.15||t.Time>=28),"targets follow acoustic attacks and preserve silent bridge");
        Check(generated.Select(t=>t.Pattern).Distinct().Count()>1,"acoustic sections produce existing pattern variation");
    }
    private double Simulate(GameplayData data,bool mixed)
    {
        var state=new SatietyState(data);
        for(int i=0;i<data.Phrases.Count;i++)
        {
            double t=data.Phrases[i].TargetSeconds;state.Advance(t);Check(state.Value>0,"survives scheduled drain "+(mixed?"mixed":"perfect")+" "+i);
            int choice=i%20;var judgment=!mixed||choice<12?RhythmJudgment.Perfect:choice<17?RhythmJudgment.Good:choice<19?RhythmJudgment.Bad:RhythmJudgment.Miss;
            state.Resolve(new RhythmJudgmentResult(judgment,0,new RhythmEventData{TimeSeconds=t}));
        }
        state.StopDrainAfterFinalTarget(data.Phrases[^1].TargetSeconds);state.Advance(data.DurationSeconds);return state.Value;
    }
    private async Task Run()
    {
        int exit=0;
        try
        {
            NumericChecks();_router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();_router.SavePath="user://logs/generator-quality-"+OS.GetProcessId()+".cfg";AddChild(_router);await Frames();
            var map=await LocalMapGenerator.Generate(this,GeneratedMapTestFixture.Source,text=>GD.Print(text));_createdId=map.SongId;
            var data=GameplayData.Load(map.SongId);var report=JsonDocument.Parse(FileAccess.GetFileAsString("user://custom_maps/"+map.SongId+"/analysis.json"));
            double perfect=Simulate(data,false),mixed=Simulate(data,true);
            Check(perfect>=data.ClearThreshold&&mixed>=data.ClearThreshold,"actual runtime All PERFECT and representative mixed both clear");
            Check(Math.Abs(perfect-report.RootElement.GetProperty("PerfectFinal").GetDouble())<.02&&Math.Abs(mixed-report.RootElement.GetProperty("MixedFinal").GetDouble())<.02,"generation simulation matches real SatietyState");
            var camera=StagePresentationChart.Load(map.SongId,map.Duration);var effects=LocalCsv.Read(map.InterferencePath);
            Check(data.TargetCount>0&&camera.Length>0&&effects.Count>0,"real local MP3 produces chart, camera and interference");
            var targets=report.RootElement.GetProperty("Targets").EnumerateArray().Select(t=>t.GetProperty("Time").GetDouble()).ToArray();
            Check(targets.Zip(data.Phrases,(a,b)=>Math.Abs(a-b.TargetSeconds)<.000001).All(v=>v),"runtime uses original selected musical target times");
            var evidence=new {map.Title,map.MusicBpm,map.GameplayBpm,Targets=data.TargetCount,Patterns=data.Phrases.GroupBy(p=>p.PatternId).ToDictionary(g=>g.Key,g=>g.Count()),Camera=camera.Select(c=>new{c.Time,c.Type,c.Duration,c.PanPercent}),Interference=effects,Perfect=perfect,Mixed=mixed,map.DrainPerSecond,Report=report.RootElement};
            string reportPath=OS.HasFeature("bite_release")?"user://logs/generator-quality-report.json":"res://artifacts/generator-quality-report.json";
            using(var file=FileAccess.Open(reportPath,FileAccess.ModeFlags.Write))file.StoreString(JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));
            _router.GoToLobby();await Frames();_router.ScreenHost!.GetChild<LobbyScreen>(0).SelectCategory(true);_router.GoToGameplay(map.StageId);await Frames();var game=_router.ScreenHost.GetChild<GameplayScreen>(0);
            Check(game.RunState==GameplayScreen.GameplayRunState.Playing&&game.Music.Playing&&game.Presentation.Events.Length==camera.Length,"generated MP3 launches with camera loaded");
            var pair=effects.GroupBy(r=>r["time_seconds"]).FirstOrDefault(g=>g.Count()==2);
            if(pair!=null){game.SuspendForSettings();game.Interference.Present(LocalCsv.Number(pair.Key)+.01);Check(pair.All(e=>game.Interference.GetEffect(e["type"]).IsActive),"limited climax pair actually runs both existing interference effects");}
            game.StopGameplay();
            _router.GoToLobby();await Frames();_router.ScreenHost.GetChild<LobbyScreen>(0).SelectCategory(false);_router.GoToGameplay("stage_1");await Frames();game=_router.ScreenHost.GetChild<GameplayScreen>(0);
            Check(game.Data.TargetCount==64&&game.RunState==GameplayScreen.GameplayRunState.Playing,"shared camera/drain services still load Main Stage 1");game.StopGameplay();
            GD.Print($"GENERATOR QUALITY VERIFIED targets={data.TargetCount} shots={camera.Length} interference={effects.Count} perfect={perfect:F2} mixed={mixed:F2}");
        }
        catch(Exception error){GD.PushError(error.ToString());exit=1;}
        finally{if(_router!=null){_router.QueueFree();await Frames();}if(_createdId!=null)LocalMapGenerator.Delete(_createdId);GetTree().Quit(exit);}
    }
}
