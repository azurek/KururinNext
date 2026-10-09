using System.Collections.Generic;
using System.Linq;
using Godot;

public static class LevelFileStore
{
	public const string UserDirectory = "user://levels";

	public static LevelDefinition LoadById(string id, out List<string> errors)
	{
		errors = new List<string>();
		var userPath = $"{UserDirectory}/{id}.json";
		if (FileAccess.FileExists(userPath))
		{
			if (TryLoad(userPath, out var userLevel, out var userErrors) && userLevel!.Id == id)
			{
				return userLevel;
			}
			errors.AddRange(userErrors.Count > 0 ? userErrors : new[] { $"Level ID in {userPath} does not match '{id}'." });
		}

		var resourcePath = $"res://Levels/{id}.json";
		if (FileAccess.FileExists(resourcePath))
		{
			if (TryLoad(resourcePath, out var resourceLevel, out var resourceErrors) && resourceLevel!.Id == id)
			{
				return resourceLevel;
			}
			errors.AddRange(resourceErrors.Count > 0 ? resourceErrors : new[] { $"Level ID in {resourcePath} does not match '{id}'." });
		}
		return null;
	}

	public static bool TryLoad(string path, out LevelDefinition level, out List<string> errors)
	{
		level = null;
		errors = new List<string>();
		if (!FileAccess.FileExists(path))
		{
			errors.Add($"Level file not found: {path}");
			return false;
		}
		return LevelDefinition.TryFromJson(FileAccess.GetFileAsString(path), out level, out errors);
	}

	public static bool Save(LevelDefinition level, out string path, out List<string> errors)
	{
		path = $"{UserDirectory}/{level.Id}.json";
		errors = level.Validate();
		if (errors.Count > 0)
		{
			return false;
		}

		var directoryPath = ProjectSettings.GlobalizePath(UserDirectory);
		if (!DirAccess.DirExistsAbsolute(directoryPath))
		{
			var directoryError = DirAccess.MakeDirRecursiveAbsolute(directoryPath);
			if (directoryError != Error.Ok && !DirAccess.DirExistsAbsolute(directoryPath))
			{
				errors.Add($"Could not create the user levels folder: {directoryError}.");
				return false;
			}
		}
		var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			errors.Add($"Could not save level: {FileAccess.GetOpenError()}.");
			return false;
		}
		file.StoreString(level.ToJson());
		file.Close();
		return true;
	}

	public static IReadOnlyList<string> GetAvailableIds()
	{
		var ids = new HashSet<string> { "course-01", "test-small" };
		AddJsonIds("res://Levels", ids);
		AddJsonIds(UserDirectory, ids);
		return ids.OrderBy(id => id, System.StringComparer.Ordinal).ToArray();
	}

	public static IReadOnlyList<string> GetAvailableUserIds()
	{
		var ids = new HashSet<string>(System.StringComparer.Ordinal);
		AddJsonIds(UserDirectory, ids);
		return ids.OrderBy(id => id, System.StringComparer.Ordinal).ToArray();
	}

	private static void AddJsonIds(string directory, ISet<string> ids)
	{
		var absolutePath = ProjectSettings.GlobalizePath(directory);
		if (!DirAccess.DirExistsAbsolute(absolutePath))
		{
			return;
		}
		using var access = DirAccess.Open(directory);
		if (access == null)
		{
			return;
		}
		foreach (var fileName in access.GetFiles())
		{
			if (fileName.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
			{
				ids.Add(fileName[..^5]);
			}
		}
	}
}