using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using Gamejam2.Data;
namespace Gamejam2.Lobby;
/// <summary>Menu-only Music-bus crossfade. Never participates in Song Clock or gameplay playback.</summary>
public partial class SongPreview : Node
{
    [Export] public AudioStreamPlayer PlayerA { get; set; } = null!;
    [Export] public AudioStreamPlayer PlayerB { get; set; } = null!;
    public string SongId { get; private set; } = "";
    public double CrossfadeSeconds { get; private set; }
    private Dictionary<string,Dictionary<string,string>>? _mainTiming;
    private AudioStreamPlayer? _current;
    private Tween? _fade;
    private string? _pending;
    private bool _leaving;
    private double _segmentStart, _segmentEnd;
    public void Select(string songId)
    {
        if(_leaving)return;
        if(_fade!=null&&_fade.IsRunning()){_pending=songId;return;}
        if(SongId==songId)return;
        var row=CustomMapRegistry.Find(songId)?.TimingRow() ?? (_mainTiming??=LocalCsv.Read("res://data/balance/song_timing.csv").ToDictionary(r=>r["song_id"]))[songId];
        var stream=Gamejam2.Audio.SongAudio.Load(row["audio_path"]);
        if(stream==null)throw new InvalidOperationException($"Preview audio missing: {songId}");
        CrossfadeSeconds=LocalCsv.Number(row["crossfade_sec"]);
        _segmentStart=LocalCsv.Number(row["preview_start_sec"]);
        _segmentEnd=Math.Min(stream.GetLength(),_segmentStart+LocalCsv.Number(row["preview_length_sec"]));
        if(_segmentStart<0||_segmentEnd<=_segmentStart||CrossfadeSeconds<=0)throw new ArgumentException("Preview segment invalid");
        double volume=Math.Pow(10,LocalCsv.Number(row["preview_db"])/20);
        _fade?.Kill();var old=_current;
        // Repeated navigation keeps the louder deck as outgoing. Reuse only the quieter deck.
        if(PlayerA.Playing&&PlayerB.Playing)old=PlayerA.VolumeLinear>=PlayerB.VolumeLinear?PlayerA:PlayerB;
        var incoming=old==PlayerA?PlayerB:PlayerA;
        incoming.Stop();incoming.Stream=stream;incoming.VolumeLinear=0;incoming.Play((float)_segmentStart);
        _current=incoming;SongId=songId;
        _fade=CreateTween().SetParallel();
        _fade.TweenProperty(incoming,"volume_linear",volume,CrossfadeSeconds);
        if(old!=null){_fade.TweenProperty(old,"volume_linear",0.0,CrossfadeSeconds);_fade.Chain().TweenCallback(Callable.From(old.Stop));}
    }
    public void FadeOut(Action completed)
    {
        _leaving=true;_pending=null;_fade?.Kill();_fade=CreateTween().SetParallel();
        _fade.TweenProperty(PlayerA,"volume_linear",0.0,.18);
        _fade.TweenProperty(PlayerB,"volume_linear",0.0,.18);
        _fade.Chain().TweenCallback(Callable.From(()=>{PlayerA.Stop();PlayerB.Stop();completed();}));
    }
    public override void _Process(double delta)
    {
        if(_leaving)return;
        if(_pending!=null&&(_fade==null||!_fade.IsRunning())){var id=_pending;_pending=null;Select(id);}
        if(_current==null||!_current.Playing||_segmentEnd<=_segmentStart)return;
        // Keep the segment seam inaudible with the same two-deck crossfade.
        if(_current.GetPlaybackPosition()>=_segmentEnd-CrossfadeSeconds)
        {string id=SongId;SongId="";Select(id);}
    }
    public override void _ExitTree(){_fade?.Kill();PlayerA.Stop();PlayerB.Stop();}
}
