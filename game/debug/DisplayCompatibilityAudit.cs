using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Godot;
using Gamejam2.Startup;
using Gamejam2.Settings;
using Gamejam2.Save;
using Gamejam2.Title;
using Gamejam2.Lobby;
using Gamejam2.Calibration;
using Gamejam2.Tutorial;
using Gamejam2.Gameplay;
namespace Gamejam2.Debugging;
// Explicitly launched development check; never installed in a player scene.
public partial class DisplayCompatibilityAudit:Node
{
 private readonly SortedDictionary<string,object> _snapshots=new();
 private readonly List<string> _checks=new();
 private string _out="";
 private async Task Wait(double t=.12){await ToSignal(GetTree().CreateTimer(t),SceneTreeTimer.SignalName.Timeout);GetWindow().GrabFocus();RenderingServer.ForceDraw(false);}
 private void Check(bool ok,string message){if(!ok)throw new Exception(message);_checks.Add(message);GD.Print("PASS "+message);}
 private static IEnumerable<Node> Walk(Node n){yield return n;foreach(var child in n.GetChildren())foreach(var x in Walk(child))yield return x;}
 private static float R(float n)=>MathF.Round(n,2);
 private static float[] Vec(Vector2 v)=>new[]{R(v.X),R(v.Y)};
 private void Snapshot(string name,Node root)
 {
  var nodes=new SortedDictionary<string,object>();
  foreach(var n in Walk(root))
  {
   var row=new Dictionary<string,object>{{"type",n.GetClass().ToString()}};
   if(n is Control c){row["anchors"]=new[]{c.AnchorLeft,c.AnchorTop,c.AnchorRight,c.AnchorBottom};row["offsets"]=new[]{R(c.OffsetLeft),R(c.OffsetTop),R(c.OffsetRight),R(c.OffsetBottom)};row["size"]=Vec(c.Size);row["position"]=Vec(c.GlobalPosition);row["mouse"]=c.MouseFilter.ToString();row["visible"]=c.Visible;row["layer"]=c.GetCanvasLayerNode()?.Layer??0;}
   if(n is Label label){row["text"]=label.Text;row["font_size"]=label.GetThemeFontSize("font_size");row["theme_variation"]=label.ThemeTypeVariation.ToString();}
   if(n is TextureRect t){row["texture"]=t.Texture?.ResourcePath??"<null>";row["stretch"]=t.StretchMode.ToString();row["expand"]=t.ExpandMode.ToString();row["material"]=t.Material?.GetClass().ToString()??"none";}
   if(n is Sprite2D s)row["texture"]=s.Texture?.ResourcePath??"<null>";
   if(n is AnimatedSprite2D a){row["frames"]=a.SpriteFrames?.ResourcePath??"<null>";row["animations"]=a.SpriteFrames?.GetAnimationNames()??Array.Empty<string>();}
   if(n is CanvasLayer l){row["layer"]=l.Layer;row["follow"]=l.FollowViewportEnabled;}
   nodes[root.GetPathTo(n).ToString()]=row;
  }
  _snapshots[name]=nodes;
 }
 private async Task Capture(string name,Node root){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Snapshot(name,root);var drawn=ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);RenderingServer.ForceDraw(true);await drawn;RenderingServer.ForceSync();GetViewport().GetTexture().GetImage().SavePng(_out+"/"+name+".png");}
 private async Task Click(Control c)
 {
  var local=c.Size*.5f;if(c is Slider)local.X=c.Size.X*.3f;var p=c.GetGlobalTransformWithCanvas()*local;
  GetViewport().PushInput(new InputEventMouseMotion{Position=p,GlobalPosition=p},true);
  GD.Print("CLICK "+c.GetPath()+" "+p+" visible="+c.IsVisibleInTree()+" hovered="+GetViewport().GuiGetHoveredControl()?.GetPath());
  GetViewport().PushInput(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=true},true);

  GetViewport().PushInput(new InputEventMouseButton{Position=p,GlobalPosition=p,ButtonIndex=MouseButton.Left,Pressed=false},true);await Wait();
 }
 private void Root(Control c,string name)=>Check(c.Size.IsEqualApprox(new Vector2(1920,1080)),name+" full reference size");
 private void World(GameplayScreen g,string name)
 {
  Root(g,name);Root(g.GetNode<Control>("Composition"),name+" Composition");
  Check(g.Combo.OffsetLeft==-110&&g.Combo.OffsetRight==110,name+" centered combo bounds");Root(g.Juice,name+" Juice");
  Check(g.GetNode<Control>("Composition/SharkAnchor").GlobalPosition.IsEqualApprox(new Vector2(960,1080)),name+" shark bottom-center");
  var bg=g.Environment.Background.GetGlobalRect();
  Check(bg.Position.X<=0&&bg.Position.Y<=0&&bg.End.X>=1920&&bg.End.Y>=1080,name+" background cover");
  Check(g.Environment.Background.Texture!=null,name+" background texture loaded");
  if(g.Presentation.WorldCamera!=null)Check(g.Gauge.GetCanvasLayerNode()?.Layer==3&&g.SettingsButton.GetCanvasLayerNode()?.Layer==3,name+" fixed HUD layer");else Check(g is TutorialGameplayScreen,name+" intentionally camera-free");
  foreach(var b in Walk(g).OfType<InterferenceBubble>())Check(b.Initialized,name+" bubble "+b.EffectType);
 }
 public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
 private async Task Run()
 {
  try{
   _out=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--display-out="))?.Split('=',2)[1]??"/private/tmp/bite-display-source";
   System.IO.Directory.CreateDirectory(_out);
   var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath=_out+"/isolated-player-"+OS.GetProcessId()+".cfg";AddChild(router);GetWindow().GrabFocus();
   await Click(router.ScreenHost!.GetChild<TitleScreen>(0).StartButton);Check(router.ScreenHost.GetChild(0) is CalibrationScreen,"fresh Start enters Calibration");router.ScreenHost.GetChild<CalibrationScreen>(0).Flow!.Calibration!.SkipWithDefaults();await Wait(.3);Check(router.ScreenHost.GetChild(0) is TutorialGameplayScreen,"Calibration completion enters Tutorial");
   SettingsService.CompleteCalibration();foreach(var stage in new[]{"stage_2","stage_3","stage_4"})ProgressService.UnlockStage(stage);
   foreach(var size in new[]{new Vector2I(1920,1080),new Vector2I(1600,900),new Vector2I(1280,720)})
   {
    GetWindow().Size=size;string prefix=size.X.ToString();router.GoToTitle();await Wait();var title=router.ScreenHost!.GetChild<TitleScreen>(0);Root(title,"Title "+prefix);Check(Math.Abs(title.Logo.Position.X-960)<1,"Title logo centered "+prefix);await Capture(prefix+"-title",title);
    router.GoToCalibration(CalibrationReason.Settings);await Wait();var cal=router.ScreenHost.GetChild<CalibrationScreen>(0);Root(cal,"Calibration "+prefix);foreach(var pair in new[]{("StartGroup","측정 시작"),("RepeatGroup","다시 측정"),("ApplyGroup","적용하고 계속"),("SkipGroup","기본값으로 시작"),("ConfirmGroup","확인 시작"),("FinishGroup","보정 완료")})Check(cal.GetNode<Label>("Presentation/MainLayout/Actions/"+pair.Item1+"/Caption").Text==pair.Item2,"Calibration caption "+pair.Item1+" "+prefix);await Capture(prefix+"-calibration",cal);
    router.GoToLobby();await Wait();var lobby=router.ScreenHost.GetChild<LobbyScreen>(0);Root(lobby,"Lobby "+prefix);await Capture(prefix+"-main-menu",lobby);lobby.SelectCategory(true);await Wait(.45);await Capture(prefix+"-custom-menu",lobby);
    router.GoToTutorial(true);await Wait(.18);var t=router.ScreenHost.GetChild<TutorialGameplayScreen>(0);t.SuspendForSettings();t.SetProcess(false);await Wait();World(t,"Tutorial "+prefix);Check(t.Vision.MouseFilter==Control.MouseFilterEnum.Ignore&&t.Attack.MouseFilter==Control.MouseFilterEnum.Ignore,"Tutorial overlays ignore mouse "+prefix);Check(t.Vision.Blackout==0&&t.Attack.Blackout==0,"Tutorial no initial blackout "+prefix);await Capture(prefix+"-tutorial",t);
    t.OpenSkipConfirmation();await Wait();await Capture(prefix+"-tutorial-skip",t);await Click(t.ContinueButton);Check(!t.SkipConfirmation.Visible,"Tutorial skip cancel mouse "+prefix);
    t.SettingsButton.EmitSignal(Button.SignalName.Pressed);await Wait(.4);var settings=router.Settings!;Root(settings,"Settings "+prefix);Check(settings.GetNode<Label>("Drawer/Margins/Layout/ScrollContainer/SettingsContent/DataSection/Divider/Title").Text=="데이터","Settings data caption "+prefix);Check(settings.GetGlobalTransformWithCanvas().IsEqualApprox(Transform2D.Identity),"Settings fixed canvas "+prefix);await Capture(prefix+"-settings",settings);
    foreach(var slider in new[]{settings.MasterSlider,settings.MusicSlider,settings.EffectsSlider}){settings.Scroll.EnsureControlVisible(slider);await Wait();slider.Value=75;double before=slider.Value;await Click(slider);Check(slider.Value!=before,"slider mouse "+slider.Name+" "+prefix);}
    foreach(var offset in new[]{settings.InputOffset,settings.VisualOffset}){settings.Scroll.EnsureControlVisible(offset);await Wait();double before=offset.Value;await Click(offset.Plus);Check(offset.Value==before+offset.Step,"offset mouse "+offset.Name+" "+prefix);await Click(offset.Minus);Check(offset.Value==before,"offset restore "+prefix);}
    settings.Scroll.ScrollVertical=0;await Wait();foreach(var option in new[]{settings.DisplayMode,settings.ResolutionChoice}){await Click(option);Check(option.GetPopup().Visible,"dropdown mouse "+option.Name+" "+prefix);option.GetPopup().Hide();}

    settings.Scroll.EnsureControlVisible(settings.ResetProgressButton);await Wait();await Click(settings.ResetProgressButton);Check(settings.ResetConfirmation.Visible,"Reset confirmation mouse "+prefix);await Capture(prefix+"-reset",settings);await Click(settings.CancelResetButton);Check(!settings.ResetConfirmation.Visible,"Reset cancel mouse "+prefix);
    await Wait();await Click(settings.LeaveButton);Check(settings.ExitConfirmation.Visible,"Exit confirmation mouse "+prefix);await Capture(prefix+"-exit",settings);await Click(settings.CancelExitButton);Check(!settings.ExitConfirmation.Visible,"Exit cancel mouse "+prefix);settings.CloseImmediately();
    router.GoToLobby();router.GoToGameplay("stage_1");await Wait();var g=router.ScreenHost.GetChild<GameplayScreen>(0);g.SuspendForSettings();g.SetProcess(false);World(g,"Stage1 "+prefix);await Capture(prefix+"-stage1",g);
    var fixedBefore=new CanvasItem[]{g.Gauge,g.Combo,g.SettingsButton,g.Feedback}.Select(c=>c.GetGlobalTransformWithCanvas()).ToArray();
    g.Presentation.WorldCamera!.ApplyShot(new Vector2(-450,0),1.12f,g.Juice.MouthAnchor.GlobalPosition);await Wait();Check(new CanvasItem[]{g.Gauge,g.Combo,g.SettingsButton,g.Feedback}.Select((c,i)=>c.GetGlobalTransformWithCanvas().IsEqualApprox(fixedBefore[i])).All(x=>x),"camera HUD invariant "+prefix);router.Settings!.Open();await Wait(.4);Check(router.Settings.GetGlobalTransformWithCanvas().IsEqualApprox(Transform2D.Identity),"camera Settings invariant "+prefix);await Capture(prefix+"-camera-settings",router.Settings);router.Settings.CloseImmediately();g.Presentation.WorldCamera.ApplyShot(Vector2.Zero,1,g.Juice.MouthAnchor.GlobalPosition);
    var indicators=g.GetNode<InterferenceIndicators>("InterferenceIndicators");g.Interference.Initialize(g.SongId,Array.Empty<(string,double)>());foreach(var type in new[]{"whale","fishing_float","ship_horn","sardine"})g.Interference.Trigger(type,10);g.Interference.Present(11);indicators.Present(11,false);var bubbles=Walk(indicators).OfType<InterferenceBubble>().ToArray();Check(bubbles.All(b=>b.Visible),"four animated indicators visible "+prefix);Check(!bubbles.Single(b=>b.EffectType=="ship_horn").GetGlobalRect().Intersects(bubbles.Single(b=>b.EffectType=="fishing_float").GetGlobalRect()),"indicator top-left no overlap "+prefix);await Capture(prefix+"-indicators",g);indicators.Stop();
    g.Result.ShowResult(new GameplayResult("Hear the Tide",880,800,20,7,6,0,21,33));g.Result.Reveal();await Wait();Root(g.Result,"Result "+prefix);Check(g.Result.Gauge.Percentage.GetGlobalRect().Position.Y>=g.Result.Gauge.SharkImage.GetGlobalRect().End.Y,"Result percent below art "+prefix);Check(g.Result.Gauge.SharkImage.Size.X>400,"Result shark anchor "+prefix);await Capture(prefix+"-result",g.Result);g.Result.HideResult();g.GameOver.ShowStarvation();g.GameOver.Reveal();await Wait();Root(g.GameOver,"GameOver "+prefix);await Capture(prefix+"-gameover",g.GameOver);
   }
   foreach(string id in new[]{"stage_2","stage_3","stage_4"}.Concat(Gamejam2.Data.CustomMapRegistry.Maps.Where(m=>m.Generated&&m.Available).Select(m=>m.StageId)))
   {router.GoToLobby();if(id.StartsWith("custom_"))router.ScreenHost!.GetChild<LobbyScreen>(0).SelectCategory(true);router.GoToGameplay(id);await Wait();var g=router.ScreenHost!.GetChild<GameplayScreen>(0);Check(g.RunState==GameplayScreen.GameplayRunState.Playing,id+" resource entry");g.SuspendForSettings();g.SetProcess(false);World(g,id);g.Combo.UpdateCombo(27,0);g.Combo.Present(1);await Capture("1280-"+id,g);}
   System.IO.File.WriteAllText(_out+"/snapshots.json",JsonSerializer.Serialize(_snapshots));System.IO.File.WriteAllText(_out+"/checks.json",JsonSerializer.Serialize(_checks));GD.Print("DISPLAY AUDIT COMPLETE "+_checks.Count+" checks / "+_snapshots.Count+" screens");GetTree().Quit();
  }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
 }
}
