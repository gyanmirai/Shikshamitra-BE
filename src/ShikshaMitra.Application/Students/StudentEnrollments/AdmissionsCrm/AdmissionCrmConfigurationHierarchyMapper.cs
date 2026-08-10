using System;
using System.Collections.Generic;
using System.Linq;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmConfigurationHierarchyMapper
{
	public static AdmissionCrmHierarchyMapping Build(AdmissionCrmConfigurationSnapshot source,
		AdmissionCrmConfigurationSnapshot target)
	{
		var map = new Dictionary<Guid, Guid>();
		foreach (var sourceBoard in source.EducationBoards.OrderBy(x => x.Id))
		{
			var targetBoard = target.EducationBoards.OrderBy(x => x.Id).FirstOrDefault(x => x.Type == sourceBoard.Type);
			if (targetBoard != null) map[sourceBoard.Id] = targetBoard.Id;
		}
		foreach (var sourceMedium in source.EducationMediums.OrderBy(x => x.Id))
		{
			if (!map.TryGetValue(sourceMedium.EducationBoardId, out var targetBoardId)) continue;
			var targetMedium = target.EducationMediums.OrderBy(x => x.Id).FirstOrDefault(x =>
				x.EducationBoardId == targetBoardId && NamesMatch(x.Name, sourceMedium.Name));
			if (targetMedium != null) map[sourceMedium.Id] = targetMedium.Id;
		}
		foreach (var sourceStage in source.EducationMediumStages.OrderBy(x => x.Id))
		{
			if (!map.TryGetValue(sourceStage.EducationMediumId, out var targetMediumId)) continue;
			var targetStage = target.EducationMediumStages.OrderBy(x => x.Id).FirstOrDefault(x =>
				x.EducationMediumId == targetMediumId && NamesMatch(x.Name, sourceStage.Name));
			if (targetStage != null) map[sourceStage.Id] = targetStage.Id;
		}
		foreach (var sourceStandard in source.Standards.OrderBy(x => x.Id))
		{
			if (!map.TryGetValue(sourceStandard.EducationMediumStageId, out var targetStageId)) continue;
			var targetStandard = target.Standards.OrderBy(x => x.Id).FirstOrDefault(x =>
				x.EducationMediumStageId == targetStageId && NamesMatch(x.Name, sourceStandard.Name));
			if (targetStandard != null) map[sourceStandard.Id] = targetStandard.Id;
		}
		foreach (var sourceDivision in source.Divisions.OrderBy(x => x.Id))
		{
			if (!map.TryGetValue(sourceDivision.StandardId, out var targetStandardId)) continue;
			var targetDivision = target.Divisions.OrderBy(x => x.Id).FirstOrDefault(x =>
				x.StandardId == targetStandardId && NamesMatch(x.Name, sourceDivision.Name));
			if (targetDivision != null) map[sourceDivision.Id] = targetDivision.Id;
		}

		var kinds = source.EducationBoards.ToDictionary(x => x.Id, _ => "education board");
		foreach (var item in source.EducationMediums) kinds[item.Id] = "academic medium";
		foreach (var item in source.EducationMediumStages) kinds[item.Id] = "academic medium stage";
		foreach (var item in source.Standards) kinds[item.Id] = "standard";
		foreach (var item in source.Divisions) kinds[item.Id] = "division";
		var referenced = AdmissionCrmConfigurationCloneJson.Values(source).SelectMany(AdmissionCrmConfigurationCloneJson.Tokens)
			.Where(kinds.ContainsKey).ToHashSet();
		var conflicts = new HashSet<string>(StringComparer.Ordinal);
		var boardIds = source.EducationBoards.Select(x => x.Id).ToHashSet();
		var mediumIds = source.EducationMediums.Select(x => x.Id).ToHashSet();
		var standardIds = source.Standards.Select(x => x.Id).ToHashSet();
		foreach (var series in source.NumberSeries.OrderBy(x => x.Id))
		{
			AddDirect(series.EducationBoardId, "education board", series.Code, boardIds, referenced, conflicts);
			AddDirect(series.AcademicMediumId, "academic medium", series.Code, mediumIds, referenced, conflicts);
			AddDirect(series.StandardId, "standard", series.Code, standardIds, referenced, conflicts);
		}
		foreach (var sourceId in referenced.OrderBy(x => x))
			if (kinds.TryGetValue(sourceId, out var kind) && !map.ContainsKey(sourceId))
				conflicts.Add($"Referenced source-year {kind} '{sourceId:D}' has no equivalent record in the target academic year.");
		return new AdmissionCrmHierarchyMapping(map, referenced, conflicts.OrderBy(x => x, StringComparer.Ordinal).ToList());
	}

	public static bool CanMapNumberSeries(AdmissionCrmNumberSeries series, AdmissionCrmConfigurationSnapshot source,
		IReadOnlyDictionary<Guid, Guid> map) =>
		CanMapOptional(series.EducationBoardId, source.EducationBoards.Select(x => x.Id), map) &&
		CanMapOptional(series.AcademicMediumId, source.EducationMediums.Select(x => x.Id), map) &&
		CanMapOptional(series.StandardId, source.Standards.Select(x => x.Id), map);

	private static void AddDirect(Guid? sourceId, string kind, string code, IReadOnlySet<Guid> validIds,
		ISet<Guid> referenced, ISet<string> conflicts)
	{
		if (!sourceId.HasValue) return;
		referenced.Add(sourceId.Value);
		if (!validIds.Contains(sourceId.Value))
			conflicts.Add($"Identifier number series '{code}' references {kind} '{sourceId.Value:D}', which does not belong to the source academic year.");
	}

	private static bool CanMapOptional(Guid? sourceId, IEnumerable<Guid> validIds, IReadOnlyDictionary<Guid, Guid> map) =>
		!sourceId.HasValue || (validIds.Contains(sourceId.Value) && map.ContainsKey(sourceId.Value));
	private static bool NamesMatch(string left, string right) =>
		string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}

internal sealed record AdmissionCrmHierarchyMapping(IReadOnlyDictionary<Guid, Guid> IdMap,
	IReadOnlySet<Guid> ReferencedSourceIds, IReadOnlyList<string> Conflicts);
