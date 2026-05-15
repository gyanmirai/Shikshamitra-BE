using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Settings;

/// <summary>
/// Application service interface for managing deduction components
/// </summary>
public interface IDeductionComponentAppService : ICrudAppService<
	DeductionComponentDto,
	Guid,
	PagedAndSortedResultRequestDto,
	CreateDeductionComponentDto,
	UpdateDeductionComponentDto>
{
	Task<PagedResultDto<DeductionComponentDto>> GetFilteredListAsync(int skipCount, int maxResultCount, string? search);

	/// <summary>
	/// Get all active deduction components with pagination support
	/// </summary>
	Task<PagedResultDto<DeductionComponentDto>> GetAllActiveAsync(PagedResultRequestDto input);

	Task<PayrollComponentUpdateImpactPreviewDto> PreviewUpdateImpactAsync(
		Guid id,
		UpdateDeductionComponentDto input,
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
	/// Activate a deduction component
	/// </summary>
	Task ActivateAsync(Guid id);

	/// <summary>
	/// Deactivate a deduction component
	/// </summary>
	Task DeactivateAsync(Guid id);
}
