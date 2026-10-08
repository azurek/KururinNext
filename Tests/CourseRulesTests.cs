using Xunit;

public class CourseRulesTests
{
	[Theory]
	[InlineData("{\"gameplay\":{\"collision_recoil_radians\":0.9}}", 0.9f)]
	[InlineData("{\"gameplay\":{\"collision_recoil_radians\":-0.25}}", 0f)]
	[InlineData("{\"gameplay\":{\"collision_recoil_radians\":4}}", 3.1415927f)]
	public void CollisionRecoilLoadsAndClampsConfiguration(string json, float expected)
	{
		Assert.InRange(MathF.Abs(GameplaySettings.ParseCollisionRecoilRadians(json) - expected), 0f, 0.00001f);
	}

	[Fact]
	public void InvalidCollisionRecoilConfigurationUsesDefault()
	{
		Assert.Equal(GameplaySettings.DefaultCollisionRecoilRadians, GameplaySettings.ParseCollisionRecoilRadians("not json"));
		Assert.Equal(GameplaySettings.DefaultCollisionRecoilRadians, GameplaySettings.ParseCollisionRecoilRadians("{}"));
		Assert.Equal(GameplaySettings.DefaultCollisionRecoilRadians, GameplaySettings.ParseCollisionRecoilRadians("[]"));
		Assert.Equal(GameplaySettings.DefaultCollisionRecoilRadians, GameplaySettings.ParseCollisionRecoilRadians("{\"gameplay\":[]}"));
	}

	[Fact]
	public void RunStartsWithThreeHeartsAndAdvancesTimer()
	{
		var run = new LevelRunState();

		Assert.Equal(3, run.HeartsRemaining);
		run.Advance(12.5);
		Assert.Equal(12.5, run.ElapsedSeconds);
	}

	[Fact]
	public void EachCollisionAddsThreeSecondsAndConsumesOneHeart()
	{
		var run = new LevelRunState();
		run.Advance(4.25);

		Assert.True(run.RegisterCollision());
		Assert.Equal(2, run.HeartsRemaining);
		Assert.Equal(7.25, run.ElapsedSeconds);
		Assert.False(run.IsFailed);
	}

	[Fact]
	public void ThirdCollisionFailsAndStopsTheRun()
	{
		var run = new LevelRunState();
		run.RegisterCollision();
		run.RegisterCollision();
		run.RegisterCollision();

		Assert.Equal(0, run.HeartsRemaining);
		Assert.True(run.IsFailed);
		Assert.True(run.IsTerminal);
		Assert.False(run.RegisterCollision());
		run.Advance(10);
		Assert.Equal(9, run.ElapsedSeconds);
	}

	[Fact]
	public void CompletingRunPreservesFinalTime()
	{
		var run = new LevelRunState();
		run.Advance(9.5);
		run.Complete();

		Assert.True(run.IsComplete);
		run.Advance(10);
		Assert.False(run.RegisterCollision());
		Assert.Equal(9.5, run.ElapsedSeconds);
		Assert.Equal(3, run.HeartsRemaining);
	}

	[Fact]
	public void ResetStartsANewAttemptFromZero()
	{
		var run = new LevelRunState();
		run.Advance(10);
		run.RegisterCollision();

		run.Reset();

		Assert.Equal(3, run.HeartsRemaining);
		Assert.Equal(0, run.ElapsedSeconds);
		Assert.False(run.IsTerminal);
	}

	[Theory]
	[InlineData(-1f, 1.6f)]
	[InlineData(0f, 2.4f)]
	[InlineData(1f, 3.2f)]
	public void AutomaticSpinContinuesWhileInputAdjustsItsRate(float adjustment, float expectedSpeed)
	{
		var actualSpeed = CourseRules.GetAutomaticSpinSpeed(adjustment, 2.4f, 0.8f);
		Assert.InRange(MathF.Abs(actualSpeed - expectedSpeed), 0f, 0.00001f);
		Assert.True(actualSpeed > 0f);
	}

	[Theory]
	[InlineData(0f, 7f)]
	[InlineData(1.5707964f, 58f)]
	public void RotorProjectionMatchesCapsuleOrientation(float angle, float expectedHalfWidth)
	{
		Assert.InRange(MathF.Abs(CourseRules.GetRotorHalfWidth(angle, 51f, 7f) - expectedHalfWidth), 0f, 0.001f);
	}

	[Fact]
	public void VerticalRotorFitsNarrowGate()
	{
		Assert.True(CourseRules.RotorFitsThroughGate(0f, 51f, 7f, 40f, 0f));
	}

	[Fact]
	public void HorizontalRotorDoesNotFitNarrowGate()
	{
		Assert.False(CourseRules.RotorFitsThroughGate(MathF.PI / 2f, 51f, 7f, 40f, 0f));
	}

	[Fact]
	public void GateFitIncludesPlayerOffsetFromOpeningCenter()
	{
		Assert.True(CourseRules.RotorFitsThroughGate(0f, 51f, 7f, 40f, 32f));
		Assert.False(CourseRules.RotorFitsThroughGate(0f, 51f, 7f, 40f, 34f));
	}

	[Fact]
	public void FreshProgressUnlocksOnlyTheFirstImplementedStage()
	{
		var progress = ProgressionState.CreateNew();
		var firstStage = WorldStageCatalog.FindStage("world-01-stage-01");
		var nextStage = WorldStageCatalog.FindStage("world-01-stage-02")!;

		Assert.NotNull(firstStage);
		Assert.NotNull(nextStage);
		Assert.True(progress.IsUnlocked(firstStage));
		Assert.False(progress.IsUnlocked(nextStage));
		Assert.False(progress.IsCompleted(firstStage));
	}

	[Fact]
	public void CompletingStageUnlocksNextAndSurvivesSerialization()
	{
		var progress = ProgressionState.CreateNew();
		var firstStage = WorldStageCatalog.FirstStage;
		var nextStage = WorldStageCatalog.FindStage("world-01-stage-02")!;

		Assert.True(progress.CompleteStage(firstStage.Id));
		Assert.True(progress.IsCompleted(firstStage));
		Assert.True(progress.IsUnlocked(nextStage));

		var restored = ProgressionState.Load(progress.ToJson());
		Assert.True(restored.IsCompleted(firstStage));
		Assert.True(restored.IsUnlocked(nextStage));
	}

	[Fact]
	public void SecondStageIsPlayableAfterFirstStageCompletion()
	{
		var progress = ProgressionState.CreateNew();
		var firstStage = WorldStageCatalog.FirstStage;
		var secondStage = WorldStageCatalog.FindStage("world-01-stage-02")!;

		Assert.False(progress.IsUnlocked(secondStage));
		Assert.True(progress.CompleteStage(firstStage.Id));
		Assert.True(progress.IsUnlocked(secondStage));
		Assert.True(secondStage.IsImplemented);
		Assert.Equal("course-02", secondStage.LevelDataId);
	}

	[Theory]
	[InlineData("not json")]
	[InlineData("{\"version\":0,\"completedStageIds\":[\"world-01-stage-01\"]}")]
	[InlineData("{\"version\":999,\"completedStageIds\":[\"world-01-stage-01\"]}")]
	public void MalformedOrUnsupportedProgressStartsFresh(string json)
	{
		var progress = ProgressionState.Load(json);
		var firstStage = WorldStageCatalog.FirstStage;
		var nextStage = WorldStageCatalog.FindStage("world-01-stage-02")!;

		Assert.False(progress.IsCompleted(firstStage));
		Assert.False(progress.IsUnlocked(nextStage));
	}
}