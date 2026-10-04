using System;
using System.Threading.Tasks;
using Godot;
using Gamejam2.Rhythm;
namespace Gamejam2.Debugging;
public partial class AntiMashChecks : Node
{
    private RhythmController _rhythm = null!;
    private int _results, _checks;
    private void Check(bool ok,string name) { if(!ok) throw new InvalidOperationException(name); _checks++; GD.Print("PASS "+name); }
    private async Task At(double time) { while(_rhythm.SongTimeSeconds < time) await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); }
    private void Start(params double[] times)
    {
        var chart = new RhythmChart(); foreach(double time in times) chart.Events.Add(new RhythmEventData{TimeSeconds=time,EventType=RhythmEventType.InputTarget});
        _rhythm.Chart=chart; _results=0; Check(_rhythm.StartSong(),"start authoritative clock");
    }
    public override void _Ready() => Callable.From(()=>{_=Run();}).CallDeferred();
    private async Task Run()
    {
        try
        {
            var music = new AudioStreamPlayer(); AddChild(music);
            _rhythm = new RhythmController{MusicPlayer=music, Chart=new RhythmChart(),EnableDebugLogging=false}; AddChild(_rhythm);
            _rhythm.JudgmentResolved += _=>_results++;
            Start(.5,3); await At(.5); Check(_rhythm.TryJudgeInput() && _rhythm.LastResult!.Value.Judgment==RhythmJudgment.Perfect,"accurate first attempt PERFECT");
            Start(.5,3); await At(.29); Check(_rhythm.TryJudgeInput() && _rhythm.LastResult!.Value.Judgment==RhythmJudgment.Miss,"early captured input consumes as MISS");
            await At(.49); Check(!_rhythm.TryJudgeInput() && _results==1 && _rhythm.LastResult!.Value.Judgment==RhythmJudgment.Miss,"accurate later input cannot repair MISS");
            Start(.5,3); await At(.325); Check(_rhythm.TryJudgeInput() && _rhythm.LastResult!.Value.Judgment==RhythmJudgment.Bad,"early BAD consumes target");
            await At(.5); Check(!_rhythm.TryJudgeInput() && _results==1,"later PERFECT cannot repair BAD");
            Start(.5,3); await At(.1); Check(!_rhythm.TryJudgeInput() && _results==0,"far input ignored");
            for(int i=0;i<7;i++){await At(.2+i*.1);_rhythm.TryJudgeInput();}
            Check(_results==1 && _rhythm.LastResult!.Value.Judgment!=RhythmJudgment.Perfect,"10 presses per second cannot fish for PERFECT (first capture remains committed)");
            Start(.5,3); await At(.7); Check(_results==0,"no premature auto MISS at successful window end"); await At(.77); Check(_results==1 && _rhythm.LastResult!.Value.Judgment==RhythmJudgment.Miss,"auto MISS after late capture deadline");
            Start(.5,.6,3); await At(.57); Check(_rhythm.TryJudgeInput() && _results==1 && Math.Abs(_rhythm.LastResult!.Value.TargetEvent.TimeSeconds-.6)<.001,"one press captures only closest target out of order");
            Check(!_rhythm.TryJudgeInput() && _results==1,"consumed closest target shields neighbour from duplicate press");
            await At(.77); Check(_results==2,"earlier unresolved target still auto misses after out-of-order capture");
            Start(.5,3); bool reentered=false; void Again(RhythmJudgmentResult _) { reentered=_rhythm.TryJudgeInput(); }
            _rhythm.JudgmentResolved+=Again; await At(.5); _rhythm.TryJudgeInput(); _rhythm.JudgmentResolved-=Again;
            Check(!reentered && _results==1,"consume before callbacks prevents reentrant attempt");
            GD.Print($"ANTI MASH VERIFIED: {_checks} checks"); GetTree().Quit();
        }
        catch(Exception error){GD.PushError(error.ToString());GetTree().Quit(1);}
    }
}
