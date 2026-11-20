using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.ExternalReceivables;

public partial class FinanceExternalSettlementAppService
{
	[UnitOfWork(IsDisabled = true)]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApplyWaiver)]
	public Task<FinanceExternalWaiverDto> RequestWaiverAsync(FinanceExternalWaiverInput input) =>
		CommandAsync(input, "RequestWaiver", ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApplyWaiver,
			async (context, replay) =>
			{
				if (replay != null) return await WaiverResultAsync(context, replay.ResultEntityId!.Value);
				await EnsureRevisionAsync(context, input.Revision);
				var (binding, policy) = await WaiverPolicyAsync(context);
				await EnsureWaivableAsync(context, input.Amount);
				var request = new FinanceExternalWaiverRequest(GuidGenerator.Create(), CurrentTenant.Id!.Value,
					context.Link.Id, binding.CollectionPolicyVersionId, input.Amount, context.Link.Currency,
					policy.RequiresApproval, context.ActorId, input.Reason);
				if (!request.RequiresApproval) await ApplyWaiverAsync(context, request, input.IdempotencyKey, input.Reason);
				await waiverRequests.InsertAsync(request, autoSave: false);
				await RecordAsync(context, input, "RequestWaiver", request.Id, "WaiverRequest");
				return await WaiverResultAsync(context, request.Id);
			});

	[UnitOfWork(IsDisabled = true)]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApproveWaiver)]
	public Task<FinanceExternalWaiverDto> ApproveWaiverAsync(FinanceExternalWaiverDecisionInput input) =>
		DecideWaiverAsync(input, approve: true);

	[UnitOfWork(IsDisabled = true)]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.RejectWaiver)]
	public Task<FinanceExternalWaiverDto> RejectWaiverAsync(FinanceExternalWaiverDecisionInput input) =>
		DecideWaiverAsync(input, approve: false);

	private Task<FinanceExternalWaiverDto> DecideWaiverAsync(FinanceExternalWaiverDecisionInput input, bool approve)
	{
		var operation = approve ? "ApproveWaiver" : "RejectWaiver";
		var action = approve ? ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApproveWaiver
			: ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.RejectWaiver;
		return CommandAsync(input, operation, action, async (context, replay) =>
		{
			var request = await RequestAsync(context, replay?.ResultEntityId ?? input.WaiverRequestId);
			if (replay == null)
			{
				await EnsureRevisionAsync(context, input.Revision);
				var (binding, policy) = await WaiverPolicyAsync(context);
				if (request.PolicyVersionId != binding.CollectionPolicyVersionId || !request.RequiresApproval)
					throw new BusinessException(LibraryFinanceErrorCodes.InvalidFinancePolicy);
				await authority.EnsureWaiverApproverAsync(context, policy, request.RequestedByUserId);
				if (approve)
				{
					await EnsureWaivableAsync(context, request.Amount);
					await ApplyWaiverAsync(context, request, input.IdempotencyKey, input.Reason);
				}
				else request.Reject(context.ActorId, Clock.Now, Reason(input.Reason));
				await waiverRequests.UpdateAsync(request, autoSave: false);
				await RecordAsync(context, input, operation, request.Id, "WaiverRequest");
			}
			return await WaiverResultAsync(context, request.Id);
		});
	}

	[UnitOfWork(IsDisabled = true)]
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ReverseWaiver)]
	public Task<FinanceExternalWaiverDto> ReverseWaiverAsync(ReverseFinanceExternalWaiverInput input) =>
		CommandAsync(input, "ReverseWaiver", ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ReverseWaiver,
			async (context, replay) =>
			{
				var application = await waivers.GetAsync(input.WaiverApplicationId, includeDetails: false);
				if (application.TenantId != CurrentTenant.Id || application.ChargeId != context.Charge.Id)
					throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
				var request = await waiverRequests.FindAsync(row => row.TenantId == CurrentTenant.Id &&
					row.SourceLinkId == context.Link.Id && row.AppliedWaiverId == application.Id)
					?? throw new BusinessException(LibraryFinanceErrorCodes.SourceRequiresReview);
				if (replay == null)
				{
					await EnsureRevisionAsync(context, input.Revision);
					FinanceExternalSettlementAmounts.EnsureMoney(input.Amount, positive: true);
					var reversed = (await reversals.GetListAsync(row => row.TenantId == CurrentTenant.Id &&
						row.WaiverApplicationId == application.Id)).Sum(row => row.Amount);
					if (input.Amount > application.EffectiveAmount - reversed) throw new BusinessException(LibraryFinanceErrorCodes.InvalidAmount);
					var reversal = new FinanceChargeWaiverReversal(GuidGenerator.Create(), CurrentTenant.Id!.Value,
						application.Id, input.Amount, context.Link.Currency, context.ActorId, input.Reason);
					await reversals.InsertAsync(reversal, autoSave: false);
					await RecordAsync(context, input, "ReverseWaiver", reversal.Id, "WaiverReversal");
				}
				return await WaiverResultAsync(context, request.Id);
			});

	private async Task<(FinanceExternalSettlementPolicy Binding, CollectionPolicy Policy)> WaiverPolicyAsync(FinanceExternalSettlementContext context)
	{
		var binding = await BindingAsync(context) ?? throw new BusinessException(LibraryFinanceErrorCodes.InvalidFinancePolicy);
		var policy = await policyRules.ReadCollectionAsync(context, binding.CollectionPolicyVersionId);
		if (!policy.EnableChargeWaiver) throw new BusinessException(LibraryFinanceErrorCodes.InvalidFinancePolicy);
		return (binding, policy);
	}
	private async Task EnsureWaivableAsync(FinanceExternalSettlementContext context, decimal amount)
	{
		FinanceExternalSettlementAmounts.EnsureMoney(amount, positive: true);
		if (context.Charge.Status != ChargeStatus.Approved || amount > (await balances.ReadAsync(context.Charge)).Amounts.Collectible)
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidAmount);
	}
	private async Task ApplyWaiverAsync(FinanceExternalSettlementContext context, FinanceExternalWaiverRequest request, string key, string reason)
	{
		var payer = new FinanceExternalPayerIdentity(context.Source.Payer.Kind,
			context.Source.Payer.StudentId ?? context.Source.Payer.EmployeeId!.Value);
		var application = ChargeWaiverApplication.CreateExternal(GuidGenerator.Create(), CurrentTenant.Id!.Value, payer,
			context.Charge.Id, context.Link.AcademicYearId, context.Charge.Amount, request.Amount, request.Reason,
			context.ActorId, key.Trim());
		await waivers.InsertAsync(application, autoSave: false);
		request.Apply(application.Id, context.ActorId, Clock.Now, Reason(reason));
	}
	private async Task<FinanceExternalWaiverRequest> RequestAsync(FinanceExternalSettlementContext context, Guid id)
	{
		var request = await waiverRequests.GetAsync(id, includeDetails: false);
		if (request.TenantId != CurrentTenant.Id || request.SourceLinkId != context.Link.Id)
			throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource);
		return request;
	}
	private async Task<FinanceExternalWaiverDto> WaiverResultAsync(FinanceExternalSettlementContext context, Guid requestId)
	{
		var request = await RequestAsync(context, requestId);
		var reversed = request.AppliedWaiverId.HasValue
			? (await reversals.GetListAsync(row => row.TenantId == CurrentTenant.Id && row.WaiverApplicationId == request.AppliedWaiverId)).Sum(row => row.Amount)
			: 0m;
		return new() { RequestId = request.Id, ApplicationId = request.AppliedWaiverId, State = request.State,
			RequestedAmount = request.Amount, AppliedAmount = request.AppliedWaiverId.HasValue ? request.Amount : 0m,
			ReversedAmount = reversed, Receivable = await projection.ReadAsync(context.Link, context.Source) };
	}
}
