using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class CourseWorld : Node2D
{
	private const float CorridorHalfWidth = 240f;
	private const float BendingCorridorHalfWidth = 90f;
	private const float ChallengeCorridorHalfWidth = 155f;
	private const float GateThickness = 24f;
	private const float SidewaysCourseLength = 2500f;
	private static readonly Color CourseColor = new("#304a41");
	private static readonly Color WallColor = new("#f08a68");
	private readonly List<CourseGate> _gates = new();
	private readonly List<PistonHazard> _pistons = new();
	private readonly List<Rect2> _sidewaysObstacles = new();
	private readonly List<Vector2> _diamondObstacles = new();
	private readonly List<Vector2> _trackCenterline = new();
	private readonly List<Vector2> _leftBoundary = new();
	private readonly List<Vector2> _rightBoundary = new();
	private RotorPlayer _player = null!;
	private string _levelDataId = "course-01";
	private float _activeCorridorHalfWidth = CorridorHalfWidth;
	private bool _isBendingCourse;
	private bool _isSidewaysCourse;
	private bool _checkpointActivated;
	private float _hazardTime;
	private Vector2 _startPosition = new(0, 80);
	private Vector2 _checkpointPosition = new(0, 1115);
	private Vector2 _finishPosition = new(0, 1995);
	private LevelDefinition _levelDefinition;
	private float _levelLength = 2090;
	private float _checkpointRadius = 36;
	private float _finishRadius = 46;

	public event Action HitWall;
	public event Action CheckpointReached;
	public event Action Finished;
	public RotorPlayer Player => _player;

	public void Configure(string levelDataId, LevelDefinition levelDefinition = null)
	{
		_levelDataId = levelDataId;
		_levelDefinition = levelDefinition;
	}

	public override void _Ready()
	{
		if (_levelDataId == "course-01" && _levelDefinition == null)
		{
			_levelDefinition = LevelFileStore.LoadById(_levelDataId, out var loadErrors);
			foreach (var error in loadErrors)
			{
				GD.PushWarning(error);
			}
		}

		if (_levelDefinition != null && _levelDefinition.Validate().Count == 0)
		{
			BuildDataCourse(_levelDefinition);
		}
		else if (_levelDataId == "course-02")
		{
			BuildBendingCourse();
		}
		else if (_levelDataId == "course-03")
		{
			BuildSidewaysCourse();
		}
		else if (_levelDataId == "course-04" || _levelDataId == "course-05")
		{
			BuildPistonCourse(_levelDataId == "course-05");
		}
		else
		{
			BuildCourse();
		}

		_player = new RotorPlayer { Position = _startPosition };
		_player.HitWall += () => HitWall?.Invoke();
		AddChild(_player);
		AddTrigger("Checkpoint", _checkpointPosition, _checkpointRadius, () =>
		{
			if (_checkpointActivated)
			{
				return;
			}
			_checkpointActivated = true;
			_player.SetCheckpoint(_checkpointPosition);
			CheckpointReached?.Invoke();
		});
		AddTrigger("Finish", _finishPosition, _finishRadius, () => Finished?.Invoke());
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_pistons.Count == 0)
		{
			return;
		}

		_hazardTime += (float)delta;
		foreach (var piston in _pistons)
		{
			piston.Update(_hazardTime, _activeCorridorHalfWidth);
		}
		QueueRedraw();
	}

	public string GetAlignmentStatus()
	{
		if (_isSidewaysCourse)
		{
			return "DODGE THE DIAMONDS";
		}

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
		if (_levelDefinition != null && _levelDefinition.Validate().Count == 0)
		{
			DrawDataCourse(_levelDefinition);
			return;
		}
		if (_isSidewaysCourse)
		{
			DrawSidewaysCourse();
			return;
		}

		if (_isBendingCourse)
		{
			DrawBendingCourse();
			return;
		}

		DrawRect(new Rect2(-_activeCorridorHalfWidth - 60, 0, (_activeCorridorHalfWidth + 60) * 2, 2090), new Color("#1b332e"));
		DrawRect(new Rect2(-_activeCorridorHalfWidth, 0, _activeCorridorHalfWidth * 2, 2090), CourseColor);
		DrawRect(new Rect2(-_activeCorridorHalfWidth - 20, 0, 20, 2090), WallColor);
		DrawRect(new Rect2(_activeCorridorHalfWidth, 0, 20, 2090), WallColor);
		foreach (var gate in _gates)
		{
			var leftEdge = gate.CenterX - gate.Width / 2;
			var rightEdge = gate.CenterX + gate.Width / 2;
			DrawGateWalls(gate, leftEdge, rightEdge);
			DrawRect(new Rect2(leftEdge, gate.Y - 2, gate.Width, 4), new Color("#f4efd9"));
		}
		DrawCircle(new Vector2(0, 1115), 36, new Color("#8de0b1", 0.25f));
		DrawArc(new Vector2(0, 1115), 36, 0, Mathf.Tau, 48, new Color("#8de0b1"), 3);
		DrawFinishMarker(_finishPosition, _finishRadius);
		DrawPistons();
	}

	private void DrawSidewaysCourse()
	{
		DrawRect(new Rect2(-120, -_activeCorridorHalfWidth - 120,
			SidewaysCourseLength + 240, (_activeCorridorHalfWidth + 120) * 2), new Color("#1b332e"));
		DrawRect(new Rect2(0, -_activeCorridorHalfWidth, SidewaysCourseLength, _activeCorridorHalfWidth * 2), CourseColor);
		DrawRect(new Rect2(0, -_activeCorridorHalfWidth - 20, SidewaysCourseLength, 20), WallColor);
		DrawRect(new Rect2(0, _activeCorridorHalfWidth, SidewaysCourseLength, 20), WallColor);
		foreach (var obstacle in _sidewaysObstacles)
		{
			DrawRect(obstacle, WallColor);
		}
		foreach (var center in _diamondObstacles)
		{
			var points = DiamondPoints(center, 30);
			DrawColoredPolygon(points, new Color("#e6c85e"));
			DrawPolyline(points, new Color("#f4efd9"), 3, true);
		}
		DrawCircle(_checkpointPosition, 36, new Color("#8de0b1", 0.25f));
		DrawArc(_checkpointPosition, 36, 0, Mathf.Tau, 48, new Color("#8de0b1"), 3);
		DrawFinishMarker(_finishPosition, _finishRadius);
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

	private void BuildDataCourse(LevelDefinition level)
	{
		_activeCorridorHalfWidth = level.CorridorHalfWidth;
		_levelLength = level.Length;
		foreach (var element in level.Elements)
		{
			switch (element.Type)
			{
				case "wall":
					AddWall(new Rect2(element.X - element.Width / 2, element.Y - element.Height / 2,
						element.Width, element.Height), Mathf.DegToRad(element.RotationDegrees));
					break;
				case "gate":
					AddGate(element.Y, element.X, element.Width, element.Height);
					break;
				case "diamond":
					AddDiamond(new Vector2(element.X, element.Y), element.Width, Mathf.DegToRad(element.RotationDegrees));
					break;
				case "piston":
					AddPiston(element.Y, element.Side, element.Phase, element.Frequency, element.Height,
						false, false,
						element.MinimumExtension, element.Width);
					break;
				case "start":
					_startPosition = new Vector2(element.X, element.Y);
					break;
				case "checkpoint":
					_checkpointPosition = new Vector2(element.X, element.Y);
					_checkpointRadius = element.Width;
					break;
				case "finish":
					_finishPosition = new Vector2(element.X, element.Y);
					_finishRadius = element.Width;
					break;
			}
		}
		QueueRedraw();
	}

	private void DrawDataCourse(LevelDefinition level)
	{
		DrawRect(new Rect2(-_activeCorridorHalfWidth - 60, 0, (_activeCorridorHalfWidth + 60) * 2, _levelLength), new Color("#1b332e"));
		DrawRect(new Rect2(-_activeCorridorHalfWidth, 0, _activeCorridorHalfWidth * 2, _levelLength), CourseColor);
		foreach (var element in level.Elements)
		{
			switch (element.Type)
			{
				case "wall":
					DrawSetTransform(new Vector2(element.X, element.Y), Mathf.DegToRad(element.RotationDegrees), Vector2.One);
					DrawRect(new Rect2(-element.Width / 2, -element.Height / 2, element.Width, element.Height), WallColor);
					DrawSetTransform(Vector2.Zero, 0, Vector2.One);
					break;
				case "gate":
					var gate = new CourseGate(element.Y, element.X, element.Width, element.Height);
					DrawGateWalls(gate, gate.CenterX - gate.Width / 2, gate.CenterX + gate.Width / 2);
					DrawRect(new Rect2(gate.CenterX - gate.Width / 2, gate.Y - gate.Thickness / 2, gate.Width, gate.Thickness), new Color("#f4efd9"));
					break;
				case "diamond":
					DrawSetTransform(new Vector2(element.X, element.Y), Mathf.DegToRad(element.RotationDegrees), Vector2.One);
					var points = DiamondPoints(Vector2.Zero, element.Width);
					DrawColoredPolygon(points, new Color("#e6c85e"));
					DrawPolyline(points, new Color("#f4efd9"), 3, true);
					DrawSetTransform(Vector2.Zero, 0, Vector2.One);
					break;
				case "piston":
					break;
				case "start":
					DrawCircle(new Vector2(element.X, element.Y), element.Width, new Color("#8de0b1", 0.18f));
					DrawArc(new Vector2(element.X, element.Y), element.Width, 0, Mathf.Tau, 32, new Color("#8de0b1"), 3);
					break;
				case "checkpoint":
					DrawCircle(new Vector2(element.X, element.Y), element.Width, new Color("#8de0b1", 0.25f));
					DrawArc(new Vector2(element.X, element.Y), element.Width, 0, Mathf.Tau, 48, new Color("#8de0b1"), 3);
					break;
				case "finish":
					DrawFinishMarker(new Vector2(element.X, element.Y), element.Width);
					break;
			}
		}
		DrawPistons();
	}

	private void DrawFinishMarker(Vector2 position, float radius)
	{
		DrawCircle(position, radius, new Color("#8de0b1", 0.22f));
		DrawArc(position, radius, 0, Mathf.Tau, 48, new Color("#8de0b1"), 3);
	}

	private void BuildSidewaysCourse()
	{
		_isSidewaysCourse = true;
		_activeCorridorHalfWidth = ChallengeCorridorHalfWidth;
		_startPosition = new Vector2(80, 0);
		_checkpointPosition = new Vector2(1490, 0);
		_finishPosition = new Vector2(2420, 0);
		AddWall(new Rect2(0, -_activeCorridorHalfWidth - 20, SidewaysCourseLength, 20));
		AddWall(new Rect2(0, _activeCorridorHalfWidth, SidewaysCourseLength, 20));
		AddSidewaysProtrusion(380, true, 120, 230);
		AddSidewaysProtrusion(790, false, 120, 230);
		AddSidewaysProtrusion(1200, true, 120, 230);
		AddSidewaysProtrusion(1590, false, 120, 230);
		AddSidewaysProtrusion(2000, true, 120, 230);
		AddDiamond(new Vector2(670, 85));
		AddDiamond(new Vector2(1080, -85));
		AddDiamond(new Vector2(1870, -85));
		AddDiamond(new Vector2(2310, 85));
		QueueRedraw();
	}

	private void BuildPistonCourse(bool advanced)
	{
		_activeCorridorHalfWidth = ChallengeCorridorHalfWidth;
		AddWall(new Rect2(-_activeCorridorHalfWidth - 20, 0, 20, 2050));
		AddWall(new Rect2(_activeCorridorHalfWidth, 0, 20, 2050));
		var frequency = advanced ? 1.65f : 1.2f;
		var maxExtension = advanced ? 290f : 260f;
		var pistonPositions = advanced
			? new[] { 300f, 540f, 790f, 1030f, 1270f, 1510f, 1750f, 1900f }
			: new[] { 390f, 720f, 1050f, 1380f, 1710f, 1900f };
		for (var index = 0; index < pistonPositions.Length; index++)
		{
			var side = index % 2 == 0 ? -1f : 1f;
			AddPiston(pistonPositions[index], side, index * 1.35f, frequency, maxExtension, false, advanced);
		}
		QueueRedraw();
	}

	private void AddSidewaysProtrusion(float x, bool fromTop, float depth, float length)
	{
		var rectangle = fromTop
			? new Rect2(x, -CorridorHalfWidth, length, depth)
			: new Rect2(x, CorridorHalfWidth - depth, length, depth);
		_sidewaysObstacles.Add(rectangle);
		AddWall(rectangle);
	}

	private void AddDiamond(Vector2 center, float radius = 30, float rotation = 0)
	{
		var body = new StaticBody2D { CollisionLayer = 2, CollisionMask = 0 };
		body.Position = center;
		body.Rotation = rotation;
		body.AddChild(new CollisionShape2D
		{
			Shape = new ConvexPolygonShape2D
			{
				Points = new[]
				{
					new Vector2(0, -radius),
					new Vector2(radius, 0),
					new Vector2(0, radius),
					new Vector2(-radius, 0)
				}
			}
		});
		AddChild(body);
		_diamondObstacles.Add(center);
	}

	private void AddPiston(float coursePosition, float side, float phase, float frequency, float maxExtension, bool sideways,
		bool advanced, float minimumExtension = -1, float thickness = 0)
	{
		var shape = new RectangleShape2D();
		var body = new AnimatableBody2D { CollisionLayer = 2, CollisionMask = 0, SyncToPhysics = true };
		body.AddChild(new CollisionShape2D { Shape = shape });
		AddChild(body);
		var piston = new PistonHazard(body, shape, coursePosition, side, sideways, phase, frequency,
			minimumExtension > 0 ? minimumExtension : advanced ? 16f : 24f, maxExtension,
			thickness > 0 ? thickness : advanced ? 74f : 90f);
		piston.Update(0, _activeCorridorHalfWidth);
		_pistons.Add(piston);
	}

	private void DrawPistons()
	{
		foreach (var piston in _pistons)
		{
			DrawRect(piston.Bounds, WallColor);
			DrawRect(piston.TipBounds, new Color("#e6c85e"));
		}
	}

	private static Vector2[] DiamondPoints(Vector2 center, float radius) => new[]
	{
		center + new Vector2(0, -radius),
		center + new Vector2(radius, 0),
		center + new Vector2(0, radius),
		center + new Vector2(-radius, 0)
	};

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

	private void AddGate(float y, float centerX, float width, float thickness = GateThickness)
	{
		var gate = new CourseGate(y, centerX, width, thickness);
		_gates.Add(gate);
		var leftEdge = centerX - width / 2;
		var rightEdge = centerX + width / 2;
		var corridorLeft = centerX - _activeCorridorHalfWidth;
		var corridorRight = centerX + _activeCorridorHalfWidth;
		AddWall(new Rect2(corridorLeft, y - thickness / 2, leftEdge - corridorLeft, thickness));
		AddWall(new Rect2(rightEdge, y - thickness / 2, corridorRight - rightEdge, thickness));
	}

	private void DrawGateWalls(CourseGate gate, float leftEdge, float rightEdge)
	{
		var corridorLeft = gate.CenterX - _activeCorridorHalfWidth;
		var corridorRight = gate.CenterX + _activeCorridorHalfWidth;
		DrawRect(new Rect2(corridorLeft, gate.Y - gate.Thickness / 2, leftEdge - corridorLeft, gate.Thickness), WallColor);
		DrawRect(new Rect2(rightEdge, gate.Y - gate.Thickness / 2, corridorRight - rightEdge, gate.Thickness), WallColor);
	}

	private void AddWall(Rect2 rectangle, float rotation = 0)
	{
		if (rectangle.Size.X <= 0 || rectangle.Size.Y <= 0)
		{
			return;
		}
		var body = new StaticBody2D { CollisionLayer = 2, CollisionMask = 0 };
		body.Position = rectangle.Position + rectangle.Size / 2;
		body.Rotation = rotation;
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

	private sealed class PistonHazard
	{
		private readonly AnimatableBody2D _body;
		private readonly RectangleShape2D _shape;
		private readonly float _coursePosition;
		private readonly float _side;
		private readonly bool _isSideways;
		private readonly float _phase;
		private readonly float _frequency;
		private readonly float _minimumExtension;
		private readonly float _maximumExtension;
		private readonly float _thickness;

		public Rect2 Bounds { get; private set; }
		public Rect2 TipBounds { get; private set; }

		public PistonHazard(AnimatableBody2D body, RectangleShape2D shape, float coursePosition, float side,
			bool isSideways, float phase, float frequency, float minimumExtension, float maximumExtension, float thickness)
		{
			_body = body;
			_shape = shape;
			_coursePosition = coursePosition;
			_side = side;
			_isSideways = isSideways;
			_phase = phase;
			_frequency = frequency;
			_minimumExtension = minimumExtension;
			_maximumExtension = maximumExtension;
			_thickness = thickness;
		}

		public void Update(float time, float corridorHalfWidth)
		{
			var cycle = (Mathf.Sin(time * _frequency + _phase) + 1) / 2;
			var extension = Mathf.Lerp(_minimumExtension, _maximumExtension, cycle);
			if (_isSideways)
			{
				_shape.Size = new Vector2(_thickness, extension);
				_body.Position = new Vector2(_coursePosition, _side * (corridorHalfWidth - extension / 2));
				Bounds = new Rect2(_coursePosition - _thickness / 2,
					_side < 0 ? -corridorHalfWidth : corridorHalfWidth - extension, _thickness, extension);
				TipBounds = new Rect2(_coursePosition - _thickness / 2,
					_side < 0 ? -corridorHalfWidth + extension - 8 : corridorHalfWidth - extension, _thickness, 8);
				return;
			}

			_shape.Size = new Vector2(extension, _thickness);
			_body.Position = new Vector2(_side * (corridorHalfWidth - extension / 2), _coursePosition);
			Bounds = new Rect2(_side < 0 ? -corridorHalfWidth : corridorHalfWidth - extension,
				_coursePosition - _thickness / 2, extension, _thickness);
			TipBounds = new Rect2(_side < 0 ? -corridorHalfWidth + extension - 8 : corridorHalfWidth - extension,
				_coursePosition - _thickness / 2, 8, _thickness);
		}
	}

	private sealed record CourseGate(float Y, float CenterX, float Width, float Thickness = GateThickness);
}