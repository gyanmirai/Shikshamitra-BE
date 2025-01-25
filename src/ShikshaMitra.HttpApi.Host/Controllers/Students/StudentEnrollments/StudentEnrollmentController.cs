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

namespace ShikshaMitra.Controllers.Students.StudentEnrollments;


[Route("api/student-enrollment")]
[Authorize]

public class StudentEnrollmentController(IStudentEnrollmentAppService studentEnrollmentAppService) : AbpController
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
}