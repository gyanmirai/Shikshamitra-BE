using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.Students.Attendance;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.AttendanceAndLeaves;

public class StudentLeaveService(
	StudentLeaveManager studentLeaveManager,
	IRepository<StudentLeave,Guid> studentLeaveRepository) 
	: CrudAppService<StudentLeave, StudentLeaveDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateStudentLeaveDto>(studentLeaveRepository)
{
	public override async Task<StudentLeaveDto> UpdateAsync(Guid id, CreateUpdateStudentLeaveDto input)
	{
		StudentLeave studentLeave = await studentLeaveRepository.GetAsync(id);

		// check if the leave request is pending then only update the leave request
		studentLeaveManager.IsLeaveStatusIsPending(studentLeave);

		return  await base.UpdateAsync(id, input);
	}

	public async Task<StudentLeaveDto> UpdateLeaveRequestStatusAsync(Guid id, LeaveApprovalDto input)
	{
		StudentLeave studentLeave = await studentLeaveRepository.GetAsync(id);
		studentLeave =
			await studentLeaveManager.UpdateLeaveRequestStatusAsync(studentLeave, input.Status,
				input.RejectionReason);
		return ObjectMapper.Map<StudentLeave, StudentLeaveDto>(studentLeave);
	}
}
