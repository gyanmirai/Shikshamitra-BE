using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Import;

public class AttendanceImportScopeReader(
	TakeAttendanceAccessService attendanceAccess,
	HrEmployeeScopeQueryService employeeScope,
	HrMediumStageScopeReader stageScope,
	IAcademicYearRepository academicYears,
	IEmployeeRepository employees,
	IEmployeeAttendanceRepository attendance,
	ICurrentTenant tenant,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	internal async Task<UserScopeContext> GetScopeAsync()
	{
		var scope = await attendanceAccess.EnsureCanOperateAttendanceAsync();
		if (!tenant.Id.HasValue || scope.TenantId != tenant.Id)
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);

		return scope;
	}

	internal async Task<Dictionary<(Guid EmployeeId, DateOnly Date), AttendanceImportTarget>> ResolveAsync(
		UserScopeContext scope,
		IReadOnlyCollection<(Guid EmployeeId, DateOnly Date)> requested)
	{
		if (requested.Count == 0)
			return [];

		var employeeIds = requested.Select(x => x.EmployeeId).Distinct().ToList();
		var currentEmployees = (await asyncExecuter.ToListAsync((await employees.GetQueryableAsync())
			.Where(x => x.TenantId == tenant.Id && employeeIds.Contains(x.Id))
			.Select(x => x.Id))).ToHashSet();
		var first = requested.Min(x => x.Date);
		var last = requested.Max(x => x.Date);
		var existingRows = await asyncExecuter.ToListAsync((await attendance.GetQueryableAsync())
			.Where(x => x.TenantId == tenant.Id && employeeIds.Contains(x.EmployeeId)
				&& x.AttendanceDate >= first && x.AttendanceDate <= last)
			.Select(x => new { x.EmployeeId, x.AttendanceDate, x.EducationMediumStageId }));
		var existing = existingRows.ToDictionary(x => (x.EmployeeId, x.AttendanceDate), x => x.EducationMediumStageId);
		var candidates = requested.Where(x => currentEmployees.Contains(x.EmployeeId));
		if (HrUserScopeAccess.HasHrWideAccess(scope))
			return candidates.ToDictionary(x => x, x => new AttendanceImportTarget(existing.ContainsKey(x), existing.GetValueOrDefault(x)));

		var assignments = await employeeScope.BuildEmployeeStageLookupAsync(currentEmployees);
		var accessibleStages = (await stageScope.GetAllAsync())
			.Where(x => scope.CanAccessMediumStage(x.StageId, x.MediumId, x.BoardId, x.AcademicYearId))
			.ToDictionary(x => x.StageId, x => x.AcademicYearId);
		var yearIds = accessibleStages.Values.Distinct().ToList();
		var years = (await asyncExecuter.ToListAsync((await academicYears.GetQueryableAsync())
			.Where(x => x.TenantId == tenant.Id && yearIds.Contains(x.Id))
			.Select(x => new { x.Id, x.StartDate, x.EndDate })))
			.ToDictionary(x => x.Id, x => (Start: DateOnly.FromDateTime(x.StartDate), End: DateOnly.FromDateTime(x.EndDate)));

		bool CanUseStage(Guid id, DateOnly date) => accessibleStages.TryGetValue(id, out var yearId)
			&& years.TryGetValue(yearId, out var year) && date >= year.Start && date <= year.End;

		var result = new Dictionary<(Guid EmployeeId, DateOnly Date), AttendanceImportTarget>();
		foreach (var target in candidates)
		{
			if (!assignments.TryGetValue(target.EmployeeId, out var assignedStages))
				continue;

			var assignedStage = assignedStages.OrderBy(x => x).FirstOrDefault(id => CanUseStage(id, target.Date));
			var exists = existing.TryGetValue(target, out var recordedStage);
			if (assignedStage == Guid.Empty || (recordedStage.HasValue && !CanUseStage(recordedStage.Value, target.Date)))
				continue;

			result.Add(target, new AttendanceImportTarget(exists, recordedStage ?? assignedStage));
		}

		return result;
	}
}

internal sealed record AttendanceImportTarget(bool Exists, Guid? StageId);
