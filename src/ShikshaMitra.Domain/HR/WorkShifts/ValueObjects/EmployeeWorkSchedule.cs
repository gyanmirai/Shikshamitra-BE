using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
namespace ShikshaMitra.HR.WorkShifts;
public sealed record EmployeeWorkSchedule(IReadOnlyList<EmployeeShiftAssignment> Assignments)
{
	public EmployeeShiftAssignment Require(DateOnly date) => Assignments.SingleOrDefault(x => x.Covers(date))
	 ?? throw new BusinessException(HRErrorCodes.ShiftAssignmentMissing, "No employee work shift is assigned for this date.");
}