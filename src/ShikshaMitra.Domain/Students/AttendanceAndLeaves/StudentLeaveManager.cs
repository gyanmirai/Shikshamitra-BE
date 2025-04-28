using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.Students.Attendance;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.AttendanceAndLeaves;

public class StudentLeaveManager(IRepository<StudentLeave, Guid> studentLeaveRepository)
	: DomainService
{
	public StudentLeave IsLeaveStatusIsPending(StudentLeave studentLeave)
	{
		if (studentLeave.Status != EmployeeLeaveStatusType.Pending)
		{
			throw new BusinessException("Leave request is already approved or rejected");
		}

		return studentLeave;
	}

	public async Task<StudentLeave> UpdateLeaveRequestStatusAsync(StudentLeave studentLeave,
		EmployeeLeaveStatusType status, string? rejectionReason)
	{
		studentLeave.UpdateLeaveStatus(status, rejectionReason);
		return await studentLeaveRepository.UpdateAsync(studentLeave);
	}
}
