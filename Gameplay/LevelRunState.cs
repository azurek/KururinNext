using System;

public sealed class LevelRunState
{
	public const int StartingHearts = 3;
	public const double CollisionPenaltySeconds = 3;

	public int HeartsRemaining { get; private set; }
	public double ElapsedSeconds { get; private set; }
	public bool IsFailed { get; private set; }
	public bool IsComplete { get; private set; }
	public bool IsTerminal => IsFailed || IsComplete;

	public LevelRunState()
	{
		Reset();
	}

	public void Advance(double seconds)
	{
		if (seconds < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(seconds));
		}
		if (!IsTerminal)
		{
			ElapsedSeconds += seconds;
		}
	}

	public bool RegisterCollision()
	{
		if (IsTerminal)
		{
			return false;
		}

		ElapsedSeconds += CollisionPenaltySeconds;
		HeartsRemaining--;
		if (HeartsRemaining == 0)
		{
			IsFailed = true;
		}
		return true;
	}

	public void Complete()
	{
		if (!IsFailed)
		{
			IsComplete = true;
		}
	}

	public void Reset()
	{
		HeartsRemaining = StartingHearts;
		ElapsedSeconds = 0;
		IsFailed = false;
		IsComplete = false;
	}
}