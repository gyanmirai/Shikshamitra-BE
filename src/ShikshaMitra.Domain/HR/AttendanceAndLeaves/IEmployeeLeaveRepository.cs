using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public interface IEmployeeLeaveRepository : IRepository<EmployeeLeave, Guid>
{
}