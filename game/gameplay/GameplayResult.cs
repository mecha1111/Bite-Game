using Gamejam2.Rhythm;
namespace Gamejam2.Gameplay;
/// <summary>Immutable player result data. No persistence/progression framework.</summary>
public sealed record GameplayResult(string SongTitle,double FinalSatiety,double ClearThreshold,int Perfect,int Good,int Bad,int Miss,int MaxCombo,int ExpectedTargets)
{
    public long Score { get; init; }
    public bool Cleared=>FinalSatiety>=ClearThreshold;
    public bool FullCombo=>ExpectedTargets>0 && Perfect+Good+Bad==ExpectedTargets && Miss==0;
    public bool AllPerfect=>FullCombo && Perfect==ExpectedTargets;
}
