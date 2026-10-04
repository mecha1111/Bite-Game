using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Gameplay;
using Gamejam2.Rhythm;
using Gamejam2.Settings;
namespace Gamejam2.Debugging;
public partial class PolishChecks : Node
{
    private int _checks;
    private void Check(bool ok,string label){if(!ok)throw new Exception(label);GD.Print("PASS "+label);_checks++;}
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private void Capture(string name){RenderingServer.ForceDraw();GetViewport().GetTexture().GetImage().SavePng("/private/tmp/bite-polish-"+name+".png");}
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            Gamejam2.Save.SaveStore.Initialize("/private/tmp/bite-polish-check.cfg");Gamejam2.Save.ProgressService.Initialize();SettingsService.Initialize();SettingsService.InputOffsetSeconds=SettingsService.VisualOffsetSeconds=0;SettingsService.SetResolution(2);GetWindow().GrabFocus();
            for(int song=1;song<=4;song++)
            {
                var data=GameplayData.Load("song_"+song);var state=new SatietyState(data);
                foreach(var prey in data.Phrases){state.Advance(prey.TargetSeconds);Check(state.Value>0,"all PERFECT survives to target "+song+":"+prey.Index);state.Resolve(new(RhythmJudgment.Perfect,0,new()));}
                state.Advance(data.DurationSeconds);Check(state.Value>=800,"all PERFECT can CLEAR song "+song);GD.Print($"ECONOMY song_{song} Start={data.StartingSatiety} Count={data.Phrases.Count} Recovery={data.BaseRecovery*data.Phrases.Count} Drain={data.DrainPerSecond*data.DurationSeconds:F6} Final={state.Value:F6}");
                var penalty=new SatietyState(data);penalty.EmptyBite();Check(penalty.LastMissPenalty==0,"single MISS no extra loss");penalty.EmptyBite();Check(penalty.LastMissPenalty==5,"second MISS five");penalty.EmptyBite();Check(penalty.LastMissPenalty==10,"third MISS ten");
            }
            var title=GD.Load<PackedScene>("res://game/title/Title.tscn").Instantiate();AddChild(title);await Wait(3);Capture("title");title.QueueFree();await Wait(.1);
            var game=GD.Load<PackedScene>("res://game/gameplay/Gameplay.tscn").Instantiate<GameplayScreen>();game.SongId="song_1";AddChild(game);game.SetProcessInput(false);await Wait(.4);game.Rhythm.PauseSong();game.SetProcess(false);
            game.Environment.Present(2,false,.016);await Wait(.08);Capture("world-a");game.Environment.Present(5,false,.016);await Wait(.08);Capture("world-b");
            for(int frame=0;frame<20;frame++)
            {
                game.Shark.Bite(10);game.Shark.Present(10+(frame+.1)/20*.36);
                Check(Math.Abs(game.Shark.Art.Position.Y)<.01,"frame bottom position stable "+frame);
            }
            game.Shark.Present(20);Check(game.Shark.Art.Animation=="idle","bite returns idle");
            var baseY=game.Shark.Position.Y;game.Shark.Present(21);Check(game.Shark.Position.Y==baseY&&game.Shark.Art.Scale.Y>1.92,"idle breath on bottom-anchored child");
            var vortex=game.Interference.GetEffect("sardine").Vortex!;
            vortex.Begin(10);vortex.Present(10.25);Check(vortex.Visible&&vortex.Strength>.4f&&vortex.Strength<.6f,"current forms smoothly");
            vortex.Present(11);Check(vortex.Strength==1,"current full strength");await Wait(.08);Capture("sardine");
            vortex.Present(14.75);Check(vortex.Strength>.4f&&vortex.Strength<.6f,"current recedes smoothly");vortex.Present(15);Check(!vortex.Visible&&vortex.Strength==0,"current ends at five seconds");
            game.Feedback.Visible=false;
            foreach(var grade in new[]{RhythmJudgment.Perfect,RhythmJudgment.Good,RhythmJudgment.Bad})
            {
                game.Juice.Reset(game.Data);game.Shark.Bite(30);game.Shark.Present(30.05);game.Juice.Judgment(grade,30,1);
                var burst=game.Juice.CatchBursts.Single();burst.Present(30.17);Check(burst.PieceCount==(grade==RhythmJudgment.Perfect?10:grade==RhythmJudgment.Good?7:4),"supplied debris count "+grade);
                Check(burst.DebrisRoot.GetChildren().OfType<CatchDebrisPiece>().Count(p=>p.Velocity.Y>0)>=Math.Floor(burst.PieceCount*.6),"mostly downward "+grade);
                Check(burst.GlobalPosition==game.Shark.MouthImpact.GlobalPosition&&game.Juice.ZIndex==3,"catch actual mouth inside world "+grade);
                Check(burst.BloodPieceCount==(grade==RhythmJudgment.Perfect?4:grade==RhythmJudgment.Good?3:grade==RhythmJudgment.Bad?1:0),"original blood accents "+grade);
                burst.Present(30.25);await Wait(.08);Capture("catch-"+grade);burst.Present(30.7);Check(burst.Finished,"catch disappears before next cue "+grade);
            }
            game.Juice.Reset(game.Data);game.Juice.Judgment(RhythmJudgment.Miss,40,0);Check(game.Juice.CatchBursts.Count==0&&game.Juice.LastCatchImpact==null,"MISS no fish debris or successful ring");
            for(int i=0;i<5;i++){game.Juice.Judgment(RhythmJudgment.Perfect,50+i*.15,i);game.Juice.Present(50+i*.15,50+i*.15,false,false,.016);}
            await Wait(.08);Capture("dense");game.Juice.Present(52,52,false,false,.016);Check(game.Juice.CatchBursts.Count==0,"dense chain completely cleans up");
            typeof(SatietyState).GetProperty("Value")!.SetValue(game.Satiety,0d);game._Process(.016);await Wait(1.1);Check(game.GameOver.IsRevealed&&game.GameOver.Gauge.DisplayedSatiety==0,"starvation empty gauge actions revealed");Check(game.GameOver.Gauge.SharkImage.Texture.ResourcePath.Contains("투명한"),"Game Over correct hollow shark resource");Capture("game-over");game.QueueFree();
            GD.Print($"POLISH CHECKS OK {_checks}");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
