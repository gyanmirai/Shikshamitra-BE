using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Settings;

[RemoteService(false)]
public class EarningComponentAppService(
	IRepository<EarningComponent, Guid> repository,
	EarningComponentMutationService mutationService,
	EarningComponentReader reader) : CrudAppService<
		EarningComponent,
		EarningComponentDto,
		Guid,
		PagedAndSortedResultRequestDto,
		CreateEarningComponentDto,
		UpdateEarningComponentDto>(repository), IEarningComponentAppService
{
	public override Task<EarningComponentDto> CreateAsync(CreateEarningComponentDto input) =>
		mutationService.CreateAsync(input);

	public override Task<EarningComponentDto> UpdateAsync(Guid id, UpdateEarningComponentDto input) =>
		mutationService.UpdateAsync(id, input);

	public Task<PayrollComponentUpdateImpactPreviewDto> PreviewUpdateImpactAsync(
		Guid id,
		UpdateEarningComponentDto input,
		bool includeEmployees = false,
		int skipCount = 0,
		int maxResultCount = 20) =>
		mutationService.PreviewUpdateImpactAsync(id, input, includeEmployees, skipCount, maxResultCount);

	public Task<PayrollComponentUpdateImpactPreviewDto> PreviewDeleteImpactAsync(
		Guid id,
		bool includeEmployees = false,
		int skipCount = 0,
		int maxResultCount = 20) =>
		mutationService.PreviewDeleteImpactAsync(id, includeEmployees, skipCount, maxResultCount);

	public override Task DeleteAsync(Guid id)
	{
		throw new BusinessException(HRErrorCodes.PayrollComponentPreviewRequired)
			.WithData("message", "Preview payroll component delete impact before deleting.");
	}

	public Task DeleteConfirmedAsync(Guid id, string previewHash) =>
		mutationService.DeleteConfirmedAsync(id, previewHash);

	public override Task<PagedResultDto<EarningComponentDto>> GetListAsync(PagedAndSortedResultRequestDto input) =>
		GetFilteredListAsync(input.SkipCount, input.MaxResultCount, null);

	public override Task<EarningComponentDto> GetAsync(Guid id) =>
		reader.GetAsync(id);

	public Task<PagedResultDto<EarningComponentDto>> GetFilteredListAsync(int skipCount, int maxResultCount, string? search) =>
		reader.GetFilteredListAsync(skipCount, maxResultCount, search);

	public Task<PagedResultDto<EarningComponentDto>> GetAllActiveAsync(PagedResultRequestDto input) =>
		reader.GetAllActiveAsync(input);

	public Task ActivateAsync(Guid id) =>
		mutationService.ActivateAsync(id);

	public Task DeactivateAsync(Guid id) =>
		mutationService.DeactivateAsync(id);
}
