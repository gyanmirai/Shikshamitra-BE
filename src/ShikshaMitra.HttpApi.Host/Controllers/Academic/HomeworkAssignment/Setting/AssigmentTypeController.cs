using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.HomeworkAssignment.Setting;
using System;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.HomeworkAssignment.Setting
{
    [Authorize]
    [Route("api/academic/homework&assigment/setting")]
    [ApiController]
    public class AssignmentTypeController(IAssigmentTypeAppService assignmentTypeAppService) : AbpControllerBase
    {
        [HttpPost("assignment-type")]
        public async Task<IActionResult> CreateAssignmentTypeAsync([FromBody] CreateAssigmentTypeDto createAssignmentTypeDto)
        {
            var assignmentType = await assignmentTypeAppService.CreateAssigmentTypeAsync(createAssignmentTypeDto);
            return Ok(assignmentType);
        }

        [HttpGet("assignment-types/{academicMediumId}")]
        public async Task<IActionResult> GetAllAssignmentTypesAsync(Guid academicMediumId)
        {
            var result = await assignmentTypeAppService.GetListAsync(academicMediumId);
            return Ok(result);
        }

        [HttpPut("assignment-type/{id}")]
        public async Task<IActionResult> UpdateAssignmentType(Guid id, [FromBody] UpdateAssigmentTypeDto updateAssignmentTypeDto)
        {
            var result = await assignmentTypeAppService.UpdateAsync(id, updateAssignmentTypeDto);
            return Ok(result);
        }

        [HttpDelete("assignment-type/{id}")]
        public async Task<IActionResult> DeleteAssignmentType(Guid id)
        {
            await assignmentTypeAppService.DeleteAsync(id);
            return Ok(new { Message = "Assignment type has been deleted successfully." });
        }
    }
}
