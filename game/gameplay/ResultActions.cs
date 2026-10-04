using System;
using Godot;
namespace Gamejam2.Gameplay;
/// <summary>Shared native actions for receipt and starvation screens.</summary>
public partial class ResultActions : HBoxContainer
{
    [Export] public Button RetryButton { get; set; } = null!;
    [Export] public Button SongSelectButton { get; set; } = null!;
    public event Action? RetryRequested;
    public event Action? SongSelectRequested;
    public override void _Ready()
    { RetryButton.Pressed+=()=>RetryRequested?.Invoke();SongSelectButton.Pressed+=()=>SongSelectRequested?.Invoke(); }
    public void SetEnabled(bool enabled)
    { RetryButton.Disabled=SongSelectButton.Disabled=!enabled; if(enabled)RetryButton.GrabFocus();else if(RetryButton.HasFocus()||SongSelectButton.HasFocus())GetViewport().GuiReleaseFocus(); }
}
