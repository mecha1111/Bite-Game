using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Audio;
using Gamejam2.Data;
using Gamejam2.Gameplay;
using Gamejam2.Settings;
using Gamejam2.Rhythm;
namespace Gamejam2.Debugging;

/// <summary>Independent development listening player. Never seeks or edits Song Clock.
/// Targets stay in CSV; deliberate save derives all starts before overlap validation.</summary>
public partial class ChartAudition : Control
{
    private AudioStreamPlayer _music=null!;
    private OptionButton _songs=null!;
    private Label _details=null!,_status=null!,_position=null!;
    private Button _approve=null!;
    private GameplayData _data=null!;
    private List<Dictionary<string,string>> _rows=new();
    private int _song,_index;
    private double _target,_stop;
    private bool _heard,_dirty;
    private string _audioHash="";
    private string _chartHash="";
    private static string Number(double value)=>value.ToString("F12",CultureInfo.InvariantCulture);
    private static string FileHash(string path)=>Convert.ToHexString(SHA256.HashData(FileAccess.GetFileAsBytes(path)));
    public override void _Ready()
    {
        Gamejam2.Save.SaveStore.Initialize();SettingsService.Initialize();
        _music=GetNode<AudioStreamPlayer>("Music");_songs=GetNode<OptionButton>("Panel/Song");
        _details=GetNode<Label>("Panel/Details");_status=GetNode<Label>("Panel/Status");_position=GetNode<Label>("Panel/Position");_approve=GetNode<Button>("Panel/Approve");
        foreach(var song in LocalCsv.Read("res://data/balance/songs.csv"))_songs.AddItem(song["display_name"]);
        _songs.ItemSelected+=song=>LoadSong((int)song);
        GetNode<Button>("Panel/Previous").Pressed+=()=>Move(-1);
        GetNode<Button>("Panel/Next").Pressed+=()=>Move(1);
        GetNode<Button>("Panel/Replay").Pressed+=Replay;
        GetNode<Button>("Panel/Earlier").Pressed+=()=>Nudge(-.01);
        GetNode<Button>("Panel/Later").Pressed+=()=>Nudge(.01);
        GetNode<Button>("Panel/Save").Pressed+=SaveTarget;
        _approve.Pressed+=Approve;
        LoadSong(0);
        if(OS.GetCmdlineUserArgs().Contains("--audition-check"))Callable.From(()=>{_=VerifyAudition();}).CallDeferred();
    }
    private void LoadSong(int song)
    {
        _music.Stop();_song=song;_index=0;_data=GameplayData.Load("song_"+(song+1));_rows=LocalCsv.Read(_data.ChartPath);
        _music.Stream=SongAudio.Load(_data.AudioPath);_audioHash=FileHash(_data.AudioPath);_chartHash=FileHash(_data.ChartPath);
        Select();
    }
    private void Select()
    {
        _music.Stop();_target=LocalCsv.Number(_rows[_index]["target_time_sec"]);_dirty=_heard=false;_approve.Disabled=true;Describe();
        var row=_rows[_index];bool approved=FileAccess.FileExists("res://data/charts/listening_reviews.csv")&&LocalCsv.Read("res://data/charts/listening_reviews.csv").Any(r=>r["song_id"]=="song_"+(_song+1)&&Math.Abs(LocalCsv.Number(r["target_time_sec"])-_target)<1e-8&&r["audio_sha256"]==_audioHash&&r["pattern_id"]==row["pattern_id"]&&r["side"]==row["side"]&&r.GetValueOrDefault("gameplay_bpm","")==Number(_data.Chart.Bpm));
        _status.Text=(approved?"청취 승인 기록 있음":"청취 미승인")+" · Space 재생 / ← → 목표 이동 / A D ±10ms / S CSV 저장";
    }
    private void Move(int direction){_index=Math.Clamp(_index+direction,0,_rows.Count-1);Select();}
    private void Describe()
    {
        var row=_rows[_index];double beat=60/_data.MusicalBpm;double pos=(_target-_data.BeatOffsetSeconds)/beat;double nearest=_data.BeatOffsetSeconds+Math.Round(pos*4)*beat/4;
        _details.Text=$"{_data.SongName}    {_index+1} / {_rows.Count}\n\nPattern: {row["pattern_id"]}    Side: {row["side"]}\nStart: {_target-240/_data.Chart.Bpm:F6}s    Target: {_target:F6}s\nMusic: {_data.MusicalBpm:F6} BPM    Gameplay pulse: {_data.Chart.Bpm:F6} BPM\nNearest 1/4 beat: {nearest:F6}s    Grid position: {pos:F3}\nSection: {row.GetValueOrDefault("optional_section_id","")}\n\n{row.GetValueOrDefault("optional_notes","")}";
        GD.Print($"[AUDITION] Song={_data.SongName} SongTime={_music.GetPlaybackPosition():F6} Target={_target:F6} Pattern={row["pattern_id"]} Side={row["side"]} NearestSubdivision={nearest:F6} Section={row.GetValueOrDefault("optional_section_id","")} Audio={_data.AudioPath} Chart={_data.ChartPath}");
    }
    private void Replay(){_heard=false;_approve.Disabled=true;_stop=Math.Min(_music.Stream.GetLength(),_target+1);_music.Play((float)Math.Max(0,_target-2));_status.Text="원곡 청취 중 · 목표에 별도 정답 소리는 재생하지 않습니다";}
    private void Nudge(double seconds){_music.Stop();_target+=seconds;_dirty=true;_heard=false;_approve.Disabled=true;Describe();_status.Text="미저장 수정 · S 저장 후 다시 듣고 승인하세요";}
    private void SaveTarget()
    {
        try
        {
            if(FileHash(_data.ChartPath)!=_chartHash)throw new InvalidOperationException("CSV가 외부에서 변경됐습니다. 곡을 다시 선택한 뒤 수정하세요.");
            var candidate=_rows.Select(row=>new Dictionary<string,string>(row)).ToList();candidate[_index]["target_time_sec"]=Number(_target);
            double previous=-1;string previousPattern="";double late=GD.Load<InputCaptureWindows>("res://game/rhythm/InputCaptureWindows.tres").LateSeconds-Math.Min(0,SettingsService.InputOffsetSeconds);
            for(int i=0;i<candidate.Count;i++)
            {
                var row=candidate[i];double target=LocalCsv.Number(row["target_time_sec"]),start=target-240/_data.Chart.Bpm;
                if(start<0||target+late>=_data.DurationSeconds)throw new ArgumentException("목표가 곡 시작/끝 범위를 벗어났습니다.");
                if(i>0)GameplayData.ValidateEncounterGap("song_"+(_song+1),i-1,0,previous,start,late,previousPattern,row["pattern_id"]);
                row["start_time_sec"]=Number(start);previous=target;previousPattern=row["pattern_id"];
                row["nearest_musical_beat_sec"]=Number(_data.BeatOffsetSeconds+Math.Round((target-_data.BeatOffsetSeconds)/(60/_data.MusicalBpm)*4)*(60/_data.MusicalBpm)/4);
            }
            candidate[_index]["timing_grid"]="audition_edited_target";
            candidate[_index]["optional_notes"]+="; target manually edited in audition; listening pending";
            WriteCsv(_data.ChartPath,candidate);_chartHash=FileHash(_data.ChartPath);_rows=candidate;_data=GameplayData.Load("song_"+(_song+1));_dirty=false;_status.Text="CSV 저장 완료 · Start를 4 pulse 전으로 다시 계산했습니다. 재생 후 승인하세요";
        }
        catch(Exception e){_status.Text="저장 거부: "+e.Message;GD.PushError(e.Message);}
    }
    private static void WriteCsv(string path,List<Dictionary<string,string>> rows)
    {
        using var file=FileAccess.Open(path,FileAccess.ModeFlags.Write)??throw new InvalidOperationException("개발용 CSV를 쓸 수 없습니다: "+path);
        var fields=rows[0].Keys.ToArray();file.StoreCsvLine(fields);foreach(var row in rows)file.StoreCsvLine(fields.Select(k=>row[k]).ToArray());
        if(!FileAccess.FileExists(path+".import")){using var import=FileAccess.Open(path+".import",FileAccess.ModeFlags.Write);import?.StoreString("[remap]\nimporter=\"keep\"\n");}
    }
    private void Approve()
    {
        if(!_heard||_dirty)return;
        if(FileHash(_data.ChartPath)!=_chartHash||FileHash(_data.AudioPath)!=_audioHash){_status.Text="외부에서 차트/음원이 바뀌었습니다. 곡을 다시 선택해 청취하세요.";return;}
        const string path="res://data/charts/listening_reviews.csv";
        var reviews=FileAccess.FileExists(path)?LocalCsv.Read(path):new List<Dictionary<string,string>>();
        var row=_rows[_index];reviews.Add(new(){["song_id"]="song_"+(_song+1),["target_time_sec"]=Number(_target),["pattern_id"]=row["pattern_id"],["side"]=row["side"],["gameplay_bpm"]=Number(_data.Chart.Bpm),["audio_sha256"]=_audioHash,["status"]="listener_approved",["reviewed_utc"]=DateTime.UtcNow.ToString("O")});
        WriteCsv(path,reviews);_status.Text="이 목표의 청취 승인 기록 저장됨 · 자동 재생은 승인으로 기록하지 않습니다";
    }
    public override void _Process(double delta)
    {
        if(_music==null)return;
        double pos=_music.Playing?Math.Max(0,_music.GetPlaybackPosition()+AudioServer.GetTimeSinceLastMix()-AudioServer.GetOutputLatency()):_music.GetPlaybackPosition();_position.Text=$"SongTime {pos:F3}s    Target {_target:F3}s    Δ {(pos-_target)*1000:F0}ms";
        _position.Modulate=Math.Abs(pos-_target)<.065?new Color(.5f,1,1):Colors.White;
        if(_music.Playing&&pos>=_stop){_music.Stop();_heard=true;_approve.Disabled=_dirty;_status.Text=_dirty?"청취 종료 · 수정한 목표를 먼저 저장하세요":"청취 종료 · 음악적으로 맞으면 청취 승인 / 아니면 A D로 수정";}
    }
    public override void _Input(InputEvent input)
    {
        if(_songs.GetPopup().Visible||input is not InputEventKey{Pressed:true,Echo:false} key)return;
        switch(key.PhysicalKeycode){case Key.Space:Replay();break;case Key.Left:Move(-1);break;case Key.Right:Move(1);break;case Key.A:Nudge(-.01);break;case Key.D:Nudge(.01);break;case Key.S:SaveTarget();break;case Key.Y:Approve();break;default:return;}
        GetViewport().SetInputAsHandled();
    }
    private async Task VerifyAudition()
    {
        try
        {
            for(int song=0;song<4;song++)
            {
                _songs.Select(song);LoadSong(song);
                if(_data.Phrases.Any(p=>p.TargetSeconds+1>=_music.Stream.GetLength()||string.IsNullOrWhiteSpace(p.SectionId)))throw new Exception("Audition bounds/section metadata invalid");
                foreach(int index in new[]{0,_rows.Count-1})
                {
                    _index=index;Select();Replay();
                    while(_music.Playing)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
                    if(!_heard||_approve.Disabled)throw new Exception("Complete 3-second audition did not unlock deliberate listener approval");
                    GD.Print($"PASS native audition window song_{song+1} #{index} {_target-2:F6}..{_target+1:F6}; no automatic approval written");
                }
                Nudge(-.01);if(!_dirty||!_approve.Disabled)throw new Exception("Edited target needs a new listening review");Select();
                if(!_music.Stream.ResourcePath.Equals(_data.AudioPath))throw new Exception("Audition used wrong source");
            }
            await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);RenderingServer.ForceDraw();GetViewport().GetTexture().GetImage().SavePng("/private/tmp/bite-chart-audition.png");
            GD.Print("CHART AUDITION TOOL VERIFIED: native first/last windows of all songs, metadata, review gating; human taste NOT verified");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
