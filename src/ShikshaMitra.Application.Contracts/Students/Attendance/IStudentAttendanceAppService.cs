using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Attendance;

public interface IStudentAttendanceAppService : ICrudAppService<StudentAttendanceDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentAttendanceDto>
{
}
