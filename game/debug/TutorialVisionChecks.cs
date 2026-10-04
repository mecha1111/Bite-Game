using System;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Save;
using Gamejam2.Settings;
using Gamejam2.Tutorial;
namespace Gamejam2.Debugging;
public partial class TutorialVisionChecks : Node
{
    private int _checks;
    private void Check(bool ok,string label){if(!ok)throw new Exception(label);_checks++;GD.Print("PASS "+label);}
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private Image Capture(string name){RenderingServer.ForceDraw();var image=GetViewport().GetTexture().GetImage();image.SavePng("res://artifacts/song-end-satiety/"+name+".png");return image;}
    private static float Light(Color c)=>c.R*.2126f+c.G*.7152f+c.B*.0722f;
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            GetWindow().GrabFocus();SaveStore.Initialize("/private/tmp/bite-vision-"+Time.GetTicksUsec()+".cfg");ProgressService.Initialize();SettingsService.Initialize();SettingsService.InputOffsetSeconds=SettingsService.VisualOffsetSeconds=0;
            var game=GD.Load<PackedScene>("res://game/tutorial/TutorialGameplay.tscn").Instantiate<TutorialGameplayScreen>();AddChild(game);game.SetProcess(false);game.Rhythm.SetProcess(false);
            var presentFish=typeof(TutorialGameplayScreen).GetMethod("PresentFish",BindingFlags.NonPublic|BindingFlags.Instance)!;
            foreach(var p in game.Data.Phrases)
            {
                double time=(p.StartSeconds+p.TargetSeconds)*.5;
                presentFish.Invoke(game,new object[]{time});var before=game.Fish.GlobalPosition;
                presentFish.Invoke(game,new object[]{time+.01});var after=game.Fish.GlobalPosition;
                Check(game.Fish.Visible&&Math.Abs(after.X-before.X)>.01&&game.Fish.FlipH==(after.X>before.X),"LEFT-facing source mirrors actual movement "+(p.FromLeft?"LEFT to RIGHT":"RIGHT to LEFT"));
                presentFish.Invoke(game,new object[]{p.TargetSeconds});
                Check(game.Fish.GlobalPosition.DistanceTo(game.Shark.MouthImpact.GlobalPosition)<5,"sighted prey reaches chart mouth at TargetTime "+p.Index);
            }
            // A uniform world fixture checks the rendered material, rather than repeating shader math.
            var flat=new ColorRect{Color=new Color(.4f,.6f,.8f),ZIndex=0,MouseFilter=Control.MouseFilterEnum.Ignore};AddChild(flat);flat.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var mask=new TutorialVision{Material=(ShaderMaterial)game.Vision.Material.Duplicate(),ZIndex=1,MouseFilter=Control.MouseFilterEnum.Ignore};AddChild(mask);mask.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var ui=new ColorRect{Color=Colors.White,ZIndex=5,Position=new Vector2(20,20),Size=new Vector2(80,40)};AddChild(ui);
            game.Visible=false;mask.Present(1,0);await Wait(.05);var image=Capture("vision-gradient-fixture");int width=image.GetWidth(),height=image.GetHeight(),y=height/2;
            float previous=Light(image.GetPixel((int)(width*.3),y));bool smooth=true;
            for(int x=(int)(width*.3)+1;x<width*.72;x++){float current=Light(image.GetPixel(x,y));if(Math.Abs(current-previous)>.025)smooth=false;previous=current;}
            Check(smooth,"rendered center has continuous feather, no hard black seam");
            Check(Light(image.GetPixel((int)(width*.8),y))<Light(image.GetPixel((int)(width*.2),y))*.15,"far right is visibly blind");
            Check(Light(image.GetPixel((int)(width*.85),(int)(height*.04)))<Light(image.GetPixel((int)(width*.85),y)),"damaged-side corners add restrained vignette");
            Check(image.GetPixel(40,30).R>.99,"vision material never darkens higher-layer HUD");
            mask.Present(0,0);await Wait(.05);image=Capture("vision-full-fixture");
            Check(Light(image.GetPixel(width/2,height/2))>.02&&Light(image.GetPixel(width/2,height/2))<.10,"complete blindness retains extremely dark navy world, not pure black");
            flat.QueueFree();mask.QueueFree();ui.QueueFree();game.Visible=true;await Wait(.05);
            game.SetProcess(true);game.Rhythm.SetProcess(true);Check(game.DebugJump("RightEyeLoss"),"real attack checkpoint starts");await Wait(2);
            Check(game.RightEyeVisibility==0&&game.LeftEyeVisibility==1,"story attack leaves right blind / left visible");
            Check(game.Attack.ZIndex<game.GetNode<Control>("TutorialHud").ZIndex&&game.Vision.ZIndex<game.Shark.GetParent<Control>().ZIndex,"attack/mask are world-only, shark senses and tutorial HUD above vision");
            game.SuspendForSettings();Capture("tutorial-right-eye");
            game.DebugJump("LeftEyeFade");await Wait(.5);game.SuspendForSettings();Capture("tutorial-left-fade");
            Check(game.LeftEyeVisibility<.9&&game.LeftEyeVisibility>.65,"left degradation uses existing gradual visibility progression");
            game.DebugJump("FinalExam");await Wait(.2);game.SuspendForSettings();Capture("tutorial-full-blindness");
            Check(!game.Fish.Visible&&game.Attack.Blackout==0&&game.WaveSenseStrength==1,"final exam keeps wave-only world readable without full-screen black cut");
            game.StopGameplay();game.QueueFree();await Wait(.05);
            GD.Print($"TUTORIAL VISION VERIFIED {_checks}");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
