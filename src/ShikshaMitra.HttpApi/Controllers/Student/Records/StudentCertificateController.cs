using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Students.Records;

[ApiController]
[Authorize]
[Route("api/students/records/certificates")]
public class StudentCertificateController(
	IStudentCertificateAppService certificateAppService) : AbpController
{
	[HttpGet("placeholders")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View)]
	public async Task<Result<List<string>>> GetSupportedPlaceholders()
	{
		return Result.Success(await certificateAppService.GetSupportedPlaceholdersAsync());
	}

	[HttpGet("templates/{certificateType}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View)]
	public async Task<Result<StudentCertificateTemplateDto>> GetTemplate(StudentCertificateType certificateType)
	{
		return Result.Success(await certificateAppService.GetTemplateAsync(certificateType));
	}

	[HttpPut("templates/{certificateType}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.Add)]
	public async Task<Result<StudentCertificateTemplateDto>> UpsertTemplate(
		StudentCertificateType certificateType,
		[FromBody] StudentCertificateTemplateDto input)
	{
		return Result.Success(await certificateAppService.UpsertTemplateAsync(certificateType, input));
	}

	[HttpPost("preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View)]
	public async Task<Result<StudentCertificateResponseDto>> Preview([FromBody] StudentCertificatePreviewRequestDto input)
	{
		return Result.Success(await certificateAppService.PreviewAsync(input));
	}

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.Add)]
	public async Task<Result<StudentCertificateResponseDto>> Generate([FromBody] CreateStudentCertificateDto input)
	{
		return Result.Success(await certificateAppService.GenerateAsync(input));
	}

	[HttpGet("{certificateId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View)]
	public async Task<Result<StudentCertificateResponseDto>> Get(Guid certificateId)
	{
		return Result.Success(await certificateAppService.GetAsync(certificateId));
	}

	[HttpGet("students/{studentId}/history")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View)]
	public async Task<Result<List<StudentCertificateHistoryDto>>> GetHistory(Guid studentId)
	{
		return Result.Success(await certificateAppService.GetHistoryAsync(studentId));
	}

	[HttpPost("{certificateId}/duplicate")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.Add)]
	public async Task<Result<StudentCertificateResponseDto>> Duplicate(Guid certificateId, [FromBody] StudentDocumentActionDto input)
	{
		return Result.Success(await certificateAppService.DuplicateAsync(certificateId, input));
	}
	[HttpPost("{certificateId:guid}/reprint")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.Add)]
	public async Task<IActionResult> Reprint(Guid certificateId, [FromBody] StudentDocumentActionDto input)
	{
		var pdf = await certificateAppService.ReprintAsync(certificateId, input);
		return File(pdf.Content, pdf.ContentType, pdf.FileName);
	}
	[HttpPost("{certificateId:guid}/void")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.Delete)]
	public async Task<Result<StudentCertificateResponseDto>> Void(Guid certificateId, [FromBody] StudentDocumentActionDto input) => Result.Success(await certificateAppService.VoidAsync(certificateId, input));

	[HttpGet("{certificateId}/pdf")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Records_Certificates.View)]
	public async Task<IActionResult> GeneratePdf(Guid certificateId)
	{
		var pdf = await certificateAppService.GeneratePdfAsync(certificateId);
		return File(pdf.Content, pdf.ContentType, pdf.FileName);
	}
}
