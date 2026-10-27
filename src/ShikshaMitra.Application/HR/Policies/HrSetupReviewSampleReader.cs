using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class HrSetupReviewSampleReader(
	IUserScopeService scopes,
	HrEmployeeScopeQueryService employeeScopes,
	IRepository<Employee, Guid> employees,
	IRepository<HrPolicyAssignment, Guid> assignments,
	HrPolicyResolver resolver,
	HrSetupReviewConfigurationReader configurationReader) : ApplicationService, ITransientDependency
{
	public async Task<HrSetupReviewDto> ReadAsync(AcademicYear year, HrSetupReviewQuery input, bool recordedSample = false)
	{
		var scope = await scopes.GetUserScopeAsync(CurrentUser.Id!.Value);
		var result = new HrSetupReviewDto { AcademicYearId = year.Id, Section = HrSetupReviewSections.Sample };
		var date = (input.Date ?? year.StartDate).Date;
		result.Date = date;
		if (date < year.StartDate.Date || date > year.EndDate.Date)
		{
			if (!recordedSample) throw new BusinessException(HrSetupReviewSections.Invalid);
			result.ConfigurationState = "deferred";
			result.Notices.Add("The recorded sample date is outside the updated academic year. Run a new sample check.");
			return result;
		}
		// An empty school is different from an empty authorized staff scope.
		if (!await employees.AnyAsync(x => x.TenantId == CurrentTenant.Id))
		{
			if (input.EmployeeId.HasValue && !recordedSample) throw new AbpAuthorizationException();
			result.ConfigurationState = "no_staff";
			result.Notices.Add("No staff exists yet. The sample check is not applicable yet; complete it after adding staff.");
			return result;
		}
		if (!HrUserScopeAccess.HasHrWideAccess(scope) && !HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
		{
			if (input.EmployeeId.HasValue && !recordedSample) throw new AbpAuthorizationException();
			result.ConfigurationState = "deferred";
			result.Notices.Add("The sample check is deferred until staff in your authorized scope is available.");
			return result;
		}
		var query = await employeeScopes.BuildScopedEmployeeQueryAsync(scope, new HrEmployeeScopeOptions {
			AcademicYearId = year.Id, IncludeUnassignedEmployeesWhenAcademicYearScoped = true });
		query = query.Where(x => x.TenantId == CurrentTenant.Id);
		var employee = input.EmployeeId.HasValue
			? await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.Id == input.EmployeeId))
			: await AsyncExecuter.FirstOrDefaultAsync(query.OrderBy(x => x.Id));
		if (employee == null)
		{
			if (input.EmployeeId.HasValue && !recordedSample) throw new AbpAuthorizationException();
			result.ConfigurationState = "deferred";
			result.Notices.Add("No staff in your authorized year scope is available for the sample check.");
			return result;
		}
		result.EmployeeId = employee.Id;
		var fingerprints = new List<string>();
		var assignmentRows = await assignments.GetListAsync(x => x.TenantId == year.TenantId && x.AcademicYearId == year.Id);
		foreach (var section in new[] { HrSetupReviewSections.Calendar, HrSetupReviewSections.Attendance, HrSetupReviewSections.Leave, HrSetupReviewSections.Payroll })
			fingerprints.Add((await configurationReader.ReadAsync(year, section)).Fingerprint);
		object resolution;
		try
		{
			var resolved = await resolver.ResolveAsync(employee.Id, date, year.Id);
			result.ConfigurationState = "valid";
			result.Details.Add($"{employee.EmployeeName} on {date:yyyy-MM-dd}: source {resolved.PolicySource}");
			result.Details.Add($"Attendance: {resolved.AttendancePolicy.Name}; payroll: {resolved.PayrollPolicy.Name}; leave: {resolved.LeavePolicy?.PolicyName}");
			result.Details.Add($"Attendance calendar: {resolved.AttendanceWorkCalendarPolicy.Name}; payroll calendar: {resolved.PayrollWorkCalendarPolicy.Name}");
			resolution = new { resolved.PolicySource, Assignment = resolved.Assignment?.Id, Payroll = resolved.PayrollPolicy.Id,
				Attendance = resolved.AttendancePolicy.Id, Leave = resolved.LeavePolicy?.Id,
				AttendanceCalendar = resolved.AttendanceWorkCalendarPolicy.Id, PayrollCalendar = resolved.PayrollWorkCalendarPolicy.Id,
				resolved.ResolutionSteps };
		}
		catch (BusinessException exception) when (exception.Code != null && exception.Code.StartsWith("ShikshaMitra:", StringComparison.Ordinal))
		{
			result.ConfigurationState = "invalid";
			result.Notices.Add("Effective policies could not be resolved for this staff member and date. Review defaults and assignments.");
			resolution = new { Error = exception.Code };
		}
		result.Fingerprint = HrSetupReviewConfigurationReader.Hash(new { Schema = 1, year.TenantId, YearId = year.Id,
			EmployeeId = employee.Id, employee.ConcurrencyStamp, Date = date, Configuration = fingerprints,
			Assignments = assignmentRows.OrderBy(x => x.Id).Select(HrPolicyMapping.ToDto), Resolution = resolution });
		return result;
	}
}
