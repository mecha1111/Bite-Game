using System;
using System.Collections.Generic;
using Godot;
using Gamejam2.Rhythm;
namespace Gamejam2.Gameplay;
/// <summary>Observes existing events. Never schedules targets or changes the rhythm clock.</summary>
public partial class GameplayJuice : Control
{
    // Shared native names avoid disposable per-frame wrappers.
    private static readonly StringName UniformIntensity = "intensity";
    private static readonly StringName UniformPhase = "phase";
    private static readonly StringName UniformPresentationDim = "presentation_dim";
    private static readonly StringName UniformPresentationDull = "presentation_dull";
    private static readonly StringName UniformPresentationImpulsePixels = "presentation_impulse_pixels";
    private static readonly StringName UniformPresentationStrength = "presentation_strength";
    private static readonly StringName UniformPresentationTint = "presentation_tint";
    private static readonly StringName UniformPressureStrength = "pressure_strength";

    [Export] public PackedScene CatchBurstScene { get; set; } = null!;
    [Export] public SharkJumpBubbleEffect JumpBubbles { get; set; }=null!;
    [Export] public CatchConfirmation CatchSeal { get; set; } = null!;
    [Export] public GameplayJuiceProfile Profile { get; set; } = null!;
    [Export] public ColorRect LeftCue { get; set; } = null!;
    [Export] public ColorRect RightCue { get; set; } = null!;
    [Export] public WavePulse ImpactTemplate { get; set; } = null!;
    public Control MouthAnchor { get; set; } = null!;
    public SharkPresenter Shark { get; set; } = null!;
    public ColorRect Water { get; set; } = null!;
    private static PackedScene? _burstScene;
    public override void _Ready(){Gamejam2.UI.AuthoredControlLayout.Restore(this,"res://game/gameplay/presentation/GameplayJuice.tscn",".");WavePulse.Preload();_burstScene??=GD.Load<PackedScene>("res://game/gameplay/presentation/JuiceBurst.tscn");SetProcess(false);}
    private GameplayData _data=null!;
    private int _nextPrey;
    private double _leftAt=double.NegativeInfinity,_rightAt=double.NegativeInfinity;
    private double _hitAt=double.NegativeInfinity,_pressureAt=double.NegativeInfinity;
    private double _whaleUntil=double.NegativeInfinity,_sardineUntil=double.NegativeInfinity;
    private RhythmJudgment _judgment;
    private float _pauseBlend;
    private readonly List<WavePulse> _rings=new();
    private readonly List<JuiceBurst> _bursts=new();
    private readonly List<CatchBurstEffect> _catches=new();
    public IReadOnlyList<CatchBurstEffect> CatchBursts=>_catches;
    public int EncounterCueCount { get; private set; }
    public int BiteEffectCount { get; private set; }
    public int JudgmentEffectCount { get; private set; }
    public int InterferencePulseCount { get; private set; }
    public float PauseBlend=>_pauseBlend;
    public WavePulse? LastCatchImpact { get; private set; }
    public int CatchImpactCount { get; private set; }
    public void Reset(GameplayData data)
    {
        JumpBubbles.Reset();
        foreach(var caught in _catches)caught.QueueFree();_catches.Clear();
        CatchSeal.Clear();LastCatchImpact=null;CatchImpactCount=0;_data=data;_nextPrey=0;_pauseBlend=0;Modulate=Colors.White;
        _leftAt=_rightAt=_hitAt=_pressureAt=_whaleUntil=_sardineUntil=double.NegativeInfinity;
        EncounterCueCount=BiteEffectCount=JudgmentEffectCount=InterferencePulseCount=0;
        foreach(var ring in _rings)ring.QueueFree();_rings.Clear();
        foreach(var burst in _bursts)burst.QueueFree();_bursts.Clear();
    }
    public void ClearTransientEffects()
    {
        JumpBubbles.Reset();CatchSeal.Clear();LastCatchImpact=null;
        foreach(var effect in _catches)effect.QueueFree();_catches.Clear();
        foreach(var effect in _rings)effect.QueueFree();_rings.Clear();
        foreach(var effect in _bursts)effect.QueueFree();_bursts.Clear();
    }
    internal void SkipFutureEncounterCues()=>_nextPrey=_data.Phrases.Count;
    private WavePulse Ring(Vector2 origin,double time,float diameter,float opacity,Color color,double duration=.18)
    {
        var ring=WavePulse.Spawn(this,ImpactTemplate,time,origin);ring.MediumDiameter=diameter;ring.PeakOpacity=ring.CoreFlashOpacity=opacity;ring.DurationSeconds=duration;ring.ActivationBoost=0;ring.Modulate=color;ring.Present(time);_rings.Add(ring);return ring;
    }
    private JuiceBurst Burst(Vector2 origin,double time,int count,Color color,double? observedTime=null)
    {
        var burst=_burstScene!.Instantiate<JuiceBurst>();
        AddChild(burst);burst.GlobalPosition=origin;burst.Begin(time,count,color);burst.Present(observedTime??time);_bursts.Add(burst);return burst;
    }
    public void Bite(double time)
    {BiteEffectCount++;JumpBubbles.Emit(time,Shark.GlobalPosition+new Vector2(0,-28),seed:BiteEffectCount);}
    public void Judgment(RhythmJudgment judgment,double time,int combo)
    {
        JudgmentEffectCount++;_judgment=judgment;_hitAt=time;LastCatchImpact=null;
        if(judgment!=RhythmJudgment.Miss)
        {
            var caught=CatchBurstScene.Instantiate<CatchBurstEffect>();
            AddChild(caught);caught.GlobalPosition=Shark.MouthImpact.GlobalPosition;caught.Begin(judgment,time);_catches.Add(caught);
            CatchSeal.GlobalPosition=MouthAnchor.GlobalPosition;CatchSeal.ShowCatch(judgment,time);
            var effect=_data.JudgmentEffects[judgment];var tint=new Color(.78f,1,1);
            if(effect.Bubbles>0)caught.ImpactBubbles=Burst(caught.GlobalPosition,time+caught.FishRecognitionHold,effect.Bubbles,tint,time);
        }
    }
    public void InterferenceStarted(string type,double time)
    {
        if(type=="whale")_whaleUntil=time+2;
        if(type=="sardine")_sardineUntil=time+3;
    }
    public void InterferenceEmission(string type,Vector2 origin,double time)
    {
        InterferencePulseCount++;
        if(type is "ship_horn" or "whale")_pressureAt=time;
        if(type=="fishing_float")Ring(origin,time+Gamejam2.Settings.SettingsService.VisualOffsetSeconds,80,.25f,new Color(.6f,.95f,1));
    }
    public void Present(double visualTime,double worldTime,bool paused,bool countdown,double delta)
    {
        JumpBubbles.Present(worldTime);
        while(_nextPrey<_data.Phrases.Count&&_data.Phrases[_nextPrey].StartSeconds<=visualTime)
        {var p=_data.Phrases[_nextPrey++];if(p.FromLeft)_leftAt=p.StartSeconds;else _rightAt=p.StartSeconds;EncounterCueCount++;}
        CatchSeal.GlobalPosition=Shark.MouthImpact.GlobalPosition;CatchSeal.Present(worldTime);Cue(LeftCue,visualTime-_leftAt);Cue(RightCue,visualTime-_rightAt);
        for(int i=_rings.Count-1;i>=0;i--){_rings[i].Present(worldTime);if(_rings[i].Finished){_rings[i].QueueFree();_rings.RemoveAt(i);}}
        for(int i=_bursts.Count-1;i>=0;i--){_bursts[i].Present(worldTime);if(_bursts[i].Finished){_bursts[i].QueueFree();_bursts.RemoveAt(i);}}
        _pauseBlend=Mathf.MoveToward(_pauseBlend,paused&&!countdown?1:0,(float)(delta/Math.Max(.01,Profile.PauseEaseSeconds)));
        for(int i=_catches.Count-1;i>=0;i--){if(_catches[i].IsRecognitionHold(worldTime)){_catches[i].GlobalPosition=Shark.MouthImpact.GlobalPosition;if(_catches[i].ImpactRing!=null)_catches[i].ImpactRing!.GlobalPosition=_catches[i].GlobalPosition;if(_catches[i].ImpactBubbles!=null)_catches[i].ImpactBubbles!.GlobalPosition=_catches[i].GlobalPosition;}_catches[i].Present(worldTime);if(_catches[i].Finished){_catches[i].QueueFree();_catches.RemoveAt(i);}}
        float hit=Envelope(worldTime-_hitAt,.24);
        float pressure=Math.Max(Envelope(visualTime-_pressureAt,.28),visualTime<_whaleUntil?.5f:0);
        float shimmer=visualTime<_sardineUntil?.12f:0;
        var water=(ShaderMaterial)Water.Material;
        float impulseAge=(float)(worldTime-_hitAt);
        float impulse=impulseAge>=0&&impulseAge<.09f?Mathf.Sin(impulseAge/.09f*Mathf.Pi*2)*(1-impulseAge/.09f):0;
        water.SetShaderParameter(UniformPresentationImpulsePixels,Vector2.Zero);
        water.SetShaderParameter(UniformPressureStrength,Profile.InterferenceRefractionStrength*(pressure+shimmer));
        water.SetShaderParameter(UniformPresentationDim,pressure*.035f+_pauseBlend*.045f+(_judgment==RhythmJudgment.Miss?hit*Profile.MissDimStrength:0));
        water.SetShaderParameter(UniformPresentationDull,_judgment==RhythmJudgment.Miss?hit*.15f:0);
        var tint=_judgment==RhythmJudgment.Bad?new Vector3(.4f,.65f,.6f):new Vector3(.45f,.95f,1);
        float strength=_data.JudgmentEffects[_judgment].ScreenPulse;
        water.SetShaderParameter(UniformPresentationTint,tint);water.SetShaderParameter(UniformPresentationStrength,strength*hit);
    }
    private static float Envelope(double age,double duration)=>age<0?0:(float)Math.Clamp(1-age/duration,0,1);
    private void Cue(ColorRect cue,double age)
    {
        float phase=(float)Math.Clamp(age/Math.Max(.01,Profile.DirectionCueDuration),0,1);
        var material=(ShaderMaterial)cue.Material;
        material.SetShaderParameter(UniformPhase,phase);material.SetShaderParameter(UniformIntensity,Envelope(age,Profile.DirectionCueDuration)*.12f);
    }
}
