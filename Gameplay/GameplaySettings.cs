using System;
using System.Text.Json;

public static class GameplaySettings
{
	public const float DefaultCollisionRecoilRadians = 0.7f;
	public static float CollisionRecoilRadians { get; private set; } = DefaultCollisionRecoilRadians;

	public static void Load(string json)
	{
		CollisionRecoilRadians = ParseCollisionRecoilRadians(json);
	}

	public static float ParseCollisionRecoilRadians(string json)
	{
		try
		{
			using var document = JsonDocument.Parse(json);
			if (document.RootElement.ValueKind == JsonValueKind.Object
				&& document.RootElement.TryGetProperty("gameplay", out var gameplay)
				&& gameplay.ValueKind == JsonValueKind.Object
				&& gameplay.TryGetProperty("collision_recoil_radians", out var recoil)
				&& recoil.TryGetSingle(out var radians)
				&& float.IsFinite(radians))
			{
				return Math.Clamp(radians, 0, MathF.PI);
			}
		}
		catch (JsonException)
		{
		}

		return DefaultCollisionRecoilRadians;
	}
}