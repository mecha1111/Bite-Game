using System;
using System.Linq;
using System.Collections.Generic;
using Gamejam2.Data;
using Gamejam2.Rhythm;
namespace Gamejam2.Gameplay;
public sealed record JudgmentEffect(float Diameter,float Opacity,double Duration,int Bubbles,float ScreenPulse,float ComboPop,float SatietyPulse,float BiteVolume);
public static class JudgmentPresentation
{
    public static Dictionary<RhythmJudgment,JudgmentEffect> Load()
    {
        var effects=LocalCsv.Read("res://data/balance/judgment_effects.csv").ToDictionary(r=>Enum.Parse<RhythmJudgment>(r["judgment"],true),r=>new JudgmentEffect(
            (float)LocalCsv.Number(r["impact_wave_diameter"]),(float)LocalCsv.Number(r["impact_wave_opacity"]),LocalCsv.Number(r["impact_duration_sec"]),int.Parse(r["bubble_count"]),
            (float)LocalCsv.Number(r["screen_pulse_strength"]),(float)LocalCsv.Number(r["combo_pop_scale"]),(float)LocalCsv.Number(r["satiety_pulse_strength"]),(float)LocalCsv.Number(r["bite_sfx_volume_modifier"])));
        if(effects.Count!=4||Enum.GetValues<RhythmJudgment>().Any(j=>!effects.ContainsKey(j))||effects.Values.Any(e=>e.Diameter<0||e.Opacity<0||e.Opacity>1||e.Duration<=0||e.Bubbles<0||e.ScreenPulse<0||e.ComboPop<1||e.SatietyPulse<0))throw new ArgumentException("Four-grade judgment presentation config invalid");
        return effects;
    }
}
