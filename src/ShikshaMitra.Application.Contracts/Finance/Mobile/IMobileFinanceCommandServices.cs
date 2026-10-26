using System;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Common.Files;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Finance.Mobile;

public interface IFamilyConcessionService : IApplicationService
{
	Task<object> FamilyEligibilityAsync(Guid child, Guid year);
	Task<object> FamilyPreviewAsync(Guid child, Guid year, MobileConcessionInput input);
	Task<object> FamilyGetAsync(Guid child, Guid year, Guid id);
	Task<object> FamilySaveAsync(Guid child, Guid year, Guid? id, MobileConcessionInput input);
	Task<object> FamilyActionAsync(Guid child, Guid year, Guid id, MobileFinanceRequestAction input, bool submit);
	Task<ConcessionDocumentFileUploadResponseDto> FamilyUploadAsync(Guid child, Guid year, Guid concessionTypeId, Guid documentId, UploadConcessionDocumentFileDto input);
	Task<FileDownloadResult> FamilyDownloadAsync(Guid child, Guid year, Guid id, Guid documentId);
}

public interface IFamilyRefundService : IApplicationService
{
	Task<object> FamilyRefundEligibilityAsync(Guid child, Guid year);
	Task<object> FamilyRefundPreviewAsync(Guid child, Guid year, MobileRefundInput input);
	Task<MobileFinanceCommandResult> FamilyRefundCreateAsync(Guid child, Guid year, MobileRefundInput input);
}

public interface IFamilyAdjustmentService : IApplicationService
{
	Task<object> FamilyAdjustmentEligibilityAsync(Guid child, Guid year);
	Task<object> FamilyAdjustmentPreviewAsync(Guid child, Guid year, MobileAdjustmentInput input);
	Task<MobileFinanceCommandResult> FamilyAdjustmentCreateAsync(Guid child, Guid year, MobileAdjustmentInput input);
}

public interface IMobileFinanceActivityService : IApplicationService
{
	Task<MobileFinancePage<MobileFinanceActivityItem>> GetAsync(Guid child, Guid year, int page = 1, int pageSize = 20, bool unreadOnly = false, CancellationToken cancellationToken = default);
	Task MarkReadAsync(Guid child, Guid year, Guid eventId, CancellationToken cancellationToken = default);
}
