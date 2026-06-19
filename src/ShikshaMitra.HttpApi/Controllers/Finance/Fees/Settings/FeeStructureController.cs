
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Settings;

[RemoteService]
[Area("finance")]
[Route("api/fee-structures")]
[Authorize]
public class FeeStructureController(
	IFeeStructureAppService feeStructureAppService
) : AbpController
{
    [HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add)]
	public Task<FeeStructureCreateResponse> CreateAsync(
        [FromQuery] Guid feeStructureMetadataId,
        [FromBody] StandardFeeStructureSaveRequest input)
    {
        return feeStructureAppService.CreateAsync(feeStructureMetadataId, input);
    }

    [HttpPut("{id}")]
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add)]
    public Task UpdateAsync(Guid id, [FromBody] StandardFeeStructureRequest input)
    {
        return feeStructureAppService.UpdateAsync(id, input);
    }

    [HttpDelete("{id}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Delete)]
	public Task DeleteAsync(Guid id)
    {
        return feeStructureAppService.DeleteAsync(id);
    }

    [HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.View)]
    public Task<StandardFeeStructureResponse> GetByStandardAsync([FromQuery] Guid standardId)
    {
        return feeStructureAppService.GetByStandardAsync(standardId);
    }

    [HttpPatch("{id}/lock")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add)]
	public Task LockAsync(Guid id)
    {
        return feeStructureAppService.LockAsync(id);
    }

    [HttpGet("academic-year-months")]
    public Task<List<AcademicYearMonthItem>> GetMonthsByAcademicYearAsync([FromQuery] Guid academicYearId)
    {
        return feeStructureAppService.GetMonthsByAcademicYearAsync(academicYearId);
    }
}
