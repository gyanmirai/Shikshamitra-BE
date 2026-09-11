using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Admissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

[RemoteService(false)]
public class AdmissionPaymentReadService(
	IRepository<AdmissionPaymentTransaction, Guid> payments,
	AdmissionPaymentAccessService access) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionPaymentStageDto> GetAsync(Guid applicationId, Guid stageId)
	{
		var context = await access.GetContextAsync(applicationId, stageId);
		var rows = (await payments.GetListAsync(item => item.StageInstanceId == stageId))
			.OrderBy(item => item.AttemptNumber).ToList();
		var policy = context.Policy;
		var settled = rows.Where(item => item.Status == AdmissionPaymentStatuses.Settled).Sum(item => item.Amount);
		var pending = rows.Where(item => item.Status is AdmissionPaymentStatuses.Pending or AdmissionPaymentStatuses.PendingVerification)
			.Sum(item => item.Amount);
		var waived = rows.Any(item => item.Status == AdmissionPaymentStatuses.Waived);
		var required = policy?.Amount ?? 0m;
		var outstanding = decimal.Max(0m, required - settled);
		var dueAt = context.DueAt;
		var overdue = dueAt.HasValue && dueAt.Value < Clock.Now && !waived && outstanding > 0m;
		var status = Status(rows, settled, required, waived, overdue);
		var blockers = Blockers(policy, outstanding, pending, waived, overdue);
		return new AdmissionPaymentStageDto
		{
			ApplicationId = applicationId, StageId = stageId, PaymentStageEnabled = policy != null,
			Policy = policy, IsActive = context.IsActive, DueAt = dueAt, IsOverdue = overdue,
			RequiredAmount = required, SettledAmount = settled, PendingAmount = pending,
			OutstandingAmount = outstanding, Currency = policy?.Currency ?? string.Empty, Status = status,
			ConcurrencyStamp = context.Application.ConcurrencyStamp,
			Transactions = rows.Select(Map).ToList(), Blockers = blockers,
			CanRecord = context.IsActive && policy != null && policy.CollectionMode is "staff" or "either" &&
				(policy.AllowLatePayment || !overdue) && !waived && outstanding > pending,
			CanStartOnlinePayment = false,
			CanVerify = context.IsActive && rows.Any(item => item.Status == AdmissionPaymentStatuses.PendingVerification),
			CanWaive = context.IsActive && policy?.AllowWaiver == true && !waived &&
				settled == 0m && pending == 0m && outstanding > 0m,
			CanReverse = context.IsActive && rows.Any(item => item.Status == AdmissionPaymentStatuses.Settled),
			CanComplete = policy != null && blockers.Count == 0
		};
	}

	private static List<string> Blockers(
		AdmissionPaymentStagePolicy? policy, decimal outstanding, decimal pending, bool waived, bool overdue)
	{
		if (policy == null) return [];
		if (waived) return [];
		var blockers = new List<string>();
		if (outstanding > 0m) blockers.Add(pending > 0m
			? "Wait for the pending payment to be settled or rejected."
			: $"Collect the remaining {policy.Currency} {outstanding:0.00}.");
		if (overdue && !policy.AllowLatePayment) blockers.Add("The payment deadline has passed.");
		return blockers;
	}

	private static string Status(List<AdmissionPaymentTransaction> rows, decimal settled, decimal required, bool waived, bool overdue)
	{
		if (waived) return AdmissionPaymentStatuses.Waived;
		if (settled >= required && required > 0m) return AdmissionPaymentStatuses.Settled;
		if (rows.Any(item => item.Status == AdmissionPaymentStatuses.PendingVerification)) return AdmissionPaymentStatuses.PendingVerification;
		if (rows.Any(item => item.Status == AdmissionPaymentStatuses.Pending)) return AdmissionPaymentStatuses.Pending;
		if (settled > 0m) return "partial";
		if (overdue) return "overdue";
		return rows.LastOrDefault()?.Status ?? "unpaid";
	}

	private static AdmissionPaymentTransactionDto Map(AdmissionPaymentTransaction item) => new()
	{
		Id = item.Id, AttemptNumber = item.AttemptNumber, Amount = item.Amount, Currency = item.Currency,
		Method = item.Method, CollectionMode = item.CollectionMode, Status = item.Status,
		ExternalReference = item.ExternalReference, Provider = item.Provider, ReceiptNumber = item.ReceiptNumber,
		Notes = item.Notes, DecisionReason = item.DecisionReason, RecordedBy = item.RecordedBy,
		RecordedAt = item.RecordedAt, VerifiedBy = item.VerifiedBy, VerifiedAt = item.VerifiedAt,
		ConcurrencyStamp = item.ConcurrencyStamp
	};
}
