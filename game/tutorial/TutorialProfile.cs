using Godot;
namespace Gamejam2.Tutorial;
[GlobalClass]
public partial class TutorialProfile : Resource
{
    [Export] public string AudioPath { get; set; }="res://assets/music/1-2.mp3";
    [Export] public string LessonsPath { get; set; }="res://data/tutorial/lessons.csv";
    [Export] public double Bpm { get; set; }=125;
    [Export] public double BeatOffsetSeconds { get; set; }=0;
    [Export] public int RequiredSuccesses { get; set; }=3;
    [Export] public int FailuresBeforeDemo { get; set; }=3;
    [Export] public double AttackSeconds { get; set; }=1.7;
}
