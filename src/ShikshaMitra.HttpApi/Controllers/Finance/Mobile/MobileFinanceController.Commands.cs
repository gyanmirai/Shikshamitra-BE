using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Finance.Fees.FeeAdjustment;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Mobile;

namespace ShikshaMitra.Controllers.Finance.Mobile;

public partial class MobileFinanceController
{
	[HttpGet("concessions/eligibility")]
	public async Task<Result<MobileConcessionRequestContext>> ConcessionEligibility(Guid studentId, [FromQuery] Guid academicYearId, [FromServices] IFamilyConcessionService service, [FromQuery] Guid? studentFeeStructureId = null) => Result.Success(await service.FamilyEligibilityAsync(studentId, academicYearId, studentFeeStructureId));
	[HttpPost("concessions/preview")]
	public async Task<Result<MobileConcessionRequestPreview>> ConcessionPreview(Guid studentId, [FromQuery] Guid academicYearId, [FromBody] MobileConcessionInput input, [FromServices] IFamilyConcessionService service) => Result.Success(await service.FamilyPreviewAsync(studentId, academicYearId, input));
	[HttpPost("concessions/drafts")]
	public async Task<Result<MobileConcessionRequestDetail>> ConcessionDraft(Guid studentId, [FromQuery] Guid academicYearId, [FromBody] MobileConcessionInput input, [FromServices] IFamilyConcessionService service) => Result.Success(await service.FamilySaveAsync(studentId, academicYearId, null, input));
	[HttpPut("concessions/{requestId:guid}")]
	public async Task<Result<MobileConcessionRequestDetail>> ConcessionUpdate(Guid studentId, Guid requestId, [FromQuery] Guid academicYearId, [FromBody] MobileConcessionInput input, [FromServices] IFamilyConcessionService service) => Result.Success(await service.FamilySaveAsync(studentId, academicYearId, requestId, input));
	[HttpGet("concessions/{requestId:guid}")]
	public async Task<Result<MobileConcessionRequestDetail>> ConcessionDetail(Guid studentId, Guid requestId, [FromQuery] Guid academicYearId, [FromServices] IFamilyConcessionService service) => Result.Success(await service.FamilyGetAsync(studentId, academicYearId, requestId));
	[HttpPost("concessions/{requestId:guid}/submit")]
	public async Task<Result<MobileConcessionRequestDetail>> ConcessionSubmit(Guid studentId, Guid requestId, [FromQuery] Guid academicYearId, [FromBody] SubmitConcessionRequestDto input, [FromServices] IFamilyConcessionService service) => Result.Success(await service.FamilyActionAsync(studentId, academicYearId, requestId, new MobileFinanceRequestAction { IdempotencyKey=input.IdempotencyKey??string.Empty, ConcurrencyStamp=input.ConcurrencyStamp, PreviewFingerprint=input.PreviewFingerprint }, true));
	[HttpPost("concessions/{requestId:guid}/cancel")]
	public async Task<Result<MobileConcessionRequestDetail>> ConcessionCancel(Guid studentId, Guid requestId, [FromQuery] Guid academicYearId, [FromBody] CancelConcessionRequestDto input, [FromServices] IFamilyConcessionService service) => Result.Success(await service.FamilyActionAsync(studentId, academicYearId, requestId, new MobileFinanceRequestAction { IdempotencyKey=input.IdempotencyKey??string.Empty, RequestConcurrencyStamp=input.RequestConcurrencyStamp, Reason=input.Reason }, false));
	[HttpPost("concessions/documents/{concessionTypeId:guid}/{documentId:guid}"), RequestSizeLimit(15 * 1024 * 1024)]
	public async Task<Result<ConcessionDocumentFileUploadResponseDto>> ConcessionUpload(Guid studentId, Guid concessionTypeId, Guid documentId,
		[FromQuery] Guid academicYearId, [FromBody] UploadConcessionDocumentFileDto input, [FromServices] IFamilyConcessionService service) => Result.Success(await service.FamilyUploadAsync(studentId, academicYearId, concessionTypeId, documentId, input));
	[HttpGet("concessions/{requestId:guid}/documents/{documentId:guid}")]
	public async Task<FileContentResult> ConcessionDocument(Guid studentId, Guid requestId, Guid documentId, [FromQuery] Guid academicYearId, [FromServices] IFamilyConcessionService service)
	{
		var file = await service.FamilyDownloadAsync(studentId, academicYearId, requestId, documentId);
		return File(file.Content, file.ContentType, file.FileName);
	}
	[HttpGet("refunds/eligibility")]
	public async Task<Result<MobileRefundEligibility>> RefundEligibility(Guid studentId, [FromQuery] Guid academicYearId, [FromServices] IFamilyRefundService service) => Result.Success(await service.FamilyRefundEligibilityAsync(studentId, academicYearId));
	[HttpPost("refunds/preview")]
	public async Task<Result<MobileFeeRefundPreview>> RefundPreview(Guid studentId, [FromQuery] Guid academicYearId, [FromBody] MobileRefundInput input, [FromServices] IFamilyRefundService service) => Result.Success(await service.FamilyRefundPreviewAsync(studentId, academicYearId, input));
	[HttpPost("refunds")]
	public async Task<Result<MobileRefundRequest>> RefundCreate(Guid studentId, [FromQuery] Guid academicYearId, [FromBody] MobileRefundInput input, [FromServices] IFamilyRefundService service) => Result.Success(await service.FamilyRefundCreateAsync(studentId, academicYearId, input));
	[HttpGet("adjustments/eligibility")]
	public async Task<Result<MobileAdjustmentEligibility>> AdjustmentEligibility(Guid studentId, [FromQuery] Guid academicYearId, [FromServices] IFamilyAdjustmentService service) => Result.Success(await service.FamilyAdjustmentEligibilityAsync(studentId, academicYearId));
	[HttpPost("adjustments/preview")]
	public async Task<Result<MobileAdjustmentPreview>> AdjustmentPreview(Guid studentId, [FromQuery] Guid academicYearId, [FromBody] MobileAdjustmentInput input, [FromServices] IFamilyAdjustmentService service) => Result.Success(await service.FamilyAdjustmentPreviewAsync(studentId, academicYearId, input));
	[HttpPost("adjustments")]
	public async Task<Result<MobileFinanceCommandResult>> AdjustmentCreate(Guid studentId, [FromQuery] Guid academicYearId, [FromBody] MobileAdjustmentInput input, [FromServices] IFamilyAdjustmentService service) => Result.Success(await service.FamilyAdjustmentCreateAsync(studentId, academicYearId, input));
	[HttpGet("activity"), HttpGet("notifications")]
	public async Task<Result<MobileFinancePage<MobileFinanceActivityItem>>> Activity(Guid studentId, [FromQuery] Guid academicYearId,
		[FromServices] IMobileFinanceActivityService service, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
		[FromQuery] bool unreadOnly = false, CancellationToken cancellationToken = default) => Result.Success(await service.GetAsync(studentId, academicYearId, page, pageSize, unreadOnly, cancellationToken));
	[HttpPost("notifications/{eventId:guid}/read")]
	public async Task<Result<object>> ReadNotification(Guid studentId, Guid eventId, [FromQuery] Guid academicYearId,
		[FromServices] IMobileFinanceActivityService service, CancellationToken cancellationToken)
	{
		await service.MarkReadAsync(studentId, academicYearId, eventId, cancellationToken);
		return Result.Success<object>(new { IsRead = true });
	}
}
