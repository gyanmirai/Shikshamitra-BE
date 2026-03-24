using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Employees.Search;

/// <summary>
/// Application service for searching and listing employees.
/// </summary>
public interface IEmployeeSearchAppService : IApplicationService
{
	Task<EmployeeListResponse> GetFilteredListAsync(GetEmployeeListInput input);
	Task<EmployeeSearchResponse> SearchAsync(SearchEmployeeInput input);
}
