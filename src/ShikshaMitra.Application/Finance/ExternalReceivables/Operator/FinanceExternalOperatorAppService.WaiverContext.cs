using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Library;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.ExternalReceivables.Operator;

public partial class FinanceExternalOperatorAppService
{
	public async Task<FinanceExternalWaiverContextDto> GetWaiverContextAsync(FinanceExternalSourceReference source,
		FinanceExternalWaiverAction action, int page = 1, int pageSize = 20)
	{
		var context = await authority.RequireAsync(source, WaiverActionPermission(action));
		var offset = Offset(page, pageSize);
		var binding = await policies.FindAsync(row => row.TenantId == CurrentTenant.Id && row.SourceLinkId == context.Link.Id);
		var result = new FinanceExternalWaiverContextDto
		{
			Source = new() { Kind = context.Link.SourceKind, SourceId = context.Link.SourceId, SourceVersion = context.Link.SourceVersion },
			Receivable = await projection.ReadAsync(context.Link, context.Source),
			Revision = await revisions.ReadAsync(context, binding?.Id), CollectionPolicyVersionId = binding?.CollectionPolicyVersionId
		};
		var policy = await ReadWaiverContextPolicyAsync(context, binding, result);
		if (policy != null)
		{
			result.WaiverEnabled = policy.EnableChargeWaiver;
			result.RequiresApproval = policy.RequiresApproval;
			if (!policy.EnableChargeWaiver && action != FinanceExternalWaiverAction.Reverse)
				result.Issues.Add("The recorded collection policy does not allow charge waivers.");
		}
		if ((action is FinanceExternalWaiverAction.Apply or FinanceExternalWaiverAction.Approve) &&
			context.Charge.Status != ChargeStatus.Approved)
			result.Issues.Add("Only an approved assessment can receive a waiver.");
		if ((action is FinanceExternalWaiverAction.Approve or FinanceExternalWaiverAction.Reject) && policy != null)
		{
			if (!policy.RequiresApproval) result.Issues.Add("The recorded waiver policy does not require a separate approval.");
			else
			{
				try { await authority.EnsureWaiverReviewerRoleAsync(context, policy); }
				catch (AbpAuthorizationException) { result.Issues.Add("The current account is not an eligible waiver approver under the recorded policy."); }
			}
		}
		var requests = (await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceExternalWaiverRequest, Guid>>()
			.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && row.SourceLinkId == context.Link.Id);
		requests = action switch
		{
			FinanceExternalWaiverAction.Approve or FinanceExternalWaiverAction.Reject => requests.Where(row =>
				row.State == FinanceExternalWaiverState.PendingApproval && row.RequiresApproval),
			FinanceExternalWaiverAction.Reverse => requests.Where(row => row.State == FinanceExternalWaiverState.Applied && row.AppliedWaiverId.HasValue),
			_ => requests
		};
		var total = await AsyncExecuter.LongCountAsync(requests);
		List<WaiverContextRequest> rows = offset > int.MaxValue ? [] : await AsyncExecuter.ToListAsync(requests
			.OrderByDescending(row => row.CreationTime).ThenBy(row => row.Id).Skip((int)offset).Take(pageSize)
			.Select(row => new WaiverContextRequest(row.Id, row.PolicyVersionId, row.Amount, row.Reason,
				row.State, row.AppliedWaiverId, row.RequestedByUserId, row.RequiresApproval)));
		result.Requests = new PagedResultDto<FinanceExternalWaiverContextItemDto>(total,
			await BuildWaiverContextRowsAsync(context, action, binding, rows, result));
		if (action == FinanceExternalWaiverAction.Apply)
		{
			if (result.Receivable.OutstandingAmount <= 0m) result.Issues.Add("There is no collectible balance available for a waiver.");
			result.CanExecute = result.Issues.Count == 0;
		}
		else result.CanExecute = result.Issues.Count == 0 && result.Requests.Items.Any(row => row.CanAct);
		return result;
	}

	private async Task<CollectionPolicy?> ReadWaiverContextPolicyAsync(FinanceExternalSettlementContext context,
		FinanceExternalSettlementPolicy? binding, FinanceExternalWaiverContextDto result)
	{
		if (binding == null)
		{
			result.Issues.Add("Confirm an explicit collection policy version before a waiver action.");
			return null;
		}
		try
		{
			return await LazyServiceProvider.LazyGetRequiredService<FinanceExternalSettlementPolicyRules>()
				.ReadCollectionAsync(context, binding.CollectionPolicyVersionId);
		}
		catch (BusinessException error) when (error.Code == LibraryFinanceErrorCodes.InvalidFinancePolicy)
		{
			result.Issues.Add("The recorded collection policy version requires Finance review.");
			return null;
		}
	}

	private static string WaiverActionPermission(FinanceExternalWaiverAction action) => action switch
	{
		FinanceExternalWaiverAction.Apply => ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApplyWaiver,
		FinanceExternalWaiverAction.Approve => ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ApproveWaiver,
		FinanceExternalWaiverAction.Reject => ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.RejectWaiver,
		FinanceExternalWaiverAction.Reverse => ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.ReverseWaiver,
		_ => throw new BusinessException(LibraryFinanceErrorCodes.InvalidSource, "Choose a supported waiver action.")
	};

	private sealed record WaiverContextRequest(Guid Id, Guid PolicyVersionId, decimal Amount, string Reason,
		FinanceExternalWaiverState State, Guid? ApplicationId, Guid MakerId, bool RequiresApproval);
}
