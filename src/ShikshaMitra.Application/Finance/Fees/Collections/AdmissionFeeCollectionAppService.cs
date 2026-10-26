using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Fees.Collections;

[RemoteService(false)]
public partial class AdmissionFeeCollectionAppService(
	IAdmissionFinanceContextReader contextReader,
	IRepository<FinanceFeeAccount, Guid> accounts,
	IDueFeeRepository dues,
	IRepository<FeeRecordMetadata, Guid> transactions,
	IRepository<FeeCollectionDueFeeAllocation, Guid> allocations,
	IRepository<FeePaymentReceipt, Guid> receipts,
	IRepository<PaymentMethodRecord, Guid> paymentMethods,
	IReadOnlyRepository<AcademicYear, Guid> years,
	IRepository<FeeStructureDetail, Guid> structureDetails,
	IRepository<FeeHead, Guid> heads,
	StudentFeeStructureAppService assignmentCalculator,
	StudentFeeDetailsQueryService studentDetails,
	CollectFeeWorkflow collector,
	FinanceScopeAccessService scope,
	LateFeeBalanceService lateFees,
	ShikshaMitra.Domain.Finance.Policies.CollectionPolicyResolver policies,
	IUnitOfWorkManager units) : ApplicationService, IAdmissionFeeCollectionAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
	public async Task<AdmissionFeeCollectionDto> GetSummaryAsync(Guid applicationId)
	{
		var source = await contextReader.GetAsync(applicationId);
		var year = await years.GetAsync(source.AcademicYearId);
		var account = await accounts.FindAsync(a => a.ApplicationId == applicationId && a.AcademicYearId == source.AcademicYearId);
		if (account != null) await scope.EnsureFeeAccountAccessibleAsync(account);
		var result = new AdmissionFeeCollectionDto
		{
			ApplicationId = applicationId, AccountId = account?.Id, StudentId = source.StudentId,
			ApplicantName = source.ApplicantName, ApplicationNumber = source.ApplicationNumber,
			AcademicYear = new() { Id = year.Id, Name = year.Name, Year = year.StartDate.Year,
				StartDate = DateTime.SpecifyKind(year.StartDate.Date, DateTimeKind.Utc), EndDate = DateTime.SpecifyKind(year.EndDate.Date, DateTimeKind.Utc) },
			Currency = account?.Currency ?? new StudentFeeAssignmentPreviewDto().Currency,
			CanCollect = source.CanCollect && await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add),
			Blocker = source.Blocker, HasPaymentEvidence = account?.HasPaymentEvidence ?? false
		};
		if (!source.StructureId.HasValue) return result;
		var current = account?.AssignmentSnapshotJson == null ? null : ReadAssignment(account);
		if (current != null && SelectionMatches(source, current))
		{
			result.StructureName = current.StructureName; result.StructureRevision = current.RevisionNumber;
			var rows = await dues.GetListAsync(d => d.FinanceFeeAccountId == account!.Id);
			result.Proration = ProrationResult(current, rows);
			var tx = await transactions.GetListAsync(t => t.FinanceFeeAccountId == account!.Id ||
				t.FinanceFeeAccountId == null && account!.StudentId.HasValue && t.StudentId == account.StudentId && t.AcademicYearId == account.AcademicYearId);
			result.HasPaymentEvidence = result.HasPaymentEvidence || tx.Count > 0 || rows.Any(d => d.PaidAmount > 0m || d.RefundedAmount > 0m);
			var pendingIds = tx.Where(t => t.Status == FeeCollectionStatus.InReconciliation).Select(t => t.Id).ToList();
			var pending = await allocations.GetListAsync(a => pendingIds.Contains(a.FeeRecordMetadataId));
			var pendingByDue = pending.GroupBy(a => a.DueFeeId).ToDictionary(g => g.Key, g => g.Sum(a => (decimal)a.Amount));
			result.TotalAmount = rows.Sum(d => d.OriginalAmount);
			result.PendingVerificationAmount = pending.Sum(a => (decimal)a.Amount);
			result.ConfirmedPaidAmount = Math.Max(0m, rows.Sum(d => d.PaidAmount - d.RefundedAmount) - result.PendingVerificationAmount);
			result.OutstandingAmount = rows.Sum(d => d.OutstandingAmount);
			result.FeeHeads = rows.GroupBy(d => d.FeeHeadId).Select(g => new AdmissionCollectionFeeHeadDto
			{
				FeeHeadId = g.Key, FeeHeadName = current.Heads.First(h => h.FeeHeadId == g.Key).FeeHeadName,
				TotalAmount = g.Sum(d => d.OriginalAmount), PaidAmount = Math.Max(0m, g.Sum(d => d.PaidAmount - d.RefundedAmount) - g.Sum(d => pendingByDue.GetValueOrDefault(d.Id))),
				OutstandingAmount = g.Sum(d => d.OutstandingAmount), Status = g.Any(d => pendingByDue.GetValueOrDefault(d.Id) > 0m)
					? "pendingVerification" : g.Sum(d => d.OutstandingAmount) == 0m ? "paid" : g.Sum(d => d.PaidAmount - d.RefundedAmount) > 0m ? "partial" : "pending"
			}).ToList();
			if (await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_InvoiceReceipt.View))
			{
				var receiptRows = (await receipts.GetListAsync(r => r.FinanceFeeAccountId == account!.Id ||
					r.FinanceFeeAccountId == null && account!.StudentId.HasValue && r.StudentId == account.StudentId && r.AcademicYearId == account.AcademicYearId))
					.Where(r => r.LinkedFinalReceiptId == null)
					.OrderByDescending(r => r.ReceiptDate).ThenByDescending(r => r.CreationTime).Take(10).ToList();
				var ids = receiptRows.Select(r => r.FeeCollectionTransactionId).ToList();
				var methods = await paymentMethods.GetListAsync(p => ids.Contains(p.FeeRecordMetadataId));
				result.Receipts = receiptRows.Select(r => new AdmissionCollectionReceiptDto
				{
					Id = r.Id, ReceiptNumber = r.ReceiptNumber, Amount = (decimal)r.TotalPaidAmount,
					Scope = r.ReceiptScope != FeeReceiptScope.Charge ? "fee" : "charge",
					CollectedAt = new DateTimeOffset(DateTime.SpecifyKind(r.CreationTime, DateTimeKind.Utc)),
					Status = r.VerificationStatus == FeeReceiptVerificationStatus.Rejected ? "reversed" : tx.FirstOrDefault(t => t.Id == r.FeeCollectionTransactionId)?.Status is FeeCollectionStatus.InReconciliation
						? "pendingVerification" : tx.FirstOrDefault(t => t.Id == r.FeeCollectionTransactionId)?.Status is FeeCollectionStatus.Reversed ? "reversed" : "confirmed",
					PaymentMethods = methods.Where(m => m.FeeRecordMetadataId == r.FeeCollectionTransactionId).Select(m => m.PaymentMethod.ToString()).Distinct().ToList()
				}).ToList();
			}
			if (rows.Count > 0 && result.OutstandingAmount == 0m && result.PendingVerificationAmount == 0m &&
				!tx.Any(t => t.Status == FeeCollectionStatus.InReconciliation))
			{
				// Use the same Finance projection as checkout so cleared tuition never hides
				// an outstanding charge/late fee or money still awaiting verification.
                result.IsSettled = IsCollectionSettled(await BuildCollectFormAsync(source, account!, current, rows));
				if (result.IsSettled) result.CanCollect = false;
			}
		}
		else
		{
			if (account?.HasPaymentEvidence == true) throw new BusinessException(AdmissionFeeCollectionErrorCodes.SelectionChanged);
			result.VariableFeeHeads = await VariableHeadsAsync(source);
			if (result.VariableFeeHeads.Count > 0) result.Blocker = "Enter the initial amounts for the Finance-configured variable fees.";
			else
			{
				AdmissionAssignmentPreparation calculated;
				try { calculated = await assignmentCalculator.PrepareAdmissionAsync(source, account?.Id ?? GuidGenerator.Create(), []); }
				catch (BusinessException error) when (error.Code == ProrationPolicyErrorCodes.JoiningDateRequired)
				{
					result.CanCollect = false;
					result.Blocker = "Complete the actual joining date in the registration form before preparing fees.";
					return result;
				}
				result.Proration = ProrationResult(calculated.Preview, calculated.Dues);
				result.StructureName = calculated.Preview.StructureName; result.StructureRevision = calculated.Preview.RevisionNumber;
				result.TotalAmount = result.OutstandingAmount = calculated.Preview.ProratedTotal;
				result.FeeHeads = calculated.Preview.Heads.Where(h => h.Included).Select(h => new AdmissionCollectionFeeHeadDto
				{ FeeHeadId = h.FeeHeadId, FeeHeadName = h.FeeHeadName, TotalAmount = h.Obligation, OutstandingAmount = h.Obligation }).ToList();
			}
		}
		return result;
	}

	internal static bool IsCollectionSettled(FeeCollectFormDto form) =>
		form.Summary != null && form.Summary.PendingFee == 0d && form.Summary.ReconciliationAmount == 0d &&
		form.Ledger.Any(month => month.Items.Count > 0) &&
		form.Ledger.SelectMany(month => month.Items).All(line => line.Due == 0d && line.ReconciliationAmount == 0d) &&
		form.Charges.All(charge => charge.PendingAmount == 0d && charge.ReconciliationAmount == 0m);

	private static StudentFeeAssignmentPreviewDto ReadAssignment(FinanceFeeAccount account) =>
		JsonSerializer.Deserialize<StudentFeeAssignmentPreviewDto>(account.AssignmentSnapshotJson!) ?? throw new BusinessException(AdmissionFeeCollectionErrorCodes.NotPrepared);

	internal static bool SelectionMatches(AdmissionFinanceContext source, StudentFeeAssignmentPreviewDto saved) =>
		source.StructureId == saved.StructureId && source.StructureRevision == saved.RevisionNumber &&
		(!saved.RegistrationJoiningDate.HasValue && !saved.ProrationPolicyVersionId.HasValue || source.JoiningDate?.Date == saved.RegistrationJoiningDate?.Date) &&
		source.OptionalHeadIds.Order().SequenceEqual(saved.Heads.Where(h => h.Included && h.IsOptional).Select(h => h.FeeHeadId).Order());

	internal static FeeProrationResultDto? ProrationResult(StudentFeeAssignmentPreviewDto saved, System.Collections.Generic.IReadOnlyList<DueFee> rows)
	{
		var policy = saved.Policies.FirstOrDefault(x => x.Family == "proration");
		var withdrawal = rows.Sum(x => x.GetWithdrawalProrationCredit());
		if (policy == null || saved.ReductionTotal <= 0m && withdrawal <= 0m) return null;
		return new()
		{
			PolicyId = policy.PolicyId, PolicyVersionId = policy.VersionId,
			PolicyName = policy.Name, PolicyVersion = policy.Version, JoiningDate = saved.RegistrationJoiningDate ?? saved.ChargeStartDate,
			ChargeStartDate = saved.ChargeStartDate, ChargeEndDate = saved.ChargeEndDate,
			ScheduledAmount = saved.ScheduledTotal, AdmissionReduction = saved.ReductionTotal, WithdrawalCredit = withdrawal,
			PayableAmount = rows.Sum(x => Math.Max(0m, x.OriginalAmount - x.NetAmount)),
			ExcessPaidAmount = rows.Sum(x => x.OverpaymentAmount),
			Explanations = rows.Where(x => x.ScheduledAmount != x.OriginalAmount || x.GetWithdrawalProrationCredit() > 0m)
				.Select(x => x.GetWithdrawalProrationCredit() > 0m ? x.GetWithdrawalProrationExplanation() : x.ProrationExplanation ?? string.Empty).Distinct().ToList()
		};
	}

	private async Task<List<AssignmentHeadDto>> VariableHeadsAsync(AdmissionFinanceContext source)
	{
		var details = await structureDetails.GetListAsync(d => d.FeeStructureId == source.StructureId && d.IsVariable && (!d.IsOptional || source.OptionalHeadIds.Contains(d.FeeHeadId)));
		var ids = details.Select(d => d.FeeHeadId).ToList();
		var names = (await heads.GetListAsync(h => ids.Contains(h.Id))).ToDictionary(h => h.Id, h => h.Name);
		return details.Select(d => new AssignmentHeadDto { FeeHeadId = d.FeeHeadId, FeeHeadName = names[d.FeeHeadId],
			IsVariable = true, IsOptional = d.IsOptional, IsOneTime = d.IsOneTime, Included = true, PaymentTermId = d.PaymentTermId,
			PaymentTermVersionId = d.PaymentTermVersionId, AmountBasis = "CoverageTotal" }).ToList();
	}
}
