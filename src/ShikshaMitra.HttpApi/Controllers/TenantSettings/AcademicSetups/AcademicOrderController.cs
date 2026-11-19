using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp;

namespace ShikshaMitra.Controllers.TenantSettings.AcademicSetups;

[Authorize(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit)]
[Area("app")]
[RemoteService(Name = "Default")]
public class AcademicOrderController(IStandardAppService standards, IDivisionAppService divisions, IEducationMediumStageAppService stages) : AbpController
{
	[HttpPut("api/education-mediums/{mediumId:guid}/stages/order")]
	public async Task<IActionResult> ReorderStagesAsync(Guid mediumId, [FromBody] ReorderAcademicChildrenDto input)
	{
		await stages.ReorderAsync(mediumId, input);
		return NoContent();
	}

	[HttpPut("api/education-medium-stages/{stageId:guid}/standards/order")]
	public async Task<IActionResult> ReorderStandardsAsync(Guid stageId, [FromBody] ReorderAcademicChildrenDto input)
	{
		await standards.ReorderAsync(stageId, input);
		return NoContent();
	}

	[HttpPut("api/standards/{standardId:guid}/divisions/order")]
	public async Task<IActionResult> ReorderDivisionsAsync(Guid standardId, [FromBody] ReorderAcademicChildrenDto input)
	{
		await divisions.ReorderAsync(standardId, input);
		return NoContent();
	}
}
