using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Settings;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.WorkShifts;

public class EmployeeShiftResolver(IRepository<EmployeeShiftAssignment, Guid> assignments, ISettingProvider settings, IClock clock) : DomainService
{
	public async Task<DateOnly> TodayAsync()
	{
		var zone = await settings.GetOrNullAsync("ShikshaMitra.HR.Reporting.TimeZone") ?? "Asia/Kolkata";
		try { return ToSchoolDate(clock.Now, zone); }
		catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
		{ throw new BusinessException(HRErrorCodes.WorkShiftInvalid, "The school's configured timezone is invalid."); }
	}
	internal static DateOnly ToSchoolDate(DateTime timestamp, string zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timestamp, TimeZoneInfo.FindSystemTimeZoneById(zone)));

	public async Task<List<EmployeeShiftAssignment>> LoadAsync(IEnumerable<Guid> employeeIds, DateOnly from, DateOnly to)
	{
		var ids = employeeIds.Distinct().ToArray();
		return ids.Length == 0 ? [] : await assignments.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, x.EmployeeId) && x.EffectiveFrom <= to && (!x.EffectiveTo.HasValue || x.EffectiveTo > from));
	}
	public static EmployeeWorkShift Snapshot(EmployeeShiftAssignment x) => new(x.WorkShiftId, x.TenantId, x.ShiftName, x.StartTime, x.EndTime);
	public async Task<EmployeeWorkShift?> FindShiftAsync(Guid employeeId, DateOnly date)
	{ var row = await FindAsync(employeeId, date); return row == null ? null : Snapshot(row); }
	public async Task<System.Collections.Generic.Dictionary<Guid, EmployeeWorkShift>> ForDateAsync(IEnumerable<Guid> employeeIds, DateOnly date) =>
	 (await LoadAsync(employeeIds, date, date)).ToDictionary(x => x.EmployeeId, Snapshot);
	public async Task<EmployeeShiftAssignment?> FindAsync(Guid employeeId, DateOnly date) =>
		   (await LoadAsync([employeeId], date, date)).SingleOrDefault();
	public async Task<EmployeeShiftAssignment> RequireAsync(Guid employeeId, DateOnly date) =>
		await FindAsync(employeeId, date) ?? throw new BusinessException(HRErrorCodes.ShiftAssignmentMissing, "No employee work shift is assigned for this date.");
}