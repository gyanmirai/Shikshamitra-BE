using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using ShikshaMitra.Library.Shared;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Library.Setting;

[Area("Library")]
[Route("api/library-hours")]
public class LibraryHoursController : AbpController
{
	private readonly ILibraryTimingAppService _service;

	public LibraryHoursController(ILibraryTimingAppService service)
	{
		_service = service;
	}

	[HttpGet]
	public Task<PaginatedResponse<LibraryTimingDto>> GetListAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] int pageSize = 10,
		[FromQuery] int page = 1)
	{
		return _service.GetListAsync(academicYearId, page, pageSize);
	}

	[HttpPost]
	public Task<GenericUpdatedResponse> UpdateHoursAsync(
		[FromQuery] Guid academicYearId,
		[FromBody] LibraryHoursUpdateRequest input)
	{
		return _service.UpdateHoursAsync(academicYearId, input);
	}
}
