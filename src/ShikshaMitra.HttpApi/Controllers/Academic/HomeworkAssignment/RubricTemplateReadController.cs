using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.HomeworkAssignment;

[ApiController]
[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsRubricConfig.View)]
[Route("api/academic/rubrics/templates")]
public class RubricTemplateReadController(IRubricTemplateReadAppService service) : AbpControllerBase
{
    [HttpGet("paged")]
    [ProducesResponseType(typeof(RubricTemplatePageDto), 200)]
    public Task<RubricTemplatePageDto> GetPageAsync([FromQuery] GetRubricTemplatePageInput input) => service.GetPageAsync(input);
}
