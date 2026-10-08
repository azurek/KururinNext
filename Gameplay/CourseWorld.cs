using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class CourseWorld : Node2D
{
	private const float CorridorHalfWidth = 240f;
	private const float BendingCorridorHalfWidth = 90f;
	private const float GateThickness = 24f;
	private static readonly Color CourseColor = new("#304a41");
	private static readonly Color WallColor = new("#f08a68");
	private readonly List<CourseGate> _gates = new();
	private readonly List<Vector2> _trackCenterline = new();
	private readonly List<Vector2> _leftBoundary = new();
	private readonly List<Vector2> _rightBoundary = new();
	private RotorPlayer _player = null!;
	private string _levelDataId = "course-01";
	private float _activeCorridorHalfWidth = CorridorHalfWidth;
	private bool _isBendingCourse;
	private bool _checkpointActivated;
	private Vector2 _startPosition = new(0, 80);
	private Vector2 _checkpointPosition = new(0, 1115);
	private Vector2 _finishPosition = new(0, 1995);

	public event Action HitWall;
	public event Action CheckpointReached;
	public event Action Finished;
	public RotorPlayer Player => _player;

	public void Configure(string levelDataId)
	{
		_levelDataId = levelDataId;
	}

	public override void _Ready()
	{
		if (_levelDataId == "course-02")
		{
			BuildBendingCourse();
		}
		else
		{
			BuildCourse();
		}

		_player = new RotorPlayer { Position = _startPosition };
		_player.HitWall += () => HitWall?.Invoke();
		AddChild(_player);
		AddTrigger("Checkpoint", _checkpointPosition, 36, () =>
		{
			if (_checkpointActivated)
			{
				return;
			}
			_checkpointActivated = true;
			_player.SetCheckpoint(_checkpointPosition);
			CheckpointReached?.Invoke();
		});
		AddTrigger("Finish", _finishPosition, 46, () => Finished?.Invoke());
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
		_player.ResetRun(_startPosition);
	}

	public override void _Draw()
	{
		if (_isBendingCourse)
		{
			DrawBendingCourse();
			return;
		}

		DrawRect(new Rect2(-300, 0, 600, 2090), new Color("#1b332e"));
		DrawRect(new Rect2(-CorridorHalfWidth, 0, CorridorHalfWidth * 2, 2090), CourseColor);
		DrawRect(new Rect2(-260, 0, 20, 2090), WallColor);
		DrawRect(new Rect2(240, 0, 20, 2090), WallColor);
		foreach (var gate in _gates)
		{
			var leftEdge = gate.CenterX - gate.Width / 2;
			var rightEdge = gate.CenterX + gate.Width / 2;
			DrawGateWalls(gate, leftEdge, rightEdge);
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

	private void BuildBendingCourse()
	{
		_isBendingCourse = true;
		_activeCorridorHalfWidth = BendingCorridorHalfWidth;
		_startPosition = new Vector2(0, 80);
		_checkpointPosition = new Vector2(-140, 1500);
		_finishPosition = new Vector2(120, 2160);
		_trackCenterline.AddRange(new[]
		{
			new Vector2(0, 0),
			new Vector2(0, 450),
			new Vector2(170, 680),
			new Vector2(170, 990),
			new Vector2(-140, 1240),
			new Vector2(-140, 1580),
			new Vector2(120, 1840),
			new Vector2(120, 2220)
		});

		_leftBoundary.AddRange(OffsetPath(_trackCenterline, _activeCorridorHalfWidth));
		_rightBoundary.AddRange(OffsetPath(_trackCenterline, -_activeCorridorHalfWidth));
		AddWallPath(_leftBoundary);
		AddWallPath(_rightBoundary);
		AddGate(300, 0, 142);
		AddGate(830, 170, 118);
		AddGate(1430, -140, 112);
		AddGate(2010, 120, 108);
		QueueRedraw();
	}

	private void DrawBendingCourse()
	{
		DrawRect(new Rect2(-700, -100, 1400, 2400), new Color("#1b332e"));
		DrawPolyline(_trackCenterline.ToArray(), new Color("#42665a"), _activeCorridorHalfWidth * 2 + 28, true);
		DrawPolyline(_trackCenterline.ToArray(), CourseColor, _activeCorridorHalfWidth * 2, true);
		DrawPolyline(_leftBoundary.ToArray(), WallColor, GateThickness, true);
		DrawPolyline(_rightBoundary.ToArray(), WallColor, GateThickness, true);
		foreach (var gate in _gates)
		{
			var leftEdge = gate.CenterX - gate.Width / 2;
			var rightEdge = gate.CenterX + gate.Width / 2;
			DrawGateWalls(gate, leftEdge, rightEdge);
			DrawLine(new Vector2(leftEdge, gate.Y), new Vector2(rightEdge, gate.Y), new Color("#f4efd9"), 4, true);
		}

		DrawCircle(_checkpointPosition, 36, new Color("#8de0b1", 0.25f));
		DrawArc(_checkpointPosition, 36, 0, Mathf.Tau, 48, new Color("#8de0b1"), 3);
		DrawLine(
			new Vector2(_finishPosition.X - _activeCorridorHalfWidth, _finishPosition.Y),
			new Vector2(_finishPosition.X + _activeCorridorHalfWidth, _finishPosition.Y),
			new Color("#8de0b1"), 12, true);
	}

	private static List<Vector2> OffsetPath(IReadOnlyList<Vector2> path, float offset)
	{
		var result = new List<Vector2>(path.Count);
		for (var index = 0; index < path.Count; index++)
		{
			var incoming = index > 0 ? (path[index] - path[index - 1]).Normalized() : Vector2.Zero;
			var outgoing = index < path.Count - 1 ? (path[index + 1] - path[index]).Normalized() : Vector2.Zero;
			var incomingNormal = new Vector2(-incoming.Y, incoming.X);
			var outgoingNormal = new Vector2(-outgoing.Y, outgoing.X);
			if (index == 0)
			{
				result.Add(path[index] + outgoingNormal * offset);
				continue;
			}
			if (index == path.Count - 1)
			{
				result.Add(path[index] + incomingNormal * offset);
				continue;
			}

			var miter = (incomingNormal + outgoingNormal).Normalized();
			var scale = offset / Mathf.Max(0.25f, miter.Dot(outgoingNormal));
			result.Add(path[index] + miter * scale);
		}

		return result;
	}

	private void AddWallPath(IReadOnlyList<Vector2> points)
	{
		for (var index = 0; index < points.Count - 1; index++)
		{
			var start = points[index];
			var end = points[index + 1];
			var direction = end - start;
			var body = new StaticBody2D { CollisionLayer = 2, CollisionMask = 0 };
			body.Position = (start + end) / 2;
			body.Rotation = direction.Angle() - Mathf.Pi / 2;
			body.AddChild(new CollisionShape2D
			{
				Shape = new CapsuleShape2D { Height = direction.Length() + GateThickness, Radius = GateThickness / 2 }
			});
			AddChild(body);
		}
	}

	private void AddGate(float y, float centerX, float width)
	{
		var gate = new CourseGate(y, centerX, width);
		_gates.Add(gate);
		var leftEdge = centerX - width / 2;
		var rightEdge = centerX + width / 2;
		var corridorLeft = centerX - _activeCorridorHalfWidth;
		var corridorRight = centerX + _activeCorridorHalfWidth;
		AddWall(new Rect2(corridorLeft, y - GateThickness / 2, leftEdge - corridorLeft, GateThickness));
		AddWall(new Rect2(rightEdge, y - GateThickness / 2, corridorRight - rightEdge, GateThickness));
	}

	private void DrawGateWalls(CourseGate gate, float leftEdge, float rightEdge)
	{
		var corridorLeft = gate.CenterX - _activeCorridorHalfWidth;
		var corridorRight = gate.CenterX + _activeCorridorHalfWidth;
		DrawRect(new Rect2(corridorLeft, gate.Y - GateThickness / 2, leftEdge - corridorLeft, GateThickness), WallColor);
		DrawRect(new Rect2(rightEdge, gate.Y - GateThickness / 2, corridorRight - rightEdge, GateThickness), WallColor);
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