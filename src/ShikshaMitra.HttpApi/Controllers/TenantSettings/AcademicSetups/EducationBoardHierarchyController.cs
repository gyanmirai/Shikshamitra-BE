using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.TenantSettings.AcademicSetups;

[Authorize]
[Route("api/app/education-board/hierarchy")]
public class EducationBoardHierarchyController(IEducationBoardAppService service) : AbpController
{
	[HttpGet]
	public Task<ListResultDto<EducationBoardDto>> GetAsync([FromQuery] Guid academicYearId) => service.GetHierarchyAsync(academicYearId);
}
