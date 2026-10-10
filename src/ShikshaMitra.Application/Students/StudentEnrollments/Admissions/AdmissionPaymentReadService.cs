using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>The admission Payment step confirms a Finance reference, not a collection.</summary>
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
		return new AdmissionPaymentStageDto
		{
			ApplicationId = applicationId, StageId = stageId, PaymentStageEnabled = true,
			IsActive = context.IsActive, Currency = selectionContext.Currency ?? string.Empty,
			Status = confirmed ? "feeStructureConfirmed" : "feeStructureRequired",
			ConcurrencyStamp = context.Application.ConcurrencyStamp,
			Blockers = confirmed ? [] : ["Confirm the applicable Finance fee structure before continuing."],
			CanComplete = context.IsActive && confirmed, CanRecord = false, CanVerify = false,
			CanWaive = false, CanReverse = false, CanStartOnlinePayment = false
		};
	}
}
