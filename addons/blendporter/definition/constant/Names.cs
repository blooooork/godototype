using Godot;
using System.Collections.Generic;

namespace blendporter.definition;

public static class Names
{
    // Dictionary names
    public static readonly StringName Extras = new ("extras");
    public static readonly IReadOnlyList<StringName> Dictionaries = [Extras];
    // Property names
    public static readonly StringName Transform = new("transform");
    public static readonly StringName GravityScale = new("gravity_scale");
    public static readonly StringName Mass = new("mass");
    public static readonly StringName ResetPosition = new("reset_position");
    public static readonly IReadOnlyList<string> Properties = [Transform, GravityScale, Mass, ResetPosition];
}