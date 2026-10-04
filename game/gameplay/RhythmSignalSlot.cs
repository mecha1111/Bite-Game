using System.Collections.Generic;
using Godot;
namespace Gamejam2.Gameplay;
public enum WaveKind { Small, Medium, Large }
/// <summary>Each chart activation spawns its own ring at this authored origin. Never resets an older ring.</summary>
public partial class RhythmSignalSlot : Control
{
	[Export] public WavePulse Small { get; set; } = null!;
	[Export] public WavePulse Medium { get; set; } = null!;
	[Export] public WavePulse Large { get; set; } = null!;
	[Export] public double DoubleDelaySeconds { get; set; } = .10;
	[Export] public Control? OriginAnchor { get; set; }
	private readonly List<WavePulse> _waves=new();
	public IReadOnlyList<WavePulse> ActivePulses=>_waves;
	public int ActivationCount { get; private set; }
	public bool IsActive=>_waves.Exists(w=>w.Visible);
	public void Activate(WaveKind kind,int count,double scheduledTime,int encounterId=-1,Color? tint=null,string waveEventId="")
	{
		var template=kind==WaveKind.Small?Small:kind==WaveKind.Medium?Medium:Large;
		for(int i=0;i<count;i++){var wave=WavePulse.Spawn(this,template,scheduledTime+i*DoubleDelaySeconds,OriginAnchor?.GlobalPosition);wave.EncounterId=encounterId;wave.WaveEventId=waveEventId;wave.Modulate=tint??Colors.White;_waves.Add(wave);}
		ActivationCount++;
	}
	public void Present(double time)
	{
		for(int i=_waves.Count-1;i>=0;i--){var wave=_waves[i];wave.Present(time);if(wave.Finished){_waves.RemoveAt(i);wave.QueueFree();}}
	}
	public void Reset(){foreach(var wave in _waves)wave.QueueFree();_waves.Clear();ActivationCount=0;}
}
