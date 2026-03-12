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
	/// <summary>
	/// Get all active deduction components with pagination support
	/// </summary>
	Task<PagedResultDto<DeductionComponentDto>> GetAllActiveAsync(PagedResultRequestDto input);

	/// <summary>
	/// Activate a deduction component
	/// </summary>
	Task ActivateAsync(Guid id);

	/// <summary>
	/// Deactivate a deduction component
	/// </summary>
	Task DeactivateAsync(Guid id);
}
