using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using Gamejam2.Data;
namespace Gamejam2.Lobby;
public sealed record MedalSharkDefinition(string Rank,int Priority,string Criterion,Texture2D Texture,Rect2 Region,bool NaturalFacesLeft,float WidthFraction,double SwimSeconds);
/// <summary>Final-Satiety medal visuals, loaded once. FC/AP records remain independent.</summary>
public static class MedalSharkCatalog
{
    private static MedalSharkDefinition[]? _definitions;
    public static MedalSharkDefinition[] Definitions
    {
        get
        {
            if(_definitions!=null)return _definitions;
            var rows=LocalCsv.Read("res://data/presentation/medal_sharks.csv");
            _definitions=rows.Select(r=>
            {
                var texture=GD.Load<Texture2D>(r["texture_path"]);using var image=texture.GetImage();
                return new MedalSharkDefinition(r["rank"],int.Parse(r["priority"]),r["criterion"],texture,image.GetUsedRect(),bool.Parse(r["natural_faces_left"]),(float)LocalCsv.Number(r["width_fraction"]),LocalCsv.Number(r["swim_seconds"]));
            }).OrderByDescending(d=>d.Priority).ToArray();
            if(_definitions.Length!=3||_definitions.Select(d=>d.Priority).Distinct().Count()!=3||_definitions.Select(d=>d.Rank).Distinct().Count()!=3||_definitions.Any(d=>d.Priority<1||d.Criterion is not ("800" or "1000" or "1100")||d.WidthFraction<.1||d.WidthFraction>.2||!double.IsFinite(d.SwimSeconds)||d.SwimSeconds<8))throw new ArgumentException("Invalid medal shark mapping.");
            return _definitions;
        }
    }
    public static MedalSharkDefinition? Find(string rank)=>Definitions.FirstOrDefault(d=>d.Rank==rank);
    public static string RankFor(double finalSatiety)=>finalSatiety>=1100?"gold":finalSatiety>=1000?"silver":finalSatiety>=800?"bronze":"none";
    // Compatibility for old development checks; performance achievements never award medals.
    public static string RankFor(bool cleared,bool fullCombo,bool allPerfect)=>"none";
    public static int Priority(string rank)=>Find(rank)?.Priority??0;
}
