using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeRefund;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.ExternalReceivables;

public partial class FinanceExternalRefundAppService
{
	private sealed record RefundFormState(FinanceExternalRefundFormDto Form, FinancePolicyVersion? Version,
		RefundPolicySnapshot? Policy, List<PaidChargeRefundSource> Sources, decimal PayerYearCommitted);

	private async Task<RefundFormState> ReadFormAsync(FinanceExternalSettlementContext context)
	{
		var binding = await bindings.FindAsync(row => row.TenantId == CurrentTenant.Id && row.SourceLinkId == context.Link.Id);
		var eligible = await sources.ReadAsync(context.Link, context.Charge);
		var form = new FinanceExternalRefundFormDto
		{
			Receivable = await projection.ReadAsync(context.Link, context.Source), RefundPolicyVersionId = binding?.RefundPolicyVersionId,
			Sources = eligible.Select(row => new FinanceExternalRefundableAllocationDto
			{
				SourceAllocationId = row.AllocationId, SourceReceiptId = row.ReceiptId, ReceiptNumber = row.ReceiptNumber,
				SchoolReceiptDate = row.ReceiptDate, PaidAmount = row.Paid, CommittedAmount = row.Committed, AvailableAmount = row.Available
			}).ToList()
		};
		FinancePolicyVersion? version = null;
		RefundPolicySnapshot? policy = null;
		if (binding?.RefundPolicyVersionId is not { } versionId)
			form.SetupIssues.Add("Confirm an explicit refund policy version for this source.");
		else
		{
			version = await LazyServiceProvider.LazyGetRequiredService<IRepository<FinancePolicyVersion, Guid>>().FindAsync(versionId, includeDetails: false);
			if (version == null || version.TenantId != CurrentTenant.Id || version.AcademicYearId != context.Link.AcademicYearId ||
				version.PolicyType != "refund" || !string.Equals(version.Status, "Active", StringComparison.OrdinalIgnoreCase) ||
				context.Link.RefundPolicyVersionId.HasValue && context.Link.RefundPolicyVersionId != version.Id)
				form.SetupIssues.Add("The source's recorded refund policy version requires Finance review.");
			else
			{
				try { policy = JsonSerializer.Deserialize<RefundPolicySnapshot>(version.SnapshotJson); }
				catch (JsonException) { }
				if (policy == null || policy.PolicyId != version.PolicyId || policy.AcademicYearId != context.Link.AcademicYearId ||
					policy.Status != RefundPolicyStatus.Active || !policy.IncludePaidCharges)
					form.SetupIssues.Add("The pinned refund policy must permit settled paid-charge refunds.");
				else
				{
					form.AllowedPayoutMethods = policy.AllowedPayoutMethods.Select(method => CanonicalPayoutMethod(method.ToString())).ToList();
					if (context.Source.Payer.Kind == FinanceExternalPayerKind.Employee && policy.MaximumPerStudentYear.HasValue)
						form.SetupIssues.Add("This student-only annual refund cap has no configured employee meaning. Choose a compatible refund policy before binding this source.");
				}
			}
		}
		if (!eligible.Any(row => row.Available > 0m)) form.SetupIssues.Add("There is no uncommitted settled receipt allocation available for refund.");
		var yearCommitted = context.Source.Payer.StudentId is { } studentId
			? await requests.GetStudentYearCommittedAmountAsync(studentId, context.Link.AcademicYearId) : 0m;
		if (context.Source.Payer.StudentId is { } payerId)
			yearCommitted += await AsyncExecuter.SumAsync((await requests.GetQueryableAsync()).Where(request => request.TenantId == CurrentTenant.Id &&
				request.StudentId == payerId && request.AcademicYearId == context.Link.AcademicYearId && request.FinanceExternalSourceLinkId.HasValue &&
				(request.Status == FeeRefundStatus.Submitted || request.Status == FeeRefundStatus.UnderReview)).Select(request => request.CalculatedAmount));
		var facts = eligible.Select(row => new FinanceExternalLedgerFact(row.AllocationId, "RefundSource", "Settled",
			row.Paid, row.ReceiptId, row.Committed)).ToList();
		facts.Add(new(Guid.Empty, "PayerYearRefundCommitment", "Committed", yearCommitted));
		form.Revision = FinanceExternalBalanceRevision.Create(context, binding?.Id, facts);
		form.CanRequest = form.SetupIssues.Count == 0;
		return new(form, version, policy, eligible, yearCommitted);
	}
}
