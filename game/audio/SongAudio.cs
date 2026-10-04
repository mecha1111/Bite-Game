using System;
using System.Text;
using Godot;
namespace Gamejam2.Audio;
/// <summary>Loads the current metadata-selected song through Godot's export remap.
/// Shared by preview and Gameplay; does not own playback or the Song Clock.</summary>
public static class SongAudio
{
    public static AudioStreamMP3 Load(string path)
    {
        if(string.IsNullOrWhiteSpace(path)||!(path.StartsWith("res://",StringComparison.Ordinal)||path.StartsWith("user://custom_maps/custom_local_",StringComparison.Ordinal)&&!path.Contains(".."))||!path.EndsWith(".mp3",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("필수 곡 오디오 경로 오류: "+path);
        path=path.Normalize(NormalizationForm.FormC);
        var stream=path.StartsWith("user://",StringComparison.Ordinal)?new AudioStreamMP3{Data=Godot.FileAccess.GetFileAsBytes(path)}:ResourceLoader.Load<AudioStreamMP3>(path);
        if(stream==null)throw new InvalidOperationException("필수 곡 오디오 누락/읽기 실패: "+path);
        if(stream.Data.Length==0||stream.GetLength()<=0)throw new InvalidOperationException("Current song MP3 empty: "+path);
        stream.Loop=false;
        Gamejam2.Startup.ReleaseDiagnostics.Loaded("Audio",path);
        return stream;
    }
}
