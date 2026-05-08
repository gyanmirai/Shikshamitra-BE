
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using ShikshaMitra.Finance.Fees.Settings;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Settings;

[RemoteService]
[Area("Finance")]
[Route("api/fee-structures")]
[Authorize]
public class FeeStructureController(IFeeStructureAppService feeStructureAppService) : AbpController
{
    /// <summary>
    /// Create fee structures for a standard under the given fee structure metadata.
    /// </summary>
    /// <param name="feeStructureMetadataId">The parent fee structure metadata identifier (UUID)</param>
    /// <param name="input">Standard and its fee head assignments</param>
    /// <returns>Created fee structure with generated Id and Name</returns>
    [HttpPost]
    public Task<FeeStructureCreateResponse> CreateAsync(
        [FromQuery] Guid feeStructureMetadataId,
        [FromBody] StandardFeeStructureSaveRequest input)
    {
        return feeStructureAppService.CreateAsync(feeStructureMetadataId, input);
    }

    /// <summary>
    /// Replace fee head assignments of an existing, unlocked fee structure.
    /// </summary>
    /// <param name="id">Fee structure identifier (UUID)</param>
    /// <param name="input">Updated fee head assignments</param>
    [HttpPut("{id}")]
    public Task UpdateAsync(Guid id, [FromBody] StandardFeeStructureRequest input)
    {
        return feeStructureAppService.UpdateAsync(id, input);
    }

    /// <summary>
    /// Soft-delete an unlocked fee structure and all its details.
    /// </summary>
    /// <param name="id">Fee structure identifier (UUID)</param>
    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id)
    {
        return feeStructureAppService.DeleteAsync(id);
    }

    /// <summary>
    /// Get all fee structures (with fee head details) for a given standard.
    /// </summary>
    /// <param name="standardId">Standard identifier (UUID)</param>
    /// <returns>Standard with its list of fee structures and fee head assignments</returns>
    [HttpGet]
    public Task<StandardFeeStructureResponse> GetByStandardAsync([FromQuery] Guid standardId)
    {
        return feeStructureAppService.GetByStandardAsync(standardId);
    }

    /// <summary>
    /// Lock a fee structure, preventing any further modifications or deletion.
    /// </summary>
    /// <param name="id">Fee structure identifier (UUID)</param>
    [HttpPatch("{id}/lock")]
    public Task LockAsync(Guid id)
    {
        return feeStructureAppService.LockAsync(id);
    }

    /// <summary>
    /// Returns an ordered list of month-year pairs spanning the given academic year's start and end dates.
    /// </summary>
    /// <param name="academicYearId">Academic year identifier (UUID)</param>
    /// <returns>Ordered list of month and year pairs from the academic year's start month to end month</returns>
    [HttpGet("academic-year-months")]
    public Task<List<AcademicYearMonthItem>> GetMonthsByAcademicYearAsync([FromQuery] Guid academicYearId)
    {
        return feeStructureAppService.GetMonthsByAcademicYearAsync(academicYearId);
    }
}
