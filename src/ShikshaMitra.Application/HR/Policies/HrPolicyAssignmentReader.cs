using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
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
		if (!string.IsNullOrWhiteSpace(input.TargetType))
		{
			var targetType = HrPolicyMapping.ParseAssignmentTargetType(input.TargetType);
			query = query.Where(x => x.TargetType == targetType);
		}
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			query = query.Where(x => x.TargetKey.Contains(search) || (x.TargetDisplayName != null && x.TargetDisplayName.ToLower().Contains(search)));
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

		return new PagedResultDto<HrPolicyAssignmentDto>(total, dtos);
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
