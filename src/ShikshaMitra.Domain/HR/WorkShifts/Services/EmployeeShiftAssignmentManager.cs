using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace ShikshaMitra.HR.WorkShifts;

public class EmployeeShiftAssignmentManager(
 IRepository<EmployeeShiftAssignment, Guid> assignments,
 IRepository<EmployeeWorkShift, Guid> shifts,
 IEmployeeRepository employees,
 IRepository<EmployeeAttendance, Guid> attendance,
 IRepository<PayrollRunItemSnapshot, Guid> payroll,
 EmployeeShiftResolver resolver, IEmployeeShiftWriteFence fence, IPayrollInputRevisionStore payrollFence) : DomainService
{
	public async Task ScheduleAsync(Employee employee, Guid shiftId, DateOnly from, string stamp)
	{
		await LockEmployeeAsync(employee, stamp);
		var today = await resolver.TodayAsync();
		if (from < today || (employee.DateOfJoining.HasValue && from < employee.DateOfJoining))
			throw new BusinessException(HRErrorCodes.ShiftAssignmentInvalidDate, "Choose today or a future date, no earlier than joining.");
		var shift = await shifts.GetAsync(x => x.Id == shiftId && x.TenantId == CurrentTenant.Id);
		if (!shift.IsActive) throw new BusinessException(HRErrorCodes.WorkShiftInvalid, "Select an active work shift.");
		// Serialize catalogue changes with assignment creation.
		await shifts.UpdateAsync(shift, autoSave: true);
		var rows = await assignments.GetListAsync(x => x.EmployeeId == employee.Id && x.TenantId == CurrentTenant.Id);
		var next = rows.Where(x => x.EffectiveFrom > from).OrderBy(x => x.EffectiveFrom).FirstOrDefault();
		await EnsureUnrecordedAsync(employee.Id, from, next?.EffectiveFrom);
		var same = rows.SingleOrDefault(x => x.EffectiveFrom == from);
		if (same != null) { await assignments.DeleteAsync(same, autoSave: true); }
		var previous = rows.Where(x => x.EffectiveFrom < from).OrderByDescending(x => x.EffectiveFrom).FirstOrDefault();
		if (previous != null && (!previous.EffectiveTo.HasValue || previous.EffectiveTo > from))
		{ previous.CloseAt(from); await assignments.UpdateAsync(previous, autoSave: true); }
		await assignments.InsertAsync(new EmployeeShiftAssignment(GuidGenerator.Create(), employee.Id, shift, from, next?.EffectiveFrom), autoSave: true);
	}
	public async Task CancelAsync(Employee employee, Guid assignmentId, string stamp)
	{
		await LockEmployeeAsync(employee, stamp);
		var today = await resolver.TodayAsync();
		var rows = await assignments.GetListAsync(x => x.EmployeeId == employee.Id && x.TenantId == CurrentTenant.Id);
		var row = rows.SingleOrDefault(x => x.Id == assignmentId) ?? throw new BusinessException(HRErrorCodes.ShiftAssignmentMissing);
		if (row.EffectiveFrom <= today) throw new BusinessException(HRErrorCodes.ShiftAssignmentInvalidDate, "Only future assignments can be cancelled.");
		await EnsureUnrecordedAsync(employee.Id, row.EffectiveFrom, row.EffectiveTo);
		var previous = rows.Where(x => x.EffectiveTo == row.EffectiveFrom).SingleOrDefault();
		await assignments.DeleteAsync(row, autoSave: true);
		if (previous != null) { previous.CloseAt(row.EffectiveTo); await assignments.UpdateAsync(previous, autoSave: true); }
	}
	public async Task InitializeAsync(Employee employee, Guid shiftId, DateOnly from)
	{
		if (await assignments.AnyAsync(x => x.EmployeeId == employee.Id)) throw new BusinessException(HRErrorCodes.WorkShiftReferenced);
		await ScheduleAsync(employee, shiftId, from, employee.ConcurrencyStamp);
	}
	private async Task LockEmployeeAsync(Employee employee, string stamp)
	{
		await payrollFence.LockAsync();
		await fence.LockAsync(employee.Id);
		if (employee.TenantId != CurrentTenant.Id || !CurrentTenant.Id.HasValue) throw new BusinessException(HRErrorCodes.WorkShiftInvalid);
		if (string.IsNullOrWhiteSpace(stamp) || employee.ConcurrencyStamp != stamp) throw new AbpDbConcurrencyException("The work schedule changed. Refresh and try again.");
		await employees.UpdateAsync(employee, autoSave: true);
	}
	private async Task EnsureUnrecordedAsync(Guid employeeId, DateOnly from, DateOnly? to)
	{
		if (await attendance.AnyAsync(x => x.EmployeeId == employeeId && x.TenantId == CurrentTenant.Id && x.AttendanceDate >= from && (!to.HasValue || x.AttendanceDate < to.Value)))
			throw new BusinessException(HRErrorCodes.ShiftAssignmentProtected, "Saved attendance exists in the affected date range.");
		var locked = await payroll.GetListAsync(x => x.EmployeeId == employeeId && x.TenantId == CurrentTenant.Id && x.IsLocked);
		foreach (var row in locked)
		{
			if (!DateTime.TryParseExact(row.MonthYear, new[] { "yyyy-MM", "MM-yyyy", "yyyyMM", "MMM-yyyy" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var month))
				throw new BusinessException(HRErrorCodes.ShiftAssignmentProtected, "A locked payroll period could not be resolved.");
			var start = new DateOnly(month.Year, month.Month, 1);
			if (start.AddMonths(1) > from && (!to.HasValue || start < to.Value))
				throw new BusinessException(HRErrorCodes.ShiftAssignmentProtected, "Locked payroll exists in the affected date range.");
		}
	}
}