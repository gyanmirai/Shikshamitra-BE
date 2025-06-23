using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings
{
	public interface ILibraryTimingAppService : IApplicationService
	{
		Task<List<LibraryTimingDto>> GetListAsync();

		Task<LibraryTimingDto> CreateAsync(CreateUpdateLibraryTimingDto input);

		Task<LibraryTimingDto> UpdateAsync(Guid id, CreateUpdateLibraryTimingDto input);
		Task DeleteAsync(int day);
	}
}
