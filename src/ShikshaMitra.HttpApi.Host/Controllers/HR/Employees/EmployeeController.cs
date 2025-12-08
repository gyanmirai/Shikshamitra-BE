using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Employees;

[Route("api/employee")]
[Authorize]
public class EmployeeController(IEmployeeAppService employeeAppService) : AbpController
{
	[HttpPut("{employeeId}/documents")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task<IActionResult> UploadDocuments(Guid employeeId, [FromForm] IList<IFormFile>? files)
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
		await employeeAppService.UploadDocumentsAsync(employeeId, documentDtos);

		return Ok(new { Message = "Files uploaded successfully." });
	}

	[HttpGet("{employeeId}/documents/{documentId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public async Task<IActionResult> GetDocument(Guid employeeId, Guid documentId)
	{
		var document = await employeeAppService.GetDocumentAsync(employeeId, documentId);

		return Ok(document);
	}

	[HttpGet("{employeeId}/documents")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.View)]
	public async Task<IActionResult> GetDocuments(Guid employeeId)
	{
		List<DocumentMetadataDto> documents = await employeeAppService.GetDocumentMetadataAsync(employeeId);

		return Ok(documents);
	}

	[HttpPut("{employeeId}/documents/{documentId}")]
	[Consumes("multipart/form-data")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task<IActionResult> UpdateDocument(Guid employeeId, Guid documentId, IFormFile file)
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

		await employeeAppService.UpdateDocumentAsync(employeeId, documentId, documentDto);

		return Ok(new { Message = "File updated successfully." });
	}

	[HttpPut("{employeeId}/payroll-structure")]
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Employee_EmployeeList.Add)]
	public async Task<IActionResult> UpdatePayrollStructure(Guid employeeId, [FromBody] CreateUpdateEmployeePayrollStructureDto input)
	{
		await employeeAppService.UpdatePayrollStructureAsync(employeeId, input);
		return Ok(new { Message = "Payroll structure updated successfully." });
	}

}