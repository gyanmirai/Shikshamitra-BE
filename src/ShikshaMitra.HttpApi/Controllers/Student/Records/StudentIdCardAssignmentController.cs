using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController]
[Authorize]
[Route("api/students/records/ids/assignments")]
public class StudentIdCardAssignmentController(IStudentIdCardAssignmentAppService service) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View)]
	public async Task<Result<PagedResultDto<StudentIdCardAssignmentDto>>> GetListAsync([FromQuery] StudentIdCardAssignmentListQueryDto input) =>
		Result.Success(await service.GetAssignmentsAsync(input));

	[HttpPut("{academicYearId:guid}/{standardId:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add)]
	public async Task<Result<StudentIdCardAssignmentDto>> SaveAsync(Guid academicYearId, Guid standardId, [FromBody] SaveStudentIdCardAssignmentDto input) =>
		Result.Success(await service.SaveAssignmentAsync(academicYearId, standardId, input));

	[HttpDelete("{academicYearId:guid}/{standardId:guid}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Delete)]
	public Task DeleteAsync(Guid academicYearId, Guid standardId, [FromQuery] string? concurrencyStamp) =>
		service.DeleteAssignmentAsync(academicYearId, standardId, concurrencyStamp);

	[HttpGet("{academicYearId:guid}/{standardId:guid}/roster")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View)]
	public async Task<Result<PagedResultDto<StudentIdCardAssignmentRosterStudentDto>>> GetRosterAsync(Guid academicYearId, Guid standardId,
		[FromQuery] StudentIdCardAssignmentRosterQueryDto input) =>
		Result.Success(await service.GetAssignmentRosterAsync(academicYearId, standardId, input));

	[HttpPost("{academicYearId:guid}/{standardId:guid}/preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View)]
	public async Task<Result<List<StudentIdCardDto>>> PreviewAsync(Guid academicYearId, Guid standardId, [FromBody] StudentIdCardAssignmentPreviewDto input) =>
		Result.Success(await service.PreviewAssignmentAsync(academicYearId, standardId, input));

	[HttpPost("{academicYearId:guid}/{standardId:guid}/export")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add)]
	public async Task<IActionResult> ExportAsync(Guid academicYearId, Guid standardId, [FromBody] StudentIdCardAssignmentExportDto input)
	{
		var pdf = await service.ExportAssignmentAsync(academicYearId, standardId, input);
		return File(pdf.Content, pdf.ContentType, pdf.FileName);
	}

	[HttpPost("{academicYearId:guid}/{standardId:guid}/issue")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add)]
	public async Task<Result<List<StudentIdCardDto>>> IssueAsync(Guid academicYearId, Guid standardId, [FromBody] StudentIdCardAssignmentIssueDto input) =>
		Result.Success(await service.IssueAssignmentAsync(academicYearId, standardId, input));
}
