using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using Gamejam2.Lobby;
using Gamejam2.Save;
namespace Gamejam2.Data;
/// <summary>The only core dependency on optional custom content. Missing module means no custom category.</summary>
public static class CustomMapRegistry
{
    private const string Manifest="res://custom_maps/registry/catalog.json";
    private static CustomMapDefinition[]? _maps;
    public static IReadOnlyList<CustomMapDefinition> Maps=>_maps??=Read();
    public static void Reload()=>_maps=null;
    private static CustomMapDefinition[] Read()
    {
        if(!FileAccess.FileExists(Manifest))return Array.Empty<CustomMapDefinition>();
        var maps=JsonSerializer.Deserialize<CustomMapDefinition[]>(FileAccess.GetFileAsString(Manifest),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??Array.Empty<CustomMapDefinition>();
        if(maps.Any(m=>!m.SongId.StartsWith("custom_")||!m.StageId.StartsWith("custom_")||!m.AudioPath.StartsWith("res://custom_maps/")||!m.CoverPath.StartsWith("res://custom_maps/"))||maps.Select(m=>m.SongId).Distinct().Count()!=maps.Length)throw new ArgumentException("Invalid custom registry namespace/paths.");
        foreach(var map in maps)
        {
            if(string.IsNullOrWhiteSpace(map.ChartPath)||!FileAccess.FileExists(map.ChartPath))
            {
                map.Available=false;
                GD.PushError($"Custom Map '{map.SongId}' cannot play: missing prey ChartPath '{map.ChartPath}' in {Manifest}. Supply the authored chart before enabling this entry.");
            }
        }
        var all=maps.ToList();
        using var directory=DirAccess.Open("user://custom_maps");
        if(directory!=null)foreach(string id in directory.GetDirectories().Where(id=>id.StartsWith("custom_local_")&&!id.Contains("..")))
        {
            string root="user://custom_maps/"+id+"/",metadata=root+"metadata.json";
            if(!FileAccess.FileExists(metadata))continue;
            try
            {
                var map=JsonSerializer.Deserialize<CustomMapDefinition>(FileAccess.GetFileAsString(metadata));
                if(map==null||!map.Generated||map.SongId!=id||map.StageId!=id||new[]{map.AudioPath,map.ChartPath,map.FxPath,map.SatietySectionsPath,map.InterferencePath}.Any(path=>!path.StartsWith(root,StringComparison.Ordinal)||path.Contains("..")||!FileAccess.FileExists(path))||map.CoverPath.Length>0&&(!map.CoverPath.StartsWith(root)||map.CoverPath.Contains("..")))throw new ArgumentException("Invalid local map data");
                all.Add(map);
            }
            catch(Exception){GD.PushWarning("Local map unavailable: "+id);}
        }
        return all.ToArray();
    }
    public static CustomMapDefinition? Find(string id)=>Maps.FirstOrDefault(m=>m.SongId==id||m.StageId==id);
    /// <summary>Content membership alone unlocks a custom map; never reads main or legacy lock saves.</summary>
    public static bool IsUnlocked(string id)=>Find(id)!=null;
    public static bool IsCustom(string id)=>id.StartsWith("custom_",StringComparison.Ordinal);
    public static StageCatalog Catalog()
    {
        var catalog=new StageCatalog();
        foreach(var m in Maps)catalog.Stages.Add(new StageData{StageId=m.StageId,SongId=m.SongId,DisplayName=m.Title,SecondaryTag="CUSTOM",ScenePath="res://game/gameplay/Gameplay.tscn",BackgroundTexture=Cover(m),IsAvailable=m.Available,OptionalDescription=m.Generated?m.Artist:"커스텀 맵"});
        return catalog;
    }
    public static Texture2D? Cover(CustomMapDefinition map)
    {
        if(map.CoverPath.Length==0)return null;
        if(!map.Generated)return GD.Load<Texture2D>(map.CoverPath);
        using var image=new Image();return image.Load(map.CoverPath)==Error.Ok?ImageTexture.CreateFromImage(image):null;
    }
    public static string BestRank(string stage)=>SaveStore.Read("custom_map_progress",stage,"none").AsString();
    public static void Record(string song,bool clear,bool fullCombo,bool allPerfect,double finalSatiety=double.NaN)
    {
        var map=Find(song);if(map==null)return;
        if(fullCombo||allPerfect)SaveStore.Write("custom_map_progress",map.StageId+"_full_combo",true);
        if(allPerfect)SaveStore.Write("custom_map_progress",map.StageId+"_all_perfect",true);
        SaveStore.Flush();
        string rank=MedalSharkCatalog.RankFor(finalSatiety);
        if(MedalSharkCatalog.Priority(rank)<=MedalSharkCatalog.Priority(BestRank(map.StageId)))return;
        SaveStore.Write("custom_map_progress",map.StageId,rank);SaveStore.Flush();
    }
}
public sealed class CustomMapDefinition
{
    public bool Generated {get;set;}
    public string Artist {get;set;}="";
    public string Album {get;set;}="";
    public string InterferencePath {get;set;}="";
    public string StageId {get;set;}="";
    public string SongId {get;set;}="";
    public string Title {get;set;}="";
    public string AudioPath {get;set;}="";
    public string CoverPath {get;set;}="";
    public string BackgroundPath {get;set;}="";
    public string ChartPath {get;set;}="";
    public string FxPath {get;set;}="";
    public string SatietySectionsPath {get;set;}="";
    public string EnvironmentProfile {get;set;}="song_1";
    public double Duration {get;set;}
    public double MusicBpm {get;set;}
    public double GameplayBpm {get;set;}
    public double BeatOffset {get;set;}
    public double DrainPerSecond {get;set;}
    public bool Available {get;set;}
    public Dictionary<string,string> SongRow()=>new(){["song_id"]=SongId,["display_name"]=Title,["duration_seconds"]=Duration.ToString(System.Globalization.CultureInfo.InvariantCulture),["drain_per_3_seconds"]=(DrainPerSecond*3).ToString(System.Globalization.CultureInfo.InvariantCulture),["chart_path"]=ChartPath};
    public Dictionary<string,string> TimingRow()=>new(){["song_id"]=SongId,["audio_path"]=AudioPath,["music_bpm"]=MusicBpm.ToString(System.Globalization.CultureInfo.InvariantCulture),["bpm"]=GameplayBpm.ToString(System.Globalization.CultureInfo.InvariantCulture),["beat_offset_sec"]=BeatOffset.ToString(System.Globalization.CultureInfo.InvariantCulture),["crossfade_sec"]="0.5",["preview_start_sec"]=Math.Min(20,Duration*.2).ToString(System.Globalization.CultureInfo.InvariantCulture),["preview_length_sec"]="16",["preview_db"]="-12"};
}
