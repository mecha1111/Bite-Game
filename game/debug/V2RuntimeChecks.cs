using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Startup;
using Gamejam2.Title;
using Gamejam2.Lobby;
using Gamejam2.Gameplay;
using Gamejam2.Settings;
using Gamejam2.Save;
using Gamejam2.Data;
using Gamejam2.Rhythm;
namespace Gamejam2.Debugging;
/// <summary>Developer driver of the actual main scene. Isolated save, physical Space,
/// unskipped full music playback; never changes runtime clock/target timestamps.</summary>
public partial class V2RuntimeChecks : Node
{
    private SceneRouter _router=null!;
    private GameplayScreen? _game;
    private bool _auto;
    private int _next,_checks;
    private double _drift;
    private ulong _lastAuditFrame;
    private int _lastUnderruns;
    private double _maxFrameGap;
    private void Check(bool pass,string label){if(!pass)throw new Exception(label);GD.Print("PASS "+label);_checks++;}
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private async Task At(double time){while(_game!.Rhythm.SongTimeSeconds<time)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    private void Space(){if(OS.GetCmdlineUserArgs().Contains("--musical-audit-driver")&&_game!=null){_game._Input(new InputEventKey{PhysicalKeycode=Key.Space,Pressed=true});return;}Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.Space,Pressed=true});Input.FlushBufferedEvents();Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.Space,Pressed=false});Input.FlushBufferedEvents();}
    public override void _Process(double delta)
    {
        if(_game==null)return;
        if(_game.Rhythm.IsFocusPaused)GetWindow().GrabFocus();
        double t=_game.Rhythm.SongTimeSeconds;
        if(_auto&&_game.Rhythm.IsClockAdvancing)
        {
            ulong now=Time.GetTicksUsec();double gap=_lastAuditFrame==0?0:(now-_lastAuditFrame)/1000d;_lastAuditFrame=now;_maxFrameGap=Math.Max(_maxFrameGap,gap);
            if(gap>50)GD.Print($"AUDIT STALL {_game.SongId} time={t:F3} gap={gap:F3}ms GC={GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}");
            if(_game.AudioCues.UnderrunResyncs>_lastUnderruns){_lastUnderruns=_game.AudioCues.UnderrunResyncs;GD.Print($"AUDIT SFX underrun {_game.SongId} time={t:F3} count={_lastUnderruns} maxFrameGap={_maxFrameGap:F3}ms");}
        }
        if(_auto&&_game.IsBiteInteractive&&_next<_game.Data.Phrases.Count&&t>=_game.Data.Phrases[_next].TargetSeconds)
        {_game._Input(new InputEventKey{PhysicalKeycode=Key.Space,Pressed=true});GD.Print($"V2 HIT {_game.SongId} #{_next} at {t:F6}: {_game.Rhythm.LastResult?.Judgment}, error={_game.Rhythm.LastResult?.TimingErrorSeconds*1000:F3}ms");_next++;}
        if(_auto&&_game.Rhythm.IsClockAdvancing&&_game.Music.Playing&&t>1)
        {double heard=_game.Music.GetPlaybackPosition()+AudioServer.GetTimeSinceLastMix()-AudioServer.GetOutputLatency();double drift=Math.Abs(t-heard);if(drift>_drift){_drift=drift;if(drift>.05)GD.Print($"DRIFT {_game.SongId} at {t:F6}s = {drift*1000:F2}ms");}}
        if(_game.Result.Visible&&t<_game.Rhythm.AppliedSongEndTimeSeconds)throw new Exception("Premature v2 result");
    }
    public override void _Ready()=>Callable.From(()=>{_=Run();}).CallDeferred();
    private void Capture(string file){if(DisplayServer.GetName()=="headless")return;RenderingServer.ForceDraw();using var image=GetViewport().GetTexture().GetImage();image.SavePng("/private/tmp/bite-v2-"+file+".png");}
    private async Task Run()
    {
        try
        {
            _router=(SceneRouter)GetTree().CurrentScene;GetWindow().GrabFocus();
            Check(_router.SceneFilePath=="res://game/startup/Startup.tscn","actual main Startup scene, no scene override");
            Check(SettingsService.MasterVolume==.5,"fresh default Master 50%");SaveStore.Write("settings","master_volume",.73);SettingsService.Initialize();Check(Math.Abs(SettingsService.MasterVolume-.73)<1e-6,"existing saved Master remains unchanged");SettingsService.SetMasterVolume(.5);
            Gamejam2.Save.SaveStore.Write("progress","tutorial_completed",true);Gamejam2.Save.SaveStore.Flush();SettingsService.CompleteCalibration();SettingsService.InputOffsetSeconds=SettingsService.VisualOffsetSeconds=0;SettingsService.SetResolution(2);
            foreach(string stage in new[]{"stage_2","stage_3","stage_4"})ProgressService.UnlockStage(stage); // isolated fixture only, not production progression.
            await Wait(.6);var title=_router.ScreenHost!.GetChildren().OfType<TitleScreen>().Single();title.StartButton.EmitSignal(BaseButton.SignalName.Pressed);await Wait(.9);
            string selectedArg=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--audit-song="))??"";
            int selected=selectedArg.Length==0?0:int.Parse(selectedArg.Split('=')[1]);if(selected is <0 or >4)throw new ArgumentException("audit-song must be 1..4");
            int first=selected==0?1:selected,last=selected==0?4:selected;
            for(int song=first;song<=last;song++)
            {
                var lobby=_router.ScreenHost.GetChildren().OfType<LobbyScreen>().Single();lobby.Navigate(song-1-lobby.SelectedIndex);await Wait(.85);
                var preview=lobby.GetNode<SongPreview>("SongPreview");string path=new[]{"res://assets/music/1_edited.mp3","res://assets/music/2-2.mp3","res://assets/music/3.mp3","res://assets/music/4.mp3"}[song-1];
                Check(preview.SongId=="song_"+song&&(preview.PlayerA.Playing&&preview.PlayerA.Stream.ResourcePath==path||preview.PlayerB.Playing&&preview.PlayerB.Stream.ResourcePath==path),"actual Lobby current-source preview song "+song);
                lobby.SelectCurrent();await Wait(.4);_game=_router.ScreenHost.GetChildren().OfType<GameplayScreen>().Single();
                if(OS.GetCmdlineUserArgs().Contains("--musical-audit-driver"))_game.SetProcessInput(false);
                var data=_game.Data;Check(_game.Music.Stream.ResourcePath==path,"actual Gameplay current-source music song "+song);
                Check(((AudioStreamMP3)_game.Music.Stream).Data.SequenceEqual(FileAccess.GetFileAsBytes(path)),"playing MP3 encoded bytes match current source file song "+song);
                Check(data.Phrases.Count==LocalCsv.Read(data.ChartPath).Count&&data.ChartPath.EndsWith("_musical_chart.csv"),"actual authored chart loaded count/path song "+song);
                GD.Print($"V2 RUNTIME SongId={_game.SongId} Audio={_game.Music.Stream.ResourcePath} Duration={_game.Music.Stream.GetLength():F6} Playback={_game.Music.GetPlaybackPosition():F6} Chart={data.ChartPath} MusicalBpm={data.MusicalBpm:F6} GameplayPulseBpm={data.Chart.Bpm:F6} Count={data.Phrases.Count}");
                var csv=LocalCsv.Read(data.ChartPath);Check(csv.Count==data.Phrases.Count&&csv.Select(r=>LocalCsv.Number(r["target_time_sec"])).SequenceEqual(data.Phrases.Select(p=>p.TargetSeconds)),"runtime targets match current v2 CSV exactly song "+song);
                foreach(var p in data.Phrases.Take(5))GD.Print($"V2 EVENT {_game.SongId} #{p.Index} Pattern={p.PatternId} Side={(p.FromLeft?"L":"R")} Start={p.StartSeconds:F6} Target={p.TargetSeconds:F6}");
                Check(data.Phrases.Skip(1).Select((p,i)=>p.StartSeconds>=data.Phrases[i].TargetSeconds+.25).All(x=>x),"all encounter resolve deadlines precede next start song "+song);
                Check(data.Phrases.All(p=>Math.Abs(p.TargetSeconds-p.StartSeconds-240/data.Chart.Bpm)<1e-9),"all authored starts derive four gameplay pulses from target song "+song);
                if(OS.GetCmdlineUserArgs().Contains("--v2-baseline-only"))
                {
                    _game.SuspendForSettings();_game.SetProcess(false);double frozen=_game.Rhythm.SongTimeSeconds;
                    foreach(int resolution in new[]{2,0})
                    {
                        SettingsService.SetResolution(resolution);await Wait(.12);_game.Shark.Bite(frozen);
                        for(int frame=0;frame<20;frame++)
                        {
                            _game.Shark.Present(frozen+(frame+.2)/20*_game.Shark.BiteDurationSeconds);
                            await Wait(.03); // Allow CanvasItem's queued redraw before the screenshot.
                            var texture=_game.Shark.Art.SpriteFrames.GetFrameTexture("bite",frame);using var image=texture.GetImage();var art=_game.Shark.Art;
                            float bottom=art.ToGlobal(art.Offset-texture.GetSize()*.5f+new Vector2(0,image.GetUsedRect().End.Y)).Y;
                            Check(art.Frame==frame&&Math.Abs(bottom-1080)<.01,"rendered frame "+frame+" bottom baseline at "+SettingsService.Resolution);
                            if(frame is 0 or 3 or 7 or 19)Capture("rendered-"+SettingsService.Resolution.X+"-frame-"+frame);
                        }
                        _game.Shark.Present(frozen+_game.Shark.BiteDurationSeconds+.001);await Wait(.03);Check(_game.Shark.Art.Animation=="idle","rendered Bite returns to idle");
                    }
                    GD.Print("V2 RENDERED BASELINE VERIFIED");_game.StopGameplay();_game=null;_router.QueueFree();await Wait(.1);GetTree().Quit();return;
                }
                if(song==1)
                {
                    while(!_game.Rhythm.IsClockAdvancing)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                    int bites=_game.Shark.BiteCount;Space();Check(_game.Shark.BiteCount==bites+1&&_game.Satiety.EmptyBites==1&&_game.Satiety.MissStreak==1,"empty input before first prey animates and produces MISS");
                    await At(data.Phrases[0].StartSeconds+.06);Space();Check(_game.PreyState==GameplayScreen.PreyEncounterState.Resolved&&_game.Rhythm.LastResult?.Judgment==RhythmJudgment.Miss,"premature physical Bite consumes prey as MISS");
                    for(int tap=1;tap<=4;tap++){await At(data.Phrases[0].StartSeconds+.06+tap*60/data.MusicalBpm);Space();}
                    Check(_game.Satiety.CreateResult().Perfect==0&&_game.Satiety.Combo==0&&_game.Satiety.MissStreak>=6&&_game.Satiety.LastMissPenalty==10,"constant quarter-note mashing cannot repair prey and incurs penalties");
                    _router.Settings!.Open();await Wait(.35);bites=_game.Shark.BiteCount;_router.Settings.MasterSlider.GrabFocus();Space();Check(_game.Shark.BiteCount==bites,"Settings excludes Bite/MISS");
                    _router.Settings.Close();await Wait(.3);Space();Check(_game.IsCountingDown&&_game.Shark.BiteCount==bites,"resume countdown excludes Bite/MISS");await Wait(3.1);
                    _game.SuspendForSettings();double frozen=_game.Rhythm.SongTimeSeconds;_game.Shark.Bite(frozen);Vector2 basePosition=_game.Shark.Art.Position;
                    for(int frame=0;frame<20;frame++)
                    {
                        _game.Shark.Present(frozen+(frame+.2)/20*_game.Shark.BiteDurationSeconds);
                        var texture=_game.Shark.Art.SpriteFrames.GetFrameTexture("bite",frame);using var image=texture.GetImage();var bounds=image.GetUsedRect();
                        var art=_game.Shark.Art;float bottom=art.ToGlobal(art.Offset-texture.GetSize()*.5f+new Vector2(0,bounds.End.Y)).Y;
                        Check(art.Position==basePosition&&Math.Abs(bottom-1080)<.01&&bounds.End.Y==256,"frame "+frame+" shares frame-zero viewport bottom baseline");
                        if(frame is 0 or 3 or 7 or 19)Capture("shark-frame-"+frame);
                    }
                    _game.Shark.Present(frozen+_game.Shark.BiteDurationSeconds+.001);Check(_game.Shark.Art.Animation=="idle"&&_game.Shark.BiteDurationSeconds==.36,"readable .36-second Bite returns to idle");
                    _game.Restart();await Wait(.2);
                }
                _game.SetProcessInput(false); // Only isolate external input in automated playback, after physical-input preflight.
                _next=0;_drift=0;_lastAuditFrame=0;_lastUnderruns=0;_maxFrameGap=0;_auto=true;
                while(!_game.Result.Visible)
                {await Wait(15);GD.Print($"V2 PROGRESS {_game.SongId} {_game.Rhythm.SongTimeSeconds:F1}s/{_game.Music.Stream.GetLength():F1}s");if(_game.GameOver.Visible)throw new Exception("Unexpected starvation in accurate full v2 run");}
                _auto=false;await Wait(2);var stats=_game.Result.Result!;GD.Print($"FULL STATS {_game.SongId}: Perfect={stats.Perfect} Good={stats.Good} Bad={stats.Bad} Miss={stats.Miss} Satiety={stats.FinalSatiety} MaxDriftMs={_drift*1000:F2} SFXunderruns={_game.AudioCues.UnderrunResyncs} MaxFrameGapMs={_maxFrameGap:F2}");
                Check(_next==data.Phrases.Count&&stats.Perfect+stats.Good+stats.Bad==_next&&stats.Miss==0,"every authored target resolves once in complete unskipped song "+song);
                Check(_game.ResultPresentationCount==1&&_game.Rhythm.SongTimeSeconds>=_game.Music.Stream.GetLength(),"only actual song end shows one Result song "+song);
                Check(_game.AudioCues.UnderrunResyncs==0,"all synchronized authored cue audio has no underruns song "+song);
                Check(_game.Presentation.EventsTriggered==_game.Presentation.Events.Length,"continuous song visits every authored presentation event song "+song);
                Check(stats.AllPerfect&&ProgressService.BestStageRank("stage_"+song)=="gold","actual Result persists highest Gold shark song "+song);
                if(song==4)Check(_game.Environment.ActiveEnvironmentSongId=="song_1"&&!_game.Environment.PreviousBackground.Visible,"full EX ends after the authored shallow return transition");
                Check(_drift<.05,"music/Song Clock drift <50ms song "+song+" max="+(_drift*1000).ToString("F2")+"ms");Capture("song-"+song+"-result");
                _game.Result.SongSelectButton.EmitSignal(BaseButton.SignalName.Pressed);_game=null;await Wait(.35);
            }
            GD.Print($"MUSICAL CHART RUNTIME VERIFIED: {_checks} checks, selected current-source songs {first}..{last} complete without seeks.");_router.QueueFree();await Wait(.1);GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());_game?.StopGameplay();_game=null;_router?.QueueFree();await Wait(.1);GetTree().Quit(1);}
    }
}
