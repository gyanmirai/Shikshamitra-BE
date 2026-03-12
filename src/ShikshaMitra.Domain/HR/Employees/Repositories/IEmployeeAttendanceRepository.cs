using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Employees;

public interface IEmployeeAttendanceRepository : IRepository<EmployeeAttendance, Guid>
{
}