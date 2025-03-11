using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.TenantSettings;

public interface IShiftManagementAppService : ICrudAppService<ShiftManagementDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateShiftManagementDto>
{
}
