using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ShikshaMitra.HR;

public static class HrUserScopeAccess
{
	public static bool HasHrWideAccess(UserScopeContext scope) =>
		scope.HasTenantWideScope();

	public static bool CanReadAllLeaveRequests(UserScopeContext scope) => scope.IsAdmin;

	public static bool HasAnyConcreteHrStageAccess(UserScopeContext scope) =>
		HasHrWideAccess(scope) ||
		scope.MediumStageIds.Count > 0 ||
		scope.WildcardBoardAcademicYearIds.Count > 0 ||
		scope.WildcardMediumBoardIds.Count > 0 ||
		scope.WildcardMediumStageMediumIds.Count > 0 ||
		scope.BoardIds.Count > 0 ||
		scope.MediumIds.Count > 0;

	public static bool HasSingleConcreteMediumStage(UserScopeContext scope) =>
		scope.MediumStageIds.Count == 1;

	public static bool CanAccessEmployeeStage(UserScopeContext scope, Guid? employeeStageId) =>
		HasHrWideAccess(scope) ||
		(employeeStageId.HasValue && scope.CanAccessMediumStage(employeeStageId.Value));

	public static string BuildScopeSummaryKey(UserScopeContext scope)
	{
		if (HasHrWideAccess(scope))
		{
			return "hr-wide";
		}

		var raw = string.Join("|",
			$"ay:{JoinIds(scope.AcademicYearIds)}",
			$"b:{JoinIds(scope.BoardIds)}",
			$"m:{JoinIds(scope.MediumIds)}",
			$"ms:{JoinIds(scope.MediumStageIds)}",
			$"s:{JoinIds(scope.StandardIds)}",
			$"d:{JoinIds(scope.DivisionIds)}",
			$"way:{JoinIds(scope.WildcardBoardAcademicYearIds)}",
			$"wb:{JoinIds(scope.WildcardMediumBoardIds)}",
			$"wm:{JoinIds(scope.WildcardMediumStageMediumIds)}",
			$"wms:{JoinIds(scope.WildcardStandardMediumStageIds)}",
			$"ws:{JoinIds(scope.WildcardDivisionStandardIds)}");
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
		return $"hr-scoped-{Convert.ToHexString(hash)[..32].ToLowerInvariant()}";
	}

	public static List<Guid> FilterEmployeeIdsByStage(
		IReadOnlyCollection<Guid> employeeIds,
		IReadOnlyDictionary<Guid, Guid?> employeeStageMap,
		UserScopeContext scope,
		bool includeUnknownStageEmployees)
	{
		if (HasHrWideAccess(scope))
		{
			return employeeIds.ToList();
		}

		return employeeIds
			.Where(id =>
			{
				if (!employeeStageMap.TryGetValue(id, out var stageId) || !stageId.HasValue)
				{
					return includeUnknownStageEmployees;
				}

				return scope.CanAccessMediumStage(stageId.Value);
			})
			.ToList();
	}

	private static string JoinIds(IEnumerable<Guid> ids) =>
		string.Join(",", ids.OrderBy(x => x).Select(x => x.ToString("N")));
}
