using System;
using System.Linq;
using Godot;
using Gamejam2.Gameplay;
namespace Gamejam2.Debugging;
/// <summary>Short direction-only checks; no song playback.</summary>
public partial class PreySideChecks : Node
{
    private static void Check(bool ok,string label){if(!ok)throw new Exception(label);GD.Print("PASS "+label);}
    public override void _Ready()
    {
        try
        {
            foreach(string song in new[]{"song_1","song_2","song_3","song_4"}.Concat(Gamejam2.Data.CustomMapRegistry.Maps.Where(m=>m.Generated&&m.Available).Select(m=>m.SongId)))
            {
                var data=GameplayData.Load(song);var times=data.Phrases.SelectMany(p=>p.TargetEvents.Select(t=>t.TargetTime)).ToArray();
                var sequence=new PreySideSequence();sequence.Prepare(data);
                var ten=data.Phrases.Take(10).ToArray();
                Check(ten.Length==10&&ten.Zip(ten.Skip(1),(a,b)=>a.FromLeft!=b.FromLeft).All(x=>x),song+" ten phrases: "+string.Join("→",ten.Select(p=>p.FromLeft?"L":"R")));
                Check(times.SequenceEqual(data.Phrases.SelectMany(p=>p.TargetEvents.Select(t=>t.TargetTime))),song+" target times unchanged");
            }
            var rapid=GameplayData.Load("song_3");var state=new PreySideSequence();state.Prepare(rapid);
            var phrase=rapid.Phrases.First(p=>p.TargetEvents.Length>=3);bool side=phrase.FromLeft;
            phrase.TargetEvents[0].ResolvedState=true;state.Complete(phrase);
            Check(state.LastPreySide==null,"first hit does not finish or flip a multi-target phrase");
            foreach(var t in phrase.TargetEvents)t.ResolvedState=true;state.Complete(phrase);
            Check(state.LastPreySide==side,"all hits completed: LastPreySide updated once for the phrase");
            var next=GameplayData.Load("song_3");state.Prepare(next);
            Check(next.Phrases[0].FromLeft!=side,"next phrase starts opposite completed rapid phrase");
            var story=new PreySideSequence();var lesson=GameplayData.Load("song_1");story.Prepare(lesson,false);
            Check(!lesson.Phrases[0].FromLeft&&lesson.Phrases[1].FromLeft,"scripted RIGHT exception then LEFT");
            foreach(var t in lesson.Phrases[0].TargetEvents)t.ResolvedState=true;story.Complete(lesson.Phrases[0]);story.RebaseCheckpoint();
            var retry=GameplayData.Load("song_1");story.Prepare(retry);
            Check(retry.Phrases[0].FromLeft,"Tutorial checkpoint resumes from completed side");
            story.Reset();Check(story.LastPreySide==null,"new song resets side state");
            GD.Print("DIRECTION CHECKS PASSED; no songs played.");GetTree().Quit();
        }
        catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}
    }
}
