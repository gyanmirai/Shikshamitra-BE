using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Collections;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Consumes existing scoped Finance contracts without creating or pricing Finance records.</summary>
[RemoteService(false)]
public class AdmissionPaymentCompletionReader(
	IRepository<AdmissionWorkflowStageDefinition, Guid> definitions,
	IRepository<AdmissionApplicationStageInstance, Guid> stages,
	IAdmissionFinanceIntegration finance,
	IAdmissionFeeCollectionAppService collections) : ApplicationService, ITransientDependency
{
	public static AdmissionPaymentCompletionPolicy ReadPolicy(string? json)
	{
		try { return AdmissionPaymentCompletionPolicy.Read(json); }
		catch (Exception exception) when (exception is ArgumentException or JsonException or InvalidOperationException)
		{ throw new BusinessException(AdmissionPaymentStageErrorCodes.InvalidPolicy, exception.Message); }
	}

	public async Task<AdmissionPaymentCompletionDto?> ReadAsync(AdmissionFeeSelectionContext context)
	{
		if (context.Owner?.StageType != AdmissionWorkflowStageType.Payment) return null;
		var current = context.Application.CurrentStageInstanceId.HasValue
			? await stages.FindAsync(context.Application.CurrentStageInstanceId.Value) : null;
		var stage = current?.StageType == AdmissionWorkflowStageType.Payment ? current : context.Owner;
		var definition = await definitions.GetAsync(stage.DefinitionStageId);
		return await ReadAsync(context.Application, stage, definition,
			context.Workflow?.Status == AdmissionWorkflowInstanceStatus.Active, context.Owner.Id);
	}

	public async Task<AdmissionPaymentCompletionDto> ReadAsync(AdmissionApplication application,
		AdmissionApplicationStageInstance stage, AdmissionWorkflowStageDefinition definition, bool workflowActive,
		Guid? selectionOwnerStageId = null)
	{
		var policy = ReadPolicy(definition.ConfigurationJson);
		var active = workflowActive && application.CurrentStageInstanceId == stage.Id && stage.Status == AdmissionRuntimeStageStatus.Active;
		var result = new AdmissionPaymentCompletionDto
		{
			PaymentRule = policy.PaymentRule, AllowCollectionLater = policy.AllowCollectionLater,
			CanDeferCollection = active && policy.AllowCollectionLater,
			Blocker = "Select a published Finance fee structure before continuing.", Status = "feeStructureRequired"
		};
		if (application.GetFeeSelection()?.OwnerStageId != (selectionOwnerStageId ?? stage.Id)) return result;
		result.Status = "collectionPending";
		result.Blocker = policy.PaymentRule == "full"
			? "Clear the entire selected Finance fee structure, including future installments, before continuing."
			: "Record a confirmed fee payment in Finance before continuing.";
		try
		{
			// Includes all installments, concessions, pending verification, charges and late fees.
			// False also covers missing/unprepared data; zero is never inferred from a missing balance.
			if (await finance.IsSettledForOnboardingAsync(application.Id))
			{
				result.Status = "financeCleared";
				result.CanComplete = active;
				result.Blocker = null;
				return result;
			}
			if (await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View))
			{
				var summary = await collections.GetSummaryAsync(application.Id);
				if (summary.AccountId.HasValue && summary.ConfirmedPaidAmount > 0m)
				{
					result.Status = "partiallyPaid";
					if (policy.PaymentRule == "partial") { result.CanComplete = active; result.Blocker = null; }
				}
				else if (summary.PendingVerificationAmount > 0m) result.Status = "pendingVerification";
			}
			else if (policy.PaymentRule == "partial")
				result.Blocker = "Finance collection view access is required to verify a partial payment. Ask an authorized staff member to continue.";
		}
		catch (AbpAuthorizationException)
		{
			result.Blocker = "Finance clearance is not accessible for this applicant. Ask an authorized staff member to verify it.";
		}
		catch (BusinessException)
		{
			// Keep selection and allowed deferral usable when Finance configuration needs attention.
			// A failed Finance read must never become an admission completion approval.
			result.Blocker = "Finance could not verify this fee structure. Review its configuration in Finance before continuing.";
		}
		return result;
	}
}
