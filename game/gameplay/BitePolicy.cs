using System;
using System.Linq;
using Gamejam2.Data;
namespace Gamejam2.Gameplay;
public sealed record BitePolicy(int PenaltyStart,double SecondPenalty,double LaterPenalty,double DebounceSeconds)
{
    public double Penalty(int streak)=>streak<PenaltyStart?0:streak==PenaltyStart?SecondPenalty:LaterPenalty;
    public static BitePolicy Load()
    {
        var row=LocalCsv.Read("res://data/balance/bite_policy.csv").Single();
        var result=new BitePolicy(int.Parse(row["miss_streak_penalty_start"]),LocalCsv.Number(row["miss_streak_penalty_2"]),LocalCsv.Number(row["miss_streak_penalty_3_plus"]),LocalCsv.Number(row["hardware_debounce_seconds"]));
        if(result.PenaltyStart<2||result.SecondPenalty<0||result.LaterPenalty<0||result.DebounceSeconds<0||result.DebounceSeconds>.06)throw new ArgumentException("Bite policy range invalid");
        return result;
    }
}
