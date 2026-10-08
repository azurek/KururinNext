using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class CourseWorld : Node2D
{
	private const float CorridorHalfWidth = 240f;
	private const float GateThickness = 24f;
	private static readonly Color CourseColor = new("#304a41");
	private static readonly Color WallColor = new("#f08a68");
	private readonly List<CourseGate> _gates = new();
	private RotorPlayer _player = null!;
	private bool _checkpointActivated;

	public event Action HitWall;
	public event Action CheckpointReached;
	public event Action Finished;
	public RotorPlayer Player => _player;

	public override void _Ready()
	{
		BuildCourse();
		_player = new RotorPlayer { Position = new Vector2(0, 80) };
		_player.HitWall += () => HitWall?.Invoke();
		AddChild(_player);
		AddTrigger("Checkpoint", new Vector2(0, 1115), 36, () =>
		{
			if (_checkpointActivated)
			{
				return;
			}
			_checkpointActivated = true;
			_player.SetCheckpoint(new Vector2(0, 1115));
			CheckpointReached?.Invoke();
		});
		AddTrigger("Finish", new Vector2(0, 1995), 46, () => Finished?.Invoke());
	}

	public string GetAlignmentStatus()
	{
		var gate = _gates.FirstOrDefault(candidate => candidate.Y + GateThickness > _player.GlobalPosition.Y - 50);
		if (gate == null)
		{
			return "FINISH AHEAD";
		}
		var fits = CourseRules.RotorFitsThroughGate(
			_player.RotorAngle,
			_player.RotorHalfLength,
			_player.RotorRadius,
			gate.Width / 2,
			_player.GlobalPosition.X - gate.CenterX);
		return fits ? "ROTOR ALIGNED" : "TURN TO FIT";
	}

	public void ResetRun()
	{
		_checkpointActivated = false;
		_player.ResetRun(new Vector2(0, 80));
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(-300, 0, 600, 2090), new Color("#1b332e"));
		DrawRect(new Rect2(-CorridorHalfWidth, 0, CorridorHalfWidth * 2, 2090), CourseColor);
		DrawRect(new Rect2(-260, 0, 20, 2090), WallColor);
		DrawRect(new Rect2(240, 0, 20, 2090), WallColor);
		foreach (var gate in _gates)
		{
			var leftEdge = gate.CenterX - gate.Width / 2;
			var rightEdge = gate.CenterX + gate.Width / 2;
			DrawRect(new Rect2(-240, gate.Y - GateThickness / 2, leftEdge + 240, GateThickness), WallColor);
			DrawRect(new Rect2(rightEdge, gate.Y - GateThickness / 2, 240 - rightEdge, GateThickness), WallColor);
			DrawRect(new Rect2(leftEdge, gate.Y - 2, gate.Width, 4), new Color("#f4efd9"));
		}
		DrawCircle(new Vector2(0, 1115), 36, new Color("#8de0b1", 0.25f));
		DrawArc(new Vector2(0, 1115), 36, 0, Mathf.Tau, 48, new Color("#8de0b1"), 3);
		DrawRect(new Rect2(-240, 1960, 480, 12), new Color("#8de0b1"));
		DrawRect(new Rect2(-240, 2045, 480, 12), new Color("#8de0b1"));
	}

	private void BuildCourse()
	{
		AddWall(new Rect2(-260, 0, 20, 2050));
		AddWall(new Rect2(240, 0, 20, 2050));
		AddGate(300, -65, 132);
		AddGate(590, 95, 88);
		AddGate(880, -60, 150);
		AddGate(1260, 95, 92);
		AddGate(1580, -20, 118);
		QueueRedraw();
	}

	private void AddGate(float y, float centerX, float width)
	{
		var gate = new CourseGate(y, centerX, width);
		_gates.Add(gate);
		var leftEdge = centerX - width / 2;
		var rightEdge = centerX + width / 2;
		AddWall(new Rect2(-240, y - GateThickness / 2, leftEdge + 240, GateThickness));
		AddWall(new Rect2(rightEdge, y - GateThickness / 2, 240 - rightEdge, GateThickness));
	}

	private void AddWall(Rect2 rectangle)
	{
		if (rectangle.Size.X <= 0 || rectangle.Size.Y <= 0)
		{
			return;
		}
		var body = new StaticBody2D { CollisionLayer = 2, CollisionMask = 0 };
		body.Position = rectangle.Position + rectangle.Size / 2;
		body.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = rectangle.Size } });
		AddChild(body);
	}

	private void AddTrigger(string triggerName, Vector2 position, float radius, Action entered)
	{
		var area = new Area2D { Name = triggerName, CollisionLayer = 0, CollisionMask = 1 };
		area.Position = position;
		area.AddChild(new CollisionShape2D { Shape = new CircleShape2D { Radius = radius } });
		area.BodyEntered += body =>
		{
			if (body == _player)
			{
				entered();
			}
		};
		AddChild(area);
	}

	private sealed record CourseGate(float Y, float CenterX, float Width);
}