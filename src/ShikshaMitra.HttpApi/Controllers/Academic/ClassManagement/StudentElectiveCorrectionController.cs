using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Academic.ClassManagement;

[ApiController]
[Route("api/class-management/student-elective-assignments/corrections")]
[Authorize(ShikshaMitraSubmodulePermissions.ClassManagement.StudentElectiveAssignment.Add)]
public class StudentElectiveCorrectionController(IStudentElectiveCorrectionAppService service) : AbpController
{
	[HttpPost("preview")]
	public Task<StudentElectiveCorrectionReviewDto> PreviewAsync(StudentElectiveCorrectionInput input) => service.PreviewAsync(input);

	[HttpPost("confirm")]
	public async Task<IActionResult> ConfirmAsync(ConfirmStudentElectiveCorrectionInput input)
	{
		await service.ConfirmAsync(input);
		return NoContent();
	}
}
