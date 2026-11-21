using System;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Reservations;

public interface ILibraryReservationMemberAppService : IApplicationService
{
	Task<PaginatedResponse<LibraryReservationMemberDto>> SearchAsync(Guid academicYearId, string? search = null, int page = 1, int pageSize = 20);
	Task<LibraryReservationMemberDto> ResolveAsync(Guid academicYearId, string identifier);
}
