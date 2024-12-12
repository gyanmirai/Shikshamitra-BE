using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.TenantSettings.Standards;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.TenantSettings.AcademicSetups.Standards;

[Authorize]
[Route("api/education-medium")]
public class StandardsController(IStandardAppService standardAppService) : AbpController
{
	[HttpGet("{educationMediumId}/standards")]
	public async Task<IActionResult> GetStandardsByEducationMediumId(Guid educationMediumId) =>
		Ok(await standardAppService.GetStandardsByEducationMediumId(educationMediumId));
}