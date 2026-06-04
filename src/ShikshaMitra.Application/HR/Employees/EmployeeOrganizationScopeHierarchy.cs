using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.HR.Employees;

internal static class EmployeeOrganizationScopeHierarchy
{
	public static void AddResolvedStageIds(
		HashSet<Guid> stageIds,
		UserScopeAssignment assignment,
		IReadOnlyCollection<EmployeeOrganizationStageInfo> stageInfos,
		IReadOnlyDictionary<Guid, EmployeeOrganizationStageInfo> stageInfoById,
		IReadOnlyDictionary<Guid, Guid> standardStageLookup,
		IReadOnlyDictionary<Guid, Guid> divisionStageLookup)
	{
		if (assignment.EducationMediumStageId.HasValue)
		{
			AddIfInScope(stageIds, assignment.EducationMediumStageId.Value, stageInfoById);
		}

		if (assignment.StandardId.HasValue &&
			standardStageLookup.TryGetValue(assignment.StandardId.Value, out var standardStageId))
		{
			AddIfInScope(stageIds, standardStageId, stageInfoById);
		}

		if (assignment.DivisionId.HasValue &&
			divisionStageLookup.TryGetValue(assignment.DivisionId.Value, out var divisionStageId))
		{
			AddIfInScope(stageIds, divisionStageId, stageInfoById);
		}

		if (assignment.StandardId.HasValue || assignment.DivisionId.HasValue || assignment.EducationMediumStageId.HasValue)
		{
			return;
		}

		foreach (var stage in ExpandBroadScope(assignment, stageInfos))
		{
			stageIds.Add(stage.Id);
		}
	}

	private static IEnumerable<EmployeeOrganizationStageInfo> ExpandBroadScope(
		UserScopeAssignment assignment,
		IEnumerable<EmployeeOrganizationStageInfo> stageInfos)
	{
		if (assignment.EducationMediumId.HasValue)
		{
			return stageInfos.Where(x => x.MediumId == assignment.EducationMediumId.Value);
		}

		if (assignment.EducationBoardId.HasValue)
		{
			return stageInfos.Where(x => x.BoardId == assignment.EducationBoardId.Value);
		}

		if (assignment.AcademicYearId.HasValue)
		{
			return stageInfos.Where(x => x.AcademicYearId == assignment.AcademicYearId.Value);
		}

		return stageInfos;
	}

	private static void AddIfInScope(
		HashSet<Guid> stageIds,
		Guid stageId,
		IReadOnlyDictionary<Guid, EmployeeOrganizationStageInfo> stageInfoById)
	{
		if (stageInfoById.ContainsKey(stageId))
		{
			stageIds.Add(stageId);
		}
	}
}

internal sealed class EmployeeOrganizationStageInfo
{
	public Guid Id { get; init; }
	public string Name { get; init; } = string.Empty;
	public Guid MediumId { get; init; }
	public Guid BoardId { get; init; }
	public Guid AcademicYearId { get; init; }
}
