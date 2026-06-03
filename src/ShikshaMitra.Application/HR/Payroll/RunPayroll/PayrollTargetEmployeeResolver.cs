using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.RunPayroll;

internal static class PayrollTargetEmployeeResolver
{
	public static List<PayrollTargetEmployee> ResolveTargetEmployees(
		List<PayrollTargetEmployee> scopedEmployees,
		List<Guid>? requestedEmployeeIds)
	{
		if (requestedEmployeeIds == null || requestedEmployeeIds.Count == 0)
		{
			return scopedEmployees;
		}

		var requestedSet = requestedEmployeeIds.Distinct().ToHashSet();
		var scopedById = scopedEmployees.ToDictionary(e => e.Id, e => e);
		var outOfScopeIds = requestedSet.Where(id => !scopedById.ContainsKey(id)).ToList();
		if (outOfScopeIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope)
				.WithData("employeeIds", outOfScopeIds);
		}

		return requestedSet.Select(id => scopedById[id]).ToList();
	}

	public static List<PayrollTargetEmployee> ResolvePayrollLockTargetEmployees(
		List<PayrollTargetEmployee> allScopedEmployees,
		List<PayrollTargetEmployee> eligibleEmployees,
		List<Guid>? requestedEmployeeIds,
		string month)
	{
		if (requestedEmployeeIds == null || requestedEmployeeIds.Count == 0)
		{
			return eligibleEmployees;
		}

		var requestedSet = requestedEmployeeIds.Distinct().ToHashSet();
		var allScopedById = allScopedEmployees.ToDictionary(e => e.Id, e => e);
		var outOfScopeIds = requestedSet.Where(id => !allScopedById.ContainsKey(id)).ToList();
		if (outOfScopeIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope)
				.WithData("employeeIds", outOfScopeIds);
		}

		var eligibleById = eligibleEmployees.ToDictionary(e => e.Id, e => e);
		var ineligibleIds = requestedSet.Where(id => !eligibleById.ContainsKey(id)).ToList();
		if (ineligibleIds.Count > 0)
		{
			throw new BusinessException(HRErrorCodes.PayrollNoEmployees)
				.WithData("month", month)
				.WithData("employeeIds", ineligibleIds);
		}

		return requestedSet.Select(id => eligibleById[id]).ToList();
	}
}
