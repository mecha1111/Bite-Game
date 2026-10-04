using System;
using Godot;
namespace Gamejam2.Gameplay;
/// <summary>UI-only paused countdown. Its completion never starts a second timeline.</summary>
public partial class ResumeCountdown : Control
{
	[Export] public WavePulse Sonar { get; set; } = null!;
	[Export] public Label Number { get; set; } = null!;
	[Export] public ColorRect Shade { get; set; } = null!;
	[Export] public float PopScale { get; set; } = 1.35f;
	public bool IsActive { get; private set; }
	public string CurrentNumber => Number.Text;
	private double _elapsed;
	public void Begin() { _elapsed=0;IsActive=true;Visible=true;Present(); }
	public void Cancel() {IsActive=false;Visible=false;}
	public bool Advance(double delta)
	{
		if(!IsActive) return false;_elapsed=Math.Min(3,_elapsed+delta);
		if(_elapsed>=3){Cancel();return true;}Present();return false;
	}
	private void Present()
	{
		double phase=_elapsed-Math.Floor(_elapsed);
		Sonar.PresentAge(phase);
		Number.Text=(3-(int)_elapsed).ToString();
		Number.Scale=Vector2.One*(float)(1+(PopScale-1)*Math.Max(0,1-phase/.18));
		Number.Modulate=new Color(1,1,1,(float)Math.Clamp((1-phase)/.22,0,1));
		Shade.Color=new Color(.005f,.035f,.075f,(float)(.36*(1-_elapsed/3)));
	}
}
