using Godot;

public partial class Main : Control
{
	private const string SettingsPath = "user://kururinnext.cfg";
	private const string ProgressPath = "user://kururinnext_progress.json";
	private const string AudioSection = "audio";
	private const string MasterVolumeKey = "master_volume";
	private static readonly Color Ink = new("#142a28");
	private static readonly Color Paper = new("#f4efd9");
	private static readonly Color Muted = new("#aab9a7");
	private static readonly Color Mint = new("#8de0b1");
	private static readonly Color Coral = new("#f08a68");
	private Screen _currentScreen;

	private MarginContainer _pageMargin = null!;
	private VBoxContainer _pageContent = null!;
	private HSlider _volumeSlider = null!;
	private Label _volumeValue = null!;
	private float _masterVolume = 0.8f;
	private GameplayScreen _gameplay = null!;
	private LevelEditorScreen _levelEditor = null!;
	private ProgressionState _progress = ProgressionState.CreateNew();

	private enum Screen
	{
		MainMenu,
		Options,
		StageSelect,
		LevelEditor,
		Play
	}

	public override void _Ready()
	{
		LoadSettings();
		LoadProgress();
		ApplyMasterVolume();

		const string gameplaySettingsPath = "res://appsettings.json";
		if (FileAccess.FileExists(gameplaySettingsPath))
		{
			GameplaySettings.Load(FileAccess.GetFileAsString(gameplaySettingsPath));
		}
		_pageMargin = new MarginContainer();
		_pageMargin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_pageMargin.AddThemeConstantOverride("margin_left", 56);
		_pageMargin.AddThemeConstantOverride("margin_top", 36);
		_pageMargin.AddThemeConstantOverride("margin_right", 56);
		_pageMargin.AddThemeConstantOverride("margin_bottom", 32);
		AddChild(_pageMargin);

		_pageContent = new VBoxContainer();
		_pageContent.AddThemeConstantOverride("separation", 18);
		_pageMargin.AddChild(_pageContent);
		ShowMainMenu();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel") || @event.IsEcho())
		{
			return;
		}

		if (_currentScreen is Screen.Options or Screen.StageSelect)
		{
			ShowMainMenu();
			GetViewport().SetInputAsHandled();
		}
	}

	private void ShowMainMenu()
	{
		CloseGameplay();
		CloseLevelEditor();
		_pageMargin.Visible = true;
		_currentScreen = Screen.MainMenu;
		ClearPage();
		AddHeader("A LITTLE WORLD IN MOTION");

		var body = CreateBody();
		var copy = CreateCopyColumn(body);
		AddTitle(copy, "KURURIN\nNEXT", 54);
		AddLabel(copy, "FIND YOUR WAY THROUGH THE SPIN.", 14, Muted);
		AddSpacer(copy, 16);

		var stages = CreateButton("STAGE SELECT", true);
		stages.Pressed += ShowStageSelect;
		copy.AddChild(stages);

		var options = CreateButton("OPTIONS");
		options.Pressed += ShowOptions;
		copy.AddChild(options);

		var editor = CreateButton("LEVEL EDITOR (DEV)");
		editor.Pressed += ShowLevelEditor;
		copy.AddChild(editor);

		var quit = CreateButton("QUIT");
		quit.Pressed += () => GetTree().Quit();
		copy.AddChild(quit);

		AddArtwork(body);
		AddFooter("ARROWS / WASD  MOVE     ENTER  SELECT     ESC  BACK");
		stages.GrabFocus();
	}

	private void ShowStageSelect()
	{
		CloseGameplay();
		_pageMargin.Visible = true;
		_currentScreen = Screen.StageSelect;
		ClearPage();
		AddHeader("SELECT STAGE");

		var body = CreateBody();
		var copy = CreateCopyColumn(body);
		AddTitle(copy, "WORLDS", 36);
		Button firstPlayableStage = null;
		foreach (var world in WorldStageCatalog.Worlds)
		{
			AddLabel(copy, world.DisplayName, 13, Mint);
			foreach (var stage in world.Stages)
			{
				var unlocked = _progress.IsUnlocked(stage);
				var completed = _progress.IsCompleted(stage);
				var status = completed ? "COMPLETED" : unlocked ? "UNLOCKED" : "LOCKED";
				if (unlocked && !stage.IsImplemented)
				{
					status = "UNLOCKED · IN DEVELOPMENT";
				}

				var button = CreateButton($"{stage.Number:00}  {stage.DisplayName}    {status}");
				button.CustomMinimumSize = new Vector2(380, 46);
				button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
				button.Disabled = !unlocked || !stage.IsImplemented;
				button.Pressed += () => StartGameplay(stage);
				copy.AddChild(button);
				if (!button.Disabled && firstPlayableStage == null)
				{
					firstPlayableStage = button;
				}
			}
		}

		var back = CreateButton("BACK TO MENU");
		back.Pressed += ShowMainMenu;
		copy.AddChild(back);
		AddArtwork(body);
		AddFooter("ENTER  SELECT     ESC  BACK");
		(firstPlayableStage ?? back).GrabFocus();
	}

	private void ShowOptions()
	{
		_currentScreen = Screen.Options;
		ClearPage();
		AddHeader("SETTINGS");

		var body = CreateBody();
		var copy = CreateCopyColumn(body);
		AddTitle(copy, "OPTIONS", 42);
		AddLabel(copy, "AUDIO", 14, Mint);

		var volumeRow = new HBoxContainer();
		volumeRow.AddThemeConstantOverride("separation", 16);
		copy.AddChild(volumeRow);

		_volumeSlider = new HSlider
		{
			MinValue = 0,
			MaxValue = 1,
			Step = 0.01,
			Value = _masterVolume,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			CustomMinimumSize = new Vector2(220, 42),
			FocusMode = FocusModeEnum.All
		};
		_volumeSlider.ValueChanged += OnVolumeChanged;
		volumeRow.AddChild(_volumeSlider);

		_volumeValue = new Label
		{
			CustomMinimumSize = new Vector2(52, 0),
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center
		};
		_volumeValue.AddThemeColorOverride("font_color", Paper);
		_volumeValue.AddThemeFontSizeOverride("font_size", 16);
		volumeRow.AddChild(_volumeValue);
		UpdateVolumeLabel();

		var back = CreateButton("BACK");
		back.Pressed += ShowMainMenu;
		copy.AddChild(back);
		AddArtwork(body);
		AddFooter("LEFT / RIGHT  ADJUST     ESC  BACK");
		_volumeSlider.GrabFocus();
	}

	private void StartGameplay(StageDefinition stage)
	{
		if (!stage.IsImplemented || !_progress.IsUnlocked(stage))
		{
			return;
		}

		OpenGameplay(stage, null, false);
	}

	private void ShowLevelEditor()
	{
		CloseGameplay();
		CloseLevelEditor();
		_pageMargin.Visible = false;
		_currentScreen = Screen.LevelEditor;
		_levelEditor = new LevelEditorScreen();
		_levelEditor.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_levelEditor.BackRequested += ShowMainMenu;
		_levelEditor.PlaytestRequested += StartEditorPlaytest;
		AddChild(_levelEditor);
	}

	private void StartEditorPlaytest(LevelDefinition level)
	{
		if (_levelEditor == null)
		{
			return;
		}
		_levelEditor.Visible = false;
		_levelEditor.ProcessMode = ProcessModeEnum.Disabled;
		OpenGameplay(WorldStageCatalog.FirstStage, level, true);
	}

	private void OpenGameplay(StageDefinition stage, LevelDefinition level, bool isPlaytest)
	{
		_currentScreen = Screen.Play;
		_pageMargin.Visible = false;
		_gameplay = new GameplayScreen();
		_gameplay.Configure(stage, level, isPlaytest);
		_gameplay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_gameplay.ReturnToMenuRequested += ShowMainMenu;
		_gameplay.StageSelectRequested += ShowStageSelect;
		if (isPlaytest)
		{
			_gameplay.ReturnToEditorRequested += ReturnToEditor;
		}
		_gameplay.StageCompleted += OnStageCompleted;
		AddChild(_gameplay);
	}

	private void ReturnToEditor()
	{
		CloseGameplay();
		if (_levelEditor == null)
		{
			ShowMainMenu();
			return;
		}
		_levelEditor.Visible = true;
		_levelEditor.ProcessMode = ProcessModeEnum.Inherit;
		_currentScreen = Screen.LevelEditor;
	}

	private void CloseLevelEditor()
	{
		if (_levelEditor == null)
		{
			return;
		}
		RemoveChild(_levelEditor);
		_levelEditor.QueueFree();
		_levelEditor = null;
	}

	private void CloseGameplay()
	{
		if (_gameplay == null)
		{
			return;
		}

		RemoveChild(_gameplay);
		_gameplay.QueueFree();
		_gameplay = null;
	}

	private void OnStageCompleted(string stageId)
	{
		if (_progress.CompleteStage(stageId))
		{
			SaveProgress();
		}
	}

	private void LoadProgress()
	{
		_progress = FileAccess.FileExists(ProgressPath)
			? ProgressionState.Load(FileAccess.GetFileAsString(ProgressPath))
			: ProgressionState.CreateNew();
	}

	private void SaveProgress()
	{
		var file = FileAccess.Open(ProgressPath, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			GD.PushWarning($"Could not save stage progress: {FileAccess.GetOpenError()}");
			return;
		}

		file.StoreString(_progress.ToJson());
		file.Close();
	}

	private void ClearPage()
	{
		foreach (var child in _pageContent.GetChildren())
		{
			_pageContent.RemoveChild(child);
			child.QueueFree();
		}
	}

	private void AddHeader(string text)
	{
		var header = new HBoxContainer();
		var brand = new Label { Text = "K / N" };
		brand.AddThemeColorOverride("font_color", Mint);
		brand.AddThemeFontSizeOverride("font_size", 18);
		var section = new Label
		{
			Text = text,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalAlignment = HorizontalAlignment.Right
		};
		section.AddThemeColorOverride("font_color", Muted);
		section.AddThemeFontSizeOverride("font_size", 12);
		header.AddChild(brand);
		header.AddChild(section);
		_pageContent.AddChild(header);
	}

	private HBoxContainer CreateBody()
	{
		var body = new HBoxContainer
		{
			SizeFlagsVertical = SizeFlags.ExpandFill,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center,
			CustomMinimumSize = new Vector2(0, 440)
		};
		body.AddThemeConstantOverride("separation", 28);
		_pageContent.AddChild(body);
		return body;
	}

	private static VBoxContainer CreateCopyColumn(HBoxContainer body)
	{
		var copy = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(380, 0),
			SizeFlagsVertical = SizeFlags.ShrinkCenter
		};
		copy.AddThemeConstantOverride("separation", 12);
		body.AddChild(copy);
		return copy;
	}

	private static void AddArtwork(HBoxContainer body)
	{
		var artwork = new RotorArtwork
		{
			CustomMinimumSize = new Vector2(360, 360),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		body.AddChild(artwork);
	}

	private static void AddTitle(VBoxContainer parent, string text, int size)
	{
		var title = new Label { Text = text };
		title.AddThemeColorOverride("font_color", Paper);
		title.AddThemeFontSizeOverride("font_size", size);
		parent.AddChild(title);
	}

	private static void AddLabel(VBoxContainer parent, string text, int size, Color color)
	{
		var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeFontSizeOverride("font_size", size);
		parent.AddChild(label);
	}

	private static void AddSpacer(VBoxContainer parent, int height)
	{
		parent.AddChild(new Control { CustomMinimumSize = new Vector2(0, height) });
	}

	private void AddFooter(string text)
	{
		var footer = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		footer.AddThemeColorOverride("font_color", Muted);
		footer.AddThemeFontSizeOverride("font_size", 12);
		_pageContent.AddChild(footer);
	}

	private static Button CreateButton(string text, bool primary = false)
	{
		var button = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(280, 52),
			SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
			FocusMode = FocusModeEnum.All
		};
		button.AddThemeFontSizeOverride("font_size", 16);
		button.AddThemeColorOverride("font_color", primary ? Ink : Paper);
		button.AddThemeColorOverride("font_hover_color", primary ? Ink : Mint);
		button.AddThemeColorOverride("font_focus_color", primary ? Ink : Mint);
		button.AddThemeStyleboxOverride("normal", ButtonStyle(primary ? Mint : new Color("#263f3a"), new Color("#263f3a")));
		button.AddThemeStyleboxOverride("hover", ButtonStyle(primary ? new Color("#a8efc5") : new Color("#35534a"), Mint));
		button.AddThemeStyleboxOverride("pressed", ButtonStyle(primary ? new Color("#70c995") : new Color("#1e3430"), Mint));
		button.AddThemeStyleboxOverride("focus", ButtonStyle(Colors.Transparent, Coral));
		return button;
	}

	private static StyleBoxFlat ButtonStyle(Color background, Color border)
	{
		var style = new StyleBoxFlat
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
		style.ContentMarginLeft = 18;
		style.ContentMarginRight = 18;
		return style;
	}

	private void OnVolumeChanged(double value)
	{
		_masterVolume = (float)value;
		ApplyMasterVolume();
		UpdateVolumeLabel();
		SaveSettings();
	}

	private void UpdateVolumeLabel()
	{
		_volumeValue.Text = $"{Mathf.RoundToInt(_masterVolume * 100)}%";
	}

	private void ApplyMasterVolume()
	{
		var masterBus = AudioServer.GetBusIndex("Master");
		if (masterBus >= 0)
		{
			AudioServer.SetBusVolumeDb(masterBus, _masterVolume <= 0 ? -80 : Mathf.LinearToDb(_masterVolume));
		}
	}

	private void LoadSettings()
	{
		var settings = new ConfigFile();
		if (settings.Load(SettingsPath) == Error.Ok)
		{
			_masterVolume = Mathf.Clamp((float)settings.GetValue(AudioSection, MasterVolumeKey, 0.8f), 0, 1);
		}
	}

	private void SaveSettings()
	{
		var settings = new ConfigFile();
		settings.SetValue(AudioSection, MasterVolumeKey, _masterVolume);
		settings.Save(SettingsPath);
	}

	private sealed partial class RotorArtwork : Control
	{
		public override void _Draw()
		{
			var center = Size / 2;
			var radius = Mathf.Min(Size.X, Size.Y) * 0.39f;
			DrawCircle(center, radius * 1.22f, new Color("#1c3733"));
			DrawArc(center, radius * 1.08f, 0, Mathf.Tau, 96, new Color("#42665a"), 1.5f, true);
			DrawArc(center, radius * 0.86f, 0, Mathf.Tau, 96, new Color("#42665a"), 1.0f, true);

			for (var index = 0; index < 4; index++)
			{
				var angle = Mathf.Pi * 0.25f + index * Mathf.Pi * 0.5f;
				var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
				var start = center + direction * radius * 0.24f;
				var end = center + direction * radius * 0.94f;
				DrawLine(start, end, index % 2 == 0 ? Coral : Mint, 13, true);
				DrawCircle(end, 8, Paper);
			}

			DrawCircle(center, radius * 0.22f, Paper);
			DrawCircle(center, radius * 0.15f, Coral);
			DrawCircle(center, radius * 0.055f, Ink);
		}
	}
}