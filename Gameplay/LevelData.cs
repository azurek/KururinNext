#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public sealed class LevelDefinition
{
	public const int CurrentVersion = 1;
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true,
		WriteIndented = true
	};

	public int Version { get; set; } = CurrentVersion;
	public string Id { get; set; } = "untitled";
	public string Name { get; set; } = "Untitled Level";
	public float CorridorHalfWidth { get; set; } = 240;
	public float Length { get; set; } = 2090;
	public List<LevelElement> Elements { get; set; } = new();

	public LevelDefinition Clone() => JsonSerializer.Deserialize<LevelDefinition>(ToJson(), JsonOptions)!;

	public static LevelDefinition CreateNew(string id, string name)
	{
		var level = new LevelDefinition { Id = id, Name = name };
		level.Elements.Add(new LevelElement { Type = "start", X = 0, Y = 80, Width = 36 });
		level.Elements.Add(new LevelElement { Type = "checkpoint", X = 0, Y = 1000, Width = 36 });
		level.Elements.Add(new LevelElement { Type = "finish", X = 0, Y = 1900, Width = 46 });
		return level;
	}

	public List<string> Validate()
	{
		var errors = new List<string>();
		if (Version != CurrentVersion)
		{
			errors.Add($"Unsupported level version {Version}; expected {CurrentVersion}.");
		}
		if (string.IsNullOrWhiteSpace(Id) || Id.Length > 80 ||
			Id.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
		{
			errors.Add("Level ID must be a non-empty file-safe name.");
		}
		if (string.IsNullOrWhiteSpace(Name))
		{
			errors.Add("Level name cannot be empty.");
		}
		if (!IsPositiveFinite(CorridorHalfWidth) || CorridorHalfWidth > 5000)
		{
			errors.Add("Corridor half-width must be between 0 and 5000.");
		}
		if (!IsPositiveFinite(Length) || Length > 10000)
		{
			errors.Add("Level length must be between 0 and 10000.");
		}
		if (Elements == null)
		{
			errors.Add("Level geometry is missing.");
			return errors;
		}

		foreach (var marker in new[] { "start", "checkpoint", "finish" })
		{
			var count = Elements.Count(element => element?.Type == marker);
			if (count != 1)
			{
				errors.Add($"Level must contain exactly one {marker} marker; found {count}.");
			}
		}

		for (var index = 0; index < Elements.Count; index++)
		{
			var element = Elements[index];
			if (element == null)
			{
				errors.Add($"Geometry item {index + 1} is empty.");
				continue;
			}
			if (!AllowedTypes.Contains(element.Type))
			{
				errors.Add($"Geometry item {index + 1} has unsupported type '{element.Type}'.");
				continue;
			}
			if (!IsFinite(element.X) || !IsFinite(element.Y) || Math.Abs(element.X) > 10000 || Math.Abs(element.Y) > 10000)
			{
				errors.Add($"{element.Type} item {index + 1} has an invalid position.");
			}
			if (!IsPositiveFinite(element.Width) || element.Width > 10000)
			{
				errors.Add($"{element.Type} item {index + 1} must have a width/radius between 0 and 10000.");
			}
			if (element.Type is "wall" or "gate" && (!IsPositiveFinite(element.Height) || element.Height > 10000))
			{
				errors.Add($"{element.Type} item {index + 1} must have a height between 0 and 10000.");
			}
			if (!IsFinite(element.RotationDegrees) || Math.Abs(element.RotationDegrees) > 3600)
			{
				errors.Add($"{element.Type} item {index + 1} has an invalid rotation.");
			}
			if (element.Type == "gate" && IsPositiveFinite(CorridorHalfWidth) && element.Width >= CorridorHalfWidth * 2)
			{
				errors.Add($"Gate item {index + 1} must be narrower than the corridor.");
			}
			if (element.Type == "gate" && IsPositiveFinite(CorridorHalfWidth) &&
				Math.Abs(element.X) + element.Width / 2 >= CorridorHalfWidth)
			{
				errors.Add($"Gate item {index + 1} opening must fit inside the corridor.");
			}
			if (element.Type == "gate" && Math.Abs(element.RotationDegrees) > 0.001f)
			{
				errors.Add($"Gate item {index + 1} must remain horizontal.");
			}
			if (element.Type == "piston")
			{
				if (!IsPositiveFinite(element.Height) || element.Height > 10000 ||
					!IsPositiveFinite(element.MinimumExtension) || element.MinimumExtension >= element.Height)
				{
					errors.Add($"Piston item {index + 1} must have a positive minimum extension below its maximum height.");
				}
				if (!IsPositiveFinite(element.Frequency) || !IsFinite(element.Phase) || element.Side is not (-1 or 1))
				{
					errors.Add($"Piston item {index + 1} needs a positive frequency and side of -1 or 1.");
				}
				if (Math.Abs(element.RotationDegrees) > 0.001f)
				{
					errors.Add($"Piston item {index + 1} must align to the corridor.");
				}
			}
			if (element.Type is "start" or "checkpoint" or "finish" && IsPositiveFinite(Length) &&
				(element.Y < 0 || element.Y > Length))
			{
				errors.Add($"{element.Type} marker {index + 1} must be within the level length.");
			}
			if (element.Type is "start" or "checkpoint" or "finish" && IsPositiveFinite(CorridorHalfWidth) &&
				Math.Abs(element.X) + element.Width > CorridorHalfWidth)
			{
				errors.Add($"{element.Type} marker {index + 1} must fit inside the corridor.");
			}
		}

		return errors;
	}

	public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

	public static bool TryFromJson(string json, out LevelDefinition? level, out List<string> errors)
	{
		level = null;
		errors = new List<string>();
		try
		{
			level = JsonSerializer.Deserialize<LevelDefinition>(json, JsonOptions);
		}
		catch (JsonException exception)
		{
			errors.Add($"Level file is not valid JSON: {exception.Message}");
			return false;
		}

		if (level == null)
		{
			errors.Add("Level file is empty.");
			return false;
		}
		errors = level.Validate();
		return errors.Count == 0;
	}

	private static readonly HashSet<string> AllowedTypes = new(StringComparer.Ordinal)
	{
		"wall", "gate", "diamond", "piston", "start", "checkpoint", "finish"
	};

	private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
	private static bool IsPositiveFinite(float value) => IsFinite(value) && value > 0;
}

public sealed record LevelElement
{
	public string Type { get; set; } = "wall";
	public float X { get; set; }
	public float Y { get; set; }
	public float Width { get; set; } = 20;
	public float Height { get; set; } = 20;
	public float RotationDegrees { get; set; }
	public float MinimumExtension { get; set; } = 16;
	public float Frequency { get; set; } = 1.2f;
	public float Phase { get; set; }
	public int Side { get; set; } = -1;
}