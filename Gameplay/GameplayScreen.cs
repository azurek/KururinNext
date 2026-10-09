using System;
using Godot;

public partial class GameplayScreen : Control
{
	private static readonly Color Ink = new("#142a28");
	private static readonly Color Paper = new("#f4efd9");
	private static readonly Color Mint = new("#8de0b1");
	private static readonly Color Coral = new("#f08a68");
	private readonly LevelRunState _runState = new();
	private CourseWorld _world = null!;
	private Label _status = null!;
	private Label _timer = null!;
	private Label _hearts = null!;
	private Label _message = null!;
	private Control _overlay = null!;
	private bool _paused;
	private bool _finished;
	private bool _failed;
	private bool _failurePending;
	private float _failureDelay;
	private float _messageTime;
	private StageDefinition _stage = WorldStageCatalog.FirstStage;

	public event Action ReturnToMenuRequested;
	public event Action StageSelectRequested;
	public event Action<string> StageCompleted;

	public void Configure(StageDefinition stage)
	{
		_stage = stage;
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;
		MouseFilter = MouseFilterEnum.Stop;
		_world = new CourseWorld { Name = "CourseWorld", ProcessMode = ProcessModeEnum.Pausable };
		_world.Configure(_stage.LevelDataId);
		AddChild(_world);
		_world.HitWall += OnHitWall;
		_world.CheckpointReached += OnCheckpointReached;
		_world.Finished += ShowFinish;
		BuildHud();
	}

	public override void _Process(double delta)
	{
		if (_failurePending)
		{
			_failureDelay -= (float)delta;
			if (_failureDelay <= 0)
			{
				ShowFailure();
			}
			return;
		}

		if (_paused || _finished || _failed || _runState.IsTerminal)
		{
			return;
		}

		_runState.Advance(delta);
		_status.Text = _world.GetAlignmentStatus();
		_timer.Text = $"TIME {FormatTime(_runState.ElapsedSeconds)}";
		_hearts.Text = $"HEARTS {_runState.HeartsRemaining} / {LevelRunState.StartingHearts}";
		if (_messageTime > 0)
		{
			_messageTime = Mathf.Max(0, _messageTime - (float)delta);
			if (_messageTime == 0)
			{
				_message.Text = "WASD / ARROWS MOVE    Q / E ADJUST SPIN";
				_message.AddThemeColorOverride("font_color", Paper);
			}
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsEcho())
		{
			return;
		}
		if (@event.IsActionPressed("pause"))
		{
			if (_finished || _failed || _failurePending || _runState.IsTerminal)
			{
				return;
			}
			SetPaused(!_paused);
			GetViewport().SetInputAsHandled();
		}
		else if (@event.IsActionPressed("retry") && !_paused && !_finished && !_failurePending && !_runState.IsTerminal)
		{
			_world.Player.RetryFromCheckpoint();
			SetMessage("BACK IN THE COURSE", Mint);
			GetViewport().SetInputAsHandled();
		}
	}

	private void BuildHud()
	{
		var layer = new CanvasLayer { Name = "HUD" };
		AddChild(layer);
		var root = new Control();
		root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		root.MouseFilter = MouseFilterEnum.Ignore;
		layer.AddChild(root);

		var title = MakeLabel($"{_stage.Number:00} / {_stage.DisplayName}", 14, Mint);
		title.Position = new Vector2(28, 20);
		title.Size = new Vector2(520, 24);
		root.AddChild(title);
		_timer = MakeLabel("TIME 00:00.0", 14, Paper);
		_timer.Position = new Vector2(570, 20);
		_timer.Size = new Vector2(145, 24);
		root.AddChild(_timer);
		_hearts = MakeLabel("HEARTS 3 / 3", 14, Coral);
		_hearts.Position = new Vector2(745, 20);
		_hearts.Size = new Vector2(170, 24);
		root.AddChild(_hearts);
		_status = MakeLabel("TURN TO FIT", 16, Paper);
		_status.Position = new Vector2(28, 48);
		_status.Size = new Vector2(500, 26);
		root.AddChild(_status);
		_message = MakeLabel("WASD / ARROWS MOVE    Q / E ADJUST SPIN", 13, Paper);
		_message.AnchorTop = 1;
		_message.AnchorBottom = 1;
		_message.OffsetLeft = 28;
		_message.OffsetRight = 720;
		_message.OffsetTop = -48;
		_message.OffsetBottom = -20;
		root.AddChild(_message);

		var buttons = new HBoxContainer
		{
			AnchorLeft = 1,
			AnchorRight = 1,
			OffsetLeft = -270,
			OffsetRight = -18,
			OffsetTop = 18,
			OffsetBottom = 66
		};
		buttons.AddThemeConstantOverride("separation", 8);
		root.AddChild(buttons);
		var retry = MakeButton("RETRY");
		retry.Pressed += () => _world.Player.RetryFromCheckpoint();
		buttons.AddChild(retry);
		var pause = MakeButton("PAUSE");
		pause.Pressed += () => SetPaused(true);
		buttons.AddChild(pause);

		_overlay = new Control { Visible = false, MouseFilter = MouseFilterEnum.Stop };
		_overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		root.AddChild(_overlay);
		UpdateRunHud();
	}

	private void OnHitWall()
	{
		if (!_runState.RegisterCollision())
		{
			return;
		}
		_world.Player.ApplyCollisionRecoil();
		UpdateRunHud();
		SetMessage($"HIT! +3 SEC    {_runState.HeartsRemaining} HEARTS LEFT", Coral);
		if (_runState.IsFailed)
		{
			_failurePending = true;
			_failureDelay = RotorPlayer.CollisionRecoverySeconds + 0.06f;
		}
	}

	private void OnCheckpointReached()
	{
		SetMessage("CHECKPOINT REACHED", Mint);
	}

	private void SetMessage(string text, Color color)
	{
		_message.Text = text;
		_message.AddThemeColorOverride("font_color", color);
		_messageTime = 2.5f;
	}

	private void ShowFinish()
	{
		if (_finished || _failed)
		{
			return;
		}
		_runState.Complete();
		_finished = true;
		StageCompleted?.Invoke(_stage.Id);
		_paused = true;
		GetTree().Paused = true;
		ShowOverlay("COURSE COMPLETE", $"Final time: {FormatTime(_runState.ElapsedSeconds)}", MakeFinishButtons);
	}

	private void ShowFailure()
	{
		_failurePending = false;
		_failed = true;
		_paused = true;
		GetTree().Paused = true;
		ShowOverlay("LEVEL FAILED", $"All hearts lost. Time: {FormatTime(_runState.ElapsedSeconds)}", MakeFailureButtons);
	}

	private void SetPaused(bool paused)
	{
		if (_finished || _failed || _failurePending)
		{
			return;
		}
		_paused = paused;
		GetTree().Paused = paused;
		if (paused)
		{
			ShowOverlay("PAUSED", "Take a breath, then keep going.", MakePauseButtons);
		}
		else
		{
			_overlay.Visible = false;
		}
	}

	private void ShowOverlay(string heading, string detail, Action<VBoxContainer> addButtons)
	{
		foreach (var child in _overlay.GetChildren())
		{
			_overlay.RemoveChild(child);
			child.QueueFree();
		}
		_overlay.Visible = true;
		var backdrop = new ColorRect
		{
			Color = new Color("#142a28", 0.84f),
			MouseFilter = MouseFilterEnum.Stop
		};
		backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_overlay.AddChild(backdrop);
		var content = new VBoxContainer
		{
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -170,
			OffsetTop = -100,
			OffsetRight = 170,
			OffsetBottom = 120,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		content.AddThemeConstantOverride("separation", 14);
		_overlay.AddChild(content);
		var headingLabel = MakeLabel(heading, 32, Mint);
		headingLabel.HorizontalAlignment = HorizontalAlignment.Center;
		headingLabel.CustomMinimumSize = new Vector2(340, 42);
		content.AddChild(headingLabel);
		var detailLabel = MakeLabel(detail, 15, Paper);
		detailLabel.HorizontalAlignment = HorizontalAlignment.Center;
		detailLabel.CustomMinimumSize = new Vector2(340, 42);
		detailLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		content.AddChild(detailLabel);
		addButtons(content);
	}

	private void MakePauseButtons(VBoxContainer content)
	{
		var resume = AddOverlayButton(content, "RESUME", () => SetPaused(false));
		resume.GrabFocus();
		AddOverlayButton(content, "RETRY CHECKPOINT", () =>
		{
			_world.Player.RetryFromCheckpoint();
			SetMessage("BACK IN THE COURSE", Mint);
			SetPaused(false);
		});
		AddOverlayButton(content, "RETURN TO MENU", ReturnToMenu);
	}

	private void MakeFinishButtons(VBoxContainer content)
	{
		var returnButton = AddOverlayButton(content, "STAGE SELECT", ReturnToStageSelect);
		returnButton.GrabFocus();
	}

	private void MakeFailureButtons(VBoxContainer content)
	{
		var restartButton = AddOverlayButton(content, "RESTART LEVEL", RestartLevel);
		restartButton.GrabFocus();
		AddOverlayButton(content, "RETURN TO MENU", ReturnToMenu);
	}

	private void RestartLevel()
	{
		GetTree().Paused = false;
		_paused = false;
		_finished = false;
		_failed = false;
		_failurePending = false;
		_failureDelay = 0;
		_runState.Reset();
		_world.ResetRun();
		_message.Text = "WASD / ARROWS MOVE    Q / E ADJUST SPIN";
		_message.AddThemeColorOverride("font_color", Paper);
		_messageTime = 0;
		_overlay.Visible = false;
		UpdateRunHud();
	}

	private Button AddOverlayButton(VBoxContainer parent, string text, Action action)
	{
		var button = MakeButton(text);
		button.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
		button.Pressed += action;
		parent.AddChild(button);
		return button;
	}

	private void ReturnToMenu()
	{
		GetTree().Paused = false;
		ReturnToMenuRequested?.Invoke();
	}

	private void ReturnToStageSelect()
	{
		GetTree().Paused = false;
		StageSelectRequested?.Invoke();
	}

	private void UpdateRunHud()
	{
		_timer.Text = $"TIME {FormatTime(_runState.ElapsedSeconds)}";
		_hearts.Text = $"HEARTS {_runState.HeartsRemaining} / {LevelRunState.StartingHearts}";
	}

	private static string FormatTime(double seconds)
	{
		var tenths = (long)(seconds * 10);
		return $"{tenths / 600:00}:{(tenths / 10) % 60:00}.{tenths % 10}";
	}

	private static Label MakeLabel(string text, int size, Color color)
	{
		var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static Button MakeButton(string text)
	{
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(118, 42), FocusMode = FocusModeEnum.All };
		button.AddThemeFontSizeOverride("font_size", 13);
		button.AddThemeColorOverride("font_color", Paper);
		button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color("#263f3a"), new Color("#42665a")));
		button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color("#35534a"), Mint));
		button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color("#1e3430"), Coral));
		return button;
	}

	private static StyleBoxFlat ButtonStyle(Color background, Color border)
	{
		return new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border,
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4
		};
	}

}