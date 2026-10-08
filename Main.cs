using Godot;

public partial class Main : Control
{
	public override void _Ready()
	{
		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

		var message = new Label
		{
			Text = "KururinNext\nProject foundation ready",
			HorizontalAlignment = HorizontalAlignment.Center
		};
		message.AddThemeFontSizeOverride("font_size", 32);

		center.AddChild(message);
		AddChild(center);
	}
}