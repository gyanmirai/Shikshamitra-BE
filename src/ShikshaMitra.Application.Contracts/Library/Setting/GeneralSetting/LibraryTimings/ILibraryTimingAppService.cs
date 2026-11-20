using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings
{
	public interface ILibraryTimingAppService : IApplicationService
	{
		Task<LibraryHoursListDto> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10);
		Task<GenericUpdatedResponse> UpdateHoursAsync(Guid academicYearId, LibraryHoursUpdateRequest input);
	}
}
