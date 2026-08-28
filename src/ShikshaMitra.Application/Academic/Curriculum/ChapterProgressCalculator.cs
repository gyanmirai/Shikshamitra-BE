using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Academic.Curriculum;

internal readonly record struct ChapterProgressSnapshot(
	Guid ChapterId,
	Guid DivisionId,
	DateTime EffectiveDate,
	DateTime RecordedAt,
	int Percentage,
	bool IsActual);

internal static class ChapterProgressCalculator
{
	public static IReadOnlyDictionary<Guid, int> Calculate(
		IEnumerable<ChapterProgressSnapshot> snapshots)
	{
		return snapshots
			.GroupBy(snapshot => snapshot.ChapterId)
			.ToDictionary(
				chapterGroup => chapterGroup.Key,
				chapterGroup => CalculateChapterProgress(chapterGroup));
	}

	private static int CalculateChapterProgress(
		IEnumerable<ChapterProgressSnapshot> chapterSnapshots)
	{
		var currentProgressByDivision = chapterSnapshots
			.GroupBy(snapshot => snapshot.DivisionId)
			.Select(SelectCurrentDivisionProgress)
			.ToList();

		return (int)Math.Round(
			currentProgressByDivision.Average(),
			MidpointRounding.AwayFromZero);
	}

	private static int SelectCurrentDivisionProgress(
		IEnumerable<ChapterProgressSnapshot> divisionSnapshots)
	{
		var snapshots = divisionSnapshots.ToList();
		var actualSnapshots = snapshots.Where(snapshot => snapshot.IsActual).ToList();
		var candidates = actualSnapshots.Count > 0 ? actualSnapshots : snapshots;

		return candidates
			.OrderByDescending(snapshot => snapshot.EffectiveDate)
			.ThenByDescending(snapshot => snapshot.RecordedAt)
			.Select(snapshot => Math.Clamp(snapshot.Percentage, 0, 100))
			.First();
	}
}
