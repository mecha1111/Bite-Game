using System;
using System.Linq;
namespace Gamejam2.Gameplay;
/// <summary>One side per phrase, independent of targets, input results and interference.</summary>
public sealed class PreySideSequence
{
    /// <summary>true = LEFT, false = RIGHT; null before the first completed phrase.</summary>
    public bool? LastPreySide {get;private set;}
    private double _lastCompletedTarget=double.NegativeInfinity;
    public void Reset(){LastPreySide=null;_lastCompletedTarget=double.NegativeInfinity;}
    public void RebaseCheckpoint()=>_lastCompletedTarget=double.NegativeInfinity;
    public void Prepare(GameplayData data,bool? scriptedFirstSide=null)
    {
        if(data.Phrases.Count==0)return;
        bool left=scriptedFirstSide??(LastPreySide.HasValue?!LastPreySide.Value:data.Phrases[0].FromLeft);
        for(int i=0;i<data.Phrases.Count;i++)
        {data.Phrases[i]=data.Phrases[i] with {FromLeft=left};left=!left;}
    }
    public void Complete(PreyPhrase phrase)
    {
        if(!phrase.TargetEvents.All(t=>t.ResolvedState)||phrase.LastTargetSeconds<_lastCompletedTarget)return;
        // An older late timeout must not roll the side backward after a later rapid response.
        LastPreySide=phrase.FromLeft;_lastCompletedTarget=phrase.LastTargetSeconds;
    }
}
