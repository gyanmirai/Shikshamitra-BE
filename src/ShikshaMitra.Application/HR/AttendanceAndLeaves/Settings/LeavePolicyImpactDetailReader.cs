using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class LeavePolicyImpactDetailReader(IUserScopeService scopes, HrEmployeeScopeQueryService employeeScopes,
	LeaveActorAccessService leaveActors) : ApplicationService, ITransientDependency
{
	public async Task<LeavePolicyImpactPageDto> ReadAsync(LeavePolicyImpactAssessment assessment, LeavePolicyImpactDetailsRequest input)
	{
		var impact = assessment.Preview.LeaveImpact!;
		if (!impact.Measures.TryGetValue(input.Metric, out var measure) || measure.Status != "calculated" || !measure.Count.HasValue)
			throw new BusinessException(HRErrorCodes.HrPolicyImpactBlocking);
		var rows = new List<LeavePolicyImpactRowDto>();
		HashSet<Guid>? accessible;
		bool restricted = false;
		if (input.Metric is "effectiveStaff" or "changedStaff")
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View);
			accessible = await AccessibleEmployeesAsync(assessment.Preview.AcademicYearId);
			rows = assessment.Population.Staff.Where(x => accessible.Contains(x.EmployeeId) && (input.Metric == "effectiveStaff"
				? x.Current.PolicyId == assessment.Preview.PolicyId && x.Current.Usable : x.ChangesInWindow))
				.Select(x => new LeavePolicyImpactRowDto { Id = x.EmployeeId, EmployeeId = x.EmployeeId,
					Label = $"{x.EmployeeCode} {x.EmployeeName}".Trim(), CurrentPolicyId = x.Current.PolicyId,
					ProposedPolicyId = x.Proposed.PolicyId, CurrentSource = x.Current.Source, ProposedSource = x.Proposed.Source,
					Status = x.ChangesInWindow ? "configuration_changes" : "resolved" }).ToList();
		}
		else if (input.Metric.EndsWith("AssignmentReferences", StringComparison.Ordinal) || input.Metric == "assignmentReferences")
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.Policies.View);
			accessible = await AccessibleEmployeesAsync(assessment.Preview.AcademicYearId);
			var date = impact.AsOfDate;
			rows = assessment.References.Assignments.Where(x => input.Metric switch
			{
				"assignmentReferences" => true,
				"currentAssignmentReferences" => x.IsActive && x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= date),
				"futureAssignmentReferences" => x.IsActive && x.EffectiveFrom > date,
				"historicalAssignmentReferences" => !x.IsActive || x.EffectiveTo < date,
				_ => false
			}).Where(x => x.TargetType != Policies.HrPolicyAssignmentTargetType.Employee ||
				Guid.TryParse(x.TargetKey, out var id) && accessible.Contains(id))
				.Select(x => new LeavePolicyImpactRowDto { Id = x.Id, Label = $"{x.TargetType} assignment", Status = x.IsActive ? "active" : "inactive",
					FromDate = x.EffectiveFrom, ToDate = x.EffectiveTo }).ToList();
		}
		else if (input.Metric.EndsWith("RequestReferences", StringComparison.Ordinal) || input.Metric == "requestReferences")
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_LeaveCenter.View);
			var actor = await leaveActors.ResolveContextAsync();
			var ids = await leaveActors.ResolveScopedEmployeeIdsAsync(actor);
			accessible = ids?.ToHashSet();
			var from = DateOnly.FromDateTime(impact.FromDate);
			var to = DateOnly.FromDateTime(impact.ToDate);
			rows = assessment.References.Requests.Where(x => (accessible == null || accessible.Contains(x.EmployeeId)) && (input.Metric switch
			{
				"requestReferences" => true,
				"historicalRequestReferences" => x.EndDateOnly < from,
				"futureRequestReferences" => x.StartDateOnly > to,
				"openRequestReferences" => (x.Status is EmployeeLeaveStatusType.Pending or EmployeeLeaveStatusType.Reviewed) && x.StartDateOnly <= to && x.EndDateOnly >= from,
				"pendingRequestReferences" => x.Status == EmployeeLeaveStatusType.Pending && x.StartDateOnly <= to && x.EndDateOnly >= from,
				"reviewedRequestReferences" => x.Status == EmployeeLeaveStatusType.Reviewed && x.StartDateOnly <= to && x.EndDateOnly >= from,
				"approvedRequestReferences" => x.Status == EmployeeLeaveStatusType.Approved && x.StartDateOnly <= to && x.EndDateOnly >= from,
				_ => false
			})).Select(x => new LeavePolicyImpactRowDto { Id = x.Id, EmployeeId = x.EmployeeId, Label = $"Request {x.PublicId}",
				Status = x.Status.ToString(), CurrentPolicyId = x.LeavePolicyId,
				FromDate = x.StartDateOnly.ToDateTime(TimeOnly.MinValue), ToDate = x.EndDateOnly.ToDateTime(TimeOnly.MinValue) }).ToList();
		}
		else if (input.Metric == "settlementReferences")
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.LeaveManagement_SettingLeavePolicy.View);
			accessible = await AccessibleEmployeesAsync(assessment.Preview.AcademicYearId);
			rows = assessment.References.Settlements.Where(x => accessible.Contains(x.EmployeeId)).Select(x => new LeavePolicyImpactRowDto
				{ Id = x.Id, EmployeeId = x.EmployeeId, Label = "Pinned settlement", Status = x.Status.ToString(), Period = x.PayrollMonth }).ToList();
		}
		else if (input.Metric == "payrollExposure")
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.View);
			accessible = await AccessibleEmployeesAsync(assessment.Preview.AcademicYearId);
			restricted = assessment.References.PayrollSnapshots.Any(x => !accessible.Contains(x.EmployeeId)) ||
				assessment.References.Settlements.Any(x => !accessible.Contains(x.EmployeeId));
			foreach (var period in impact.PayrollPeriods)
			{
				var snapshots = assessment.References.PayrollSnapshots.Where(x => x.MonthYear == period.Month && accessible.Contains(x.EmployeeId)).ToList();
				var settlements = assessment.References.Settlements.Where(x => x.PayrollMonth == period.Month && accessible.Contains(x.EmployeeId)).ToList();
				if (snapshots.Count == 0 && settlements.Count == 0) continue;
				var locked = snapshots.Count(x => x.IsLocked);
				rows.Add(new() { Id = snapshots.FirstOrDefault()?.Id ?? settlements[0].Id, Label = period.Month, Period = period.Month,
					Status = snapshots.Count == 0 ? "no_snapshot" : locked == 0 ? "unlocked" : locked == snapshots.Count ? "locked" : "mixed",
					IsLocked = snapshots.Count == 0 || locked > 0 && locked < snapshots.Count ? null : locked > 0 });
			}
		}
		else throw new BusinessException(HRErrorCodes.HrPolicyInvalidType);
		return new() { AssessedTotalCount = measure.Count.Value, TotalCount = rows.Count,
			RedactedCount = Math.Max(0, measure.Count.Value - rows.Count), IsScopeRestricted = restricted || rows.Count != measure.Count.Value,
			Items = rows.OrderBy(x => x.Period, StringComparer.Ordinal).ThenBy(x => x.Id).Skip(input.SkipCount).Take(input.MaxResultCount).ToList() };
	}

	private async Task<HashSet<Guid>> AccessibleEmployeesAsync(Guid year)
	{
		var scope = await scopes.GetUserScopeAsync(CurrentUser.Id ?? throw new BusinessException(HRErrorCodes.LeaveUserNotAuthenticated));
		var query = await employeeScopes.BuildScopedEmployeeQueryAsync(scope,
			new() { AcademicYearId = year, IncludeUnassignedEmployeesWhenAcademicYearScoped = true });
		return (await AsyncExecuter.ToListAsync(query.Select(x => x.Id))).ToHashSet();
	}
}
