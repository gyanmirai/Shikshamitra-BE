using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students;
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
	private readonly IRepository<StudentAdditionalDetail, Guid> _studentAdditionalDetailRepository;
	private readonly InquiryPreferenceManager _inquiryPreferenceManager;
	private readonly StudentScopeAccessService _studentScopeAccessService;

	public InquiryPreferenceAppService(
		IRepository<InquiryPreference, Guid> inquiryPreferenceRepository,
		IRepository<StudentAdditionalDetail, Guid> studentAdditionalDetailRepository,
		InquiryPreferenceManager inquiryPreferenceManager,
		StudentScopeAccessService studentScopeAccessService)
	{
		_inquiryPreferenceRepository = inquiryPreferenceRepository;
		_studentAdditionalDetailRepository = studentAdditionalDetailRepository;
		_inquiryPreferenceManager = inquiryPreferenceManager;
		_studentScopeAccessService = studentScopeAccessService;
	}

	/// <summary>
	/// Gets a paginated list of inquiry preferences for the current tenant
	/// </summary>
    public async Task<PagedResultDto<InquiryPreferenceDto>> GetListAsync(GetInquiryPreferenceListInput input, Guid academicYearId)
	{
		await _studentScopeAccessService.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(GetListAsync));

		var queryable = await _inquiryPreferenceRepository.GetQueryableAsync();

		// Filter by current tenant
		var query = queryable
            .Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);

		if (input.IsActive.HasValue)
		{
			query = query.Where(x => x.IsActive == input.IsActive.Value);
		}

		if (!string.IsNullOrWhiteSpace(input.FilterText))
		{
			var normalizedFilter = input.FilterText.Trim().ToLower();
			query = query.Where(x =>
				x.PreferenceName.ToLower().Contains(normalizedFilter) ||
				(x.Description != null && x.Description.ToLower().Contains(normalizedFilter)));
		}

		query = query
			.OrderByDescending(x => x.IsActive)
			.ThenBy(x => x.DisplayOrder)
			.ThenBy(x => x.PreferenceName);

		var totalCount = await AsyncExecuter.CountAsync(query);

		if (totalCount == 0)
		{
			return new PagedResultDto<InquiryPreferenceDto>(0, new List<InquiryPreferenceDto>());
		}

		var pagedQuery = query
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount);

		var inquiryPreferences = await AsyncExecuter.ToListAsync(pagedQuery);
		var assignedCounts = await GetAssignedCountsAsync(inquiryPreferences.Select(x => x.Id));

        var dtos = inquiryPreferences
			.Select(ip => ToDto(ip, assignedCounts.GetValueOrDefault(ip.Id)))
			.ToList();

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

		return ToDto(inquiryPreference, await GetAssignedCountAsync(id));
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
            input.PreferenceName,
			input.Description,
			input.DisplayOrder
        );

		await _inquiryPreferenceRepository.InsertAsync(inquiryPreference, autoSave: true);

		return ToDto(inquiryPreference, 0);
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
            input.PreferenceName,
			input.Description,
			input.DisplayOrder
        );

		await _inquiryPreferenceRepository.UpdateAsync(inquiryPreference, autoSave: true);

		return ToDto(inquiryPreference, await GetAssignedCountAsync(id));
	}

	/// <summary>
	/// Activates or deactivates an inquiry preference
	/// </summary>
	public async Task<InquiryPreferenceDto> UpdateStatusAsync(Guid id, UpdateInquiryPreferenceStatusDto input)
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
			nameof(UpdateStatusAsync));

		if (input.IsActive)
		{
			inquiryPreference.Activate();
		}
		else
		{
			inquiryPreference.Deactivate(input.DeactivationReason);
		}

		await _inquiryPreferenceRepository.UpdateAsync(inquiryPreference, autoSave: true);

		return ToDto(inquiryPreference, await GetAssignedCountAsync(id));
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

		var assignedCount = await _studentAdditionalDetailRepository.CountAsync(x =>
			x.TenantId == CurrentTenant.Id &&
			x.InquiryPreferenceId == id);
		if (assignedCount > 0)
		{
			throw new BusinessException("InquiryPreference:InUse")
				.WithData("Id", id)
				.WithData("PreferenceName", inquiryPreference.PreferenceName)
				.WithData("AssignedStudentsCount", assignedCount);
		}

		await _inquiryPreferenceRepository.DeleteAsync(inquiryPreference, autoSave: true);
	}

	private async Task<int> GetAssignedCountAsync(Guid inquiryPreferenceId)
	{
		return await _studentAdditionalDetailRepository.CountAsync(x =>
			x.TenantId == CurrentTenant.Id &&
			x.InquiryPreferenceId == inquiryPreferenceId);
	}

	private async Task<Dictionary<Guid, int>> GetAssignedCountsAsync(IEnumerable<Guid> inquiryPreferenceIds)
	{
		var ids = inquiryPreferenceIds.Distinct().ToList();
		if (ids.Count == 0)
		{
			return new Dictionary<Guid, int>();
		}

		var queryable = await _studentAdditionalDetailRepository.GetQueryableAsync();
		var rows = await AsyncExecuter.ToListAsync(
			queryable
				.Where(x =>
					x.TenantId == CurrentTenant.Id &&
					x.InquiryPreferenceId.HasValue &&
					ids.Contains(x.InquiryPreferenceId.Value))
				.GroupBy(x => x.InquiryPreferenceId!.Value)
				.Select(x => new { InquiryPreferenceId = x.Key, Count = x.Count() }));

		return rows.ToDictionary(x => x.InquiryPreferenceId, x => x.Count);
	}

	private static InquiryPreferenceDto ToDto(InquiryPreference inquiryPreference, int assignedCount)
	{
		return new InquiryPreferenceDto
		{
			Id = inquiryPreference.Id,
			PreferenceName = inquiryPreference.PreferenceName,
			Description = inquiryPreference.Description,
			AcademicYearId = inquiryPreference.AcademicYearId,
			DisplayOrder = inquiryPreference.DisplayOrder,
			IsActive = inquiryPreference.IsActive,
			DeactivationReason = inquiryPreference.DeactivationReason,
			AssignedStudentsCount = assignedCount,
			CanDelete = assignedCount == 0,
			CreationTime = inquiryPreference.CreationTime,
			CreatorId = inquiryPreference.CreatorId,
			LastModificationTime = inquiryPreference.LastModificationTime,
			LastModifierId = inquiryPreference.LastModifierId
		};
	}
}
