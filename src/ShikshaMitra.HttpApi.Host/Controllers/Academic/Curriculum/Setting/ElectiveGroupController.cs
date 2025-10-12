
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Setting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Curriculum.Setting
{
    [Authorize]
    [Route("api/academic/curriculum/setting")]
    [ApiController]
    public class ElectiveGroupController(IElectiveGroupAppService electiveGroupAppService) : AbpControllerBase
	{     

        [HttpGet("elective-groups/{academicMediumId}")]
        public async Task<IActionResult> GetAllElectiveGroups(Guid academicMediumId)
        {
            var groups = await electiveGroupAppService.GetListelectivegroupAsync(academicMediumId);

            return Ok(groups);	
        }

        [HttpGet("elective-group/{id}")]
        public async Task<ActionResult<ElectiveGroupDto>> GetElectiveGroupById(Guid id)
        {
            var group = await electiveGroupAppService.GetelectivegroupAsync(id);

            if (group == null)
                return NotFound();

            return Ok(group);
        }

        [HttpPost("elective-group")]
        public async Task<ActionResult<ElectiveGroupDto>> CreateElectiveGroup(
            [FromBody] CreateElectiveGroupDto createElectiveGroupDto)
        {
            var electiveGroup = await electiveGroupAppService.CreateelectivegroupAsync(createElectiveGroupDto);

            return Ok(electiveGroup);
		}

        [HttpPut("elective-group/{id}")]
        public async Task<IActionResult> UpdateElectiveGroup(Guid id, [FromBody] UpdateElectiveGroupDto updateElectiveGroupDto)
        {
			var updated = await electiveGroupAppService.UpdateelectivegroupAsync(id, updateElectiveGroupDto);

            if (updated == null)
                return NotFound();

            return Ok(updated);
        }

        [HttpDelete("elective-group/{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            await electiveGroupAppService.DeleteelectivegroupAsync(id);

			return Ok(new
			{
				message = "ElectiveGroup has been deleted successfully.",
			});
		}
    }
}
