using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Policies;

public interface ILibraryCalendarAppService : IApplicationService
{
	Task<LibraryCalendarSettingsDto> GetSettingsAsync(Guid? academicYearId = null);
	Task<LibraryCalendarSettingsDto> UpdateSettingsAsync(LibraryCalendarSettingsRequest input);
	Task<PaginatedResponse<LibraryCalendarClosureDto>> GetClosuresAsync(Guid academicYearId, int page = 1, int pageSize = 20);
	Task<LibraryCalendarClosureDto> CreateClosureAsync(Guid academicYearId, LibraryCalendarClosureRequest input);
	Task<LibraryCalendarClosureDto> UpdateClosureAsync(Guid closureId, LibraryCalendarClosureRequest input);
	Task DeleteClosureAsync(Guid closureId);
	Task<LibraryCalendarCalculationDto> CalculateAsync(LibraryCalendarCalculateRequest input);
}
