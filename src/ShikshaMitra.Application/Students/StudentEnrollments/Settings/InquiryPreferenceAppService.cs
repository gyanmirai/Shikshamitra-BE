using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

/// <summary>
/// Application service for managing inquiry preferences
/// </summary>
[RemoteService(false)]
public class InquiryPreferenceAppService : ApplicationService, IInquiryPreferenceAppService
{
	private readonly IInquiryPreferenceRepository _inquiryPreferenceRepository;
	private readonly InquiryPreferenceManager _inquiryPreferenceManager;

	public InquiryPreferenceAppService(
		IInquiryPreferenceRepository inquiryPreferenceRepository,
		InquiryPreferenceManager inquiryPreferenceManager)
	{
		_inquiryPreferenceRepository = inquiryPreferenceRepository;
		_inquiryPreferenceManager = inquiryPreferenceManager;
	}

	/// <summary>
	/// Gets a paginated list of inquiry preferences for the current tenant
	/// </summary>
	public async Task<PagedResultDto<InquiryPreferenceDto>> GetListAsync(PagedAndSortedResultRequestDto input)
	{
		var queryable = await _inquiryPreferenceRepository.GetQueryableAsync();

		// Filter by current tenant
		var query = queryable
			.Where(x => x.TenantId == CurrentTenant.Id)
			.OrderBy(x => x.PreferenceName);

		var totalCount = await AsyncExecuter.CountAsync(query);

		if (totalCount == 0)
		{
			return new PagedResultDto<InquiryPreferenceDto>(0, new List<InquiryPreferenceDto>());
		}

		var pagedQuery = query
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount);

		var inquiryPreferences = await AsyncExecuter.ToListAsync(pagedQuery);

		var dtos = inquiryPreferences.Select(ip => new InquiryPreferenceDto
		{
			Id = ip.Id,
			PreferenceName = ip.PreferenceName,
			CreationTime = ip.CreationTime,
			CreatorId = ip.CreatorId,
			LastModificationTime = ip.LastModificationTime,
			LastModifierId = ip.LastModifierId
		}).ToList();

		return new PagedResultDto<InquiryPreferenceDto>(totalCount, dtos);
	}

	/// <summary>
	/// Gets an inquiry preference by ID
	/// </summary>
	public async Task<InquiryPreferenceDto> GetAsync(Guid id)
	{
		var inquiryPreference = await _inquiryPreferenceRepository.GetAsync(id);

		// Verify tenant isolation
		if (inquiryPreference.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("InquiryPreference:NotFound")
				.WithData("Id", id);
		}

		return new InquiryPreferenceDto
		{
			Id = inquiryPreference.Id,
			PreferenceName = inquiryPreference.PreferenceName,
			CreationTime = inquiryPreference.CreationTime,
			CreatorId = inquiryPreference.CreatorId,
			LastModificationTime = inquiryPreference.LastModificationTime,
			LastModifierId = inquiryPreference.LastModifierId
		};
	}

	/// <summary>
	/// Creates a new inquiry preference
	/// </summary>
	public async Task<InquiryPreferenceDto> CreateAsync(CreateUpdateInquiryPreferenceDto input)
	{
		var inquiryPreference = await _inquiryPreferenceManager.CreateAsync(
			CurrentTenant.Id,
			input.PreferenceName
		);

		await _inquiryPreferenceRepository.InsertAsync(inquiryPreference, autoSave: true);

		return new InquiryPreferenceDto
		{
			Id = inquiryPreference.Id,
			PreferenceName = inquiryPreference.PreferenceName,
			CreationTime = inquiryPreference.CreationTime,
			CreatorId = inquiryPreference.CreatorId
		};
	}

	/// <summary>
	/// Updates an existing inquiry preference
	/// </summary>
	public async Task<InquiryPreferenceDto> UpdateAsync(Guid id, CreateUpdateInquiryPreferenceDto input)
	{
		var inquiryPreference = await _inquiryPreferenceRepository.GetAsync(id);

		// Verify tenant isolation
		if (inquiryPreference.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("InquiryPreference:NotFound")
				.WithData("Id", id);
		}

		await _inquiryPreferenceManager.UpdateAsync(
			inquiryPreference,
			input.PreferenceName
		);

		await _inquiryPreferenceRepository.UpdateAsync(inquiryPreference, autoSave: true);

		return new InquiryPreferenceDto
		{
			Id = inquiryPreference.Id,
			PreferenceName = inquiryPreference.PreferenceName,
			CreationTime = inquiryPreference.CreationTime,
			CreatorId = inquiryPreference.CreatorId,
			LastModificationTime = inquiryPreference.LastModificationTime,
			LastModifierId = inquiryPreference.LastModifierId
		};
	}

	/// <summary>
	/// Deletes an inquiry preference
	/// </summary>
	public async Task DeleteAsync(Guid id)
	{
		var inquiryPreference = await _inquiryPreferenceRepository.GetAsync(id);

		// Verify tenant isolation
		if (inquiryPreference.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("InquiryPreference:NotFound")
				.WithData("Id", id);
		}

		await _inquiryPreferenceRepository.DeleteAsync(inquiryPreference, autoSave: true);
	}
}
