using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Rhythm;
using Gamejam2.Save;
using Gamejam2.Settings;
using Gamejam2.Tutorial;
namespace Gamejam2.Debugging;
public partial class TutorialMasteryChecks : Node
{
    private int _checks;
    private TutorialGameplayScreen _game=null!;
    private void Check(bool ok,string name){if(!ok)throw new Exception(name);_checks++;GD.Print("PASS "+name);}
    private async Task Frame(){GetWindow().GrabFocus();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private async Task Until(Func<bool> condition,double seconds=25){ulong end=Time.GetTicksMsec()+(ulong)(seconds*1000);while(!condition()){if(Time.GetTicksMsec()>end)throw new TimeoutException("mastery fixture");await Frame();}}
    private void Press()=>_game._Input(new InputEventKey{PhysicalKeycode=Key.Space,Pressed=true});
    private async Task Hit(int index,double error){await Until(()=>_game.Rhythm.SongTimeSeconds>=_game.Data.Phrases[index].TargetSeconds+error);Press();}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            GetWindow().GrabFocus();SaveStore.Initialize("/private/tmp/bite-mastery-"+Time.GetTicksUsec()+".cfg");ProgressService.Initialize();SettingsService.Initialize();SettingsService.InputOffsetSeconds=SettingsService.VisualOffsetSeconds=0;
            _game=GD.Load<PackedScene>("res://game/tutorial/TutorialGameplay.tscn").Instantiate<TutorialGameplayScreen>();AddChild(_game);
            var present=typeof(TutorialGameplayScreen).GetMethod("PresentFish",BindingFlags.Instance|BindingFlags.NonPublic)!;
            _game.SetProcess(false);_game.Rhythm.SetProcess(false);
            var fixture=new Node2D();AddChild(fixture);
            var backdrop=new ColorRect{Color=new Color(.75f,.85f,.9f),Size=new Vector2(1920,1080)};fixture.AddChild(backdrop);
            foreach(bool fromLeft in new[]{true,false})
            {
                var p=_game.Data.Phrases.First(p=>p.FromLeft==fromLeft);double time=(p.StartSeconds+p.TargetSeconds)/2;
                present.Invoke(_game,new object[]{time});var before=_game.Fish.GlobalPosition;
                present.Invoke(_game,new object[]{time+.01});var after=_game.Fish.GlobalPosition;
                Check(_game.Fish.Visible&&(fromLeft?after.X>before.X:after.X<before.X),"actual prey moves toward mouth "+fromLeft);
                Check(_game.Fish.FlipH==fromLeft,"LEFT-facing artwork faces actual travel direction "+fromLeft);
                var copy=new Sprite2D{Texture=_game.Fish.Texture,FlipH=_game.Fish.FlipH,Position=new Vector2(fromLeft?500:1400,500),Scale=Vector2.One*7};fixture.AddChild(copy);
                present.Invoke(_game,new object[]{p.TargetSeconds});Check(_game.Fish.GlobalPosition.DistanceTo(_game.Shark.MouthImpact.GlobalPosition)<5,"fish arrives at mouth exactly at chart target "+fromLeft);
            }
            await Frame();if(DisplayServer.GetName()!="headless"){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);GetViewport().GetTexture().GetImage().SavePng("res://artifacts/tutorial-mastery/fish-directions.png");}
            fixture.QueueFree();_game.SetProcess(true);_game.Rhythm.SetProcess(true);
            await Until(()=>_game.PracticeInputEnabled);
            Check(_game.Profile.RequiredSuccesses==3,"mastery requires three GOOD+");
            // Six actual chart encounters on the current music clock. No fixture seek/restart.
            double beat=60/_game.Profile.Bpm,bar=4*beat;
            double start=_game.Profile.BeatOffsetSeconds+Math.Ceiling((_game.Rhythm.SongTimeSeconds+.4-_game.Profile.BeatOffsetSeconds)/bar)*bar;
            var template=_game.Data.Phrases[0];
            var plan=Enumerable.Range(0,6).Select(i=>template with {Index=i,StartSeconds=start+i*6*beat,TargetSeconds=start+i*6*beat+4*beat,Cues=template.CueSlots.Select(slot=>start+i*6*beat+slot*.5*beat).ToArray()}).ToArray();
            typeof(GameplayScreen).GetMethod("InstallPracticeData",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(_game,new object[]{GameplayData.TutorialFromPlan(_game.Data,plan)});
            typeof(TutorialGameplayScreen).GetField("_segmentStart",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(_game,start);
            typeof(TutorialGameplayScreen).GetField("_segmentEnd",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(_game,plan[^1].TargetSeconds+.5);
            int musicStarts=0;_game.Rhythm.AudioPlaybackStarted+=_=>musicStarts++;
            int lesson=_game.LessonIndex;
            await Hit(0,.1);Check(_game.SuccessCount==1&&_game.Rhythm.LastResult?.Judgment==RhythmJudgment.Good,"attempt 1 GOOD earns one");
            await Hit(1,-.23);Check(_game.SuccessCount==1&&_game.Rhythm.LastResult?.Judgment==RhythmJudgment.Miss,"attempt 2 MISS earns zero");
            await Hit(2,0);Check(_game.SuccessCount==2,"attempt 3 PERFECT earns second");
            await Hit(3,.1);
            Check(_game.SuccessCount==3&&_game.LessonState==TutorialGameplayScreen.TutorialLessonState.Mastered,"attempt 4 third GOOD+ immediately marks Mastered");
            Check(_game.SkippedByMastery.Order().SequenceEqual(new[]{4,5}),"attempts 5 and 6 immediately SkippedByMastery");
            Check(!_game.PracticeInputEnabled&&_game.Mastery.Text=="● ● ●","completed dots retain feedback and stop further input");
            Check(_game.Juice.CatchBursts.Count>0&&_game.Shark.Art.Animation=="bite"&&_game.Feedback.Art.Visible,"third success Bite/burst/judgment not cut off");
            double mastered=_game.MasteredAtSeconds,transition=_game.NextLessonTransitionSeconds;
            Check(transition>=mastered+.85&&transition<mastered+.85+bar+.01,"nearest next bar after full short feedback");
            Check(Math.Abs((transition-_game.Profile.BeatOffsetSeconds)/bar-Math.Round((transition-_game.Profile.BeatOffsetSeconds)/bar))<1e-8,"transition is on actual musical bar");
            var stats=_game.Satiety.CreateResult();double value=_game.Satiety.Value;int judgments=0;
            void Result(RhythmJudgmentResult _)=>judgments++;_game.Rhythm.JudgmentResolved+=Result;
            typeof(RhythmController).GetMethod("AdvanceEvents",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(_game.Rhythm,new object[]{plan[^1].TargetSeconds+.4,plan[^1].TargetSeconds+.4});
            _game.Rhythm.JudgmentResolved-=Result;
            Check(judgments==0&&_game.Satiety.CreateResult().Miss==stats.Miss&&_game.Satiety.Value==value&&_game.Satiety.MissStreak==0,"skipped future deadlines do not create MISS/stats/streak/satiety effects");
            _game.Signals.Present(plan[^1].TargetSeconds+.4);
            Check(!_game.Signals.LeftLane.Slots.Concat(_game.Signals.RightLane.Slots).SelectMany(s=>s.ActivePulses).Any(w=>w.EncounterId is 4 or 5),"skipped attempts never spawn waves including resolved-target exception");
            await Until(()=>_game.LessonIndex!=lesson,5);
            Check(_game.Rhythm.SongTimeSeconds<plan[4].TargetSeconds&&_game.SuccessCount==0,"next lesson starts before unused attempt 5 target (clock="+_game.Rhythm.SongTimeSeconds+", unused="+plan[4].TargetSeconds+", transition="+transition+", successes="+_game.SuccessCount+")");
            Check(musicStarts==0&&_game.MusicCheckpointRestarts==0&&_game.Music.Playing,"mastery never restarts/seeks/stops tutorial music");
            Check(_game.Juice.CatchBursts.Count==0,"completed third catch finished before next chart install");
            Check(_game.Data.Phrases[0].StartSeconds>=transition&&_game.LessonState==TutorialGameplayScreen.TutorialLessonState.Demonstrating,"next lesson uses future real chart demonstration");
            _game.StopGameplay();_game.QueueFree();await Frame();await Frame();
            GD.Print("TUTORIAL MASTERY VERIFIED "+_checks);GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());_game?.StopGameplay();GetTree().Quit(1);}
    }
}
