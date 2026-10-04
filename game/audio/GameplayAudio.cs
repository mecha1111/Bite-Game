using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Gamejam2.Data;
using Gamejam2.Gameplay;
using Gamejam2.Rhythm;
namespace Gamejam2.Audio;
public sealed record WaveAudioEvent(double Time,string Key,float Pan,int Prey,float Gain,string WaveEventId="");
/// <summary>Sample-addressed chart SFX. Follows music start/stop/resume; never owns a rhythm clock.
/// Wave audio uses raw chart time, while Visual Offset remains visual-only.</summary>
public partial class GameplayAudio : Node
{
    [Export] public int LookaheadFrames { get; set; }=3072;
    [Export] public AudioStreamPlayer CuePlayer { get; set; } = null!;
    [Export] public Node FeedbackVoices { get; set; } = null!;
    private const int Rate=48000;
    
    private sealed record Voice(WaveAudioEvent Event,SfxClip Clip)
    {
        public float Gain { get; }=Mathf.DbToLinear(Clip.VolumeDb)*Event.Gain;
        public float LeftPan { get; }=MathF.Sqrt(1-Event.Pan*Clip.Stereo);
        public float RightPan { get; }=MathF.Sqrt(1+Event.Pan*Clip.Stereo);
    }
    private Dictionary<string,SfxClip> _clips=new();
    private Dictionary<string,Dictionary<string,string>> _catchMix=new();
    private Dictionary<RhythmJudgment,JudgmentEffect> _effects=new();
    private readonly HashSet<int> _consumed=new();
    private readonly List<Voice> _active=new();
    private Vector2[] _mixBuffer=Array.Empty<Vector2>();
    private AudioStreamPlayer[] _feedback=Array.Empty<AudioStreamPlayer>();
    private AudioStreamGeneratorPlayback? _playback;
    private RhythmController _clock=null!;
    private long _sampleCursor;
    private int _next,_capacity,_skips,_poolCursor;
    public IReadOnlyList<WaveAudioEvent> Events { get; private set; }=Array.Empty<WaveAudioEvent>();
    public int WaveEventsRendered { get; private set; }
    public int UnderrunResyncs { get; private set; }
    public int CatchStacks { get; private set; }
    public int WhiffSounds { get; private set; }
    public int GulpLayers { get; private set; }
    public bool IsCuePlaying=>CuePlayer.Playing;
    public bool IsFeedbackPlaying=>_feedback.Any(p=>p.Playing);
    public double RenderedThroughSeconds=>(double)_sampleCursor/Rate;
    public override void _Ready(){_feedback=FeedbackVoices.GetChildren().OfType<AudioStreamPlayer>().ToArray();SetProcess(false);}
    public void Initialize(GameplayData data,InterferencePresenter interference,RhythmController clock,float interferenceGain=1)
    {
        if(_mixBuffer.Length<LookaheadFrames)_mixBuffer=new Vector2[LookaheadFrames];
        Stop();if(_clock!=null){_clock.AudioPlaybackStarted-=Start;_clock.AudioPlaybackSuspended-=Suspend;}
        _clock=clock;_clock.AudioPlaybackStarted+=Start;_clock.AudioPlaybackSuspended+=Suspend;
        if(_clips.Count==0)_clips=SfxCatalog.Load();_effects=data.JudgmentEffects;
        if(_catchMix.Count==0)_catchMix=LocalCsv.Read("res://data/balance/catch_audio.csv").ToDictionary(r=>r["judgment"]);
        var events=data.Chart.Events.Where(e=>e.EventType==RhythmEventType.Cue||(e.EventType==RhythmEventType.InputTarget&&e.WaveEventId.Length>0)).Select(e=>{var p=data.Phrases[int.Parse(e.CueId)];return new WaveAudioEvent(e.TimeSeconds+data.Chart.SongOffsetSeconds,p.WaveKind.ToString().ToLowerInvariant(),p.FromLeft?-1:1,p.Index,1,e.WaveEventId);}).ToList();
        float width=GetViewport().GetVisibleRect().Size.X;
        events.AddRange(interference.AudioSequences().SelectMany(e=>
        {
            var clip=_clips[e.Type];
            return clip.Mode=="emission"
                ?e.Emissions.Select(p=>new WaveAudioEvent(p.Time,e.Type,Math.Clamp(p.Origin.X/width*2-1,-1,1),-1,interferenceGain))
                :new[]{new WaveAudioEvent(e.Time,e.Type,Math.Clamp(e.Origin.X/width*2-1,-1,1),-1,interferenceGain)};
        }));
        Events=events.OrderBy(e=>e.Time).ToArray();_consumed.Clear();WaveEventsRendered=UnderrunResyncs=CatchStacks=WhiffSounds=GulpLayers=0;
    }
    internal void ContinueTimeline(double time)=>Start(time+AudioServer.GetTimeToNextMix()+AudioServer.GetOutputLatency());
    public void ConsumePrey(int index)=>_consumed.Add(index);
    public void Stop()
    {SetProcess(false);CuePlayer.Stop();_playback=null;_active.Clear();foreach(var player in _feedback)player.Stop();}
    private void Suspend()
    {
        SetProcess(false);CuePlayer.Stop();_playback=null;_active.Clear();
        // Pauses freeze all SFX. A terminal stop allows the last short whiff/impact
        // to finish, so starvation does not cut off its own confirmation sound.
        if(_clock.IsRunning)foreach(var player in _feedback)player.Stop();
    }
    private void SeekCursor(double time)
    {
        _sampleCursor=(long)Math.Round(time*Rate);_next=0;_active.Clear();
        while(_next<Events.Count&&Events[_next].Time<=time)
        {
            var e=Events[_next++];var clip=_clips[e.Key];
            if(e.Time+clip.Duration>time&&!(e.WaveEventId.Length==0&&e.Prey>=0&&_consumed.Contains(e.Prey)))_active.Add(new(e,clip));
        }
    }
    private void Start(double time)
    {
        SetProcess(true);CuePlayer.Play();_playback=(AudioStreamGeneratorPlayback)CuePlayer.GetStreamPlayback();
        _capacity=_playback.GetFramesAvailable();_skips=_playback.GetSkips();SeekCursor(time);Pump();
    }
    public override void _Process(double delta)
    {
        if(_playback==null||!CuePlayer.Playing)return;
        int skips=_playback.GetSkips();
        if(skips>_skips)
        {
            // Recover only this SFX buffer from the authoritative clock after an underrun.
            // Music and chart are never restarted, paused, offset or ducked by an effect.
            // Godot permits ClearBuffer only on an inactive generator. Recreate this SFX
            // playback instead; the song player and authoritative clock remain untouched.
            double resume=_clock.SongTimeSeconds+AudioServer.GetTimeToNextMix()+AudioServer.GetOutputLatency();
            CuePlayer.Stop();_playback=null;Start(resume);UnderrunResyncs++;return;
        }
        _skips=skips;Pump();
    }
    private void Pump()
    {
        if(_playback==null)return;
        int free=_playback.GetFramesAvailable(),queued=_capacity-free;
        // Sample-addressed lookahead; presentation stalls never move music or input targets.
        int count=Math.Min(free,Math.Max(0,LookaheadFrames-queued));if(count==0)return;
        // Reuse the same PCM storage. Span length preserves the exact queued sample count.
        if(_mixBuffer.Length<count)_mixBuffer=new Vector2[count];
        var frames=_mixBuffer.AsSpan(0,count);
        for(int i=0;i<count;i++,_sampleCursor++)
        {
            double time=(double)_sampleCursor/Rate;
            while(_next<Events.Count&&Events[_next].Time<=time)
            {
                var e=Events[_next++];
                if(e.WaveEventId.Length==0&&e.Prey>=0&&_consumed.Contains(e.Prey))continue;
                _active.Add(new(e,_clips[e.Key]));WaveEventsRendered++;
            }
            float left=0,right=0;
            for(int v=_active.Count-1;v>=0;v--)
            {
                var voice=_active[v];var clip=voice.Clip;var e=voice.Event;
                double pos=(time-e.Time)*Rate*clip.Pitch;
                if(time-e.Time>=clip.Duration||e.WaveEventId.Length==0&&e.Prey>=0&&_consumed.Contains(e.Prey)){_active.RemoveAt(v);continue;}
                if(pos<0)continue;int sample=(int)pos;
                int next=Math.Min(sample+1,clip.Left.Length-1);float fraction=(float)(pos-sample);
                float gain=voice.Gain*clip.Fade(time-e.Time);
                float l=Mathf.Lerp(clip.Left[sample],clip.Left[next],fraction)*gain;
                float r=Mathf.Lerp(clip.Right[sample],clip.Right[next],fraction)*gain;
                left+=l*voice.LeftPan;right+=r*voice.RightPan;
            }
            // Quiet overlapping interference retains headroom; clamp only as a final PCM safeguard.
            frames[i]=new Vector2(Math.Clamp(left,-.95f,.95f),Math.Clamp(right,-.95f,.95f));
        }
        _playback.PushBuffer(frames);
    }
    private void PlayFeedback(string key,float gain)
    {
        var clip=_clips[key];var player=_feedback.FirstOrDefault(p=>!p.Playing)??_feedback[_poolCursor++%_feedback.Length];
        SfxCatalog.Play(player,clip,gain);
    }
    public void Catch(RhythmJudgment judgment)
    {
        var mix=_catchMix[judgment.ToString().ToLowerInvariant()];CatchStacks++;
        PlayFeedback("bite_success",(float)LocalCsv.Number(mix["snap_gain_db"])+_effects[judgment].BiteVolume);
        PlayFeedback("gulp",(float)LocalCsv.Number(mix["gulp_gain_db"]));GulpLayers++;
        if(bool.Parse(mix["play_impact"]))PlayFeedback("perfect_impact",(float)LocalCsv.Number(mix["impact_gain_db"]));
    }
    public void Whiff(){WhiffSounds++;PlayFeedback("bite_miss",0);}
    public override void _EnterTree(){if(_clock!=null){_clock.AudioPlaybackStarted+=Start;_clock.AudioPlaybackSuspended+=Suspend;}}
    public override void _ExitTree(){if(_clock!=null){_clock.AudioPlaybackStarted-=Start;_clock.AudioPlaybackSuspended-=Suspend;}}
}
