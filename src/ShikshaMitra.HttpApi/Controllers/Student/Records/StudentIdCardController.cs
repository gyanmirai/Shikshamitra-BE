using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController]
[Authorize]
[Route("api/students/records/ids")]
public class StudentIdCardController(IStudentIdCardAppService service) : AbpController
{
	[HttpGet("templates")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View)]
	public async Task<Result<StudentIdCardTemplateDto>> GetTemplateAsync() => Result.Success(await service.GetTemplateAsync());
	[HttpPut("templates")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add)]
	public async Task<Result<StudentIdCardTemplateDto>> SaveTemplateAsync([FromBody] StudentIdCardTemplateDto input) => Result.Success(await service.SaveTemplateAsync(input));
	[HttpGet("templates/list")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View)]
	public async Task<Result<List<StudentIdCardTemplateDto>>> TemplatesAsync() => Result.Success(await service.GetTemplatesAsync());
	[HttpGet("templates/{id:guid}")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View)]
	public async Task<Result<StudentIdCardTemplateDto>> TemplateAsync(Guid id) => Result.Success(await service.GetTemplateByIdAsync(id));
	[HttpPost("templates")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add)]
	public async Task<Result<StudentIdCardTemplateDto>> CreateTemplateAsync([FromBody] StudentIdCardTemplateDto input) => Result.Success(await service.CreateTemplateAsync(input));
	[HttpPut("templates/{id:guid}")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add)]
	public async Task<Result<StudentIdCardTemplateDto>> UpdateTemplateAsync(Guid id, [FromBody] StudentIdCardTemplateDto input) => Result.Success(await service.UpdateTemplateAsync(id, input));
	[HttpDelete("templates/{id:guid}")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Delete)]
	public async Task<IActionResult> DeleteTemplateAsync(Guid id) { await service.DeleteTemplateAsync(id); return NoContent(); }
	[HttpPost("preview")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View)]
	public async Task<Result<List<StudentIdCardDto>>> PreviewAsync([FromBody] StudentIdCardPreviewRequestDto input) => Result.Success(await service.PreviewAsync(input));
	[HttpPost("issue")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add)]
	public async Task<Result<List<StudentIdCardDto>>> IssueAsync([FromBody] IssueStudentIdCardsDto input) => Result.Success(await service.IssueAsync(input));
	[HttpPost("export")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add)]
	public async Task<IActionResult> ExportAsync([FromBody] StudentIdCardsPdfRequestDto input) { var pdf = await service.ExportPdfAsync(input); return File(pdf.Content, pdf.ContentType, pdf.FileName); }
	[HttpGet("students/{studentId:guid}/history")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View)]
	public async Task<Result<List<StudentIdCardDto>>> HistoryAsync(Guid studentId) => Result.Success(await service.GetHistoryAsync(studentId));
	[HttpGet("{id:guid}/pdf")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View)]
	public async Task<IActionResult> PdfAsync(Guid id) { var pdf = await service.GetPdfAsync(id); return File(pdf.Content, pdf.ContentType, pdf.FileName); }
	[HttpPost("{id:guid}/reprint")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add)]
	public async Task<IActionResult> ReprintAsync(Guid id, [FromBody] StudentDocumentActionDto input) { var pdf = await service.ReprintAsync(id, input); return File(pdf.Content, pdf.ContentType, pdf.FileName); }
	[HttpPost("{id:guid}/void")][Authorize(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Delete)]
	public async Task<Result<StudentIdCardDto>> VoidAsync(Guid id, [FromBody] StudentDocumentActionDto input) => Result.Success(await service.VoidAsync(id, input));
}
