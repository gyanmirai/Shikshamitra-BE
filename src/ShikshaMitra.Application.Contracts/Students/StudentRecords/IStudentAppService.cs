using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentRecords.BulkUpload;
using System;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentRecords;

public interface IStudentAppService : ICrudAppService<StudentDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentDto>
{
    Task<CustomFileResult> GenerateCertificate(Guid studentId, StudentCertificateType certificateType);
    Task<CustomFileResult> GenerateReportAsync(StudentReportType reportType);
    Task<StudentDto> UpdateStudentStatusAsync(Guid studentId, StudentStatus newStatus);
    Task<StudentCsvUploadResultDto> ImportStudentsFromCsvAsync(Stream fileStream, string fileName);
}
