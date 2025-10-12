using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Examination.Setting;
using System;
using System.Threading.Tasks;
namespace ShikshaMitra.Controllers.Academic.Examination.Setting;

[Route("api/academic/examination/setting")]
[Authorize]
[ApiController]
public class GradeScaleController(IGradeScaleAppService gradeScaleAppService) : ControllerBase
{
	[HttpGet("grade-scales/{academicMediumId}")]
	public async Task<IActionResult> GetAllGradeScales(Guid academicMediumId)
	{
		var gradescale = await gradeScaleAppService.GetGradeScaleListAsync(academicMediumId);

		return Ok(gradescale);
	}

	[HttpGet("grade-scale/{id}")]
	public async Task<IActionResult> GetGradeScaleById(Guid id)
	{
		var gradeScale = await gradeScaleAppService.GetGradeScaleByIdAsync(id);

		if (gradeScale == null)
		{
			return NotFound();
		}

		return Ok(gradeScale);
	}


	[HttpPost("grade-scale")]
	public async Task<IActionResult> CreateGradeScale(
		[FromBody] CreateGradeScaleDto createGradeScaleDto
	)
	{
		var gradescale = await gradeScaleAppService.CreateGradeScaleAsync(createGradeScaleDto);

		return Ok(gradescale);
	}

	[HttpPut("grade-scale/{id}")]
	public async Task<IActionResult> UpdateGradeScale(
		Guid id,
		[FromBody] UpdateGradeScaleDto updateGradeScaleDto
	)
	{
		var gradescale = await gradeScaleAppService.UpdateGradeScaleAsync(id, updateGradeScaleDto);

		return Ok(gradescale);
	}

    [HttpDelete("grade-scale/{id}")]
    public async Task<IActionResult> DeleteGradeScale(Guid id)
    {
		await gradeScaleAppService.DeleteGradeScaleAsync(id);

		return Ok(new
		{
			message = "GradeScale has been deleted successfully.",
		});
	}
}
