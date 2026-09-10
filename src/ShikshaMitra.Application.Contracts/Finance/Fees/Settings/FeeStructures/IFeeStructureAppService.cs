using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Settings;

public interface IFeeStructureAppService : IApplicationService
{
	Task<FeeStructureContextDto> GetContextAsync(Guid feeStructureMetadataId, Guid standardId);
	Task<FeeStructureCreateResponse> PatchAsync(Guid id, FeeStructurePatchRequest input);
	Task<FeeStructureSettingsDto> GetSettingsAsync(Guid feeStructureMetadataId, Guid standardId);
	Task<FeeStructurePreviewDto> PreviewAsync(FeeStructurePreviewRequest input);
	Task<FeeStructureCreateResponse> PublishAsync(Guid id, FeeStructurePublishRequest input);
	Task<FeeStructureCreateResponse> CreateRevisionAsync(Guid id, FeeStructureRevisionRequest input);
    /// <summary>Creates fee structures for a standard under the given fee structure metadata.</summary>
    Task<FeeStructureCreateResponse> CreateAsync(Guid feeStructureMetadataId, StandardFeeStructureSaveRequest input);

    /// <summary>Replaces the fee head assignments of an existing, unlocked fee structure.</summary>
    Task<FeeStructureCreateResponse> UpdateAsync(Guid id, StandardFeeStructureRequest input);

    /// <summary>Soft-deletes an unlocked fee structure and its details.</summary>
    Task DeleteAsync(Guid id);

    /// <summary>Returns all fee structures (with details) for a given standard.</summary>
    Task<StandardFeeStructureResponse> GetByStandardAsync(Guid standardId);

    /// <summary>Locks a fee structure, preventing further modifications.</summary>
    Task LockAsync(Guid id);

    /// <summary>Returns an ordered list of month-year pairs spanning the given academic year's start and end dates.</summary>
    Task<List<AcademicYearMonthItem>> GetMonthsByAcademicYearAsync(Guid academicYearId);

	Task<CollectionPolicyAttachmentImpactResponse> ValidateCollectionPolicyAttachmentAsync(CollectionPolicyAttachmentImpactRequest input);
}
