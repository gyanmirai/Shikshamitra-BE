using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Application service interface for managing earning components
/// </summary>
public interface IEarningComponentAppService : ICrudAppService<
	EarningComponentDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateEarningComponentDto,
	UpdateEarningComponentDto>
{
	/// <summary>
	/// Get all active earning components with pagination support
	/// </summary>
	Task<PagedResultDto<EarningComponentDto>> GetAllActiveAsync(PagedResultRequestDto input);

	/// <summary>
	/// Activate an earning component
	/// </summary>
	Task ActivateAsync(Guid id);

	/// <summary>
	/// Deactivate an earning component
	/// </summary>
	Task DeactivateAsync(Guid id);
}
