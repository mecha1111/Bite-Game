using System;
using Godot;
using Gamejam2.Rhythm;
namespace Gamejam2.Gameplay;
/// <summary>1000 is 100% presentation; maximum 1100 is a distinct overfill state.
/// Only visual interpolation moves slowly. Gameplay satiety and displayed percent are immediate.</summary>
public partial class SatietyGauge : Control
{
    // Shared native names avoid disposable per-frame wrappers.
    private static readonly StringName UniformCriticalStrength = "critical_strength";
    private static readonly StringName UniformFillRatio = "fill_ratio";
    private static readonly StringName UniformLossPulse = "loss_pulse";
    private static readonly StringName UniformLowStrength = "low_strength";
    private static readonly StringName UniformOverfillStrength = "overfill_strength";
    private static readonly StringName UniformRecoveryPhase = "recovery_phase";
    private static readonly StringName UniformRecoveryPulse = "recovery_pulse";
    private static readonly StringName UniformTimeSeconds = "time_seconds";

    public const double FullBodySatiety=1000;
    [Export] public Control FillClip { get; set; } = null!;
    [Export] public Control Threshold { get; set; } = null!;
    [Export] public Label Amount { get; set; } = null!;
    [Export] public TextureRect Frame { get; set; } = null!;
    public GameplayJuiceProfile? JuiceProfile { get; set; }
    public System.Collections.Generic.Dictionary<RhythmJudgment,JudgmentEffect> JudgmentEffects { get; set; } = JudgmentPresentation.Load();
    public double DisplayedValue { get; private set; }
    public double DisplayedPercent { get; private set; }
    public float BodyFillRatio { get; private set; }
    public float OverfillStrength { get; private set; }
    public float RecoveryPulse { get; private set; }
    public bool IsClear { get; private set; }
    private ShaderMaterial _material=null!;
    private double _gainAt=double.NegativeInfinity,_gainFrom,_lossAt=double.NegativeInfinity,_lossFrom,_lastTime=double.NaN;
    private float _strength;
    public override void _Ready()=>_material=(ShaderMaterial)Frame.Material;
    public void ResetDisplay(double value)
    {DisplayedValue=Math.Max(0,value);_gainAt=_lossAt=double.NegativeInfinity;_lastTime=double.NaN;_strength=0;Frame.SelfModulate=Colors.White;}
    public void NotifyGain(double time,RhythmJudgment judgment)
    {
        if(judgment==RhythmJudgment.Miss){NotifyMiss();return;}
        _lossAt=double.NegativeInfinity;_gainFrom=DisplayedValue;_gainAt=time;_strength=JudgmentEffects[judgment].SatietyPulse;
    }
    public void NotifyPenalty(double time){_gainAt=double.NegativeInfinity;_lossAt=time;_lossFrom=DisplayedValue;}
    public void NotifyMiss(){_gainAt=double.NegativeInfinity;_strength=0;}
    public void Present(double value,double max,double threshold)=>Present(value,max,threshold,double.PositiveInfinity);
    public void Present(double value,double max,double threshold,double time)
    {
        double actual=double.IsFinite(value)?Math.Clamp(value,0,Math.Max(0,max)):0;
        bool timed=double.IsFinite(time);double age=time-_gainAt,lossAge=time-_lossAt;
        float phase=(float)Math.Clamp(age/.22,0,1);
        if(!timed)DisplayedValue=actual;
        else if(age>=0&&age<.22){float ease=1-(1-phase)*(1-phase)*(1-phase);DisplayedValue=_gainFrom+(actual-_gainFrom)*ease;}
        else if(lossAge>=0&&lossAge<.18)DisplayedValue=_lossFrom+(actual-_lossFrom)*Math.Clamp(lossAge/.18,0,1);
        else if((double.IsFinite(_gainAt)&&age>=.22&&(!double.IsFinite(_lastTime)||_lastTime<_gainAt+.22))
             ||(double.IsFinite(_lossAt)&&lossAge>=.18&&(!double.IsFinite(_lastTime)||_lastTime<_lossAt+.18)))DisplayedValue=actual;
        else
        {
            double dt=double.IsFinite(_lastTime)?Math.Clamp(time-_lastTime,0,.3):0;
            DisplayedValue+=(actual-DisplayedValue)*(1-Math.Exp(-dt*20));
            if(Math.Abs(actual-DisplayedValue)<.05)DisplayedValue=actual;
        }
        _lastTime=timed?time:double.NaN;DisplayedValue=Math.Clamp(DisplayedValue,0,Math.Max(0,max));
        DisplayedPercent=actual/10;Amount.Text=$"{DisplayedPercent:0.#}%";
        BodyFillRatio=(float)Math.Clamp(DisplayedValue/FullBodySatiety,0,1);
        OverfillStrength=(float)Math.Clamp((DisplayedValue-FullBodySatiety)/100,0,1);
        IsClear=actual>=threshold;
        FillClip.AnchorRight=BodyFillRatio;
        Threshold.AnchorLeft=Threshold.AnchorRight=(float)Math.Clamp(threshold/FullBodySatiety,0,1);
        Threshold.Modulate=IsClear?new Color(.6f,1,1):new Color(.72f,.83f,.88f);
        RecoveryPulse=timed?(float)Math.Clamp(1-age/.30,0,1)*_strength:0;
        float loss=timed?(float)Math.Clamp(1-lossAge/.24,0,1):0;
        float slow=timed?.5f+.5f*Mathf.Sin((float)time*1.8f):0;
        Frame.SelfModulate=Colors.White.Lerp(new Color(.7f,1,1),Math.Clamp(RecoveryPulse*(JuiceProfile?.SatietyGaugePulseStrength??.16f)+OverfillStrength*(.05f+.04f*slow),0,1));
        _material.SetShaderParameter(UniformFillRatio,BodyFillRatio);_material.SetShaderParameter(UniformOverfillStrength,OverfillStrength);
        _material.SetShaderParameter(UniformTimeSeconds,timed?(float)time:0);_material.SetShaderParameter(UniformRecoveryPulse,RecoveryPulse);
        _material.SetShaderParameter(UniformRecoveryPhase,phase);_material.SetShaderParameter(UniformLossPulse,loss);
        _material.SetShaderParameter(UniformLowStrength,actual<300?1f:0f);_material.SetShaderParameter(UniformCriticalStrength,actual<150?1f:0f);
    }
}
