using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Application.Contracts.HR.Calendar.CalendarView;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.HR.Calendar.CalendarView;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Calendar.View)]
public class CalendarViewAppService(
	CalendarViewReadService readService,
	CalendarItemDetailsService detailsService) : ApplicationService, ICalendarViewAppService
{
	public Task<PagedResultDto<CalendarViewItemDto>> GetViewAsync(GetCalendarViewRequestDto input) =>
		readService.GetViewAsync(input);

	public Task<CalendarItemDetailsDto> GetItemDetailsAsync(Guid itemId) =>
		detailsService.GetItemDetailsAsync(itemId);
}
