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
	Task<PagedResultDto<EarningComponentDto>> GetFilteredListAsync(int skipCount, int maxResultCount, string? search);

	/// <summary>
	/// Get all active earning components with pagination support
	/// </summary>
	Task<PagedResultDto<EarningComponentDto>> GetAllActiveAsync(PagedResultRequestDto input);

	Task<PayrollComponentUpdateImpactPreviewDto> PreviewUpdateImpactAsync(
		Guid id,
		UpdateEarningComponentDto input,
		bool includeEmployees = false,
		int skipCount = 0,
		int maxResultCount = 20);

	Task<PayrollComponentUpdateImpactPreviewDto> PreviewDeleteImpactAsync(
		Guid id,
		bool includeEmployees = false,
		int skipCount = 0,
		int maxResultCount = 20);

	Task DeleteConfirmedAsync(Guid id, string previewHash);

	/// <summary>
	/// Activate an earning component
	/// </summary>
	Task ActivateAsync(Guid id);

	/// <summary>
	/// Deactivate an earning component
	/// </summary>
	Task DeactivateAsync(Guid id);
}
