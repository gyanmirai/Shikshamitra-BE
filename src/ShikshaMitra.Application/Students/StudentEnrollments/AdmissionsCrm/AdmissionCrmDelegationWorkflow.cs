using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDelegationWorkflow(
	IRepository<AdmissionCrmWorkDelegation, Guid> delegationRepository,
	AdmissionCrmGovernanceAccess access)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmWorkDelegationDto> CreateAsync(Guid academicYearId, CreateAdmissionCrmWorkDelegationDto input)
	{
		await access.EnsureAcademicYearAccessAsync(academicYearId, "CreateDelegationAsync");
		if (input.QueueTypes.Count == 0 || input.QueueTypes.Any(x => !Enum.IsDefined(x)))
			throw new BusinessException("AdmissionCrm:DelegationQueuesRequired");
		var entity = new AdmissionCrmWorkDelegation(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
			input.DelegatorUserId, input.DelegateUserId, AdmissionCrmGovernanceMapper.Serialize(input.QueueTypes.Distinct().ToList()),
			input.StartsAt, input.EndsAt, input.Reason, Clock.Now);
		await delegationRepository.InsertAsync(entity, autoSave: true);
		return AdmissionCrmGovernanceMapper.Delegation(entity);
	}

	public async Task<IReadOnlyList<AdmissionCrmWorkDelegationDto>> GetAsync(Guid academicYearId)
	{
		await access.EnsureAcademicYearAccessAsync(academicYearId, "GetDelegationsAsync");
		var queryable = await delegationRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		foreach (var item in items) item.Refresh(Clock.Now);
		await delegationRepository.UpdateManyAsync(items, autoSave: true);
		return items.OrderByDescending(x => x.StartsAt).Select(AdmissionCrmGovernanceMapper.Delegation).ToList();
	}

	public async Task<AdmissionCrmWorkDelegationDto> CancelAsync(Guid id)
	{
		var entity = await delegationRepository.GetAsync(id);
		await access.EnsureAcademicYearAccessAsync(entity.AcademicYearId, "CancelDelegationAsync");
		entity.Cancel();
		await delegationRepository.UpdateAsync(entity, autoSave: true);
		return AdmissionCrmGovernanceMapper.Delegation(entity);
	}
}
