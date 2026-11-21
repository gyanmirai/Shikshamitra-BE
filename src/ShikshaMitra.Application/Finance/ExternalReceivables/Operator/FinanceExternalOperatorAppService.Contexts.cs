using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Charges;

namespace ShikshaMitra.Finance.ExternalReceivables.Operator;

public partial class FinanceExternalOperatorAppService
{
	private async Task<FinanceExternalOperatorContextDto> ContextAsync(FinanceExternalSourceReference source, bool approval)
	{
		var context = await authority.RequireAsync(source, approval
			? ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Approve
			: ShikshaMitraSubmodulePermissions.Finance.Fees_Charges.Update);
		var binding = await policies.FindAsync(row => row.TenantId == CurrentTenant.Id && row.SourceLinkId == context.Link.Id);
		var result = new FinanceExternalOperatorContextDto
		{
			Source = new() { Kind = context.Link.SourceKind, SourceId = context.Link.SourceId, SourceVersion = context.Link.SourceVersion },
			Receivable = await projection.ReadAsync(context.Link, context.Source),
			Revision = await revisions.ReadAsync(context, binding?.Id), SettlementPolicyBindingId = binding?.Id,
			CollectionPolicyVersionId = binding?.CollectionPolicyVersionId ?? context.Link.CollectionPolicyVersionId,
			RefundPolicyVersionId = binding?.RefundPolicyVersionId ?? context.Link.RefundPolicyVersionId
		};
		if (approval)
		{
			if (context.Charge.Status != ChargeStatus.Pending) result.Issues.Add("Only a pending assessment can be approved.");
			if (context.ActorId == context.Charge.RequestorId) result.Issues.Add("A different authorized person must perform the independent review.");
		}
		else if (binding != null) result.Issues.Add("The settlement policy is already recorded and cannot be overwritten.");
		result.CanExecute = !result.Issues.Any();
		return result;
	}
}
