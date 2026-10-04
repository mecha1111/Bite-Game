using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Gamejam2.Lobby;
namespace Gamejam2.Save;
/// <summary>해금 ID, 곡별 FC/AP와 스테이지별 최고 메달을 보관한다. 자동 해금 규칙은 없다.</summary>
public static class ProgressService
{
    private static HashSet<string> _unlocked = new();
    private static HashSet<string> _fullComboSongs = new(), _allPerfectSongs = new();
    private static Dictionary<string,string> _stageMedals=new();
    public static event Action? Changed;
    public static bool DebugUnlockAllStages {get;private set;}
    public static void EnableDebugUnlock(){if(!Gamejam2.Startup.BuildFeatures.DevelopmentEnabled)return;DebugUnlockAllStages=true;Changed?.Invoke();}
    public static void DisableDebugUnlock(){DebugUnlockAllStages=false;Changed?.Invoke();}
    public static void ResetProgress()
    {
        SaveStore.EraseSection("progress");SaveStore.EraseSection("custom_map_progress");
        _unlocked=new(){"stage_1"};_fullComboSongs.Clear();_allPerfectSongs.Clear();_stageMedals.Clear();
        SaveStore.Write("progress","unlocked_stages",new[]{"stage_1"});SaveStore.Write("progress","tutorial_completed",false);
        SaveStore.Flush();Changed?.Invoke();
    }
    public static void Initialize()
    { _unlocked = SaveStore.Read("progress", "unlocked_stages", new string[] { "stage_1" }).AsStringArray().ToHashSet(); _unlocked.Add("stage_1"); SaveStore.Write("progress", "unlocked_stages", _unlocked.OrderBy(id => id).ToArray());
      _fullComboSongs=SaveStore.Read("progress","full_combo_songs",Array.Empty<string>()).AsStringArray().ToHashSet();
      _allPerfectSongs=SaveStore.Read("progress","all_perfect_songs",Array.Empty<string>()).AsStringArray().ToHashSet();
      _stageMedals=SaveStore.Read("progress","stage_medal_ranks",new Godot.Collections.Dictionary()).AsGodotDictionary().ToDictionary(p=>p.Key.AsString(),p=>p.Value.AsString());
       }
    public static bool IsUnlocked(string stageId) => Gamejam2.Startup.BuildFeatures.DevelopmentEnabled&&DebugUnlockAllStages||_unlocked.Contains(stageId);
    /// <summary>향후 스테이지 완료 소비자가 호출할 작은 확장 지점. 현재 게임은 호출하지 않는다.</summary>
    public static void UnlockStage(string stageId)
    {
        if (string.IsNullOrWhiteSpace(stageId) || !_unlocked.Add(stageId)) return;
        SaveStore.Write("progress", "unlocked_stages", _unlocked.OrderBy(id => id).ToArray()); SaveStore.Flush(); Changed?.Invoke();
    }
    public static bool HasFullCombo(string songId)=>_fullComboSongs.Contains(songId);
    public static bool HasAllPerfect(string songId)=>_allPerfectSongs.Contains(songId);
    public static string BestStageRank(string stageId)=>_stageMedals.TryGetValue(stageId,out var rank)?rank:"none";
    private static bool UpdateStageMedals(string songId,double finalSatiety)
    {
        string rank=MedalSharkCatalog.RankFor(finalSatiety);bool changed=false;
        foreach(var stage in GD.Load<StageCatalog>("res://game/lobby/stages.tres").Stages.Where(s=>s.SongId==songId))
            if(MedalSharkCatalog.Priority(rank)>MedalSharkCatalog.Priority(BestStageRank(stage.StageId))){_stageMedals[stage.StageId]=rank;changed=true;}
        return changed;
    }
    private static void SaveMedals()
    {var values=new Godot.Collections.Dictionary();foreach(var p in _stageMedals)values[p.Key]=p.Value;SaveStore.Write("progress","stage_medal_ranks",values);SaveStore.Flush();}
    public static void RecordStageResult(string songId,bool cleared,bool fullCombo,bool allPerfect,double finalSatiety=double.NaN)
    {RecordSongAchievements(songId,fullCombo,allPerfect);if(UpdateStageMedals(songId,finalSatiety)){SaveMedals();Changed?.Invoke();}
     if(double.IsNaN(finalSatiety)?cleared:finalSatiety>=800)
     {string next=songId switch{"song_1"=>"stage_2","song_2"=>"stage_3","song_3"=>"stage_4",_=>""};if(next.Length>0)UnlockStage(next);}}
    /// <summary>Monotonic song achievements and matching stage-card decoration.</summary>
    public static void RecordSongAchievements(string songId,bool fullCombo,bool allPerfect)
    {
        if(string.IsNullOrWhiteSpace(songId))return;
        bool changed=false;
        if(fullCombo||allPerfect)changed|=_fullComboSongs.Add(songId);
        if(allPerfect)changed|=_allPerfectSongs.Add(songId);
        if(!changed)return;
        SaveStore.Write("progress","full_combo_songs",_fullComboSongs.OrderBy(id=>id).ToArray());
        SaveStore.Write("progress","all_perfect_songs",_allPerfectSongs.OrderBy(id=>id).ToArray());

        SaveStore.Flush();Changed?.Invoke();
    }
}
