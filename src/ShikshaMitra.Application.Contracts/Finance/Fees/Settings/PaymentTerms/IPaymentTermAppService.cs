using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Fees.Settings.PaymentTerms;

public interface IPaymentTermAppService : IApplicationService
{
	Task<PagedResultDto<PaymentTermListDto>> GetListAsync(PaymentTermGetListInput input);

	Task<PaymentTermUsageDto> GetUsageAsync(Guid paymentTermId, Guid? versionId = null);
	Task<PaymentTermDefaultImpactDto> GetDefaultImpactAsync(Guid academicYearId, Guid boardId, Guid? paymentTermId = null);

	Task<PaymentTermDto> GetAsync(Guid paymentTermId);

	Task<PagedResultDto<PaymentTermVersionHistoryDto>> GetVersionHistoryAsync(
		Guid paymentTermId,
		PagedAndSortedResultRequestDto input);

	Task<PaymentTermDto> CreateAsync(CreatePaymentTermDto input);

	Task<PaymentTermDto> UpdateDraftAsync(Guid paymentTermId, UpdatePaymentTermDraftDto input);

	Task DeleteAsync(Guid paymentTermId);

	Task<PaymentTermDto> CreateRevisionAsync(Guid paymentTermId, CreateRevisionDto input);

	Task<PublishResultDto> PublishVersionAsync(Guid paymentTermId, Guid versionId);

	Task ArchiveAsync(Guid paymentTermId, ArchivePaymentTermDto input);

	Task<PaymentTermDto> RestoreAsync(Guid paymentTermId);

	Task<ClonePaymentTermsResultDto> CloneAsync(ClonePaymentTermsDto input);

	Task<PaymentTermTemplatePreviewDto> PreviewTemplateAsync(PreviewPaymentTermTemplateDto input);

	Task<ImpactPreviewDto> GetImpactPreviewAsync(Guid paymentTermId);

	Task<FeeStructureSchedulePreviewDto> GetFeeStructureSchedulePreviewAsync(Guid feeStructureId);
}
