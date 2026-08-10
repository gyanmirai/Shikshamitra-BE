using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmCommunicationOptOutWorkflow(
	IRepository<AdmissionCrmCommunicationOptOut, Guid> optOutRepository,
	AdmissionCrmCommunicationAccess access)
	: ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<AdmissionCrmCommunicationOptOutDto>> GetAsync(
		Guid academicYearId,
		GetAdmissionCrmCommunicationOptOutListInput input)
	{
		await access.EnsureYearAsync(academicYearId, "GetOptOutsAsync");
		var hasWideScope = await access.HasYearWideScopeAsync(academicYearId);
		var scopedLeadIds = await access.GetScopedLeadIdsAsync(academicYearId);
		var queryable = await optOutRepository.GetQueryableAsync();
		var query = queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId &&
			(hasWideScope || (x.LeadId.HasValue && scopedLeadIds.Contains(x.LeadId.Value))));
		if (input.LeadId.HasValue) query = query.Where(x => x.LeadId == input.LeadId.Value);
		if (input.Channel.HasValue) query = query.Where(x => x.Channel == input.Channel.Value);
		if (input.IsActive.HasValue) query = query.Where(x => x.IsActive == input.IsActive.Value);
		if (!string.IsNullOrWhiteSpace(input.ContactSearch))
		{
			var contactSearch = input.ContactSearch.Trim();
			query = query.Where(x => x.ContactValue.Contains(contactSearch) || x.NormalizedContactValue.Contains(contactSearch));
		}
		var totalCount = await AsyncExecuter.CountAsync(query);
		var items = await AsyncExecuter.ToListAsync(query.OrderByDescending(x => x.OptedOutAt)
			.Skip(input.SkipCount).Take(input.MaxResultCount));
		return new PagedResultDto<AdmissionCrmCommunicationOptOutDto>(totalCount,
			items.Select(AdmissionCrmCommunicationMapper.OptOut).ToList());
	}

	public async Task<AdmissionCrmCommunicationOptOutDto> CreateAsync(Guid academicYearId, CreateAdmissionCrmCommunicationOptOutDto input)
	{
		await access.EnsureYearAsync(academicYearId, "CreateOptOutAsync");
		await access.EnsureLeadBelongsToYearAsync(input.LeadId, academicYearId, "CreateOptOutAsync");
		var normalizedContact = AdmissionCrmCommunicationOptOut.NormalizeContact(input.Channel, input.ContactValue);
		var existing = await FindAsync(academicYearId, input.Channel, normalizedContact);
		if (existing is null)
		{
			existing = new AdmissionCrmCommunicationOptOut(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
				input.LeadId, input.Channel, input.ContactValue, input.Reason, Clock.Now);
			await optOutRepository.InsertAsync(existing, autoSave: true);
		}
		else
		{
			existing.Reactivate(input.LeadId, input.Reason, Clock.Now);
			await optOutRepository.UpdateAsync(existing, autoSave: true);
		}
		return AdmissionCrmCommunicationMapper.OptOut(existing);
	}

	public async Task<AdmissionCrmCommunicationOptOutDto> ClearAsync(Guid academicYearId, ClearAdmissionCrmCommunicationOptOutDto input)
	{
		await access.EnsureYearAsync(academicYearId, "ClearOptOutAsync");
		await access.EnsureLeadBelongsToYearAsync(input.LeadId, academicYearId, "ClearOptOutAsync");
		var normalizedContact = AdmissionCrmCommunicationOptOut.NormalizeContact(input.Channel, input.ContactValue);
		var optOut = await FindAsync(academicYearId, input.Channel, normalizedContact);
		if (optOut is null || !optOut.IsActive)
			throw new BusinessException("AdmissionCrm:OptOutNotFound").WithData("Channel", input.Channel).WithData("ContactValue", input.ContactValue);
		optOut.Clear(input.Reason, Clock.Now);
		await optOutRepository.UpdateAsync(optOut, autoSave: true);
		return AdmissionCrmCommunicationMapper.OptOut(optOut);
	}

	public async Task<AdmissionCrmCommunicationOptOut?> FindActiveAsync(Guid academicYearId, AdmissionCrmMessageChannel channel, string normalizedContact)
	{
		var queryable = await optOutRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && x.Channel == channel && x.NormalizedContactValue == normalizedContact && x.IsActive));
	}

	private async Task<AdmissionCrmCommunicationOptOut?> FindAsync(Guid academicYearId, AdmissionCrmMessageChannel channel, string normalizedContact)
	{
		var queryable = await optOutRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && x.Channel == channel && x.NormalizedContactValue == normalizedContact)
			.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.OptedOutAt));
	}
}
