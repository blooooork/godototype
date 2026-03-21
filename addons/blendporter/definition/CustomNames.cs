using System.Collections.Generic;

namespace blendporter.definition;

public class CustomNames
{
    public static readonly string Transform = "transform";
    public static readonly string GravityScale = "gravity_scale";
    public static readonly string Mass = "mass";
    public static readonly string ResetPosition = "reset_position";

    public static readonly List<string> All = [Transform, GravityScale, Mass, ResetPosition];
}