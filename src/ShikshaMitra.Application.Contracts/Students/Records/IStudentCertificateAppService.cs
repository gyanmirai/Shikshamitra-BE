using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentRecords;

namespace ShikshaMitra.Students.Records;

public interface IStudentCertificateAppService
{
	Task<List<string>> GetSupportedPlaceholdersAsync();
	Task<StudentCertificateTemplateDto> GetTemplateAsync(StudentCertificateType certificateType);
	Task<StudentCertificateTemplateDto> UpsertTemplateAsync(StudentCertificateType certificateType, StudentCertificateTemplateDto input);
	Task<StudentCertificateResponseDto> PreviewAsync(StudentCertificatePreviewRequestDto input);
	Task<StudentCertificateResponseDto> GenerateAsync(CreateStudentCertificateDto input);
	Task<StudentCertificateResponseDto> GetAsync(Guid certificateId);
	Task<List<StudentCertificateHistoryDto>> GetHistoryAsync(Guid studentId);
	Task<StudentCertificateResponseDto> DuplicateAsync(Guid certificateId);
	Task<StudentCertificatePdfDto> GeneratePdfAsync(Guid certificateId);
}
