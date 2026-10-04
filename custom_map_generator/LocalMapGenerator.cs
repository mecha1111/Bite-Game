using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json;
using Godot;
using Gamejam2.Data;
namespace Gamejam2.CustomGenerator;
/// <summary>Selected media is cached only in user://. Decode on the main thread, numeric analysis on a worker.</summary>
public static class LocalMapGenerator
{
    public const string Root="user://custom_maps";
    public static async Task<CustomMapDefinition> Generate(Node owner,string selectedFile,Action<string> status)
    {
        string id="custom_local_"+Guid.NewGuid().ToString("N"),folder=Root+"/"+id;
        try
        {
            status("음악 분석 중");using var input=Godot.FileAccess.Open(selectedFile,Godot.FileAccess.ModeFlags.Read);
            if(input==null)throw new InvalidOperationException("선택한 음악을 읽을 수 없습니다.");
            if(input.GetLength()>300_000_000)throw new InvalidOperationException("음원은 300 MB 이하로 선택해 주세요.");
            var bytes=input.GetBuffer((long)input.GetLength());
            var tags=Mp3Metadata.Read(bytes,selectedFile.GetFile().GetBaseName());
            using var audio=new AudioStreamMP3{Data=bytes};double duration=audio.GetLength();
            if(duration<5||duration>1800)throw new InvalidOperationException("5초~30분 길이의 MP3를 선택해 주세요.");
            int rate=(int)AudioServer.GetMixRate(),frames=(int)Math.Ceiling(duration*rate);var mono=new List<float>((int)(duration*11025)+1);
            using(var playback=audio.InstantiatePlayback())
            {
                playback.Start();long next=0;
                for(int offset=0;offset<frames;)
                {
                    int length=Math.Min(8192,frames-offset);var block=playback.MixAudio(1,length);
                    if(block.Length==0)break;
                    for(int i=0;i<block.Length;i++)if((long)(offset+i)*11025>=next*rate){mono.Add((block[i].X+block[i].Y)*.5f);next++;}
                    offset+=block.Length;
                    if(offset%(8192*16)==0)await owner.ToSignal(owner.GetTree(),SceneTree.SignalName.ProcessFrame);
                }
                playback.Stop();
            }
            var analysis=await Task.Run(()=>MusicAnalysis.Analyze(mono.ToArray(),duration));
            status("리듬 생성 중");var targets=await Task.Run(()=>ChartGenerator.Compose(analysis));
            status("연출 구성 중");await owner.ToSignal(owner.GetTree(),SceneTree.SignalName.ProcessFrame);
            status("밸런스 조정 중");var files=SatietyAutoBalance.Compose(id,analysis,targets);
            DirAccess.MakeDirRecursiveAbsolute(folder);
            Write(folder+"/chart_v5.csv",files.Chart);Write(folder+"/camera_fx_v5.csv",files.Camera);Write(folder+"/interference.csv",files.Interference);Write(folder+"/satiety.csv",files.Satiety);
            using(var output=Godot.FileAccess.Open(folder+"/audio.mp3",Godot.FileAccess.ModeFlags.Write)){if(output==null)throw new InvalidOperationException("로컬 음악 저장 실패");output.StoreBuffer(bytes);}
            string cover="";
            if(tags.Artwork!=null)
            {
                using var image=new Image();var art=tags.Artwork;var error=art.Length>8&&art[0]==137&&art[1]==80?image.LoadPngFromBuffer(art):art.Length>2&&art[0]==255&&art[1]==216?image.LoadJpgFromBuffer(art):art.Length>12&&art[0]==82&&art[1]==73?image.LoadWebpFromBuffer(art):Error.FileUnrecognized;
                if(error==Error.Ok){if(image.GetWidth()>1024||image.GetHeight()>1024)image.Resize((int)(image.GetWidth()*Math.Min(1024.0/image.GetWidth(),1024.0/image.GetHeight())),(int)(image.GetHeight()*Math.Min(1024.0/image.GetWidth(),1024.0/image.GetHeight())));cover=folder+"/cover.png";if(image.SavePng(cover)!=Error.Ok)cover="";}
            }
            var map=new CustomMapDefinition{StageId=id,SongId=id,Title=tags.Title,Artist=tags.Artist,Album=tags.Album,Generated=true,Available=true,AudioPath=folder+"/audio.mp3",ChartPath=folder+"/chart_v5.csv",FxPath=folder+"/camera_fx_v5.csv",InterferencePath=folder+"/interference.csv",SatietySectionsPath=folder+"/satiety.csv",CoverPath=cover,EnvironmentProfile="song_1",Duration=duration,MusicBpm=analysis.Bpm,GameplayBpm=analysis.PulseBpm,DrainPerSecond=files.Drain};
            // Metadata is the commit marker; incomplete generations never appear as playable maps.
            Write(folder+"/analysis.json",JsonSerializer.Serialize(new{analysis.Bpm,analysis.PulseBpm,analysis.Confidence,analysis.BpmCandidates,analysis.Beats,analysis.Downbeats,analysis.Sections,analysis.EnergyCurve,analysis.Changes,files.PerfectFinal,files.MixedFinal}));
            Write(folder+"/metadata.json",JsonSerializer.Serialize(map));CustomMapRegistry.Reload();status("완료");return map;
        }
        catch{Delete(id);throw;}
    }
    static void Write(string path,string text){using var file=Godot.FileAccess.Open(path,Godot.FileAccess.ModeFlags.Write);if(file==null)throw new InvalidOperationException("로컬 맵 저장 실패");file.StoreString(text);}
    public static void Delete(string id)
    {
        if(!id.StartsWith("custom_local_",StringComparison.Ordinal)||id.Contains('/')||id.Contains(".."))throw new ArgumentException("Invalid local MapId");
        string folder=Root+"/"+id;using var dir=DirAccess.Open(folder);if(dir==null)return;
        foreach(string file in dir.GetFiles())DirAccess.RemoveAbsolute(folder+"/"+file);
        DirAccess.RemoveAbsolute(folder);CustomMapRegistry.Reload();
    }
}
