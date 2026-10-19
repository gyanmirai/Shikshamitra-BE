using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using static ShikshaMitra.HR.Policies.HrPolicyAssignmentProjection;
using Volo.Abp;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class HrPolicyAssignmentReader(
	IRepository<HrPolicyAssignment, Guid> repository,
	HrPolicyResolver policyResolver,
	HrPolicyAssignmentEnricher assignmentEnricher)
	: ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<HrPolicyAssignmentDto>> GetListAsync(PolicyListRequestDto input)
	{
		var query = await repository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == input.AcademicYearId);
		if (!string.IsNullOrWhiteSpace(input.TargetType) && input.TargetType != "all")
		{
			var targetType = HrPolicyMapping.ParseAssignmentTargetType(input.TargetType);
			query = query.Where(x => x.TargetType == targetType);
		}
		query = query.WhereIf(input.IsActive.HasValue, x => x.IsActive == input.IsActive)
			.WhereIf(input.PayrollPolicyId.HasValue, x => x.PayrollPolicyId == input.PayrollPolicyId)
			.WhereIf(input.AttendancePolicyId.HasValue, x => x.AttendancePolicyId == input.AttendancePolicyId)
			.WhereIf(input.ActiveOnDate.HasValue, x => x.EffectiveFrom <= input.ActiveOnDate && (!x.EffectiveTo.HasValue || x.EffectiveTo >= input.ActiveOnDate))
			.WhereIf(input.EffectiveFrom.HasValue, x => !x.EffectiveTo.HasValue || x.EffectiveTo >= input.EffectiveFrom)
			.WhereIf(input.EffectiveTo.HasValue, x => x.EffectiveFrom <= input.EffectiveTo);
		if (input.LeavePolicyId.HasValue)
		{
			var leaveQuery = await LazyServiceProvider.LazyGetRequiredService<IRepository<LeavePolicy, Guid>>().GetQueryableAsync();
			var isDefault = await AsyncExecuter.AnyAsync(leaveQuery.Where(x => x.Id == input.LeavePolicyId && x.IsDefault && x.AcademicYearId == input.AcademicYearId));
			query = query.Where(x => x.LeavePolicyId == input.LeavePolicyId || (isDefault && !x.LeavePolicyId.HasValue));
		}
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			query = query.Where(x => x.TargetKey.ToLower().Contains(search) || (x.TargetDisplayName != null && x.TargetDisplayName.ToLower().Contains(search)) || (x.AssignmentReason != null && x.AssignmentReason.ToLower().Contains(search)));
		}
		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query
			.OrderByDescending(x => x.IsActive)
			.ThenBy(x => x.TargetType)
			.ThenBy(x => x.TargetKey)
			.ThenBy(x => x.EffectiveFrom)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));

		var dtos = rows.Select(HrPolicyMapping.ToDto).ToList();
		await assignmentEnricher.EnrichTargetOrganizationsAsync(dtos);
		await EnrichPolicyNamesAsync(dtos, input.AcademicYearId);

		return new PagedResultDto<HrPolicyAssignmentDto>(total, dtos);
	}
	private async Task EnrichPolicyNamesAsync(System.Collections.Generic.List<HrPolicyAssignmentDto> rows, Guid year)
	{
		var payrollIds = rows.Select(x => x.PayrollPolicyId).Distinct().ToList();
		var attendanceIds = rows.Select(x => x.AttendancePolicyId).Distinct().ToList();
		var leaveIds = rows.Where(x => x.LeavePolicyId.HasValue).Select(x => x.LeavePolicyId!.Value).Distinct().ToList();
		var payroll = (await AsyncExecuter.ToListAsync((await LazyServiceProvider.LazyGetRequiredService<IRepository<PayrollPolicy, Guid>>().GetQueryableAsync()).Where(x => payrollIds.Contains(x.Id)))).ToDictionary(x => x.Id, x => x.Name);
		var attendance = (await AsyncExecuter.ToListAsync((await LazyServiceProvider.LazyGetRequiredService<IRepository<AttendancePolicy, Guid>>().GetQueryableAsync()).Where(x => attendanceIds.Contains(x.Id)))).ToDictionary(x => x.Id, x => x.Name);
		var leave = await AsyncExecuter.ToListAsync((await LazyServiceProvider.LazyGetRequiredService<IRepository<LeavePolicy, Guid>>().GetQueryableAsync()).Where(x => x.AcademicYearId == year && (leaveIds.Contains(x.Id) || x.IsDefault)));
		foreach (var row in rows)
		{
			row.PayrollPolicyName = payroll.GetValueOrDefault(row.PayrollPolicyId);
			row.AttendancePolicyName = attendance.GetValueOrDefault(row.AttendancePolicyId);
			row.LeavePolicyName = row.LeavePolicyId.HasValue ? leave.FirstOrDefault(x => x.Id == row.LeavePolicyId)?.PolicyName : leave.FirstOrDefault(x => x.IsDefault)?.PolicyName is string defaultName ? defaultName + " (default)" : null;
		}
	}

	public async Task<HrPolicyAssignmentDto> GetAsync(Guid id)
	{
		var dto = HrPolicyMapping.ToDto(await repository.GetAsync(id));
		await assignmentEnricher.EnrichTargetOrganizationsAsync([dto]);
		return dto;
	}

	public async Task<EffectiveEmployeePoliciesDto> GetEffectivePoliciesAsync(Guid employeeId, DateTime date)
	{
		var resolved = await policyResolver.ResolveAsync(employeeId, date);
		var dto = ToEffectivePoliciesDto(resolved);
		await assignmentEnricher.EnrichEffectiveTargetDisplayNamesAsync(dto);
		return dto;
	}

	public async Task<EffectiveEmployeePoliciesDto> GetPreviewAsync(PolicyAssignmentPreviewRequestDto input)
	{
		var resolved = await policyResolver.ResolvePreviewAsync(
			input.EmployeeId,
			input.GroupId,
			input.Date,
			input.AcademicYearId);

		var dto = ToEffectivePoliciesDto(resolved);
		await assignmentEnricher.EnrichEffectiveTargetDisplayNamesAsync(dto);
		return dto;
	}
}
