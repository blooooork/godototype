using System;
using Godot;

namespace blendporter.definition;

public static class ApplicatorDefinitions
{
    public static readonly Action<Node, object> TransformApplicator =
        (node, value) => ((Node3D)node).SetGlobalTransform((Transform3D)value);
    public static readonly Action<Node, object> PositionApplicator =
        (node, value) =>
        {
            var convertedValue = (Vector3)value;
            var convertedNode = (Node3D)node;
            var nodeBasis = convertedNode.Transform.Basis;
            if (nodeBasis.Determinant() == 0)
                nodeBasis = Basis.Identity;
            var newTransform = new Transform3D(nodeBasis, convertedValue);
            convertedNode.GlobalTransform = newTransform;
        };
    public static readonly Action<Node, object> GravityScaleApplicator =
        (node, value) => ((RigidBody3D)node).SetGravityScale((float)value);
    public static readonly Action<Node, object> MassApplicator = 
        (node, value) => ((RigidBody3D)node).SetMass((float)value);
}