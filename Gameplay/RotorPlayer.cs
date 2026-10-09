using System;
using Godot;

public partial class RotorPlayer : CharacterBody2D
{
	private const float MoveSpeed = 235f;
	private const float AutomaticSpinSpeed = 2.4f;
	private const float SpinAdjustmentSpeed = 0.8f;
	private const float RotorRadiusValue = 7f;
	private const float HitLockSeconds = 0.45f;
	private const float CollisionRecoverySpeed = 110f;
	public const float CollisionRecoverySeconds = 0.18f;
	private readonly Node2D _rotor = new();
	private readonly CollisionShape2D _rotorCollision = new();
	private readonly RotorDrawing _rotorDrawing = new();
	private readonly PilotDrawing _pilotDrawing = new();
	private Vector2 _checkpoint = new(0, 80);
	private Vector2 _recoveryVelocity;
	private float _hitLock;
	private float _recoveryRemaining;
	private float _flashTime;

	public event Action HitWall;
	public float RotorAngle => _rotor.Rotation;
	public float RotorHalfLength => GameplaySettings.RotorHalfLength;
	public float RotorRadius => RotorRadiusValue;
	public Vector2 Checkpoint => _checkpoint;

	public override void _Ready()
	{
		CollisionLayer = 1;
		CollisionMask = 2;
		FloorSnapLength = 0;
		AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = 13 } });
		_rotorCollision.Shape = new CapsuleShape2D
		{
			Height = (RotorHalfLength * 2) + (RotorRadiusValue * 2),
			Radius = RotorRadiusValue
		};
		AddChild(_rotorCollision);
		_rotor.Name = "Rotor";
		_rotor.AddChild(_rotorDrawing);
		AddChild(_rotor);
		_pilotDrawing.Position = new Vector2(0, 0);
		AddChild(_pilotDrawing);
		var camera = new Camera2D { Enabled = true, PositionSmoothingEnabled = false };
		AddChild(camera);
	}

	public override void _PhysicsProcess(double delta)
	{
		var step = (float)delta;
		var rotationAdjustment = _hitLock > 0 ? 0 : Input.GetAxis("rotate_ccw", "rotate_cw");
		var spinSpeed = CourseRules.GetAutomaticSpinSpeed(rotationAdjustment, AutomaticSpinSpeed, SpinAdjustmentSpeed);
		_rotor.Rotation += spinSpeed * step;
		_rotorCollision.Rotation = _rotor.Rotation;
		if (_flashTime > 0)
		{
			_flashTime = Mathf.Max(0, _flashTime - step);
			_pilotDrawing.QueueRedraw();
		}
		if (_hitLock > 0)
		{
			_hitLock -= step;
			Velocity = Vector2.Zero;
			if (_recoveryRemaining > 0)
			{
				var recoveryStep = Mathf.Min(step, _recoveryRemaining);
				var collision = MoveAndCollide(_recoveryVelocity * recoveryStep);
				_recoveryRemaining = collision == null ? _recoveryRemaining - recoveryStep : 0;
			}
			if (_hitLock <= 0)
			{
				_modulate(false);
			}
			return;
		}

		var move = Input.GetVector("move_left", "move_right", "move_up", "move_down");
		Velocity = move * MoveSpeed;
		MoveAndSlide();
		if (GetSlideCollisionCount() > 0)
		{
			_recoveryVelocity = GetSlideCollision(0).GetNormal() * CollisionRecoverySpeed;
			_recoveryRemaining = CollisionRecoverySeconds;
			Velocity = Vector2.Zero;
			_hitLock = HitLockSeconds;
			_flashTime = HitLockSeconds;
			_modulate(true);
			HitWall?.Invoke();
		}
	}

	public void SetCheckpoint(Vector2 position)
	{
		_checkpoint = position;
	}

	public void RetryFromCheckpoint()
	{
		_hitLock = 0;
		_recoveryRemaining = 0;
		_recoveryVelocity = Vector2.Zero;
		_flashTime = 0;
		Velocity = Vector2.Zero;
		GlobalPosition = _checkpoint;
		_modulate(false);
	}

	public void ApplyCollisionRecoil()
	{
		_rotor.Rotation -= GameplaySettings.CollisionRecoilRadians;
		_rotorCollision.Rotation = _rotor.Rotation;
	}

	public void ResetRun(Vector2 startPosition)
	{
		_checkpoint = startPosition;
		_hitLock = 0;
		_recoveryRemaining = 0;
		_recoveryVelocity = Vector2.Zero;
		_flashTime = 0;
		Velocity = Vector2.Zero;
		GlobalPosition = startPosition;
		_rotor.Rotation = 0;
		_rotorCollision.Rotation = 0;
		_modulate(false);
	}

	private void _modulate(bool hit)
	{
		_pilotDrawing.SetHit(hit);
		_rotorDrawing.SetHit(hit);
	}

	private partial class RotorDrawing : Node2D
	{
		private bool _hit;

		public void SetHit(bool hit)
		{
			_hit = hit;
			QueueRedraw();
		}

		public override void _Draw()
		{
			var color = _hit ? new Color("#ff5d5d") : new Color("#8de0b1");
			var halfLength = GameplaySettings.RotorHalfLength;
			DrawLine(new Vector2(0, -halfLength), new Vector2(0, halfLength), color, RotorRadiusValue * 2, true);
			DrawCircle(new Vector2(0, -halfLength), RotorRadiusValue, new Color("#f4efd9"));
			DrawCircle(new Vector2(0, halfLength), RotorRadiusValue, new Color("#f4efd9"));
			DrawCircle(new Vector2(0, -halfLength + 12), 3.5f, new Color("#142a28"));
		}
	}

	private partial class PilotDrawing : Node2D
	{
		private bool _hit;

		public void SetHit(bool hit)
		{
			_hit = hit;
			QueueRedraw();
		}

		public override void _Draw()
		{
			DrawCircle(Vector2.Zero, 14, new Color("#142a28"));
			DrawCircle(Vector2.Zero, 11, _hit ? new Color("#ff5d5d") : new Color("#f4efd9"));
			DrawCircle(new Vector2(3, -2), 3, new Color("#f08a68"));
		}
	}
}