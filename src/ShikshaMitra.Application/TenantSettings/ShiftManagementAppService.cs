using System;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings;

public class ShiftManagementAppService(IRepository<ShiftManagement, Guid> repository)
	: CrudAppService<ShiftManagement, ShiftManagementDto, Guid, PagedAndSortedResultRequestDto, CreateUpdateShiftManagementDto>(repository),
	IShiftManagementAppService
{
}
