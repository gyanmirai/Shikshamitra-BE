using System;
using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Employees;

internal static class EmployeeStatusProjection
{
	public static EmployeeStatusResponseDto ToResponse(
		Employee employee,
		EmployeeStatusType? previousStatus,
		EmployeeStatusType currentStatus,
		DateTimeOffset updatedAtFallback)
	{
		return new EmployeeStatusResponseDto
		{
			EmployeeId = employee.Id.ToString(),
			PreviousStatus = previousStatus,
			CurrentStatus = currentStatus,
			UpdatedAt = employee.StatusLastChangedAt ?? updatedAtFallback,
			EffectiveDate = employee.StatusEffectiveDate
		};
	}
}
