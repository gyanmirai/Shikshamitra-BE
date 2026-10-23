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
		var deferred = context.Application.GetFeeCollectionDeferral()?.StageId == stageId;
		var completion = await LazyServiceProvider.LazyGetRequiredService<AdmissionPaymentCompletionReader>()
			.ReadAsync(context.Application, context.Stage, context.Definition,
				context.Workflow.Status == AdmissionWorkflowInstanceStatus.Active, selectionContext.Owner?.Id);
		return new AdmissionPaymentStageDto
		{
			ApplicationId = applicationId, StageId = stageId, PaymentStageEnabled = true,
			IsActive = context.IsActive, Currency = selectionContext.Currency ?? string.Empty,
			Completion = completion,
			Status = deferred ? "collectionDeferred" : completion.Status,
			ConcurrencyStamp = context.Application.ConcurrencyStamp,
			Blockers = deferred || completion.Blocker == null ? [] : [completion.Blocker],
			CanComplete = completion.CanComplete, CanRecord = false, CanVerify = false,
			CanWaive = false, CanReverse = false, CanStartOnlinePayment = false
		};
	}
}
