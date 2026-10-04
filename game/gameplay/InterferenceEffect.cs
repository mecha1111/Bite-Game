using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace Gamejam2.Gameplay;
public sealed record InterferenceStyle(string Type,double Delay,double Interval,int Emissions,double Duration);
/// <summary>CSV timing plus scene-authored origins. Every emission is an independent WavePulse instance.</summary>
public partial class InterferenceEffect : Node2D
{
    [Export] public string EffectType { get; set; } = "";
    public event Action<string,Vector2,double>? Emitted;
    public SardineVortex? Vortex { get; private set; }
    private WavePulse[] _origins=Array.Empty<WavePulse>();
    private readonly List<WavePulse> _waves=new();
    private readonly List<Sequence> _sequences=new();
    private sealed class Sequence(InterferenceStyle style,double time)
    {public InterferenceStyle Style=style;public double Time=time;public int Next;public int Count;}
    private Sequence? _latest;
    public IReadOnlyList<WavePulse> ActivePulses=>_waves;
    public int EmissionCount=>_latest?.Count??0;
    public bool IsActive { get; private set; }
    public override void _Ready(){_origins=GetChildren().OfType<WavePulse>().ToArray();foreach(var origin in _origins){origin.FullyOpaque=true;origin.PeakOpacity=1;origin.Modulate=Colors.White;}Vortex=GetChildren().OfType<SardineVortex>().FirstOrDefault();}
    // Audio projects the same CSV emission rules and authored origins into sample positions.
    public IEnumerable<(double Time,Vector2 Origin,float Gain)> ScheduledEmissions(InterferenceStyle style,double time)
    {
        if(EffectType=="ship_horn")yield return(time,_origins[0].GlobalPosition,1);
        for(int i=0;i<style.Emissions;i++)
        {
            double stamp=time+style.Delay+i*style.Interval;
            if(EffectType=="sardine")foreach(var origin in _origins)yield return(stamp,origin.GlobalPosition,1/MathF.Sqrt(_origins.Length));
            else yield return(stamp,_origins[EffectType=="ship_horn"?(i+1)%_origins.Length:i%_origins.Length].GlobalPosition,1);
        }
    }
    public void Begin(InterferenceStyle style,double time)
    {
        if(EffectType=="sardine")Vortex?.Begin(time);
        var sequence=new Sequence(style,time);_sequences.Add(sequence);_latest=sequence;IsActive=true;
        if(EffectType=="ship_horn"){Emit(_origins[0],time);sequence.Count++;}
    }
    private void Emit(WavePulse origin,double time){_waves.Add(WavePulse.Spawn(this,origin,time));Emitted?.Invoke(EffectType,origin.GlobalPosition,time);}
    public void Present(double time)
    {
        Vortex?.Present(time);
        foreach(var sequence in _sequences)
        {
            var style=sequence.Style;double age=time-sequence.Time;
            while(sequence.Next<style.Emissions && age>=style.Delay+sequence.Next*style.Interval)
            {
                double start=sequence.Time+style.Delay+sequence.Next*style.Interval;
                if(EffectType=="sardine"){Emitted?.Invoke(EffectType,GlobalPosition,start);}
                else Emit(_origins[EffectType=="ship_horn"?(sequence.Next+1)%_origins.Length:sequence.Next%_origins.Length],start);
                sequence.Next++;sequence.Count++;
            }
        }
        _sequences.RemoveAll(s=>time-s.Time>=s.Style.Duration&&s.Next==s.Style.Emissions);
        IsActive=_sequences.Count>0;
        // Sequence completion never truncates a large ring before it clears the viewport.
        for(int i=_waves.Count-1;i>=0;i--){var wave=_waves[i];wave.Present(time);if(wave.Finished){_waves.RemoveAt(i);wave.QueueFree();}}
    }
    public void Reset(){Vortex?.Clear();foreach(var wave in _waves)wave.QueueFree();_waves.Clear();_sequences.Clear();_latest=null;IsActive=false;}
}
