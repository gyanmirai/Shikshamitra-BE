using ShikshaMitra.Common;
using ShikshaMitra.Students.StudentEnrollments;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentRecords;

public interface IStudentAppService : ICrudAppService<StudentDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentDto>
{
	Task<CustomFileResult> GenerateReportAsync(StudentReportType reportType);
	Task<StudentDto> UpdateStudentStatusAsync(Guid studentId, StudentStatus newStatus);
}
