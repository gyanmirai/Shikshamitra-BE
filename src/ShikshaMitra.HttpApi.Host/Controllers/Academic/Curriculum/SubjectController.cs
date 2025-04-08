using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.Curriculum;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.Curriculum;

[Route("api/subject")]
public class SubjectController(ISubjectAppService subjectAppService) : AbpController
{
	[HttpPost]
	public async Task<IActionResult> CreateSubject([FromBody] CreateSubjectDto input)
	{
		// Call the application service to create the subject
		await subjectAppService.CreateAsync(input);
		return Ok(new { Message = "Created successfully." });
	}
}
