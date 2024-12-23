using ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;
using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeavePolicyAppService(ILeavePolicyRepository leavePolicyRepository) : CrudAppService<HR.AttendanceAndLeaves.Settings.LeavePolicy, LeavePolicyDto, Guid, PagedAndSortedResultRequestDto,CreateUpdateLeavePolicyDto>(leavePolicyRepository), ILeavePolicyAppService
{
	
}	