using System;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Common.Files;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Mobile;

public interface IFamilyConcessionService : IApplicationService
{
	Task<MobileConcessionRequestContext> FamilyEligibilityAsync(Guid child, Guid year, Guid? studentFeeStructureId = null);
	Task<MobileConcessionRequestPreview> FamilyPreviewAsync(Guid child, Guid year, MobileConcessionInput input);
	Task<MobileConcessionRequestDetail> FamilyGetAsync(Guid child, Guid year, Guid id);
	Task<MobileConcessionRequestDetail> FamilySaveAsync(Guid child, Guid year, Guid? id, MobileConcessionInput input);
	Task<MobileConcessionRequestDetail> FamilyActionAsync(Guid child, Guid year, Guid id, MobileFinanceRequestAction input, bool submit);
	Task<ConcessionDocumentFileUploadResponseDto> FamilyUploadAsync(Guid child, Guid year, Guid concessionTypeId, Guid documentId, UploadConcessionDocumentFileDto input);
	Task<FileDownloadResult> FamilyDownloadAsync(Guid child, Guid year, Guid id, Guid documentId);
}

public interface IFamilyRefundService : IApplicationService
{
	Task<MobileRefundEligibility> FamilyRefundEligibilityAsync(Guid child, Guid year);
	Task<MobileFeeRefundPreview> FamilyRefundPreviewAsync(Guid child, Guid year, MobileRefundInput input);
	Task<MobileRefundRequest> FamilyRefundCreateAsync(Guid child, Guid year, MobileRefundInput input);
}

public interface IFamilyAdjustmentService : IApplicationService
{
	Task<MobileAdjustmentEligibility> FamilyAdjustmentEligibilityAsync(Guid child, Guid year);
	Task<MobileAdjustmentPreview> FamilyAdjustmentPreviewAsync(Guid child, Guid year, MobileAdjustmentInput input);
	Task<MobileFinanceCommandResult> FamilyAdjustmentCreateAsync(Guid child, Guid year, MobileAdjustmentInput input);
}

public interface IMobileFinanceActivityService : IApplicationService
{
	Task<MobileFinancePage<MobileFinanceActivityItem>> GetAsync(Guid child, Guid year, int page = 1, int pageSize = 20, bool unreadOnly = false, CancellationToken cancellationToken = default);
	Task MarkReadAsync(Guid child, Guid year, Guid eventId, CancellationToken cancellationToken = default);
}
