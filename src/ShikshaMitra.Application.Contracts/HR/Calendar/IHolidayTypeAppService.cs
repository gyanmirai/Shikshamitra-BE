using System;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Calendar;

/// <summary>
/// Application service interface for HolidayType management
/// </summary>
public interface IHolidayTypeAppService : ICrudAppService<HolidayTypeDto, Guid, GetHolidayTypeListRequestDto, CreateUpdateHolidayTypeDto>
{
}
