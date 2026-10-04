using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Rhythm;
namespace Gamejam2.Debugging;
public partial class SatietySectionChecks : Node
{
    private int _checks;
    private void Check(bool ok,string name){if(!ok)throw new Exception(name);_checks++;GD.Print("PASS "+name);}
    private void Near(double a,double b,string name)=>Check(Math.Abs(a-b)<1e-5,name);
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            var windows=new RhythmJudgmentWindows().CreateSnapshot();
            foreach(var (error,grade) in new[]{(0d,RhythmJudgment.Perfect),(.060,RhythmJudgment.Perfect),(.070,RhythmJudgment.Perfect),(.090,RhythmJudgment.Good),(.130,RhythmJudgment.Good),(.140,RhythmJudgment.Good),(.170,RhythmJudgment.Bad),(.195,RhythmJudgment.Bad),(.200,RhythmJudgment.Bad),(.210,RhythmJudgment.Miss)})
                foreach(double sign in new[]{-1d,1d})Check(windows.Classify(error*sign)==grade,"symmetric timing "+error*sign);
            var scene=GD.Load<PackedScene>("res://game/gameplay/SatietyGauge.tscn");
            var gauge=scene.Instantiate<SatietyGauge>();AddChild(gauge);
            foreach(double value in new[]{0d,250,500,799,800,999,1000,1050,1100})
            {
                gauge.ResetDisplay(value);gauge.Present(value,1100,800);
                Near(gauge.DisplayedPercent,value/10,"percent "+value);
                Near(gauge.BodyFillRatio,Math.Clamp(value/1000,0,1),"body fill "+value);
                Near(gauge.OverfillStrength,Math.Clamp((value-1000)/100,0,1),"overfill "+value);
                Near(gauge.Threshold.AnchorLeft,.8,"threshold "+value);
                Check(gauge.IsClear==(value>=800),"clear state "+value);
            }
            gauge.Present(-10,1100,800);Near(gauge.BodyFillRatio,0,"negative clamp");
            gauge.ResetDisplay(500);gauge.Present(500,1100,800,1);gauge.NotifyGain(1.1,RhythmJudgment.Perfect);
            gauge.Present(650,1100,800,1.1);Near(gauge.DisplayedValue,500,"gain starts from previous body fill");Near(gauge.DisplayedPercent,65,"actual percent immediate");
            gauge.Present(650,1100,800,1.21);Check(gauge.DisplayedValue>500&&gauge.DisplayedValue<650,"gain interpolates");
            gauge.Present(650,1100,800,1.34);Near(gauge.DisplayedValue,650,"gain settles after 220ms");
            float previous=2;
            foreach(var grade in new[]{RhythmJudgment.Perfect,RhythmJudgment.Good,RhythmJudgment.Bad}){gauge.NotifyGain(2,grade);gauge.Present(650,1100,800,2);Check(gauge.RecoveryPulse>0&&gauge.RecoveryPulse<previous,"graded catch pulse "+grade);previous=gauge.RecoveryPulse;}
            gauge.NotifyMiss();gauge.Present(650,1100,800,2);Near(gauge.RecoveryPulse,0,"MISS cancels positive pulse");
            gauge.NotifyPenalty(3);gauge.Present(600,1100,800,3.1);double paused=gauge.DisplayedValue;gauge.Present(600,1100,800,3.1);Near(gauge.DisplayedValue,paused,"paused Song Clock keeps gauge still");
            gauge.Present(600,1100,800,3.2);Near(gauge.DisplayedValue,600,"loss settles after 180ms");
            var mask=(Texture2D)((ShaderMaterial)gauge.Frame.Material).GetShaderParameter("interior_mask");
            Check(mask.GetSize()==gauge.Frame.Texture.GetSize(),"art and interior mask have identical dimensions");
            int[] counts={43,32,49,46};
            for(int song=1;song<=4;song++)
            {
                var data=GameplayData.Load("song_"+song);var profile=data.DrainProfile!;
                Check(data.Phrases.Count==counts[song-1],"chart unchanged song "+song);
                Near(profile.Spans.Sum(s=>(s.End-s.Start)*s.Multiplier),profile.TotalEffectiveSeconds,"compiled integral song "+song);
                var state=new SatietyState(data);var split=new SatietyState(data);
                double end=data.Phrases[0].TargetSeconds;
                state.Advance(end);for(double t=0;t<end;t+=.017)split.Advance(t);split.Advance(end);
                Near(state.Value,split.Value,"frame independent section drain song "+song);
                double value=split.Value;split.Advance(end);split.Advance(end-.1);Near(split.Value,value,"pause and backwards time never double drain song "+song);
                foreach(var gap in profile.FoodlessGaps.Where(g=>g.End-g.Start>=3))Check(profile.MultiplierAt((gap.Start+gap.End)/2)<=.300001,"foodless safety song "+song);
                var ctor=typeof(SatietyDrainProfile).GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).Single();
                var fallback=(SatietyDrainProfile)ctor.Invoke(new object[]{data.DurationSeconds,Array.Empty<SatietyDrainSection>(),data.Phrases,.25,3d,.3});
                var longest=fallback.FoodlessGaps.OrderByDescending(g=>g.End-g.Start).First();
                Near(fallback.MultiplierAt((longest.Start+longest.End)/2),.3,"unmarked foodless section safety song "+song);
                var prey=data.Phrases[0];Near(fallback.MultiplierAt(prey.StartSeconds+.001),1,"scheduled active encounter full drain song "+song);
                state.Resolve(new(RhythmJudgment.Miss,0,new RhythmEventData()));Near(fallback.MultiplierAt(prey.StartSeconds+.001),1,"early MISS cannot manufacture hunger protection song "+song);
                var perfect=new SatietyState(data);
                foreach(var p in data.Phrases){perfect.Advance(p.TargetSeconds);perfect.Resolve(new(RhythmJudgment.Perfect,0,new RhythmEventData()));}
                perfect.Advance(data.DurationSeconds);Check(perfect.Value>=950&&perfect.Value<=1100,"capped actual All Perfect clears song "+song);
                var starving=new SatietyState(data);starving.Advance(data.DurationSeconds);
                Check(starving.Value==0&&starving.DepletedAtSongSeconds.HasValue,"starvation remains possible song "+song);
                Near(profile.EffectiveSecondsAt(starving.DepletedAtSongSeconds!.Value)*data.DrainPerSecond,500,"exact starvation timestamp across sections song "+song);
            }
            gauge.QueueFree();
            // Scene-authored gauges at all requested values, at gameplay-like display scale.
            var gallery=new Control();AddChild(gallery);
            double[] values={0,250,500,799,800,999,1000,1050,1100};
            for(int i=0;i<values.Length;i++){var g=scene.Instantiate<SatietyGauge>();gallery.AddChild(g);g.Position=new Vector2(30+(i%3)*620,20+(i/3)*340);g.Size=new Vector2(580,300);g.ResetDisplay(values[i]);g.Present(values[i],1100,800);}
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
            if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("res://artifacts/satiety-sections/gauge-gallery.png");}
            GD.Print("SATIETY SECTIONS VERIFIED "+_checks);GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
