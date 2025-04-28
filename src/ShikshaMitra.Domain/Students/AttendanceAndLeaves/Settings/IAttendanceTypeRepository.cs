using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Attendance.settings;

public interface IAttendanceTypeRepository : IRepository<AttendanceType, Guid>
{
	
}