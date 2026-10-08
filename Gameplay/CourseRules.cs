using System;

public static class CourseRules
{
	public static float GetAutomaticSpinSpeed(float adjustment, float automaticSpeed, float adjustmentSpeed)
	{
		return MathF.Max(automaticSpeed * 0.5f, automaticSpeed + (adjustment * adjustmentSpeed));
	}

	public static float GetRotorHalfWidth(float angleRadians, float halfLength, float radius)
	{
		return MathF.Abs(MathF.Sin(angleRadians)) * halfLength + radius;
	}

	public static bool RotorFitsThroughGate(float angleRadians, float halfLength, float radius, float gateHalfWidth, float centerOffset)
	{
		return MathF.Abs(centerOffset) + GetRotorHalfWidth(angleRadians, halfLength, radius) <= gateHalfWidth;
	}
}