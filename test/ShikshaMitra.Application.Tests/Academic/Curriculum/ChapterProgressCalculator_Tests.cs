using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Academic.Curriculum;

public class ChapterProgressCalculator_Tests
{
	private static readonly Guid ChapterId = Guid.NewGuid();
	private static readonly Guid DivisionA = Guid.NewGuid();
	private static readonly Guid DivisionB = Guid.NewGuid();
	private static readonly DateTime Today = new(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void Calculate_Should_Use_Latest_Submitted_Diary_Per_Division()
	{
		var snapshots = new List<ChapterProgressSnapshot>
		{
			Snapshot(DivisionA, Today.AddDays(-1), 65, isActual: true),
			Snapshot(DivisionA, Today, 100, isActual: true)
		};

		ChapterProgressCalculator.Calculate(snapshots)[ChapterId].ShouldBe(100);
	}

	[Fact]
	public void Calculate_Should_Average_Current_Progress_Across_Divisions()
	{
		var snapshots = new List<ChapterProgressSnapshot>
		{
			Snapshot(DivisionA, Today, 100, isActual: true),
			Snapshot(DivisionB, Today, 65, isActual: true)
		};

		ChapterProgressCalculator.Calculate(snapshots)[ChapterId].ShouldBe(83);
	}

	[Fact]
	public void Calculate_Should_Use_Latest_Planned_Progress_When_Division_Has_No_Diary()
	{
		var snapshots = new List<ChapterProgressSnapshot>
		{
			Snapshot(DivisionA, Today.AddDays(-1), 30, isActual: false),
			Snapshot(DivisionA, Today, 60, isActual: false)
		};

		ChapterProgressCalculator.Calculate(snapshots)[ChapterId].ShouldBe(60);
	}

	[Fact]
	public void Calculate_Should_Prefer_Actual_Progress_Over_Planned_Progress()
	{
		var snapshots = new List<ChapterProgressSnapshot>
		{
			Snapshot(DivisionA, Today.AddDays(1), 100, isActual: false),
			Snapshot(DivisionA, Today, 75, isActual: true)
		};

		ChapterProgressCalculator.Calculate(snapshots)[ChapterId].ShouldBe(75);
	}

	private static ChapterProgressSnapshot Snapshot(
		Guid divisionId,
		DateTime effectiveDate,
		int percentage,
		bool isActual) =>
		new(
			ChapterId,
			divisionId,
			effectiveDate,
			effectiveDate,
			percentage,
			isActual);
}
