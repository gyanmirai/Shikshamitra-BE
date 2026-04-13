using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public interface ICustomAttendanceRuleAppService : ICrudAppService<CustomAttendanceRuleDto, Guid,
	PagedAndSortedResultRequestDto,
	CreateUpdateCustomAttendanceRuleDto>
{
}