using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Attendance.Settings;

public interface IAttendanceTypeAppService : ICrudAppService<AttendanceTypeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateAttendanceTypeDto>
{
	
}