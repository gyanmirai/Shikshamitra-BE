using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendancePayrollWriteGuard(
	IPayrollInputRevisionStore payrollFence,
	AttendanceRegisterLookupReader lookupReader) : ITransientDependency
{
	public async Task EnsureEditableAsync(IEnumerable<(Guid EmployeeId, DateOnly AttendanceDate)> attendance)
	{
		// Serialize the check with payroll issuance and the attendance write in this unit of work.
		await payrollFence.LockAsync();
		await EnsureEditableUnderFenceAsync(attendance);
	}

	internal async Task EnsureEditableUnderFenceAsync(IEnumerable<(Guid EmployeeId, DateOnly AttendanceDate)> attendance)
	{
		var locks = await lookupReader.BuildLockLookupAsync(attendance);
		foreach (var (key, state) in locks)
		{
			if (state.IsLocked)
			{
				throw new BusinessException(HRErrorCodes.AttendancePayrollLocked)
					.WithData("employeeId", key.EmployeeId)
					.WithData("month", key.MonthYear);
			}
		}
	}
}
