using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms;
using ShikshaMitra.Finance.Fees.Settings.PaymentTerms.Dtos;
using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Finance.Fees.Settings;

/// <summary>
/// REST controller for Payment Term management.
/// Route: <c>/api/finance/payment-terms</c>
/// </summary>
[RemoteService]
[Area("finance")]
[Route("api/finance/payment-terms")]
[Authorize]
public class PaymentTermController(
	IPaymentTermAppService paymentTermAppService
) : AbpController
{
	[HttpGet]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_PaymentTerms.View)]
	public async Task<PagedResultDto<PaymentTermListDto>> GetListAsync(PaymentTermGetListInput input)
		=> await paymentTermAppService.GetListAsync(input);

	[HttpGet("{paymentTermId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_PaymentTerms.View)]
	public async Task<PaymentTermDto> GetAsync(Guid paymentTermId)
		=> await paymentTermAppService.GetAsync(paymentTermId);

	[HttpGet("{paymentTermId}/versions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_PaymentTerms.View)]
	public async Task<PagedResultDto<PaymentTermVersionHistoryDto>> GetVersionHistoryAsync(
		Guid paymentTermId,
		PagedAndSortedResultRequestDto input)
		=> await paymentTermAppService.GetVersionHistoryAsync(paymentTermId, input);

	[HttpPost]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_PaymentTerms.Add)]
	public async Task<PaymentTermDto> CreateAsync(CreatePaymentTermDto input)
		=> await paymentTermAppService.CreateAsync(input);

	[HttpPut("{paymentTermId}/draft")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_PaymentTerms.Add)]
	public async Task<PaymentTermDto> UpdateDraftAsync(Guid paymentTermId, UpdatePaymentTermDraftDto input)
		=> await paymentTermAppService.UpdateDraftAsync(paymentTermId, input);

	[HttpDelete("{paymentTermId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_PaymentTerms.Delete)]
	public async Task DeleteAsync(Guid paymentTermId)
		=> await paymentTermAppService.DeleteAsync(paymentTermId);

	[HttpPost("{paymentTermId}/revisions")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_PaymentTerms.Add)]
	public async Task<PaymentTermDto> CreateRevisionAsync(Guid paymentTermId, CreateRevisionDto input)
		=> await paymentTermAppService.CreateRevisionAsync(paymentTermId, input);

	[HttpPost("{paymentTermId}/versions/{versionId}/publish")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_PaymentTerms.Add)]
	public async Task<PublishResultDto> PublishVersionAsync(Guid paymentTermId, Guid versionId)
		=> await paymentTermAppService.PublishVersionAsync(paymentTermId, versionId);

	[HttpPatch("{paymentTermId}/archive")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_PaymentTerms.Add)]
	public async Task ArchiveAsync(Guid paymentTermId, ArchivePaymentTermDto input)
		=> await paymentTermAppService.ArchiveAsync(paymentTermId, input);

	[HttpPatch("{paymentTermId}/restore")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_PaymentTerms.Add)]
	public async Task<PaymentTermDto> RestoreAsync(Guid paymentTermId)
		=> await paymentTermAppService.RestoreAsync(paymentTermId);

	[HttpPost("clone")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_PaymentTerms.Add)]
	public async Task<ClonePaymentTermsResultDto> CloneAsync(ClonePaymentTermsDto input)
		=> await paymentTermAppService.CloneAsync(input);

	[HttpPost("templates/preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_PaymentTerms.Add)]
	public async Task<PaymentTermTemplatePreviewDto> PreviewTemplateAsync(PreviewPaymentTermTemplateDto input)
		=> await paymentTermAppService.PreviewTemplateAsync(input);

	[HttpGet("{paymentTermId}/impact-preview")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_PaymentTerms.View)]
	public async Task<ImpactPreviewDto> GetImpactPreviewAsync(Guid paymentTermId)
		=> await paymentTermAppService.GetImpactPreviewAsync(paymentTermId);

	[HttpGet("fee-structure-schedule-preview/{feeStructureId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_PaymentTerms.View)]
	public async Task<FeeStructureSchedulePreviewDto> GetFeeStructureSchedulePreviewAsync(Guid feeStructureId)
		=> await paymentTermAppService.GetFeeStructureSchedulePreviewAsync(feeStructureId);
}
