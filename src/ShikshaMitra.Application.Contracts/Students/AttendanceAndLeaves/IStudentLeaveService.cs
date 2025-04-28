using ShikshaMitra.HR.AttendanceAndLeaves;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.AttendanceAndLeaves;

public interface IStudentLeaveService : ICrudAppService<StudentLeaveDto, Guid, PagedAndSortedResultRequestDto,
	CreateUpdateStudentLeaveDto>
{
}
