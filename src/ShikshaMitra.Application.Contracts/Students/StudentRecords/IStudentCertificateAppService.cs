using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentRecords;

public interface IStudentCertificateAppService
{
	Task UploadCertificateAsync(int studentId, byte[] pdfContent, string fileName, string? reason);
	Task<List<StudentCertificateDto>> GetCertificatesAsync(int studentId);
	Task<StudentCertificateFileResult> DownloadCertificateFileAsync(Guid certificateId);
}
