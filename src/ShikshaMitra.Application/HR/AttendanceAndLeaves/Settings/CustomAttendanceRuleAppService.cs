using ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class CustomAttendanceRuleAppService(ICustomAttendanceRuleRepository customAttendanceRuleRepository)
	: CrudAppService<CustomAttendanceRule, CustomAttendanceRuleDto, Guid, PagedAndSortedResultRequestDto,
		CreateUpdateCustomAttendanceRuleDto>(customAttendanceRuleRepository), ICustomAttendanceRuleAppServices
{
}