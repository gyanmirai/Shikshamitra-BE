using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp;

namespace ShikshaMitra.Students.StudentRecords;

[RemoteService(false)]
public class StudentCertificateAppService(
	IRepository<StudentCertificate, Guid> certificateRepository,
	ICurrentTenant currentTenant) : ShikshaMitraAppService, IStudentCertificateAppService
{
	public async Task UploadCertificateAsync(int studentId, byte[] pdfContent, string fileName, string? reason)
	{
		if (pdfContent == null || pdfContent.Length == 0)
		{
			throw new UserFriendlyException("PDF content is required.");
		}

		Guid tenantId = currentTenant.Id ?? Guid.Empty;

		// Check if a certificate already exists for the student.
		var existingCertificate = await certificateRepository.FirstOrDefaultAsync(x => x.StudentId == studentId);
		if (existingCertificate == null)
		{
			var newCertificate = new StudentCertificate(
				Guid.NewGuid(),
				tenantId,
				studentId,
				pdfContent,
				fileName,
				DateTimeOffset.UtcNow,
				reason);
			await certificateRepository.InsertAsync(newCertificate, autoSave: true);
		}
		else
		{
			existingCertificate.PdfContent = pdfContent;
			existingCertificate.FileName = fileName;
			existingCertificate.CreationTime = DateTimeOffset.UtcNow;
			existingCertificate.Reason = reason;
			await certificateRepository.UpdateAsync(existingCertificate, autoSave: true);
		}
	}


	public async Task<List<StudentCertificateDto>> GetCertificatesAsync(int studentId)
	{
		var certificates = await certificateRepository.GetListAsync(x => x.StudentId == studentId);
		var dtos = new List<StudentCertificateDto>();
		foreach (var certificate in certificates)
		{
			dtos.Add(new StudentCertificateDto
			{
				Id = certificate.Id,
				StudentId = certificate.StudentId,
				PdfContent = certificate.PdfContent,
				FileName = certificate.FileName,
				Reason = certificate.Reason,
				CreationTime = certificate.CreationTime
			});
		}
		return dtos;
	}


	public async Task<StudentCertificateFileResult> DownloadCertificateFileAsync(Guid certificateId)
	{
		var certificate = await certificateRepository.GetAsync(certificateId);
		if (certificate == null || certificate.PdfContent == null)
		{
			throw new UserFriendlyException("Certificate not found.");
		}
		// You can adjust the content type if needed.
		return new StudentCertificateFileResult(certificate.PdfContent, certificate.FileName, "application/pdf");
	}
}
