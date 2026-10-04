using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using Gamejam2.Data;
namespace Gamejam2.Gameplay;
/// <summary>CSV schedule observer; contains no input targets, reward logic or independent clock.</summary>
public partial class InterferencePresenter : Node
{
	[Export] public Node2D Effects { get; set; } = null!;
	public event Action<string,double>? Started;
	public event Action<string,Vector2,double>? Emitted;
	private void ForwardEmission(string type,Vector2 origin,double time)=>Emitted?.Invoke(type,origin,time);
	private Dictionary<string,InterferenceEffect> _effects=new();
	private Dictionary<string,InterferenceStyle> _styles=new();
	private (string Type,double Time)[] _schedule=Array.Empty<(string,double)>();
	private readonly Dictionary<string,double> _durations=new();
	private int _next;
	public void Initialize(string songId, (string Type,double Time)[]? overrideSchedule=null)
	{
		foreach(var previous in _effects.Values)previous.Emitted-=ForwardEmission;
		_effects=Effects.GetChildren().OfType<InterferenceEffect>().ToDictionary(e=>e.EffectType);
		if(_styles.Count==0)_styles=LocalCsv.Read("res://data/balance/interference_effects.csv").ToDictionary(row=>row["type"],row=>new InterferenceStyle(row["type"],LocalCsv.Number(row["delay_seconds"]),LocalCsv.Number(row["interval_seconds"]),int.Parse(row["emissions"]),LocalCsv.Number(row["duration_seconds"])));
		if(_styles.Values.Any(s=>s.Delay<0||s.Interval<=0||s.Emissions<=0||s.Duration<=0))throw new ArgumentException("간섭 설정 범위 오류");
		_schedule=overrideSchedule ?? LocalCsv.Read(CustomMapRegistry.Find(songId) is {Generated:true} local?local.InterferencePath:"res://data/balance/interference.csv").Where(row=>row["song_id"]==songId).Select(row=>(row["type"],LocalCsv.Number(row["time_seconds"]))).OrderBy(item=>item.Item2).ToArray();
		if(_schedule.Any(item=>item.Time<0||!_styles.ContainsKey(item.Type)||!_effects.ContainsKey(item.Type)))throw new ArgumentException("간섭 CSV 종류/시점 오류");
		if(!_durations.ContainsKey(songId))_durations[songId]=CustomMapRegistry.Find(songId)?.Duration ?? LocalCsv.Number(LocalCsv.Read("res://data/balance/songs.csv").Single(r=>r["song_id"]==songId)["duration_seconds"]);
		double duration=_durations[songId];
		if(_schedule.Any(item=>item.Time>=duration)||_schedule.GroupBy(item=>item.Time).Any(g=>g.Count()>1))throw new ArgumentException($"Interference starts overlap or exceed song duration: {songId}");
		_next=0;foreach(var effect in _effects.Values){effect.Reset();effect.Emitted+=ForwardEmission;}
	}
	/// <summary>Schedule and designer preview share the same trigger; caller supplies existing Song Clock time.</summary>
	public void Trigger(string type,double songTime) {Started?.Invoke(type,songTime);_effects[type].Begin(_styles[type],songTime);}
	public InterferenceEffect GetEffect(string type)=>_effects[type];
	public void ApplyCurrent(SignalPresenter signals)
	{
		var vortex=_effects["sardine"].Vortex;if(vortex==null)return;
		foreach(var slot in signals.LeftLane.Slots)foreach(var wave in slot.ActivePulses)vortex.Apply(wave);
		foreach(var slot in signals.RightLane.Slots)foreach(var wave in slot.ActivePulses)vortex.Apply(wave);
		foreach(var effect in _effects.Values)foreach(var wave in effect.ActivePulses)vortex.Apply(wave);
	}
	public IEnumerable<(string Type,double Time,Vector2 Origin,float Gain)> AudioPlan()=>_schedule.SelectMany(s=>
		_effects[s.Type].ScheduledEmissions(_styles[s.Type],s.Time).Select(e=>(s.Type,e.Time,e.Origin,e.Gain)));
	/// <summary>One processed texture/warning per interference; Float retains separate chart emissions.</summary>
	public IEnumerable<(string Type,double Time,Vector2 Origin,(double Time,Vector2 Origin,float Gain)[] Emissions)> AudioSequences()=>_schedule.Select(s=>
	{
		var emissions=_effects[s.Type].ScheduledEmissions(_styles[s.Type],s.Time).ToArray();
		return(s.Type,s.Time,emissions[0].Origin,emissions);
	});
	public void Stop(){_next=_schedule.Length;foreach(var effect in _effects.Values)effect.Reset();}
	public void Present(double time)
	{
		while(_next<_schedule.Length&&_schedule[_next].Time<=time){var item=_schedule[_next++];Trigger(item.Type,item.Time);}
		foreach(var effect in _effects.Values)effect.Present(time);
	}
}
