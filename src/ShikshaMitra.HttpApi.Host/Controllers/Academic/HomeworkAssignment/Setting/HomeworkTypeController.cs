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
	public class HomeworkTypeController(IHomeworkTypeAppService homeworkTypeAppService) : AbpControllerBase
	{
		[HttpPost("homework-type")]
		public async Task<IActionResult> CreateHomeworkTypeAsync([FromBody] CreateHomeworkTypeDto createHomeworkTypeDto)
		{
			var homeworktype = await homeworkTypeAppService.CreateHomeWorkTypeAsync(createHomeworkTypeDto);
			return Ok(homeworktype);
		}

		[HttpGet("homework-types/{academicMediumId}")]
		public async Task<IActionResult> GetHomeworkTypeAllAsync(Guid academicMediumId)
		{
			var result = await homeworkTypeAppService.GetListAsync(academicMediumId);
			return Ok(result);
		}

		[HttpPut("homework-type/{id}")]
		public async Task<IActionResult> UpdateHomeworkType(Guid id, [FromBody] UpdateHomeworkTypeDto updateHomeworkTypeDto)
		{
			var result = await homeworkTypeAppService.UpdateAsync(id, updateHomeworkTypeDto);
			return Ok(result);
		}

		[HttpDelete("homework-type/{id}")]
		public async Task<IActionResult> DeleteHomeworkType(Guid id)
		{
			await homeworkTypeAppService.DeleteAsync(id);
			return Ok(new { Message = "Homework type has been deleted successfully." });
		}
	}

}
