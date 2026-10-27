using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

[RemoteService(false)]
public class LeavePolicyImpactPopulationReader(
	IRepository<Employee, Guid> employees,
	IRepository<HrPolicyAssignment, Guid> assignments,
	LeavePolicyImpactResolutionAdapter resolver) : ApplicationService, ITransientDependency
{
	public async Task<LeaveImpactPopulation> ReadAsync(LeavePolicyImpactRequest input,
		IReadOnlyList<LeavePolicy> policies, DateTime from, DateTime to)
	{
		var employeeQuery = (await employees.GetQueryableAsync()).Where(x => x.TenantId == CurrentTenant.Id &&
			(!x.EmployeeStatusType.HasValue || x.EmployeeStatusType == EmployeeStatusType.Active));
		if (await AsyncExecuter.CountAsync(employeeQuery) > 10000)
			throw new BusinessException(HRErrorCodes.ReportPopulationTooLarge);
		var staff = await AsyncExecuter.ToListAsync(employeeQuery.OrderBy(x => x.Id));
		var boundaryRows = await AsyncExecuter.ToListAsync((await assignments.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == input.AcademicYearId && x.IsActive &&
				x.EffectiveFrom <= to && (!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= from))
			.OrderBy(x => x.Id)
			.Select(x => new { x.Id, x.EffectiveFrom, x.EffectiveTo, x.ConcurrencyStamp }));
		var dates = boundaryRows.Select(x => x.EffectiveFrom.Date)
			.Concat(boundaryRows.Where(x => x.EffectiveTo.HasValue && x.EffectiveTo.Value.Date < to)
				.Select(x => x.EffectiveTo!.Value.Date.AddDays(1)))
			.Append(from).Append(to).Append(input.AsOfDate.Date).Where(x => x >= from && x <= to).Distinct().Order().ToList();
		var targetId = input.PolicyId ?? Guid.Empty;
		var target = policies.FirstOrDefault(x => x.Id == targetId);
		var proposal = input.Proposal;
		var proposedStatus = input.Action == "archive" ? HrPolicyStatus.Archived :
			proposal != null ? LeavePolicyProjection.ParseStatus(proposal.Status) : (HrPolicyStatus?)null;
		var promotesDefault = input.Action == "set-default" || proposal?.IsDefault == true ||
			(input.Action == "create" && proposedStatus == HrPolicyStatus.Active && !policies.Any(x => x.IsDefault && x.IsActive));
		var proposedDefault = promotesDefault ? targetId : (Guid?)null;
		var configurationChanged = proposal != null && (target == null || OperationalConfiguration(LeavePolicySnapshots.Capture(target)) != OperationalConfiguration(proposal));
		var rows = new List<LeaveImpactStaffRow>();
		var complete = true;
		using var batch = resolver.BeginReadBatch();
		foreach (var employee in staff)
		{
			LeaveImpactResolution? atDate = null;
			LeaveImpactResolution? afterDate = null;
			var changed = false;
			foreach (var date in dates)
			{
				var before = await resolver.ResolveAsync(employee, input.AcademicYearId, date, policies);
				var after = await resolver.ResolveAsync(employee, input.AcademicYearId, date, policies, proposedDefault, targetId, proposedStatus);
				complete &= before.Usable && after.Usable;
				changed |= before != after || (configurationChanged && (before.PolicyId == targetId || after.PolicyId == targetId));
				if (date == input.AsOfDate.Date) { atDate = before; afterDate = after; }
			}
			rows.Add(new(employee.Id, employee.EmployeeCode, employee.EmployeeName,
				atDate!, afterDate!, changed));
		}
		return new(complete, rows, boundaryRows.Select(x => $"{x.Id}:{x.ConcurrencyStamp}").ToList());
	}

	private static string OperationalConfiguration(LeavePolicyCreateRequest input) => System.Text.Json.JsonSerializer.Serialize(new
	{
		input.Status, input.EffectiveFrom, input.EffectiveTo, Rules = input.Rules.Where(x => x.IsActive).OrderBy(x => x.PaidType)
	});
}

public sealed record LeaveImpactStaffRow(Guid EmployeeId, string EmployeeCode, string EmployeeName,
	LeaveImpactResolution Current, LeaveImpactResolution Proposed, bool ChangesInWindow);
public sealed record LeaveImpactPopulation(bool Complete, List<LeaveImpactStaffRow> Staff, List<string> AssignmentDependencies);
