using System;
using System.Collections.Generic;
using Godot;
namespace Gamejam2.UI;
/// <summary>Restores existing component-authored bounds when binary nested scene overrides lose them.</summary>
public static class AuthoredControlLayout
{
    private static readonly Dictionary<string,PackedScene> Scenes=new();
    public static void Restore(Node instance,string scenePath,params string[] nodes)
    {
        if(!Scenes.TryGetValue(scenePath,out var scene))
            Scenes[scenePath]=scene=ResourceLoader.Load<PackedScene>(scenePath)
                ??throw new InvalidOperationException("Authored layout resource missing: "+scenePath);
        // No tree entry: this does not run scripts, animations, or input handlers.
        var authored=scene.Instantiate();
        try
        {
            foreach(var path in nodes)
            {
                var from=authored.GetNode<Control>(path);var to=instance.GetNode<Control>(path);
                foreach(var side in new[]{Side.Left,Side.Top,Side.Right,Side.Bottom})
                    to.SetAnchorAndOffset(side,from.GetAnchor(side),from.GetOffset(side));
                to.GrowHorizontal=from.GrowHorizontal;to.GrowVertical=from.GrowVertical;
            }
        }
        finally{authored.Free();}
    }
}
