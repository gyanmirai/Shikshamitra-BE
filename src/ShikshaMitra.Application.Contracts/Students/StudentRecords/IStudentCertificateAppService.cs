using ShikshaMitra.Common;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShikshaMitra.Students.StudentRecords;

public interface IStudentCertificateAppService
{
	Task UploadCertificateAsync(Guid studentId, byte[] pdfContent, string fileName, string? reason);
	Task<List<StudentCertificateDto>> GetCertificatesAsync(Guid studentId);
	Task<CustomFileResult> DownloadCertificateFileAsync(Guid certificateId);
}
