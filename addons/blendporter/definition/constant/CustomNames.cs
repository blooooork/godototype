using System.Collections.Generic;

namespace blendporter.definition;

public class CustomNames
{
    public const string Transform = "transform";
    public const string GravityScale = "gravity_scale";
    public const string Mass = "mass";
    public const string ResetPosition = "reset_position";

    public static readonly List<string> All = [Transform, GravityScale, Mass, ResetPosition];
}