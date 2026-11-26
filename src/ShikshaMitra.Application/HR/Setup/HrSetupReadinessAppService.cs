using ShikshaMitra.HR.WorkShifts;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using H = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR;

namespace ShikshaMitra.HR.Setup;

[Authorize, RemoteService(false)]
public class HrSetupReadinessAppService(
	IPermissionChecker permissions, IIdentityUserRepository users, IIdentityRoleRepository roles,
	IRepository<AcademicYear, Guid> years, IRepository<EmployeeWorkShift, Guid> shifts,
	HrSetupPolicyReadinessReader policies, HrSetupPayrollReadinessReader payroll,
	IHrSetupReadinessStore store) : ApplicationService, IHrSetupReadinessAppService
{
	public async Task<HrSetupReadinessDto> GetAsync(Guid? academicYearId = null)
	{
		if (CurrentTenant.Id is not Guid tenantId || !await CanCheckAsync(tenantId))
			throw new AbpAuthorizationException();

		var checkedAt = DateTime.SpecifyKind(Clock.Now, DateTimeKind.Utc);
		var revision = academicYearId.HasValue ? await store.ReadRevisionAsync() : Guid.Empty;
		var yearRows = await years.GetListAsync(x => x.TenantId == tenantId);
		var year = academicYearId.HasValue ? yearRows.SingleOrDefault(x => x.Id == academicYearId.Value) : null;
		if (academicYearId.HasValue && year == null) throw new EntityNotFoundException(typeof(AcademicYear), academicYearId.Value);
		var result = new HrSetupReadinessDto
		{
			AcademicYearId = year?.Id,
			AvailableYears = yearRows.OrderByDescending(x => x.StartDate).Select(x => new HrSetupYearDto
			{ Id = x.Id, Name = x.Name, StartDate = x.StartDate, EndDate = x.EndDate }).ToList()
		};
		result.Checks.Add(new()
		{
			Code = "academic-year",
			Step = "policies",
			State = year == null ? "missing" : "ready",
			Reason = year != null ? "Academic year selected." : yearRows.Count == 0 ? "An administrator must create an academic year in Institute Setup." : "Select an academic year to check its policies.",
			CanConfigure = yearRows.Count > 0
		});
		var shiftRows = await shifts.GetListAsync(x => x.TenantId == tenantId);
		var usableShift = shiftRows.Any(x => x.IsActive && !string.IsNullOrWhiteSpace(x.ShiftName) && x.EndTime > x.StartTime);
		result.Checks.Add(new()
		{
			Code = "shift",
			Step = "staff",
			State = usableShift ? "ready" : shiftRows.Count == 0 ? "missing" : "invalid",
			Reason = usableShift ? "A usable staff shift is available." : "Create a staff shift with valid start and end times.",
			CanConfigure = await permissions.IsGrantedAsync(H.AttendanceAndLeave_Setting_ShiftManagement.Add)
		});
		var employeeRoles = (await roles.GetListAsync()).Where(x => x.TenantId == tenantId && EmployeeRoleEligibility.IsOnboardingAssignable(x)).ToList();
		foreach (var group in new[] { EmployeeGroupType.Teaching, EmployeeGroupType.NonTeaching })
		{
			var covered = employeeRoles.Any(x => EmployeeGroupAudience.IsAssignableTo(x.GetProperty<string>(EmployeeGroupAudience.ExtraPropertyName), group));
			var name = group == EmployeeGroupType.Teaching ? "Teaching" : "Non Teaching";
			result.Checks.Add(new()
			{
				Code = group == EmployeeGroupType.Teaching ? "teaching-role" : "non-teaching-role",
				Step = "staff",
				State = covered ? "ready" : "missing",
				Reason = covered ? $"An approved {name} employee role is available." : $"Approve an Employee-audience role for {name} onboarding in Role Management.",
				CanConfigure = await permissions.IsGrantedAsync(IdentityPermissions.Roles.Default) && await permissions.IsGrantedAsync(IdentityPermissions.Roles.Update)
			});
		}
		result.Checks.AddRange(await policies.ReadAsync(year, tenantId));
		result.Checks.AddRange(await payroll.ReadAsync(tenantId, checkedAt));
		result.IsReady = result.Checks.All(x => x.State == "ready");
		if (year != null)
			await store.RecordAsync(year.Id, revision, result.IsReady, checkedAt, ValidUntil(checkedAt));
		return result;
	}

	public async Task<bool?> GetSavedStatusAsync(Guid academicYearId)
	{
		if (CurrentTenant.Id is not Guid tenantId || !await CanCheckAsync(tenantId)) return null;
		return await store.ReadStatusAsync(academicYearId, DateTime.SpecifyKind(Clock.Now, DateTimeKind.Utc));
	}

	private async Task<bool> CanCheckAsync(Guid tenantId)
	{
		if (CurrentUser.Id is not Guid userId) return false;
		var actor = await users.FindAsync(userId);
		if (actor == null || actor.TenantId != tenantId || !actor.IsActive) return false;
		foreach (var action in HrSetupPermissions.Operators)
			if (await permissions.IsGrantedAsync(action)) return true;
		return false;
	}

	private static DateTime ValidUntil(DateTime checkedAt)
	{
		// Existing checks use UTC months for statutory rules and Indian months for PF coverage.
		var schoolZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
		var schoolTime = TimeZoneInfo.ConvertTimeFromUtc(checkedAt, schoolZone);
		var nextSchoolMonth = TimeZoneInfo.ConvertTimeToUtc(
			new DateTime(schoolTime.Year, schoolTime.Month, 1).AddMonths(1), schoolZone);
		var nextUtcMonth = new DateTime(checkedAt.Year, checkedAt.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
		return nextSchoolMonth < nextUtcMonth ? nextSchoolMonth : nextUtcMonth;
	}
}
