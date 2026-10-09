using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Godot;

public partial class LevelEditorScreen : Control
{
	private static readonly Color Ink = new("#142a28");
	private static readonly Color Paper = new("#f4efd9");
	private static readonly Color Mint = new("#8de0b1");
	private static readonly Color Coral = new("#f08a68");
	private readonly Stack<LevelDefinition> _undo = new();
	private readonly Stack<LevelDefinition> _redo = new();
	private LevelDefinition _level = LevelDefinition.CreateNew("new-level", "New Level");
	private LevelElement _selected;
	private EditorCanvas _canvas = null!;
	private OptionButton _levelPicker = null!;
	private OptionButton _userMapPicker = null!;
	private OptionButton _gridPicker = null!;
	private HSlider _zoomSlider = null!;
	private Label _zoomLabel = null!;
	private LineEdit _idInput = null!;
	private LineEdit _nameInput = null!;
	private CheckButton _snapToggle = null!;
	private Label _feedback = null!;
	private VBoxContainer _inspector = null!;
	private bool _updatingInspector;
	private bool _dragging;
	private bool _dragMoved;
	private bool _drawingWall;
	private bool _drawingWallTile;
	private bool _erasing;
	private bool _eraseStrokeChanged;
	private bool _eraserCursorVisible;
	private bool _rotatingSelection;
	private bool _tileStrokeChanged;
	private Vector2 _dragOffset;
	private Vector2 _wallStart;
	private Vector2 _wallEnd;
	private Vector2 _eraserPreviousWorld;
	private Vector2 _eraserCursorWorld;
	private Vector2 _rotationPreviousAngle;
	private float _rotationStartDegrees;
	private float _rotationDragDegrees;
	private readonly HashSet<Vector2I> _wallTileStrokeCells = new();
	private bool _dirty = true;
	private string _tool = "select";
	private float _eraserSize = 60;

	public event Action BackRequested;
	public event Action<LevelDefinition> PlaytestRequested;

	public override void _Ready()
	{
		BuildUi();
		RefreshEditor();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey key || !key.Pressed || key.Echo)
		{
			return;
		}
		if (key.Keycode == Key.Escape)
		{
			BackRequested?.Invoke();
			GetViewport().SetInputAsHandled();
			return;
		}
		if (GetViewport().GuiGetFocusOwner() is LineEdit) return;

		if (key.CtrlPressed && key.Keycode == Key.Z)
		{
			Undo();
		}
		else if (key.CtrlPressed && key.Keycode == Key.Y)
		{
			Redo();
		}
		else if (key.Keycode == Key.Delete || key.Keycode == Key.Backspace)
		{
			RemoveSelected();
		}
		else if (key.Keycode == Key.Q || key.Keycode == Key.E)
		{
			RotateSelected(key.Keycode == Key.Q ? -90 : 90);
		}
		else if (key.Keycode is Key.Left or Key.Right or Key.Up or Key.Down)
		{
			MoveSelected(key.Keycode);
		}
		else if (key.CtrlPressed && key.Keycode == Key.S)
		{
			Save();
		}
	}

	private void BuildUi()
	{
		var background = new ColorRect { Color = Ink, MouseFilter = MouseFilterEnum.Ignore };
		background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(background);

		var layout = new VBoxContainer { AnchorRight = 1, AnchorBottom = 1 };
		layout.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		layout.AddThemeConstantOverride("separation", 8);
		AddChild(layout);

		var toolbar = new HBoxContainer { CustomMinimumSize = new Vector2(0, 54) };
		toolbar.AddThemeConstantOverride("separation", 5);
		layout.AddChild(toolbar);
		AddToolbarButton(toolbar, "BACK", () => BackRequested?.Invoke(), 62);
		AddToolbarLabel(toolbar, "LEVEL");
		_levelPicker = new OptionButton { CustomMinimumSize = new Vector2(116, 38), FocusMode = FocusModeEnum.All };
		foreach (var id in LevelFileStore.GetAvailableIds())
		{
			_levelPicker.AddItem(id);
		}
		toolbar.AddChild(_levelPicker);
		AddToolbarButton(toolbar, "OPEN", OpenSelected, 62);
		AddToolbarButton(toolbar, "NEW", NewLevel, 58);
		AddToolbarButton(toolbar, "SAVE", Save, 62);
		AddToolbarButton(toolbar, "SAVE AS", SaveAs, 72);
		AddToolbarButton(toolbar, "UNDO", Undo, 62);
		AddToolbarButton(toolbar, "REDO", Redo, 62);
		_snapToggle = new CheckButton { Text = "SNAP", ButtonPressed = true, FocusMode = FocusModeEnum.All };
		toolbar.AddChild(_snapToggle);
		_gridPicker = new OptionButton { CustomMinimumSize = new Vector2(74, 38), FocusMode = FocusModeEnum.All };
		foreach (var grid in new[] { "10", "20", "40", "80" })
		{
			_gridPicker.AddItem(grid);
		}
		_gridPicker.Select(1);
		_gridPicker.ItemSelected += _ => _canvas?.QueueRedraw();
		toolbar.AddChild(_gridPicker);
		_snapToggle.Toggled += _ => _canvas?.QueueRedraw();
		AddToolbarButton(toolbar, "ROTATE", () => RotateSelected(90), 70);
		AddToolbarButton(toolbar, "DELETE", RemoveSelected, 68);
		AddToolbarButton(toolbar, "PLAYTEST", Playtest, 84, true);

		var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		body.AddThemeConstantOverride("separation", 10);
		layout.AddChild(body);
		_canvas = new EditorCanvas(this)
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			FocusMode = FocusModeEnum.All
		};
		body.AddChild(_canvas);

		var inspectorPanel = new VBoxContainer { CustomMinimumSize = new Vector2(250, 0) };
		inspectorPanel.AddThemeConstantOverride("separation", 8);
		body.AddChild(inspectorPanel);
		var levelHeading = MakeLabel("LEVEL", 13, Mint);
		inspectorPanel.AddChild(levelHeading);
		var zoomRow = new HBoxContainer();
		zoomRow.AddChild(MakeLabel("ZOOM", 10, Mint));
		_zoomSlider = new HSlider
		{
			MinValue = 0.25,
			MaxValue = 3,
			Step = 0.05,
			Value = 1,
			SizeFlagsHorizontal = SizeFlags.ExpandFill
		};
		_zoomSlider.ValueChanged += value =>
		{
			_zoomLabel.Text = $"{value:P0}";
			_canvas?.QueueRedraw();
		};
		zoomRow.AddChild(_zoomSlider);
		_zoomLabel = MakeLabel("100%", 10, Paper);
		zoomRow.AddChild(_zoomLabel);
		inspectorPanel.AddChild(zoomRow);
		_idInput = new LineEdit { PlaceholderText = "portable-level-id", CustomMinimumSize = new Vector2(0, 36) };
		_idInput.TextChanged += _ =>
		{
			if (_updatingInspector) return;
			_dirty = true;
			_canvas?.QueueRedraw();
			if (_feedback != null) UpdateFeedback();
		};
		inspectorPanel.AddChild(_idInput);
		_nameInput = new LineEdit { PlaceholderText = "Level name", CustomMinimumSize = new Vector2(0, 36) };
		_nameInput.TextChanged += text =>
		{
			if (_updatingInspector) return;
			_level.Name = text;
			_dirty = true;
			_canvas?.QueueRedraw();
			if (_feedback != null) UpdateFeedback();
		};
		inspectorPanel.AddChild(_nameInput);
		inspectorPanel.AddChild(MakeLabel("SAVED MAPS - user://levels", 11, Mint));
		var userMapRow = new HBoxContainer();
		userMapRow.AddThemeConstantOverride("separation", 5);
		_userMapPicker = new OptionButton
		{
			CustomMinimumSize = new Vector2(0, 36),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			FocusMode = FocusModeEnum.All
		};
		userMapRow.AddChild(_userMapPicker);
		var openUserMapButton = new Button { Text = "OPEN", CustomMinimumSize = new Vector2(62, 36) };
		openUserMapButton.Pressed += OpenUserSelected;
		userMapRow.AddChild(openUserMapButton);
		inspectorPanel.AddChild(userMapRow);
		var inspectorScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
		inspectorPanel.AddChild(inspectorScroll);
		var inspectorContent = new VBoxContainer();
		inspectorContent.AddThemeConstantOverride("separation", 6);
		inspectorScroll.AddChild(inspectorContent);
		inspectorContent.AddChild(MakeLabel("OBJECTS", 13, Mint));
		var toolGroup = new ButtonGroup { AllowUnpress = true };
		foreach (var tool in new[] { "select", "wall-line", "wall-tile", "rubber", "gate", "diamond", "piston", "start", "checkpoint", "finish" })
		{
			var toolRow = new HBoxContainer();
			toolRow.AddThemeConstantOverride("separation", 6);
			toolRow.AddChild(new ToolGlyph(tool) { CustomMinimumSize = new Vector2(24, 34) });
			var toolButton = new Button
			{
				Text = tool switch
				{
					"wall-line" => "WALL LINE",
					"wall-tile" => "WALL TILE",
					"rubber" => "RUBBER",
					_ => tool.ToUpperInvariant()
				},
				ToggleMode = true,
				ButtonGroup = toolGroup,
				SizeFlagsHorizontal = SizeFlags.ExpandFill,
				CustomMinimumSize = new Vector2(0, 34),
				FocusMode = FocusModeEnum.All
			};
			toolButton.AddThemeColorOverride("font_color", Paper);
			toolButton.AddThemeColorOverride("font_hover_color", Mint);
			toolButton.AddThemeStyleboxOverride("normal", ButtonStyle(new Color("#263f3a")));
			toolButton.AddThemeStyleboxOverride("hover", ButtonStyle(new Color("#35534a")));
			toolButton.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color("#42665a")));
			toolButton.Toggled += pressed => SetTool(pressed ? tool : "select");
			toolRow.AddChild(toolButton);
			inspectorContent.AddChild(toolRow);
			if (tool == "select") toolButton.ButtonPressed = true;
		}
		inspectorContent.AddChild(MakeLabel("SELECTION", 13, Mint));
		_inspector = new VBoxContainer();
		_inspector.AddThemeConstantOverride("separation", 6);
		inspectorContent.AddChild(_inspector);
		AddFooter(layout);
	}

	private void AddFooter(VBoxContainer layout)
	{
		_feedback = MakeLabel("", 12, Paper);
		_feedback.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_feedback.CustomMinimumSize = new Vector2(0, 46);
		layout.AddChild(_feedback);
	}

	private void AddToolbarButton(HBoxContainer toolbar, string text, Action action, float width, bool primary = false)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(width, 38),
			FocusMode = FocusModeEnum.All
		};
		button.AddThemeColorOverride("font_color", primary ? Ink : Paper);
		button.AddThemeColorOverride("font_hover_color", primary ? Ink : Mint);
		button.AddThemeStyleboxOverride("normal", ButtonStyle(primary ? Mint : new Color("#263f3a")));
		button.AddThemeStyleboxOverride("hover", ButtonStyle(primary ? new Color("#a8efc5") : new Color("#35534a")));
		button.Pressed += action;
		toolbar.AddChild(button);
	}

	private void SetTool(string tool)
	{
		_tool = tool;
		RebuildInspector();
		_canvas.QueueRedraw();
	}

	private static void AddToolbarLabel(HBoxContainer toolbar, string text)
	{
		toolbar.AddChild(MakeLabel(text, 11, Mint));
	}

	private void OpenSelected()
	{
		var id = _levelPicker.GetItemText(_levelPicker.Selected);
		var loaded = LevelFileStore.LoadById(id, out var errors);
		if (loaded == null)
		{
			SetFeedback(string.Join("\n", errors), true);
			return;
		}
		_level = loaded;
		_selected = null;
		_dirty = false;
		_undo.Clear();
		_redo.Clear();
		RefreshEditor();
		UpdateFeedback();
	}

	private void OpenUserSelected()
	{
		if (_userMapPicker.Selected < 0 || _userMapPicker.GetItemText(_userMapPicker.Selected) == "(no saved maps)")
		{
			return;
		}
		var loaded = LevelFileStore.LoadById(_userMapPicker.GetItemText(_userMapPicker.Selected), out var errors);
		if (loaded == null)
		{
			SetFeedback(string.Join("\n", errors), true);
			return;
		}
		_level = loaded;
		_selected = null;
		_dirty = false;
		_undo.Clear();
		_redo.Clear();
		RefreshEditor();
		UpdateFeedback();
	}

	private void NewLevel()
	{
		_level = LevelDefinition.CreateNew("new-level", "New Level");
		_selected = null;
		_dirty = true;
		_undo.Clear();
		_redo.Clear();
		RefreshEditor();
	}

	private void Save()
	{
		_level.Name = _nameInput.Text;
		SaveLevel();
	}

	private void SaveAs()
	{
		_level.Id = _idInput.Text;
		_level.Name = _nameInput.Text;
		SaveLevel();
	}

	private void SaveLevel()
	{
		if (!LevelFileStore.Save(_level, out var path, out var errors))
		{
			SetFeedback(string.Join("\n", errors), true);
			return;
		}
		_dirty = false;
		_updatingInspector = true;
		_idInput.Text = _level.Id;
		_updatingInspector = false;
		RefreshPicker();
		SetFeedback($"Saved {path}", false);
	}

	private void Playtest()
	{
		var errors = _level.Validate();
		if (errors.Count > 0)
		{
			SetFeedback(string.Join("\n", errors), true);
			return;
		}
		PlaytestRequested?.Invoke(_level.Clone());
	}

	private void Undo()
	{
		if (_undo.Count == 0)
		{
			return;
		}
		_redo.Push(_level.Clone());
		_level = _undo.Pop();
		_selected = null;
		_dirty = true;
		RefreshEditor();
	}

	private void Redo()
	{
		if (_redo.Count == 0)
		{
			return;
		}
		_undo.Push(_level.Clone());
		_level = _redo.Pop();
		_selected = null;
		_dirty = true;
		RefreshEditor();
	}

	private void RemoveSelected()
	{
		if (_selected == null || _selected.Type is "start" or "checkpoint" or "finish")
		{
			return;
		}
		RecordUndo();
		_level.Elements.Remove(_selected);
		_selected = null;
		RefreshEditor();
	}

	private void RotateSelected(float amount)
	{
		if (_selected == null || _selected.Type is "gate" or "piston" or "start" or "checkpoint" or "finish")
		{
			return;
		}
		RecordUndo();
		_selected.RotationDegrees = (_selected.RotationDegrees + amount + 360) % 360;
		if (_selected.Type == "piston")
		{
			_selected.RotationDegrees = Mathf.Round(_selected.RotationDegrees / 90) * 90 % 360;
		}
		_dirty = true;
		RefreshEditor();
	}

	private void MoveSelected(Key key)
	{
		if (_selected == null)
		{
			return;
		}
		RecordUndo();
		var step = _snapToggle.ButtonPressed ? GridStep() : 10;
		if (key == Key.Left) _selected.X -= step;
		if (key == Key.Right) _selected.X += step;
		if (key == Key.Up) _selected.Y -= step;
		if (key == Key.Down) _selected.Y += step;
		_dirty = true;
		RefreshEditor();
	}

	private void RecordUndo()
	{
		_undo.Push(_level.Clone());
		if (_undo.Count > 100)
		{
			var preserved = _undo.Take(100).Reverse().ToArray();
			_undo.Clear();
			foreach (var state in preserved) _undo.Push(state);
		}
		_redo.Clear();
	}

	private void RefreshEditor()
	{
		_updatingInspector = true;
		_idInput.Text = _level.Id;
		_nameInput.Text = _level.Name;
		_updatingInspector = false;
		RebuildInspector();
		RefreshPicker();
		_canvas?.QueueRedraw();
		if (_dirty)
		{
			var errors = _level.Validate();
			SetFeedback(errors.Count == 0 ? "Unsaved changes" : string.Join("\n", errors), errors.Count > 0);
		}
	}

	private void RefreshPicker()
	{
		if (_levelPicker == null)
		{
			return;
		}
		var ids = LevelFileStore.GetAvailableIds().ToList();
		if (!ids.Contains(_level.Id, StringComparer.Ordinal)) ids.Add(_level.Id);
		_levelPicker.Clear();
		foreach (var id in ids) _levelPicker.AddItem(id);
		var selected = ids.IndexOf(_level.Id);
		if (selected >= 0) _levelPicker.Select(selected);

		var userIds = LevelFileStore.GetAvailableUserIds().ToList();
		_userMapPicker.Clear();
		if (userIds.Count == 0)
		{
			_userMapPicker.AddItem("(no saved maps)");
			_userMapPicker.SetItemDisabled(0, true);
			return;
		}
		foreach (var id in userIds) _userMapPicker.AddItem(id);
		var userSelected = userIds.IndexOf(_level.Id);
		if (userSelected >= 0) _userMapPicker.Select(userSelected);
	}

	private void RebuildInspector()
	{
		foreach (var child in _inspector.GetChildren())
		{
			_inspector.RemoveChild(child);
			child.QueueFree();
		}
		if (_tool == "rubber")
		{
			_inspector.AddChild(MakeLabel("RUBBER SIZE", 10, Mint));
			var sizeRow = new HBoxContainer();
			var sizeSlider = new HSlider
			{
				MinValue = 10,
				MaxValue = 300,
				Step = 5,
				Value = _eraserSize,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			var sizeLabel = MakeLabel($"{_eraserSize:0}", 11, Paper);
			sizeSlider.ValueChanged += value =>
			{
				_eraserSize = (float)value;
				sizeLabel.Text = $"{_eraserSize:0}";
				_canvas.QueueRedraw();
			};
			sizeRow.AddChild(sizeSlider);
			sizeRow.AddChild(sizeLabel);
			_inspector.AddChild(sizeRow);
			_inspector.AddChild(MakeLabel("Drag over course geometry to erase it.", 11, Paper));
			return;
		}
		if (_selected == null)
		{
			_inspector.AddChild(MakeLabel("Choose a tool, then click the course. Select and drag objects to move them.", 12, Paper));
			AddLevelSpin("CORRIDOR HALF-WIDTH", 40, 5000, _level.CorridorHalfWidth,
				value => _level.CorridorHalfWidth = value);
			AddLevelSpin("LEVEL LENGTH", 100, 10000, _level.Length,
				value => _level.Length = value);
			return;
		}
		_inspector.AddChild(MakeLabel(_selected.Type.ToUpperInvariant(), 14, Coral));
		if (_selected.Type != "piston")
		{
			AddInspectorSpin("X", -5000, 5000, _selected.X, value => _selected.X = value);
		}
		AddInspectorSpin("Y", 0, 10000, _selected.Y, value => _selected.Y = value);
		AddInspectorSpin("WIDTH / RADIUS", 1, 10000, _selected.Width, value => _selected.Width = value);
		if (_selected.Type is "wall" or "gate" or "piston")
		{
			AddInspectorSpin(_selected.Type == "piston" ? "MAX REACH" : "HEIGHT", 1, 10000,
				_selected.Height, value => _selected.Height = value);
		}
		if (_selected.Type is "wall" or "diamond")
		{
			AddInspectorSpin("ROTATION", -360, 360, _selected.RotationDegrees, value => _selected.RotationDegrees = value);
		}
		if (_selected.Type == "piston")
		{
			AddInspectorSpin("MIN REACH", 1, 9999, _selected.MinimumExtension, value => _selected.MinimumExtension = value);
			AddInspectorSpin("FREQUENCY", 0.1f, 10, _selected.Frequency, value => _selected.Frequency = value);
			var side = new Button { Text = _selected.Side < 0 ? "SIDE: LEFT" : "SIDE: RIGHT", CustomMinimumSize = new Vector2(0, 34) };
			side.Pressed += () => { RecordUndo(); _selected.Side *= -1; RefreshEditor(); };
			_inspector.AddChild(side);
		}
	}

	private void AddInspectorSpin(string label, float minimum, float maximum, float value, Action<float> apply)
	{
		AddSpin(label, minimum, maximum, value, apply, true);
	}

	private void AddLevelSpin(string label, float minimum, float maximum, float value, Action<float> apply)
	{
		AddSpin(label, minimum, maximum, value, apply, false);
	}

	private void AddSpin(string label, float minimum, float maximum, float value, Action<float> apply, bool requiresSelection)
	{
		var row = new VBoxContainer();
		row.AddChild(MakeLabel(label, 10, Mint));
		var spin = new SpinBox
		{
			MinValue = minimum,
			MaxValue = maximum,
			Step = label == "FREQUENCY" ? 0.1 : 1,
			Value = value,
			CustomMinimumSize = new Vector2(0, 34),
			AllowGreater = false,
			AllowLesser = false
		};
		spin.ValueChanged += newValue =>
		{
			if (_updatingInspector || (requiresSelection && _selected == null)) return;
			RecordUndo();
			apply((float)newValue);
			_dirty = true;
			_canvas.QueueRedraw();
			UpdateFeedback();
		};
		row.AddChild(spin);
		_inspector.AddChild(row);
	}

	private void HandleCanvasInput(EditorCanvas canvas, InputEvent input)
	{
		if (input is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left)
		{
			if (button.Pressed)
			{
				_canvas.GrabFocus();
				HandleCanvasPress(button.Position);
			}
			else if (_drawingWall)
			{
				FinishWallDrawing(button.Position);
			}
			else if (_drawingWallTile)
			{
				FinishWallTileStroke();
			}
			else if (_erasing)
			{
				FinishEraseStroke();
			}
			else if (_rotatingSelection)
			{
				_rotatingSelection = false;
				RebuildInspector();
				UpdateFeedback();
			}
			else if (_dragging)
			{
				_dragging = false;
				if (!_dragMoved && _undo.Count > 0) _undo.Pop();
				RebuildInspector();
				UpdateFeedback();
			}
			canvas.AcceptEvent();
		}
		else if (input is InputEventMouseMotion motion && (motion.ButtonMask & MouseButtonMask.Left) != 0)
		{
			if (_drawingWall)
			{
				var world = CanvasToWorld(motion.Position);
				_wallEnd = new Vector2(Snap(world.X), Snap(world.Y));
				_canvas.QueueRedraw();
				canvas.AcceptEvent();
			}
			else if (_drawingWallTile)
			{
				PaintWallTilesTo(CanvasToWorld(motion.Position));
				canvas.AcceptEvent();
			}
			else if (_erasing)
			{
				EraseAlongTo(CanvasToWorld(motion.Position));
				canvas.AcceptEvent();
			}
			else if (_rotatingSelection)
			{
				UpdateSelectionRotation(motion.Position);
				canvas.AcceptEvent();
			}
			else if (_dragging)
			{
				UpdateSelectedFromCanvas(motion.Position);
				canvas.AcceptEvent();
			}
		}
		else if (input is InputEventMouseMotion hover)
		{
			_eraserCursorVisible = _tool == "rubber";
			_eraserCursorWorld = CanvasToWorld(hover.Position);
			_canvas.QueueRedraw();
		}
	}

	private void HandleCanvasPress(Vector2 position)
	{
		var world = CanvasToWorld(position);
		var selectedTool = _tool;
		if (selectedTool == "rubber")
		{
			RecordUndo();
			_erasing = true;
			_eraseStrokeChanged = false;
			_eraserPreviousWorld = world;
			_eraserCursorWorld = world;
			_eraserCursorVisible = true;
			EraseAt(world);
			return;
		}
		if (selectedTool != "select")
		{
			world = new Vector2(Snap(world.X), Snap(world.Y));
			if (selectedTool == "wall-line")
			{
				RecordUndo();
				_drawingWall = true;
				_wallStart = world;
				_wallEnd = world;
				return;
			}
			if (selectedTool == "wall-tile")
			{
				RecordUndo();
				_drawingWallTile = true;
				_tileStrokeChanged = false;
				_wallTileStrokeCells.Clear();
				_wallEnd = world;
				StampWallTile(world);
				return;
			}
			RecordUndo();
			if (selectedTool is "start" or "checkpoint" or "finish")
			{
				_selected = _level.Elements.FirstOrDefault(element => element.Type == selectedTool);
				if (_selected == null)
				{
					_selected = CreateElement(selectedTool, world);
					_level.Elements.Add(_selected);
				}
				_selected.X = world.X;
				_selected.Y = Mathf.Clamp(world.Y, 0, _level.Length);
			}
			else
			{
				_selected = CreateElement(selectedTool, world);
				_level.Elements.Add(_selected);
			}
			_dirty = true;
			RefreshEditor();
			return;
		}

		if (_selected != null && CanRotate(_selected) && position.DistanceTo(RotationHandlePosition(_selected)) <= 12)
		{
			RecordUndo();
			_rotatingSelection = true;
			_rotationPreviousAngle = position - WorldToCanvas(ElementCanvasCenter(_selected));
			_rotationStartDegrees = _selected.RotationDegrees;
			_rotationDragDegrees = 0;
			return;
		}

		_selected = HitTest(position);
		RebuildInspector();
		_canvas.QueueRedraw();
		if (_selected != null)
		{
			RecordUndo();
			_dragging = true;
			_dragMoved = false;
			_dragOffset = ElementCanvasCenter(_selected) - CanvasToWorld(position);
		}
	}

	private void EraseAlongTo(Vector2 world)
	{
		var distance = _eraserPreviousWorld.DistanceTo(world);
		var spacing = Mathf.Max(1, _eraserSize / 3);
		var segments = Mathf.Max(1, Mathf.CeilToInt(distance / spacing));
		for (var index = 1; index <= segments; index++)
		{
			EraseAt(_eraserPreviousWorld.Lerp(world, (float)index / segments));
		}
		_eraserPreviousWorld = world;
	}

	private void EraseAt(Vector2 center)
	{
		var removed = _level.Elements.Where(element => EraserTouches(element, center, _eraserSize / 2)).ToArray();
		if (removed.Length == 0) return;
		foreach (var element in removed)
		{
			_level.Elements.Remove(element);
			if (_selected == element) _selected = null;
		}
		_eraseStrokeChanged = true;
		_dirty = true;
		_canvas.QueueRedraw();
	}

	private bool EraserTouches(LevelElement element, Vector2 center, float radius)
	{
		if (element.Type == "gate")
		{
			var leftOpening = element.X - element.Width / 2;
			var rightOpening = element.X + element.Width / 2;
			return DistanceToSegment(center, new Vector2(-_level.CorridorHalfWidth, element.Y), new Vector2(leftOpening, element.Y)) <= radius + element.Height / 2 ||
				DistanceToSegment(center, new Vector2(rightOpening, element.Y), new Vector2(_level.CorridorHalfWidth, element.Y)) <= radius + element.Height / 2;
		}
		if (element.Type == "diamond")
		{
			return center.DistanceTo(new Vector2(element.X, element.Y)) <= radius + element.Width;
		}

		var objectCenter = ElementCanvasCenter(element);
		var halfWidth = element.Type == "piston" ? element.Height / 2 : element.Width / 2;
		var halfHeight = element.Type == "piston" ? element.Width / 2 : element.Height / 2;
		var relative = (center - objectCenter).Rotated(-Mathf.DegToRad(element.RotationDegrees));
		var outsideX = Mathf.Max(0, Mathf.Abs(relative.X) - halfWidth);
		var outsideY = Mathf.Max(0, Mathf.Abs(relative.Y) - halfHeight);
		return outsideX * outsideX + outsideY * outsideY <= radius * radius;
	}

	private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
	{
		var segment = end - start;
		var lengthSquared = segment.LengthSquared();
		if (lengthSquared <= 0.001f) return point.DistanceTo(start);
		var amount = Mathf.Clamp((point - start).Dot(segment) / lengthSquared, 0, 1);
		return point.DistanceTo(start + segment * amount);
	}

	private void FinishEraseStroke()
	{
		_erasing = false;
		if (!_eraseStrokeChanged && _undo.Count > 0) _undo.Pop();
		RebuildInspector();
		UpdateFeedback();
	}

	private void PaintWallTilesTo(Vector2 world)
	{
		var start = _wallEnd;
		var distance = start.DistanceTo(world);
		var step = Mathf.Max(1, GridStep() / 2);
		var segments = Mathf.Max(1, Mathf.CeilToInt(distance / step));
		for (var index = 1; index <= segments; index++)
		{
			StampWallTile(start.Lerp(world, (float)index / segments));
		}
		_wallEnd = world;
		_canvas.QueueRedraw();
	}

	private void StampWallTile(Vector2 world)
	{
		var step = GridStep();
		var cell = new Vector2I(Mathf.FloorToInt(world.X / step), Mathf.FloorToInt(world.Y / step));
		if (!_wallTileStrokeCells.Add(cell)) return;

		var x = (cell.X + 0.5f) * step;
		var y = (cell.Y + 0.5f) * step;
		if (_level.Elements.Any(element => element.Type == "wall" &&
			Mathf.IsEqualApprox(element.X, x) && Mathf.IsEqualApprox(element.Y, y) &&
			Mathf.IsEqualApprox(element.Width, step) && Mathf.IsEqualApprox(element.Height, step)))
		{
			return;
		}

		_selected = new LevelElement { Type = "wall", X = x, Y = y, Width = step, Height = step };
		_level.Elements.Add(_selected);
		_tileStrokeChanged = true;
		_dirty = true;
	}

	private void FinishWallTileStroke()
	{
		_drawingWallTile = false;
		if (!_tileStrokeChanged && _undo.Count > 0) _undo.Pop();
		RebuildInspector();
		UpdateFeedback();
	}

	private void UpdateSelectionRotation(Vector2 position)
	{
		if (_selected == null) return;
		var angle = position - WorldToCanvas(ElementCanvasCenter(_selected));
		var delta = angle.Angle() - _rotationPreviousAngle.Angle();
		if (delta > Mathf.Pi) delta -= Mathf.Tau;
		if (delta < -Mathf.Pi) delta += Mathf.Tau;
		_rotationDragDegrees += Mathf.RadToDeg(delta);
		_rotationPreviousAngle = angle;
		_selected.RotationDegrees = _rotationStartDegrees + _rotationDragDegrees;
		_dirty = true;
		_canvas.QueueRedraw();
	}

	private void FinishWallDrawing(Vector2 position)
	{
		var world = CanvasToWorld(position);
		_wallEnd = new Vector2(Snap(world.X), Snap(world.Y));
		_drawingWall = false;
		var direction = _wallEnd - _wallStart;
		var length = direction.Length();
		_selected = new LevelElement
		{
			Type = "wall",
			X = (_wallStart.X + _wallEnd.X) / 2,
			Y = (_wallStart.Y + _wallEnd.Y) / 2,
			Width = length < 1 ? 40 : length,
			Height = 20,
			RotationDegrees = length < 1 ? 0 : Mathf.RadToDeg(direction.Angle())
		};
		_level.Elements.Add(_selected);
		_dirty = true;
		RefreshEditor();
	}

	private LevelElement CreateElement(string type, Vector2 position)
	{
		var element = new LevelElement { Type = type, X = position.X, Y = position.Y };
		switch (type)
		{
			case "wall": element.Width = 40; element.Height = 40; break;
			case "gate": element.Width = _level.CorridorHalfWidth; element.Height = 24; break;
			case "diamond": element.Width = 30; element.Height = 30; break;
			case "piston": element.X = 0; element.Width = 24; element.Height = 220; element.MinimumExtension = 24; break;
			case "start": element.Width = 36; break;
			case "checkpoint": element.Width = 36; break;
			case "finish": element.Width = 46; break;
		}
		return element;
	}

	private void UpdateSelectedFromCanvas(Vector2 position)
	{
		if (_selected == null) return;
		var world = CanvasToWorld(position) + _dragOffset;
		var y = Snap(world.Y);
		var x = _selected.Type == "piston" ? 0 : Snap(world.X);
		if (Mathf.Abs(_selected.X - x) > 0.01f || Mathf.Abs(_selected.Y - y) > 0.01f)
		{
			_selected.X = x;
			_selected.Y = Mathf.Clamp(y, 0, _level.Length);
			_dragMoved = true;
			_dirty = true;
			_canvas.QueueRedraw();
		}
	}

	private LevelElement HitTest(Vector2 position)
	{
		var scale = CanvasScale();
		foreach (var element in _level.Elements.AsEnumerable().Reverse())
		{
			var center = WorldToCanvas(ElementCanvasCenter(element));
			var relative = (position - center).Rotated(-Mathf.DegToRad(element.RotationDegrees));
			var hit = element.Type switch
			{
				"wall" => Math.Abs(relative.X) <= element.Width * scale / 2 + 8 && Math.Abs(relative.Y) <= element.Height * scale / 2 + 8,
				"gate" => Math.Abs(position.Y - center.Y) <= 8 && Math.Abs(position.X - center.X) <= _level.CorridorHalfWidth * scale,
				"diamond" => Math.Abs(relative.X) <= element.Width * scale + 8 && Math.Abs(relative.Y) <= element.Width * scale + 8,
				"piston" => Math.Abs(relative.X) <= element.Height * scale / 2 + 8 && Math.Abs(relative.Y) <= element.Width * scale / 2 + 8,
				_ => position.DistanceTo(center) <= Mathf.Max(12, element.Width * scale + 5)
			};
			if (hit) return element;
		}
		return null;
	}

	private void DrawCanvas(EditorCanvas canvas)
	{
		canvas.DrawRect(new Rect2(Vector2.Zero, canvas.Size), new Color("#101f1d"));
		var left = WorldToCanvas(new Vector2(-_level.CorridorHalfWidth, 0)).X;
		var right = WorldToCanvas(new Vector2(_level.CorridorHalfWidth, 0)).X;
		var top = WorldToCanvas(Vector2.Zero).Y;
		var bottom = WorldToCanvas(new Vector2(0, _level.Length)).Y;
		canvas.DrawRect(new Rect2(left, top, right - left, bottom - top), new Color("#304a41"));
		DrawGrid(canvas);
		foreach (var element in _level.Elements) DrawElement(canvas, element);
		if (_drawingWall)
		{
			var start = WorldToCanvas(_wallStart);
			var end = WorldToCanvas(_wallEnd);
			canvas.DrawLine(start, end, Paper, Mathf.Max(3, 20 * CanvasScale()), true);
			canvas.DrawLine(start, end, Coral, Mathf.Max(1, 14 * CanvasScale()), true);
		}
		if (_selected != null) DrawSelection(canvas, _selected);
		if (_tool == "rubber" && _eraserCursorVisible)
		{
			var cursor = WorldToCanvas(_eraserCursorWorld);
			var radius = Mathf.Max(3, _eraserSize * CanvasScale() / 2);
			canvas.DrawArc(cursor, radius, 0, Mathf.Tau, 40, Paper, 2);
		}
		canvas.DrawString(ThemeDB.FallbackFont, new Vector2(16, 22), $"{_level.Name}  |  {(_dirty ? "UNSAVED" : "SAVED")}", HorizontalAlignment.Left, -1, 12, Paper);
	}

	private void DrawGrid(EditorCanvas canvas)
	{
		var topLeft = CanvasToWorld(Vector2.Zero);
		var bottomRight = CanvasToWorld(canvas.Size);
		var minX = Mathf.Min(topLeft.X, bottomRight.X);
		var maxX = Mathf.Max(topLeft.X, bottomRight.X);
		var minY = Mathf.Min(topLeft.Y, bottomRight.Y);
		var maxY = Mathf.Max(topLeft.Y, bottomRight.Y);
		var step = GridStep();
		var lineCount = Mathf.Max((maxX - minX) / step, (maxY - minY) / step);
		step *= Mathf.Max(1, Mathf.CeilToInt(lineCount / 400));
		var color = new Color("#aab9a7", 0.12f);
		var firstX = Mathf.CeilToInt(minX / step) * step;
		for (var x = firstX; x <= maxX; x += step)
		{
			var screenX = WorldToCanvas(new Vector2(x, 0)).X;
			canvas.DrawLine(new Vector2(screenX, 0), new Vector2(screenX, canvas.Size.Y), color, 1);
		}
		var firstY = Mathf.CeilToInt(minY / step) * step;
		for (var y = firstY; y <= maxY; y += step)
		{
			var screenY = WorldToCanvas(new Vector2(0, y)).Y;
			canvas.DrawLine(new Vector2(0, screenY), new Vector2(canvas.Size.X, screenY), color, 1);
		}
	}

	private void DrawElement(EditorCanvas canvas, LevelElement element)
	{
		var center = WorldToCanvas(ElementCanvasCenter(element));
		var scale = CanvasScale();
		var rotation = Mathf.DegToRad(element.RotationDegrees);
		switch (element.Type)
		{
			case "wall":
				var wallPoints = RectanglePoints(center, Mathf.Max(4, element.Width * scale), Mathf.Max(4, element.Height * scale), rotation);
				canvas.DrawColoredPolygon(wallPoints, Coral);
				canvas.DrawPolyline(Closed(wallPoints), Paper, 1, true);
				break;
			case "gate":
				var gateLeft = WorldToCanvas(new Vector2(-_level.CorridorHalfWidth, element.Y));
				var gateRight = WorldToCanvas(new Vector2(_level.CorridorHalfWidth, element.Y));
				var openingLeft = WorldToCanvas(new Vector2(element.X - element.Width / 2, element.Y));
				var openingRight = WorldToCanvas(new Vector2(element.X + element.Width / 2, element.Y));
				var thickness = Mathf.Max(4, element.Height * scale);
				canvas.DrawRect(new Rect2(gateLeft.X, center.Y - thickness / 2, openingLeft.X - gateLeft.X, thickness), Coral);
				canvas.DrawRect(new Rect2(openingRight.X, center.Y - thickness / 2, gateRight.X - openingRight.X, thickness), Coral);
				canvas.DrawLine(new Vector2(openingLeft.X, center.Y), new Vector2(openingRight.X, center.Y), Paper, 2);
				break;
			case "diamond":
				var diamond = DiamondPoints(center, Mathf.Max(5, element.Width * scale), rotation);
				canvas.DrawColoredPolygon(diamond, new Color("#e6c85e"));
				canvas.DrawPolyline(Closed(diamond), Paper, 1, true);
				break;
			case "piston":
					var rect = new Rect2(center.X - Mathf.Max(10, element.Height * scale) / 2,
						center.Y - Mathf.Max(4, element.Width * scale) / 2, Mathf.Max(10, element.Height * scale), Mathf.Max(4, element.Width * scale));
				canvas.DrawRect(rect, Coral);
				break;
			case "start":
				DrawMarker(canvas, center, element.Width * scale, Mint, "S");
				break;
			case "checkpoint":
				DrawMarker(canvas, center, element.Width * scale, new Color("#e6c85e"), "C");
				break;
			case "finish":
				DrawMarker(canvas, center, element.Width * scale, Mint, "F");
				break;
		}
	}

	private void DrawSelection(EditorCanvas canvas, LevelElement element)
	{
		var center = WorldToCanvas(ElementCanvasCenter(element));
		var scale = CanvasScale();
		var size = element.Type switch
		{
			"wall" => new Vector2(Mathf.Max(12, element.Width * scale + 8), Mathf.Max(12, element.Height * scale + 8)),
			"gate" => new Vector2(2 * _level.CorridorHalfWidth * scale, Mathf.Max(12, element.Height * scale + 8)),
			"diamond" => new Vector2(Mathf.Max(12, 2 * element.Width * scale + 8), Mathf.Max(12, 2 * element.Width * scale + 8)),
			"piston" => new Vector2(Mathf.Max(12, element.Height * scale + 8), Mathf.Max(12, element.Width * scale + 8)),
			_ => new Vector2(Mathf.Max(16, 2 * element.Width * scale + 8), Mathf.Max(16, 2 * element.Width * scale + 8))
		};
		var points = RectanglePoints(center, size.X, size.Y, CanRotate(element) ? Mathf.DegToRad(element.RotationDegrees) : 0);
		canvas.DrawPolyline(Closed(points), Coral, 2, true);
		if (CanRotate(element))
		{
			var rotation = Mathf.DegToRad(element.RotationDegrees);
			var corner = center + new Vector2(size.X / 2, -size.Y / 2).Rotated(rotation);
			var handle = RotationHandlePosition(element);
			canvas.DrawLine(corner, handle, Mint, 1);
			canvas.DrawCircle(handle, 7, Mint);
			canvas.DrawArc(handle, 4, 0.5f, 5.3f, 16, Ink, 2);
			canvas.DrawLine(handle + new Vector2(2, -3), handle + new Vector2(5, -3), Ink, 2);
			canvas.DrawLine(handle + new Vector2(2, -3), handle + new Vector2(3, 0), Ink, 2);
		}
	}

	private bool CanRotate(LevelElement element) => element.Type is "wall" or "diamond";

	private Vector2 RotationHandlePosition(LevelElement element)
	{
		var scale = CanvasScale();
		var size = element.Type switch
		{
			"wall" => new Vector2(Mathf.Max(12, element.Width * scale + 8), Mathf.Max(12, element.Height * scale + 8)),
			_ => new Vector2(Mathf.Max(12, 2 * element.Width * scale + 8), Mathf.Max(12, 2 * element.Width * scale + 8))
		};
		var center = WorldToCanvas(ElementCanvasCenter(element));
		var cornerOffset = new Vector2(size.X / 2, -size.Y / 2).Rotated(Mathf.DegToRad(element.RotationDegrees));
		return center + cornerOffset + cornerOffset.Normalized() * 9;
	}

	private static void DrawMarker(EditorCanvas canvas, Vector2 center, float radius, Color color, string label)
	{
		radius = Mathf.Max(7, radius);
		canvas.DrawCircle(center, radius, new Color(color, 0.2f));
		canvas.DrawArc(center, radius, 0, Mathf.Tau, 32, color, 2);
		canvas.DrawString(ThemeDB.FallbackFont, center + new Vector2(-4, 4), label, HorizontalAlignment.Left, -1, 10, Paper);
	}

	private Vector2 CanvasToWorld(Vector2 position)
	{
		var scale = CanvasScale();
		return new Vector2((position.X - _canvas.Size.X / 2) / scale,
			(position.Y - CanvasTop()) / scale);
	}

	private Vector2 ElementCanvasCenter(LevelElement element) => element.Type == "piston"
		? new Vector2(element.Side < 0
			? -_level.CorridorHalfWidth + element.Height / 2
			: _level.CorridorHalfWidth - element.Height / 2, element.Y)
		: new Vector2(element.X, element.Y);

	private Vector2 WorldToCanvas(Vector2 position) => new(_canvas.Size.X / 2 + position.X * CanvasScale(), CanvasTop() + position.Y * CanvasScale());
	private float CanvasTop() => (_canvas.Size.Y - _level.Length * CanvasScale()) / 2;
	private float CanvasScale() => Mathf.Max(0.01f, Mathf.Min((_canvas.Size.Y - 40) / Mathf.Max(1, _level.Length),
		(_canvas.Size.X - 40) / Mathf.Max(1, (_level.CorridorHalfWidth + 50) * 2))) * (float)_zoomSlider.Value;
	private float Snap(float value) => _snapToggle.ButtonPressed ? Mathf.Round(value / GridStep()) * GridStep() : value;
	private float GridStep() => float.TryParse(_gridPicker.GetItemText(_gridPicker.Selected), NumberStyles.Float, CultureInfo.InvariantCulture, out var step) ? step : 20;
	private float leftTrack(EditorCanvas canvas) => WorldToCanvas(new Vector2(-_level.CorridorHalfWidth, 0)).X;
	private float rightTrack(EditorCanvas canvas) => WorldToCanvas(new Vector2(_level.CorridorHalfWidth, 0)).X;
	private float topOfTrack(EditorCanvas canvas) => WorldToCanvas(Vector2.Zero).Y;
	private float bottomOfTrack(EditorCanvas canvas) => WorldToCanvas(new Vector2(0, _level.Length)).Y;

	private void UpdateFeedback()
	{
		var errors = _level.Validate();
		SetFeedback(errors.Count == 0 ? (_dirty ? "Unsaved changes" : "Level data is valid") : string.Join("\n", errors), errors.Count > 0);
	}

	private void SetFeedback(string text, bool isError)
	{
		_feedback.Text = text;
		_feedback.AddThemeColorOverride("font_color", isError ? Coral : Paper);
	}

	private static Vector2[] RectanglePoints(Vector2 center, float width, float height, float rotation)
	{
		var half = new Vector2(width / 2, height / 2);
		return new[]
		{
			center + new Vector2(-half.X, -half.Y).Rotated(rotation),
			center + new Vector2(half.X, -half.Y).Rotated(rotation),
			center + new Vector2(half.X, half.Y).Rotated(rotation),
			center + new Vector2(-half.X, half.Y).Rotated(rotation)
		};
	}

	private static Vector2[] DiamondPoints(Vector2 center, float radius, float rotation) => new[]
	{
		center + new Vector2(0, -radius).Rotated(rotation),
		center + new Vector2(radius, 0).Rotated(rotation),
		center + new Vector2(0, radius).Rotated(rotation),
		center + new Vector2(-radius, 0).Rotated(rotation)
	};

	private static Vector2[] Closed(Vector2[] points) => points.Append(points[0]).ToArray();
	private static Label MakeLabel(string text, int size, Color color)
	{
		var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static StyleBoxFlat ButtonStyle(Color color) => new()
	{
		BgColor = color,
		CornerRadiusTopLeft = 3,
		CornerRadiusTopRight = 3,
		CornerRadiusBottomLeft = 3,
		CornerRadiusBottomRight = 3,
		ContentMarginLeft = 6,
		ContentMarginRight = 6
	};

	private sealed partial class EditorCanvas : Control
	{
		private readonly LevelEditorScreen _owner;

		public EditorCanvas(LevelEditorScreen owner)
		{
			_owner = owner;
			MouseFilter = MouseFilterEnum.Stop;
		}

		public override void _Draw() => _owner.DrawCanvas(this);
		public override void _GuiInput(InputEvent @event) => _owner.HandleCanvasInput(this, @event);
	}

	private sealed partial class ToolGlyph : Control
	{
		private readonly string _tool;

		public ToolGlyph(string tool)
		{
			_tool = tool;
			MouseFilter = MouseFilterEnum.Ignore;
		}

		public override void _Draw()
		{
			var center = Size / 2;
			var coral = new Color("#f08a68");
			var mint = new Color("#8de0b1");
			var yellow = new Color("#e6c85e");
			switch (_tool)
			{
				case "select":
					DrawColoredPolygon(new[] { new Vector2(3, 2), new Vector2(3, 24), new Vector2(9, 18), new Vector2(13, 26), new Vector2(17, 24), new Vector2(13, 16), new Vector2(21, 16) }, Paper);
					break;
				case "wall-line":
					DrawRect(new Rect2(2, center.Y - 4, 20, 8), coral);
					DrawRect(new Rect2(2, center.Y - 4, 20, 8), Paper, false, 1);
					break;
				case "wall-tile":
					DrawRect(new Rect2(center.X - 8, center.Y - 8, 16, 16), coral);
					DrawRect(new Rect2(center.X - 8, center.Y - 8, 16, 16), Paper, false, 1);
					break;
				case "rubber":
					var rubber = new[] { center + new Vector2(-8, -10), center + new Vector2(8, -10), center + new Vector2(8, 2), center + new Vector2(-8, 10) };
					DrawColoredPolygon(rubber, mint);
					DrawPolyline(new[] { rubber[0], rubber[1], rubber[2], rubber[3], rubber[0] }, Paper, 1, true);
					DrawLine(center + new Vector2(-5, 2), center + new Vector2(5, -3), coral, 2);
					break;
				case "gate":
					DrawRect(new Rect2(1, center.Y - 3, 8, 6), coral);
					DrawRect(new Rect2(15, center.Y - 3, 8, 6), coral);
					DrawLine(new Vector2(9, center.Y), new Vector2(15, center.Y), Paper, 1);
					break;
				case "diamond":
					var diamond = new[] { center + new Vector2(0, -11), center + new Vector2(10, 0), center + new Vector2(0, 11), center + new Vector2(-10, 0) };
					DrawColoredPolygon(diamond, yellow);
					DrawPolyline(new[] { diamond[0], diamond[1], diamond[2], diamond[3], diamond[0] }, Paper, 1, true);
					break;
				case "piston":
					DrawRect(new Rect2(2, center.Y - 5, 12, 10), coral);
					DrawRect(new Rect2(14, center.Y - 2, 8, 4), yellow);
					break;
				case "start":
					DrawCircle(center, 9, new Color(mint, 0.28f));
					DrawArc(center, 9, 0, Mathf.Tau, 24, mint, 2);
					break;
				case "checkpoint":
					DrawCircle(center, 10, new Color(yellow, 0.22f));
					DrawArc(center, 10, 0, Mathf.Tau, 24, yellow, 2);
					DrawArc(center, 5, 0, Mathf.Tau, 16, Paper, 1);
					break;
				case "finish":
					DrawCircle(center, 9, new Color(mint, 0.2f));
					DrawArc(center, 9, 0, Mathf.Tau, 24, mint, 2);
					break;
			}
		}
	}
}