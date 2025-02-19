using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Students.StudentRecords;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentRecords;


[Route("api/students")]

public class StudentController(IStudentCertificateAppService studentCertificateAppService) : AbpController
{
	[HttpPost("upload-certificate")]
	[Consumes("multipart/form-data")]
	public async Task<IActionResult> UploadCertificate(
	[FromQuery] int studentId,
	[FromForm] IList<IFormFile>? files,
	[FromForm] string? reason)
	{
		if (files == null || files.Count == 0)
		{
			return BadRequest("PDF file is required.");
		}

		// Use the first file in the list
		var pdf = files.First();

		if (!pdf.ContentType.Equals("application/pdf", System.StringComparison.OrdinalIgnoreCase))
		{
			return BadRequest("Only PDF files are accepted.");
		}

		byte[] pdfContent;
		await using (var memoryStream = new MemoryStream())
		{
			await pdf.CopyToAsync(memoryStream);
			pdfContent = memoryStream.ToArray();
		}

		await studentCertificateAppService.UploadCertificateAsync(studentId, pdfContent, pdf.FileName, reason);
		return Ok("PDF certificate uploaded successfully.");
	}


	[HttpGet("get-certificates")]
	public async Task<IActionResult> GetCertificates([FromQuery] int studentId)
	{
		List<StudentCertificateDto> certificates = await studentCertificateAppService.GetCertificatesAsync(studentId);
		if (certificates == null || certificates.Count == 0)
		{
			return NotFound("No certificates found for the given student ID.");
		}
		return Ok(certificates);
	}

	[HttpGet("download-certificate")]
	public async Task<IActionResult> DownloadCertificate([FromQuery] Guid certificateId)
	{
		var fileResult = await studentCertificateAppService.DownloadCertificateFileAsync(certificateId);
		return File(fileResult.FileContent, fileResult.ContentType, fileResult.FileName);
	}
}
