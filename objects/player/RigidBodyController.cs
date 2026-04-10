using Godot;

namespace godototype.objects.player;

public partial class RigidBodyController : Node
{
    [Export] public float LeanLimitDeg    { get; set; } = 20f;
    [Export] public float UprightStrength { get; set; } = 30f;
    [Export] public float IdleTiltDamping { get; set; } = 5f;
    [Export] public float MoveForce       { get; set; } = 10f;
    [Export] public float MaxSpeed        { get; set; } = 5f;
    [Export] public float SprintModifier  { get; set; } = 1.2f;
    [Export] public float LeanTorque      { get; set; } = 15f;
    [Export] public float RotateSpeed     { get; set; } = 1.5f;
    [Export] public float JumpForce       { get; set; } = 8f;

    public float ForwardInput { get; set; }
    public float StrafeInput  { get; set; }
    public float RotateInput  { get; set; }
    public bool  IsSprinting  { get; set; }

    private RigidBody3D _body;
    private RayCast3D _groundRayFront;
    private RayCast3D _groundRayLeft;
    private RayCast3D _groundRayRight;
    private RayCast3D _groundRayBack;

    public override void _EnterTree()
    {
        _body           = GetParent<RigidBody3D>();
        _groundRayFront = _body.GetNodeOrNull<RayCast3D>("GroundRayFront");
        _groundRayLeft  = _body.GetNodeOrNull<RayCast3D>("GroundRayLeft");
        _groundRayRight = _body.GetNodeOrNull<RayCast3D>("GroundRayRight");
        _groundRayBack  = _body.GetNodeOrNull<RayCast3D>("GroundRayBack");
    }

    public override void _PhysicsProcess(double delta)
    {
        ApplyStabilization();
        ApplyMovement();
        ApplyTurning();
    }

    public void Jump()
    {
        if (IsGrounded())
            _body.ApplyCentralImpulse(Vector3.Up * _body.Mass * JumpForce);
    }

    private bool IsGrounded() =>
        (_groundRayFront?.IsColliding() ?? false) ||
        (_groundRayRight?.IsColliding() ?? false) ||
        (_groundRayLeft?.IsColliding()  ?? false) ||
        (_groundRayBack?.IsColliding()  ?? false);

    // Pulls the body's up vector back toward world up each frame.
    private void ApplyStabilization()
    {
        var correction = _body.Transform.Basis.Y.Cross(Vector3.Up);
        _body.ApplyTorque(correction * UprightStrength);

        if (ForwardInput == 0f && StrafeInput == 0f)
        {
            // Damp tilt angular velocity on X/Z to kill wobble when idle.
            var tiltDamp = new Vector3(-_body.AngularVelocity.X, 0f, -_body.AngularVelocity.Z) * IdleTiltDamping;
            _body.ApplyTorque(tiltDamp);
        }
    }

    // Leans the body in the move direction and drives it forward.
    private void ApplyMovement()
    {
        if (ForwardInput == 0f && StrafeInput == 0f) return;
        if (!IsGrounded()) return;

        var forward = new Vector3(-_body.Transform.Basis.Z.X, 0f, -_body.Transform.Basis.Z.Z).Normalized();
        var right   = new Vector3( _body.Transform.Basis.X.X, 0f,  _body.Transform.Basis.X.Z).Normalized();
        var moveDir = (forward * ForwardInput + right * StrafeInput).Normalized();

        var speedCap = MaxSpeed * (IsSprinting ? SprintModifier : 1f);
        if (new Vector3(_body.LinearVelocity.X, 0f, _body.LinearVelocity.Z).Length() >= speedCap) return;

        _body.ApplyCentralForce(moveDir * MoveForce * (IsSprinting ? SprintModifier : 1f) * _body.Mass);

        var tiltAngle = Mathf.Acos(Mathf.Clamp(_body.Transform.Basis.Y.Dot(Vector3.Up), -1f, 1f));
        if (tiltAngle < Mathf.DegToRad(LeanLimitDeg))
            _body.ApplyTorque(Vector3.Up.Cross(moveDir) * LeanTorque);
    }

    // Yaw only — X/Z angular velocity left to physics.
    private void ApplyTurning()
    {
        if (RotateInput == 0f) return;
        _body.AngularVelocity = new Vector3(_body.AngularVelocity.X, RotateInput * RotateSpeed, _body.AngularVelocity.Z);
    }
}
