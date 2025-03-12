using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Collections.Generic;
using System.IO;
using Volo.Abp.AspNetCore.Mvc;
using ShikshaMitra.Students.FollowUps;
using Volo.Abp.Application.Dtos;
using ShikshaMitra.Students.StudentRecords;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;


[Route("api/student-enrollment")]
[Authorize]

public class StudentEnrollmentController(IStudentEnrollmentAppService studentEnrollmentAppService, IFollowUpAppService followUpAppService) : AbpController
{
	[HttpPut("{studentEnrollmentId}/documents")]
	public async Task<IActionResult> UploadDocuments(Guid studentEnrollmentId, [FromForm] IList<IFormFile>? files)
	{
		if (files == null || files.Count == 0)
		{
			return BadRequest("No files were uploaded.");
		}

		var documentDtos = new List<DocumentDto>();

		foreach (var file in files)
		{
			if (file.Length <= 0)
			{
				continue;
			}

			await using var memoryStream = new MemoryStream();
			await file.CopyToAsync(memoryStream);

			documentDtos.Add(new DocumentDto
			{
				FileName = file.FileName,
				ContentType = file.ContentType,
				Content = memoryStream.ToArray(),
				Size = file.Length
			});
		}

		// Pass the data to the application service
		await studentEnrollmentAppService.UploadDocumentsAsync(studentEnrollmentId, documentDtos);

		return Ok(new { Message = "Files uploaded successfully." });
	}

	[HttpGet("{studentEnrollmentId}/documents/{documentId}")]
	public async Task<IActionResult> GetDocument(Guid studentEnrollmentId, Guid documentId)
	{
		var document = await studentEnrollmentAppService.GetDocumentAsync(studentEnrollmentId, documentId);

		return Ok(document);
	}

	[HttpGet("{studentEnrollmentId}/documents")]
	public async Task<IActionResult> GetDocuments(Guid studentEnrollmentId)
	{
		List<DocumentMetadataDto> documents = await studentEnrollmentAppService.GetDocumentMetadataAsync(studentEnrollmentId);

		return Ok(documents);
	}

	[HttpPut("{studentEnrollmentId}/documents/{documentId}")]
	[Consumes("multipart/form-data")]
	public async Task<IActionResult> UpdateDocument(Guid studentEnrollmentId, Guid documentId, IFormFile file)
	{
		if (file.Length <= 0)
		{
			return BadRequest("No file was uploaded.");
		}

		await using var memoryStream = new MemoryStream();
		await file.CopyToAsync(memoryStream);

		var documentDto = new DocumentDto
		{
			FileName = file.FileName,
			ContentType = file.ContentType,
			Content = memoryStream.ToArray(),
			Size = file.Length
		};

		await studentEnrollmentAppService.UpdateDocumentAsync(studentEnrollmentId, documentId, documentDto);

		return Ok(new { Message = "File updated successfully." });
	}

	[HttpGet("follow-ups")]
	public async Task<PagedResultDto<StudentEnrollmentDto>> GetStudentEnrollmentsWithFollowUps()
	{
		// read academic year from the cookie
		return await studentEnrollmentAppService.GetStudentsFollowUp(Guid.Parse(Request.Cookies["AcademicYearId"]!));
	}

	[HttpPost("{studentEnrollmentId}/add-follow-up")]
	public async Task<FollowUpWithStatusAndDateDto> AddFollowUp(Guid studentEnrollmentId, [FromBody] AddFollowUpDto input) => await followUpAppService.AddFollowUpAsync(studentEnrollmentId, input);

	[HttpGet("{studentEnrollmentId}/follow-up")]
	public async Task<FollowUpWithStatusAndDateDto> GetFollowUpDetails(Guid studentEnrollmentId) => await followUpAppService.GetFollowUpDetailsAsync(studentEnrollmentId);

	[HttpPut("{studentEnrollmentId}/update-status-due-date")]
	public async Task UpdateStatus(Guid studentEnrollmentId, FollowUpStatusType status, DateTimeOffset? dueDate) => await followUpAppService.UpdateStatusAndDueDateAsync(studentEnrollmentId, status, dueDate);

	[HttpGet("report")]
	public async Task<IActionResult> GenerateReport([FromQuery] StudentEnrollmentReportType reportType)
	{
		CustomFileResult reportResult = await studentEnrollmentAppService.GenerateReportAsync(reportType);

		if (reportResult == null || reportResult.FileContent == null)
		{
			return NotFound("Report could not be generated.");
		}

		return Ok(reportResult);
	}
}