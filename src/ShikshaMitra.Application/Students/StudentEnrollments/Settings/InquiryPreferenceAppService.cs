using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

/// <summary>
/// Application service for managing inquiry preferences
/// </summary>
[RemoteService(false)]
public class InquiryPreferenceAppService : ApplicationService, IInquiryPreferenceAppService
{
	private readonly IRepository<InquiryPreference, Guid> _inquiryPreferenceRepository;
	private readonly InquiryPreferenceManager _inquiryPreferenceManager;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public InquiryPreferenceAppService(
		IRepository<InquiryPreference, Guid> inquiryPreferenceRepository,
		InquiryPreferenceManager inquiryPreferenceManager,
		StudentScopeAccessService studentScopeAccessService)
	{
		_inquiryPreferenceRepository = inquiryPreferenceRepository;
		_inquiryPreferenceManager = inquiryPreferenceManager;
		_studentScopeAccessService = studentScopeAccessService;
	}

	/// <summary>
	/// Gets a paginated list of inquiry preferences for the current tenant
	/// </summary>
    public async Task<PagedResultDto<InquiryPreferenceDto>> GetListAsync(PagedAndSortedResultRequestDto input, Guid academicYearId)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(GetListAsync));

		var queryable = await _inquiryPreferenceRepository.GetQueryableAsync();

		// Filter by current tenant
		var query = queryable
            .Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId)
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
            AcademicYearId = ip.AcademicYearId,
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
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			inquiryPreference.AcademicYearId,
			nameof(GetAsync));

		return new InquiryPreferenceDto
		{
			Id = inquiryPreference.Id,
			PreferenceName = inquiryPreference.PreferenceName,
			AcademicYearId = inquiryPreference.AcademicYearId,
			CreationTime = inquiryPreference.CreationTime,
			CreatorId = inquiryPreference.CreatorId,
			LastModificationTime = inquiryPreference.LastModificationTime,
			LastModifierId = inquiryPreference.LastModifierId
		};
	}

	/// <summary>
	/// Creates a new inquiry preference
	/// </summary>
    public async Task<InquiryPreferenceDto> CreateAsync(Guid academicYearId, CreateUpdateInquiryPreferenceDto input)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(CreateAsync));

        var inquiryPreference = await _inquiryPreferenceManager.CreateAsync(
            CurrentTenant.Id,
            academicYearId,
            input.PreferenceName
        );

		await _inquiryPreferenceRepository.InsertAsync(inquiryPreference, autoSave: true);

		return new InquiryPreferenceDto
		{
			Id = inquiryPreference.Id,
			PreferenceName = inquiryPreference.PreferenceName,
            AcademicYearId = inquiryPreference.AcademicYearId,
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
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			inquiryPreference.AcademicYearId,
			nameof(UpdateAsync));

        await _inquiryPreferenceManager.UpdateAsync(
            inquiryPreference,
            input.PreferenceName
        );

		await _inquiryPreferenceRepository.UpdateAsync(inquiryPreference, autoSave: true);

		return new InquiryPreferenceDto
		{
			Id = inquiryPreference.Id,
			PreferenceName = inquiryPreference.PreferenceName,
            AcademicYearId = inquiryPreference.AcademicYearId,
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
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			inquiryPreference.AcademicYearId,
			nameof(DeleteAsync));

		await _inquiryPreferenceRepository.DeleteAsync(inquiryPreference, autoSave: true);
	}
}
