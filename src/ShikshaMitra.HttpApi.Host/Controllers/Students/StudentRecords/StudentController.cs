using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.StudentRecords.BulkUpload;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Validation;

namespace ShikshaMitra.Controllers.Students.StudentRecords;


[Route("api/students")]

public class StudentController(IStudentCertificateAppService studentCertificateAppService, IStudentAppService studentAppService) : AbpController
{
	[HttpPost("upload-certificate")]
	[Consumes("multipart/form-data")]
	public async Task<IActionResult> UploadCertificate(
	[FromQuery] Guid studentId,
	[FromForm] IList<IFormFile>? files,
	[FromForm] string? reason)
	{
		if (files == null || files.Count == 0)
		{
			return BadRequest("PDF file is required.");
		}

		// Use the first file in the list
		var pdf = files[0];

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
	public async Task<IActionResult> GetCertificates([FromQuery] Guid studentId)
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
		CustomFileResult? fileResult = await studentCertificateAppService.DownloadCertificateFileAsync(certificateId);
		return File(fileResult.FileContent, fileResult.ContentType, fileResult.FileName);
	}

	[HttpPost("student-report")]
	public async Task<IActionResult> GenerateReport([FromQuery] StudentReportType reportType, [FromBody] StudentReportDto reportDto)
	{
		CustomFileResult reportResult = await studentAppService.GenerateReportAsync(reportType, reportDto);

		if (reportResult == null || reportResult.FileContent == null)
		{
			return NotFound("Report could not be generated.");
		}

		return Ok(reportResult);
	}

	[HttpGet("generate-certificate")]
	public async Task<IActionResult> GenerateCertificate([FromQuery] Guid studentId, [FromQuery] StudentCertificateType certificateType)
	{
		CustomFileResult reportResult = await studentAppService.GenerateCertificate(studentId, certificateType);

		if (reportResult == null || reportResult.FileContent == null)
		{
			return NotFound("Report could not be generated.");
		}

		return Ok(reportResult);
	}
	[HttpPost("bulk-upload")]
	[DisableValidation] // Disable validation for the entire action
	[Consumes("multipart/form-data")]
	public async Task<StudentCsvUploadResultDto> UploadStudentsCsv([FromForm] IList<IFormFile>? files)
	{
		if (files == null || files.Count == 0)
		{
			throw new UserFriendlyException("CSV file is required.");
		}

		// Use the first file in the list
		var csv = files[0];

		if (!csv.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
		{
			throw new UserFriendlyException("Only CSV files are supported.");
		}

		byte[] csvContent;
		await using (var memoryStream = new MemoryStream())
		{
			await csv.CopyToAsync(memoryStream);
			csvContent = memoryStream.ToArray();
		}


		return await studentAppService.ImportStudentsFromCsvAsync(csvContent, csv.FileName);
	}
}
