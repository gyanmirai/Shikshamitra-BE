using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Attendance;

public interface IStudentAttendanceRepository : IRepository<StudentAttendance, Guid>
{
}
