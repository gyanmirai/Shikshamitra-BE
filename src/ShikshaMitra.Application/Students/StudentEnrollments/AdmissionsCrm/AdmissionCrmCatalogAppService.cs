using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
[RemoteService(false)]
public class AdmissionCrmCatalogAppService : ApplicationService, IAdmissionCrmCatalogAppService
{
	private readonly IRepository<AdmissionCrmInquirySource, Guid> _sourceRepository;
	private readonly IRepository<AdmissionCrmInquiryType, Guid> _inquiryTypeRepository;
	private readonly IRepository<AdmissionCrmExamSlot, Guid> _slotRepository;
	private readonly IRepository<AdmissionCrmDocumentType, Guid> _documentTypeRepository;
	private readonly IRepository<AdmissionCrmEntranceExamRecord, Guid> _examRepository;
	private readonly AdmissionCrmExamSlotManager _slotManager;
	private readonly StudentScopeAccessService _scopeAccess;

	public AdmissionCrmCatalogAppService(
		IRepository<AdmissionCrmInquirySource, Guid> sourceRepository,
		IRepository<AdmissionCrmInquiryType, Guid> inquiryTypeRepository,
		IRepository<AdmissionCrmExamSlot, Guid> slotRepository,
		IRepository<AdmissionCrmDocumentType, Guid> documentTypeRepository,
		IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
		AdmissionCrmExamSlotManager slotManager,
		StudentScopeAccessService scopeAccess)
	{
		_sourceRepository = sourceRepository;
		_inquiryTypeRepository = inquiryTypeRepository;
		_slotRepository = slotRepository;
		_documentTypeRepository = documentTypeRepository;
		_examRepository = examRepository;
		_slotManager = slotManager;
		_scopeAccess = scopeAccess;
	}

	public async Task<PagedResultDto<AdmissionCrmInquirySourceDto>> GetInquirySourcesAsync(Guid academicYearId, bool includeInactive = true)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetInquirySourcesAsync));
		var queryable = await _sourceRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId && (includeInactive || x.IsActive)));
		var mapped = items.OrderBy(x => x.DisplayOrder).ThenBy(x => x.DisplayName).Select(MapSource).ToList();
		return new PagedResultDto<AdmissionCrmInquirySourceDto>(mapped.Count, mapped);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmInquirySourceDto> CreateInquirySourceAsync(Guid academicYearId, AdmissionCrmInquirySourceUpsertDto input)
	{
		await EnsureAcademicYearWideMutationAccessAsync(academicYearId, nameof(CreateInquirySourceAsync));
		await EnsureUniqueSourceCodeAsync(academicYearId, input.Code, null);
		var entity = new AdmissionCrmInquirySource(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
			input.Code, input.DisplayName, input.Description, input.AllowedChannels, input.DisplayOrder, input.IsRoutingEligible);
		await _sourceRepository.InsertAsync(entity, autoSave: true);
		return MapSource(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmInquirySourceDto> UpdateInquirySourceAsync(Guid id, AdmissionCrmInquirySourceUpsertDto input)
	{
		var entity = await GetSourceAsync(id, nameof(UpdateInquirySourceAsync));
		await EnsureUniqueSourceCodeAsync(entity.AcademicYearId, input.Code, entity.Id);
		entity.Update(input.Code, input.DisplayName, input.Description, input.AllowedChannels, input.DisplayOrder, input.IsRoutingEligible);
		await _sourceRepository.UpdateAsync(entity, autoSave: true);
		return MapSource(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmInquirySourceDto> SetInquirySourceStatusAsync(Guid id, AdmissionCrmCatalogStatusDto input)
	{
		var entity = await GetSourceAsync(id, nameof(SetInquirySourceStatusAsync));
		entity.SetActive(input.IsActive, input.Reason);
		await _sourceRepository.UpdateAsync(entity, autoSave: true);
		return MapSource(entity);
	}

	public async Task<PagedResultDto<AdmissionCrmInquiryTypeDto>> GetInquiryTypesAsync(
		Guid academicYearId,
		bool includeInactive = true)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(GetInquiryTypesAsync));
		var queryable = await _inquiryTypeRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(item =>
			item.TenantId == CurrentTenant.Id &&
			item.AcademicYearId == academicYearId &&
			(includeInactive || item.IsActive)));
		var mapped = items
			.OrderBy(item => item.DisplayOrder)
			.ThenBy(item => item.DisplayName)
			.Select(MapInquiryType)
			.ToList();
		return new PagedResultDto<AdmissionCrmInquiryTypeDto>(mapped.Count, mapped);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmInquiryTypeDto> CreateInquiryTypeAsync(
		Guid academicYearId,
		AdmissionCrmInquiryTypeUpsertDto input)
	{
		await EnsureAcademicYearWideMutationAccessAsync(
			academicYearId,
			nameof(CreateInquiryTypeAsync));
		await EnsureUniqueInquiryTypeCodeAsync(academicYearId, input.Code, null);
		var entity = new AdmissionCrmInquiryType(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			academicYearId,
			input.Code,
			input.DisplayName,
			input.Description,
			input.DisplayOrder);
		await _inquiryTypeRepository.InsertAsync(entity, autoSave: true);
		return MapInquiryType(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmInquiryTypeDto> UpdateInquiryTypeAsync(
		Guid id,
		AdmissionCrmInquiryTypeUpsertDto input)
	{
		var entity = await GetInquiryTypeAsync(id, nameof(UpdateInquiryTypeAsync));
		await EnsureUniqueInquiryTypeCodeAsync(entity.AcademicYearId, input.Code, entity.Id);
		entity.Update(input.Code, input.DisplayName, input.Description, input.DisplayOrder);
		await _inquiryTypeRepository.UpdateAsync(entity, autoSave: true);
		return MapInquiryType(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageConfiguration)]
	public async Task<AdmissionCrmInquiryTypeDto> SetInquiryTypeStatusAsync(
		Guid id,
		AdmissionCrmCatalogStatusDto input)
	{
		var entity = await GetInquiryTypeAsync(id, nameof(SetInquiryTypeStatusAsync));
		entity.SetActive(input.IsActive, input.Reason);
		await _inquiryTypeRepository.UpdateAsync(entity, autoSave: true);
		return MapInquiryType(entity);
	}

	public async Task<PagedResultDto<AdmissionCrmExamSlotDto>> GetExamSlotsAsync(
		Guid academicYearId, DateTimeOffset? from = null, DateTimeOffset? to = null, bool includeInactive = false)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetExamSlotsAsync));
		var slotQueryable = await _slotRepository.GetQueryableAsync();
		var slots = await AsyncExecuter.ToListAsync(slotQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId &&
			(includeInactive || x.IsActive) && (!from.HasValue || x.EndDate >= from) && (!to.HasValue || x.StartDate <= to)));
		var slotIds = slots.Select(x => x.Id).ToList();
		var examQueryable = await _examRepository.GetQueryableAsync();
		var exams = slotIds.Count == 0 ? [] : await AsyncExecuter.ToListAsync(examQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id && slotIds.Contains(x.EntranceExamSlotId)));
		var counts = exams.GroupBy(x => x.EntranceExamSlotId).ToDictionary(x => x.Key, x => x.Count());
		var mapped = slots.OrderBy(x => x.StartDate)
			.Select(x => MapSlot(x, counts.TryGetValue(x.Id, out var scheduled) ? scheduled : 0)).ToList();
		return new PagedResultDto<AdmissionCrmExamSlotDto>(mapped.Count, mapped);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageExamConfiguration)]
	public async Task<AdmissionCrmExamSlotDto> CreateExamSlotAsync(Guid academicYearId, AdmissionCrmExamSlotUpsertDto input)
	{
		await EnsureAcademicYearWideMutationAccessAsync(academicYearId, nameof(CreateExamSlotAsync));
		var entity = await _slotManager.CreateAsync(academicYearId,
			input.PlaybookId, input.SlotTemplateCode, input.ExamDefinitionCode, input.SlotName, input.Campus,
			input.RoomOrHall, input.StartDate, input.EndDate, input.StudentLimit, input.RoomNo);
		await _slotRepository.InsertAsync(entity, autoSave: true);
		return MapSlot(entity, 0);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageExamConfiguration)]
	public async Task<AdmissionCrmExamSlotDto> UpdateExamSlotAsync(Guid id, AdmissionCrmExamSlotUpsertDto input)
	{
		var entity = await GetSlotAsync(id, nameof(UpdateExamSlotAsync));
		await _slotManager.UpdateAsync(entity, input.SlotName, input.Campus, input.RoomOrHall,
			input.StartDate, input.EndDate, input.StudentLimit, input.RoomNo);
		await _slotRepository.UpdateAsync(entity, autoSave: true);
		return MapSlot(entity, await CountScheduledAsync(entity.Id));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageExamConfiguration)]
	public async Task<AdmissionCrmExamSlotDto> SetExamSlotStatusAsync(Guid id, AdmissionCrmCatalogStatusDto input)
	{
		var entity = await GetSlotAsync(id, nameof(SetExamSlotStatusAsync));
		entity.SetActive(input.IsActive);
		await _slotRepository.UpdateAsync(entity, autoSave: true);
		return MapSlot(entity, await CountScheduledAsync(entity.Id));
	}

	public async Task<PagedResultDto<AdmissionCrmDocumentTypeDto>> GetDocumentTypesAsync(Guid academicYearId, bool includeInactive = true)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, nameof(GetDocumentTypesAsync));
		var queryable = await _documentTypeRepository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && (includeInactive || x.IsActive)));
		var mapped = items.OrderBy(x => x.DisplayOrder).ThenBy(x => x.Name).Select(MapDocumentType).ToList();
		return new PagedResultDto<AdmissionCrmDocumentTypeDto>(mapped.Count, mapped);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDocumentConfiguration)]
	public async Task<AdmissionCrmDocumentTypeDto> CreateDocumentTypeAsync(Guid academicYearId, AdmissionCrmDocumentTypeUpsertDto input)
	{
		await EnsureAcademicYearWideMutationAccessAsync(academicYearId, nameof(CreateDocumentTypeAsync));
		await EnsureUniqueDocumentCodeAsync(academicYearId, input.Code, null);
		var entity = new AdmissionCrmDocumentType(GuidGenerator.Create(), CurrentTenant.Id, academicYearId,
			input.Code, input.Name, input.MaximumSizeMb, input.AcceptedExtensions, input.RequiresOriginal,
			input.ValidityDateRequired, input.DisplayOrder);
		await _documentTypeRepository.InsertAsync(entity, autoSave: true);
		return MapDocumentType(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDocumentConfiguration)]
	public async Task<AdmissionCrmDocumentTypeDto> UpdateDocumentTypeAsync(Guid id, AdmissionCrmDocumentTypeUpsertDto input)
	{
		var entity = await GetDocumentTypeAsync(id, nameof(UpdateDocumentTypeAsync));
		await EnsureUniqueDocumentCodeAsync(entity.AcademicYearId, input.Code, entity.Id);
		entity.Update(input.Code, input.Name, input.MaximumSizeMb, input.AcceptedExtensions, input.RequiresOriginal,
			input.ValidityDateRequired, input.DisplayOrder);
		await _documentTypeRepository.UpdateAsync(entity, autoSave: true);
		return MapDocumentType(entity);
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageDocumentConfiguration)]
	public async Task<AdmissionCrmDocumentTypeDto> SetDocumentTypeStatusAsync(Guid id, AdmissionCrmCatalogStatusDto input)
	{
		var entity = await GetDocumentTypeAsync(id, nameof(SetDocumentTypeStatusAsync));
		entity.SetActive(input.IsActive);
		await _documentTypeRepository.UpdateAsync(entity, autoSave: true);
		return MapDocumentType(entity);
	}

	private async Task<AdmissionCrmInquirySource> GetSourceAsync(Guid id, string operation)
	{
		var entity = await _sourceRepository.GetAsync(id);
		await EnsureAcademicYearWideMutationAccessAsync(entity.AcademicYearId, operation);
		if (entity.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:InquirySourceNotFound");
		return entity;
	}

	private async Task<AdmissionCrmExamSlot> GetSlotAsync(Guid id, string operation)
	{
		var entity = await _slotRepository.GetAsync(id);
		await EnsureAcademicYearWideMutationAccessAsync(entity.AcademicYearId, operation);
		if (entity.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:ExamSlotNotFound");
		return entity;
	}

	private async Task<AdmissionCrmInquiryType> GetInquiryTypeAsync(Guid id, string operation)
	{
		var entity = await _inquiryTypeRepository.GetAsync(id);
		await EnsureAcademicYearWideMutationAccessAsync(entity.AcademicYearId, operation);
		if (entity.TenantId != CurrentTenant.Id)
		{
			throw new BusinessException("AdmissionCrm:InquiryTypeNotFound");
		}

		return entity;
	}

	private async Task<AdmissionCrmDocumentType> GetDocumentTypeAsync(Guid id, string operation)
	{
		var entity = await _documentTypeRepository.GetAsync(id);
		await EnsureAcademicYearWideMutationAccessAsync(entity.AcademicYearId, operation);
		if (entity.TenantId != CurrentTenant.Id) throw new BusinessException("AdmissionCrm:DocumentTypeNotFound");
		return entity;
	}

	private async Task EnsureAcademicYearWideMutationAccessAsync(Guid academicYearId, string operation)
	{
		await _scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, operation);
		if (!await _scopeAccess.CurrentUserHasAcademicYearWideScopeAsync(academicYearId))
		{
			throw new BusinessException("AdmissionCrm:AcademicYearWideScopeRequired")
				.WithData("AcademicYearId", academicYearId)
				.WithData("Operation", operation);
		}
	}

	private async Task EnsureUniqueSourceCodeAsync(Guid academicYearId, string code, Guid? excludingId)
	{
		var normalized = code.Trim().ToUpperInvariant();
		var queryable = await _sourceRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId &&
			x.Code == normalized && (!excludingId.HasValue || x.Id != excludingId.Value))))
			throw new BusinessException("AdmissionCrm:DuplicateInquirySourceCode");
	}

	private async Task EnsureUniqueDocumentCodeAsync(Guid academicYearId, string code, Guid? excludingId)
	{
		var normalized = code.Trim().ToUpperInvariant();
		var queryable = await _documentTypeRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId &&
			x.Code == normalized && (!excludingId.HasValue || x.Id != excludingId.Value))))
			throw new BusinessException("AdmissionCrm:DuplicateDocumentTypeCode");
	}

	private async Task EnsureUniqueInquiryTypeCodeAsync(
		Guid academicYearId,
		string code,
		Guid? excludingId)
	{
		var normalized = code.Trim().ToUpperInvariant();
		var queryable = await _inquiryTypeRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(queryable.Where(item =>
			item.TenantId == CurrentTenant.Id &&
			item.AcademicYearId == academicYearId &&
			item.Code == normalized &&
			(!excludingId.HasValue || item.Id != excludingId.Value))))
		{
			throw new BusinessException("AdmissionCrm:DuplicateInquiryTypeCode");
		}
	}

	private async Task<int> CountScheduledAsync(Guid slotId)
	{
		var queryable = await _examRepository.GetQueryableAsync();
		return await AsyncExecuter.CountAsync(queryable.Where(x => x.TenantId == CurrentTenant.Id && x.EntranceExamSlotId == slotId));
	}

	private static AdmissionCrmInquirySourceDto MapSource(AdmissionCrmInquirySource x) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, Code = x.Code, DisplayName = x.DisplayName,
		Description = x.Description, AllowedChannels = x.GetAllowedChannels(), DisplayOrder = x.DisplayOrder,
		IsRoutingEligible = x.IsRoutingEligible, IsActive = x.IsActive, DeactivationReason = x.DeactivationReason
	};

	private static AdmissionCrmInquiryTypeDto MapInquiryType(AdmissionCrmInquiryType inquiryType) => new()
	{
		Id = inquiryType.Id,
		AcademicYearId = inquiryType.AcademicYearId,
		Code = inquiryType.Code,
		DisplayName = inquiryType.DisplayName,
		Description = inquiryType.Description,
		DisplayOrder = inquiryType.DisplayOrder,
		IsActive = inquiryType.IsActive,
		DeactivationReason = inquiryType.DeactivationReason
	};

	private static AdmissionCrmExamSlotDto MapSlot(AdmissionCrmExamSlot x, int scheduledCount) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, PlaybookId = x.PlaybookId, SlotTemplateCode = x.SlotTemplateCode,
		ExamDefinitionCode = x.ExamDefinitionCode, SlotName = x.SlotName, Campus = x.Campus, RoomOrHall = x.RoomOrHall,
		StartDate = x.StartDate, EndDate = x.EndDate, StudentLimit = x.StudentLimit, RoomNo = x.RoomNo, IsActive = x.IsActive,
		ScheduledCount = scheduledCount, RemainingCapacity = Math.Max(0, x.StudentLimit - scheduledCount)
	};

	private static AdmissionCrmDocumentTypeDto MapDocumentType(AdmissionCrmDocumentType x) => new()
	{
		Id = x.Id, AcademicYearId = x.AcademicYearId, Code = x.Code, Name = x.Name, MaximumSizeMb = x.MaximumSizeMb,
		AcceptedExtensions = x.GetAcceptedExtensions(), RequiresOriginal = x.RequiresOriginal,
		ValidityDateRequired = x.ValidityDateRequired, DisplayOrder = x.DisplayOrder, IsActive = x.IsActive
	};
}
