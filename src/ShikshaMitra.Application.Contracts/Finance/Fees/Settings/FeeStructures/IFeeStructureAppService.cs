using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Settings;

public interface IFeeStructureAppService : IApplicationService
{
    /// <summary>Creates fee structures for a standard under the given fee structure metadata.</summary>
    Task<FeeStructureCreateResponse> CreateAsync(Guid feeStructureMetadataId, StandardFeeStructureSaveRequest input);

    /// <summary>Replaces the fee head assignments of an existing, unlocked fee structure.</summary>
    Task UpdateAsync(Guid id, StandardFeeStructureRequest input);

    /// <summary>Soft-deletes an unlocked fee structure and its details.</summary>
    Task DeleteAsync(Guid id);

    /// <summary>Returns all fee structures (with details) for a given standard.</summary>
    Task<StandardFeeStructureResponse> GetByStandardAsync(Guid standardId);

    /// <summary>Locks a fee structure, preventing further modifications.</summary>
    Task LockAsync(Guid id);
}
