using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.Dashboard;

public interface ILibraryDashboardAppService : IApplicationService
{
	Task<LibraryDashboardOverviewDto> GetOverviewAsync(Guid? academicYearId = null);
}
