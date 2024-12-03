using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

[Route("api/employee")]
public class EmployeeController(IEmployeeAppService employeeAppService) : AbpController
{
	[HttpPost("{employeeId}/documents")]
	public async Task<IActionResult> UploadDocuments(Guid employeeId, [FromForm] IList<IFormFile>? files)
	{
		if (files == null || files.Count == 0)
		{
			return BadRequest("No files were uploaded.");
		}

		var documentDtos = new List<DocumentDto>();

		foreach (var file in files)
		{
			if (file.Length > 0)
			{
				using (var memoryStream = new MemoryStream())
				{
					await file.CopyToAsync(memoryStream);

					documentDtos.Add(new DocumentDto
					{
						FileName = file.FileName,
						ContentType = file.ContentType,
						Content = memoryStream.ToArray(),
						Size = file.Length
					});
				}
			}
		}

		// Pass the data to the application service
		await employeeAppService.UploadDocumentsAsync(employeeId, documentDtos);

		return Ok(new { Message = "Files uploaded successfully." });
	}

	[HttpGet("{employeeId}/documents/{documentId}")]
	public async Task<IActionResult> GetDocument(Guid employeeId, Guid documentId)
	{
		var document = await employeeAppService.GetDocumentAsync(employeeId, documentId);

		return File(document.Content, document.ContentType, document.FileName);
	}

	[HttpGet("{employeeId}/documents")]
	public async Task<IActionResult> GetDocuments(Guid employeeId)
	{
		List<DocumentDto> documents = await employeeAppService.GetDocumentsAsync(employeeId);
		if (documents.Count == 0)
		{
			return NotFound();
		}

		return Ok(documents);
	}
}