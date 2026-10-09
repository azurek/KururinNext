#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

public sealed record StageDefinition(
	string Id,
	int Number,
	string DisplayName,
	string LevelDataId,
	string? RequiredCompletedStageId,
	bool IsImplemented);

public sealed record WorldDefinition(string Id, string DisplayName, IReadOnlyList<StageDefinition> Stages);

public static class WorldStageCatalog
{
	public const string FirstStageId = "world-01-stage-01";

	public static IReadOnlyList<WorldDefinition> Worlds { get; } = Array.AsReadOnly(new[]
	{
		new WorldDefinition("world-01", "THE WINDING WALK", Array.AsReadOnly(new[]
		{
			new StageDefinition(FirstStageId, 1, "THE WINDING WALK", "course-01", null, true),
			new StageDefinition("world-01-stage-02", 2, "THE LONGER TURN", "course-02", FirstStageId, true),
			new StageDefinition("world-01-stage-03", 3, "THE SIDEWAYS SHIFT", "course-03", "world-01-stage-02", true),
			new StageDefinition("world-01-stage-04", 4, "THE PULSE CHAMBER", "course-04", "world-01-stage-03", true),
			new StageDefinition("world-01-stage-05", 5, "THE LAST PRESS", "course-05", "world-01-stage-04", true)
		}))
	});

	public static IEnumerable<StageDefinition> Stages => Worlds.SelectMany(world => world.Stages);

	public static StageDefinition FirstStage => Stages.First(stage => stage.Id == FirstStageId);

	public static StageDefinition? FindStage(string id) => Stages.FirstOrDefault(stage => stage.Id == id);
}

public sealed class ProgressionState
{
	public const int CurrentSaveVersion = 1;
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		PropertyNameCaseInsensitive = true
	};
	private readonly HashSet<string> _completedStageIds = new(StringComparer.Ordinal);

	public static ProgressionState CreateNew() => new();

	public static ProgressionState Load(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return CreateNew();
		}

		try
		{
			var document = JsonSerializer.Deserialize<SaveDocument>(json, JsonOptions);
			if (document == null || document.Version != CurrentSaveVersion || document.CompletedStageIds == null)
			{
				return CreateNew();
			}

			var state = CreateNew();
			foreach (var stageId in document.CompletedStageIds)
			{
				if (WorldStageCatalog.FindStage(stageId) != null)
				{
					state._completedStageIds.Add(stageId);
				}
			}
			return state;
		}
		catch (JsonException)
		{
			return CreateNew();
		}
	}

	public bool IsCompleted(StageDefinition stage) => _completedStageIds.Contains(stage.Id);

	public bool IsUnlocked(StageDefinition stage) =>
		string.IsNullOrEmpty(stage.RequiredCompletedStageId) || _completedStageIds.Contains(stage.RequiredCompletedStageId);

	public bool CompleteStage(string stageId)
	{
		var stage = WorldStageCatalog.FindStage(stageId);
		if (stage == null || !stage.IsImplemented || !IsUnlocked(stage))
		{
			return false;
		}

		return _completedStageIds.Add(stageId);
	}

	public string ToJson() => JsonSerializer.Serialize(new SaveDocument
	{
		Version = CurrentSaveVersion,
		CompletedStageIds = _completedStageIds.OrderBy(stageId => stageId, StringComparer.Ordinal).ToList()
	}, JsonOptions);

	private sealed class SaveDocument
	{
		public int Version { get; set; }
		public List<string> CompletedStageIds { get; set; } = new();
	}
}