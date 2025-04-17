using ShikshaMitra.Students.Attendance.settings;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.Attendance.Settings;

public class AttendanceTypeAppService(IAttendanceTypeRepository attendanceTypeRepository) : CrudAppService<AttendanceType,AttendanceTypeDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateAttendanceTypeDto>(attendanceTypeRepository), IAttendanceTypeAppService
{
	
}