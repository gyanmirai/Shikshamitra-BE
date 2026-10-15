using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Reports collection evidence or an audited decision to collect later without creating Finance rows.</summary>
[RemoteService(false)]
public class AdmissionPaymentReadService(AdmissionPaymentAccessService access,
	AdmissionFeeSelectionAccessService feeSelection) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionPaymentStageDto> GetAsync(Guid applicationId, Guid stageId)
	{
		var context = await access.GetContextAsync(applicationId, stageId);
		var selectionContext = await feeSelection.GetContextAsync(context.Application);
		var selection = context.Application.GetFeeSelection();
		var confirmed = selection != null && selection.OwnerStageId == selectionContext.Owner?.Id;
		var deferred = context.Application.GetFeeCollectionDeferral()?.StageId == stageId;
		var recorded = selectionContext.HasPaymentEvidence;
		return new AdmissionPaymentStageDto
		{
			ApplicationId = applicationId, StageId = stageId, PaymentStageEnabled = true,
			IsActive = context.IsActive, Currency = selectionContext.Currency ?? string.Empty,
			Status = deferred ? "collectionDeferred" : recorded ? "collectionRecorded" : confirmed ? "collectionPending" : "feeStructureRequired",
			ConcurrencyStamp = context.Application.ConcurrencyStamp,
			Blockers = deferred || recorded && confirmed ? [] : ["Collect fees or choose Collect later in Onboarding and enter a reason."],
			CanComplete = context.IsActive && recorded && confirmed, CanRecord = false, CanVerify = false,
			CanWaive = false, CanReverse = false, CanStartOnlinePayment = false
		};
	}
}
