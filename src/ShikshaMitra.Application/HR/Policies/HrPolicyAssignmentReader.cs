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
			.ThenBy(x => x.EffectiveFrom).ThenBy(x => x.Id)
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

	public async Task<PolicyAssignmentResolutionPreviewDto> PreviewResolutionAsync(PolicyAssignmentResolutionPreviewRequestDto input)
	{
		var proposal = input.Assignment;
		var targetType = HrPolicyMapping.ParseAssignmentTargetType(proposal.TargetType);
		var validator = LazyServiceProvider.LazyGetRequiredService<HrPolicyAssignmentTargetValidator>();
		await validator.ValidateTargetAsync(targetType, proposal.TargetKey, proposal.AcademicYearId);
		await validator.ValidateReferencesAsync(proposal);
		if (input.AssignmentId.HasValue)
		{
			var original = await repository.GetAsync(input.AssignmentId.Value);
			if (original.AcademicYearId != proposal.AcademicYearId)
				throw new BusinessException(HRErrorCodes.HrPolicyAssignmentAcademicYearMismatch);
		}
		Guid? employeeId = targetType == HrPolicyAssignmentTargetType.Employee ? Guid.Parse(proposal.TargetKey) : input.SampleEmployeeId;
		if (targetType == HrPolicyAssignmentTargetType.Employee && input.SampleEmployeeId.HasValue && input.SampleEmployeeId != employeeId)
			throw new BusinessException(HRErrorCodes.HrPolicyAssignmentTargetInvalid);
		if (employeeId.HasValue)
			await LazyServiceProvider.LazyGetRequiredService<IEmployeeAuthorizationService>().CheckEmployeeScopeAccessAsync(employeeId.Value);
		Guid? groupId = targetType == HrPolicyAssignmentTargetType.Group ? Guid.Parse(proposal.TargetKey) : null;
		var current = await policyResolver.ResolvePreviewAsync(employeeId, groupId, input.Date, proposal.AcademicYearId);
		var draft = new HrPolicyAssignment(input.AssignmentId ?? Guid.Empty, CurrentTenant.Id, proposal.AcademicYearId,
			targetType, proposal.TargetKey, proposal.TargetDisplayName, proposal.AttendancePolicyId,
			proposal.PayrollPolicyId, proposal.LeavePolicyId, proposal.EffectiveFrom, proposal.EffectiveTo,
			proposal.IsActive, proposal.Priority, proposal.AssignmentReason);
		var proposed = await policyResolver.ResolvePreviewAsync(employeeId, groupId, input.Date, proposal.AcademicYearId,
			proposedAssignment: draft, replacedAssignmentId: input.AssignmentId);
		// New IDs are allocated only by the real write. Never pretend a temporary
		// ID can resolve the final deterministic-ID tie breaker.
		var draftStep = proposed.ResolutionSteps.FirstOrDefault(x => x.AssignmentId == draft.Id);
		var unresolvedTie = !input.AssignmentId.HasValue && draftStep != null &&
			(draftStep.SkipReason == "deterministic_tie_breaker" ||
			 (draftStep.Status == "matched" && proposed.ResolutionSteps.Any(x => x.SkipReason == "deterministic_tie_breaker")));
		var dto = new PolicyAssignmentResolutionPreviewDto
		{
			Current = ToEffectivePoliciesDto(current),
			Proposed = unresolvedTie ? null : ToEffectivePoliciesDto(proposed),
			IsComplete = !unresolvedTie,
			Scope = employeeId.HasValue ? "selected_staff" : "selected_group_only",
			UnavailableReason = unresolvedTie ? "The new assignment ties on scope, priority and start date. Its final ID is not allocated; choose a different priority or start date and preview again." : null
		};
		await assignmentEnricher.EnrichEffectiveTargetDisplayNamesAsync(dto.Current);
		if (dto.Proposed != null) await assignmentEnricher.EnrichEffectiveTargetDisplayNamesAsync(dto.Proposed);
		return dto;
	}
}