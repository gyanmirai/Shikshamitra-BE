using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public interface IEmployeeLeaveRepository : IRepository<EmployeeLeave, Guid>
{
    /// <summary>
    /// Check if employee has overlapping leave requests
    /// </summary>
    Task<bool> HasOverlappingLeaveAsync(
        Guid employeeId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        Guid? excludeLeaveId = null);

    /// <summary>
    /// Get total approved leave days for employee in a specific period
    /// </summary>
    Task<int> GetApprovedLeaveDaysAsync(
        Guid employeeId,
        Guid leaveTypeId,
        int year);
}