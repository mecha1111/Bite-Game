using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Startup;
using Gamejam2.Tutorial;
using Gamejam2.Calibration;
using Gamejam2.Settings;
using Gamejam2.Gameplay;
using Gamejam2.CustomGenerator;
using Gamejam2.Data;
using Gamejam2.Lobby;
using Gamejam2.Rhythm;
namespace Gamejam2.Debugging;
public partial class CurrentFeatureCheck:Node
{
    void Check(bool ok,string name){if(!ok)throw new Exception(name);GD.Print("PASS "+name);}
    async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    async Task Run()
    {
        string? generatedId=null;
        try
        {
            var router=GD.Load<PackedScene>("res://game/startup/Startup.tscn").Instantiate<SceneRouter>();router.SavePath="/private/tmp/bite-feature-"+OS.GetProcessId()+".cfg";AddChild(router);await Frame();
            router.GoToCalibration(CalibrationReason.Settings);await Frame();
            var screen=router.ScreenHost!.GetChild<CalibrationScreen>(0);var calibration=screen.Flow!.Calibration!;
            CalibrationSession.UserInputOffsetSeconds=-.035;calibration.BeginVisualCalibration();calibration.SetVisualOffset(.045);
            Check(calibration.Stage==CalibrationStage.Visual&&Math.Abs(CalibrationSession.UserInputOffsetSeconds+.035)<1e-9&&Math.Abs(CalibrationSession.VisualOffsetSeconds-.045)<1e-9,"Visual calibration + independent offsets");
            Check(screen.GetNode<Control>("Presentation/MainLayout/VisualOffsetControls").Visible,"Visual +/- controls visible");
            calibration.BeginConfirmation();Check(calibration.Stage==CalibrationStage.Confirmation,"8-beat confirmation");
            calibration.Finish();await Frame();await Frame();
            Check(Math.Abs(SettingsService.VisualOffsetSeconds-.045)<1e-9,"Visual offset persisted through completion");
            router.GoToTutorial(true);await Frame();var tutorial=router.ScreenHost.GetChild<TutorialGameplayScreen>(0);
            Check(tutorial.ModeLabel.Text=="시연 중","Tutorial demonstration caption");
            int before=tutorial.Satiety.EmptyBites;GetViewport().PushInput(new InputEventKey{PhysicalKeycode=Key.Space,Keycode=Key.Space,Pressed=true},true);GetViewport().PushInput(new InputEventKey{PhysicalKeycode=Key.Space,Keycode=Key.Space,Pressed=false},true);
            Check(tutorial.Satiety.EmptyBites==before,"Demo ignores physical Bite");
            var observed=typeof(TutorialGameplayScreen).GetMethod("JudgmentObserved",BindingFlags.NonPublic|BindingFlags.Instance)!;
            observed.Invoke(tutorial,new object[]{new RhythmJudgmentResult(RhythmJudgment.Perfect,0,new RhythmEventData{CueId="0"})});
            Check(tutorial.ModeLabel.Text=="이제 직접 해보세요"&&!tutorial.PracticeInputEnabled,"Demo-to-practice prompt locks only Bite");
            foreach(var (value,rank) in new[]{(799,"none"),(800,"bronze"),(1000,"silver"),(1100,"gold")})Check(MedalSharkCatalog.RankFor(value)==rank,"Satiety medal "+value+" = "+rank);
            Gamejam2.Save.ProgressService.RecordStageResult("song_1",true,false,false,1100);Gamejam2.Save.ProgressService.RecordStageResult("song_1",true,true,true,800);Check(Gamejam2.Save.ProgressService.BestStageRank("stage_1")=="gold","Medal cannot downgrade");
            router.GoToLobby();await Frame();
            var map=await LocalMapGenerator.Generate(this,"res://assets/MUSIC/1_edited.mp3",status=>GD.Print("GEN "+status));generatedId=map.SongId;
            Check(map.Generated&&map.AudioPath.StartsWith("user://custom_maps/"),"MP3 local generation committed");
            var data=GameplayData.Load(map.SongId);Check(data.TargetCount>0&&data.Phrases.All(p=>p.TargetEvents.Length==1)&&data.Phrases.Skip(1).Select((p,i)=>p.FromLeft!=data.Phrases[i].FromLeft).All(x=>x),"Generated single-target alternating chart loads");
            Check(StagePresentationChart.Load(map.SongId,map.Duration)!=null,"Generated separate camera data loads");
            // Exercise ID3 APIC parsing + local image card branch independently of musical analysis.
            using var image=Image.CreateEmpty(8,8,false,Image.Format.Rgba8);image.Fill(new Color(.3f,.8f,1));var png=image.SavePngToBuffer();
            var apic=new byte[13+png.Length];apic[0]=3;System.Text.Encoding.ASCII.GetBytes("image/png").CopyTo(apic,1);apic[10]=0;apic[11]=3;apic[12]=0;png.CopyTo(apic,13);
            var tag=new byte[20+apic.Length];System.Text.Encoding.ASCII.GetBytes("ID3").CopyTo(tag,0);tag[3]=3;int size=10+apic.Length;tag[6]=(byte)(size>>21&127);tag[7]=(byte)(size>>14&127);tag[8]=(byte)(size>>7&127);tag[9]=(byte)(size&127);System.Text.Encoding.ASCII.GetBytes("APIC").CopyTo(tag,10);tag[14]=(byte)(apic.Length>>24);tag[15]=(byte)(apic.Length>>16);tag[16]=(byte)(apic.Length>>8);tag[17]=(byte)apic.Length;apic.CopyTo(tag,20);
            Check(Mp3Metadata.Read(tag,"fallback").Artwork!.SequenceEqual(png),"Embedded album artwork read locally");
            image.SavePng("user://custom_maps/"+generatedId+"/test-cover.png");map.CoverPath="user://custom_maps/"+generatedId+"/test-cover.png";
            Check(CustomMapRegistry.Cover(map)!=null,"Local album artwork becomes cover texture");map.CoverPath="";
            var card=GD.Load<PackedScene>("res://game/lobby/StageCard.tscn").Instantiate<StageCard>();AddChild(card);card.Bind(new StageData{SongId=map.SongId,StageId=map.StageId,DisplayName=map.Title},true);
            Check(card.GetNode<Label>("Background/LocalTitleCover").Visible&&card.GetNode<Label>("Background/LocalTitleCover").Text==map.Title,"No-art card displays large title");card.QueueFree();
            router.ScreenHost.GetChild<LobbyScreen>(0).SelectCategory(true);await Frame();
            router.GoToGameplay(map.StageId);await Frame();await Frame();var gameplay=router.ScreenHost.GetChild<GameplayScreen>(0);
            Check(gameplay.RunState==GameplayScreen.GameplayRunState.Playing&&gameplay.Data.TargetCount==data.TargetCount,"Generated map enters normal Gameplay");
            router.GoToLobby();await Frame();LocalMapGenerator.Delete(generatedId);generatedId=null;
            GD.Print("CURRENT FEATURE CHECK COMPLETE");GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());if(generatedId!=null)LocalMapGenerator.Delete(generatedId);GetTree().Quit(1);}
    }
}
