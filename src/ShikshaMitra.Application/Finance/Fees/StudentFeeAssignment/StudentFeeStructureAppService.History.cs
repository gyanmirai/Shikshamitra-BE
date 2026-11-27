using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	[Authorize(ViewPermission)]
	public async Task<AssignmentHistoryPage> GetHistoryAsync(Guid studentId, AssignmentHistoryRequest input)
	{
		await RequireEnrollmentAsync(studentId, input.AcademicYearId, false);
		var assignment = await studentFeeStructureRepository.FindAsync(row => row.StudentId == studentId &&
			row.AcademicYearId == input.AcademicYearId && row.TenantId == CurrentTenant.Id)
			?? throw new BusinessException(AssignmentErrorCodes.NotAssigned);
		var incomplete = false;
		var corrections = ReadAssignmentHistory<List<AssignmentCorrection>>(
			assignment.ExtraProperties.GetValueOrDefault("AssignmentCorrectionHistory")?.ToString(), ref incomplete) ?? [];
		var policyChanges = ReadAssignmentHistory<List<AssignmentPolicyChange>>(
			assignment.ExtraProperties.GetValueOrDefault("AssignedPolicyHistory")?.ToString(), ref incomplete) ?? [];
		var snapshot = ReadAssignmentHistory<StudentFeeAssignmentPreviewDto>(assignment.AssignmentSnapshotJson, ref incomplete);
		var original = corrections.Count > 0
			? ReadAssignmentHistory<StudentFeeAssignmentPreviewDto>(corrections[0].BeforeSnapshotJson, ref incomplete)
			: snapshot;
		incomplete |= original == null;

		var serviceLog = await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceAuditEvent, Guid>>()
			.GetListAsync(row => row.TenantId == CurrentTenant.Id && row.AcademicYearId == input.AcademicYearId &&
				row.StudentId == studentId && row.EntityId == assignment.Id && row.EntityType == FinanceAuditEntityTypes.FeeAdjustment &&
				(row.Action == OptionalServiceStopped || row.Action == OptionalServiceRestarted || row.Action == OptionalServiceEnabled));

		var feeChanges = await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceAuditEvent, Guid>>()
			.GetListAsync(row => row.TenantId == CurrentTenant.Id && row.AcademicYearId == input.AcademicYearId &&
				row.StudentId == studentId && row.EntityId == assignment.Id && row.EntityType == FinanceAuditEntityTypes.FeeAdjustment &&
				row.Action == VariableFeeChanged);
		var earliestChange = feeChanges.MinBy(row => row.OccurredAt);
		if (corrections.Count == 0 && earliestChange != null &&
			ReadAssignmentHistory<VariableFeeChangeEvidence>(earliestChange.ContextJson, ref incomplete, OptionalServiceAuditJson) is { } oldest)
			original = ReadAssignmentHistory<StudentFeeAssignmentPreviewDto>(oldest.BeforeSnapshotJson, ref incomplete);

		var serviceRestarts = serviceLog.Where(row => row.Action != OptionalServiceStopped).ToDictionary(row => row.Id,
			row => ReadAssignmentHistory<OptionalServiceRestartEvidence>(row.ContextJson, ref incomplete, OptionalServiceAuditJson));
		var versionIds = policyChanges.SelectMany(change => change.Before.VersionIds().Concat(change.After.VersionIds()))
			.Concat(corrections.Where(change => change.BeforePolicies != null).SelectMany(change => change.BeforePolicies!.VersionIds()))
			.Concat(serviceRestarts.Values.OfType<OptionalServiceRestartEvidence>().Select(change => (Guid?)change.ProrationPolicyVersionId))
			.OfType<Guid>().Distinct().ToList();
		var policyVersions = (await financePolicyVersionRepository.GetListAsync(version =>
			version.TenantId == CurrentTenant.Id && version.AcademicYearId == input.AcademicYearId && versionIds.Contains(version.Id)))
			.ToDictionary(version => version.Id, version => $"{version.PolicyName} · v{version.VersionNumber}");
		string? PolicyName(Guid? id) => id is Guid value ? policyVersions.GetValueOrDefault(value, "Recorded policy version unavailable") : null;
		var names = (snapshot?.Heads ?? []).ToDictionary(head => head.FeeHeadId, head => head.FeeHeadName);
		var terms = serviceRestarts.Count == 0 ? [] : await paymentTermRepository.GetListAsync(term => term.AcademicYearId == input.AcademicYearId);
		var termVersions = terms.SelectMany(term => term.Versions.Select(version => (version.Id, Label: $"{term.Name} · v{version.VersionNumber}")))
			.ToDictionary(term => term.Id, term => term.Label);

		var activity = new List<AssignmentHistoryActivity>();
		if (assignment.CreationTime != default)
			activity.Add(new("initial", assignment.FinanceFeeAccountId.HasValue || original?.AssignmentSource == "Admission"
				? "Assigned from admission" : "Fee structure assigned", UtcTimestamp(assignment.CreationTime), assignment.CreatorId,
				null, original?.OverrideReason, HistoryDiff([], DescribeAssignment(original))));
		for (var i = 0; i < corrections.Count; i++)
		{
			var correction = corrections[i];
			var before = ReadAssignmentHistory<StudentFeeAssignmentPreviewDto>(correction.BeforeSnapshotJson, ref incomplete);
			var after = ReadAssignmentHistory<StudentFeeAssignmentPreviewDto>(correction.AfterSnapshotJson, ref incomplete);
			var beforeValues = DescribeAssignment(before);
			if (correction.BeforePolicies is { } prior)
				foreach (var (family, id) in prior.Entries())
					beforeValues[$"policy:{family.ToLowerInvariant()}"] = new($"{family} policy", PolicyName(id));
			activity.Add(new($"correction-{i}", "Fee assignment corrected", correction.OccurredAt, correction.ActorId,
				null, correction.Reason, HistoryDiff(beforeValues, DescribeAssignment(after))));
		}
		for (var i = 0; i < policyChanges.Count; i++)
		{
			var change = policyChanges[i];
			var after = change.After.Entries().ToDictionary(item => item.Family, item => item.Id);
			var details = change.Before.Entries().Where(item => item.Id != after[item.Family]).Select(item =>
				new AssignmentHistoryChange($"{item.Family} policy", PolicyName(item.Id), PolicyName(after[item.Family]), null)).ToList();
			activity.Add(new($"policies-{i}", "Assigned policies changed", change.OccurredAt, change.ActorId, null, change.Reason, details));
		}
		foreach (var row in feeChanges)
		{
			var change = ReadAssignmentHistory<VariableFeeChangeEvidence>(row.ContextJson, ref incomplete, OptionalServiceAuditJson);
			if (change == null) continue;
			var before = ReadAssignmentHistory<StudentFeeAssignmentPreviewDto>(change.BeforeSnapshotJson, ref incomplete);
			var after = ReadAssignmentHistory<StudentFeeAssignmentPreviewDto>(change.AfterSnapshotJson, ref incomplete);
			var details = HistoryDiff(DescribeAssignment(before), DescribeAssignment(after));
			details.Insert(0, new("Effective date", null, FormatHistoryDate(change.EffectiveDate), "date"));
			activity.Add(new(row.Id.ToString(), "Variable fee changed", UtcTimestamp(row.OccurredAt),
				row.ActorUserId, row.ActorNameSnapshot, row.Reason, details));
		}
		foreach (var row in serviceLog)
		{
			var details = new List<AssignmentHistoryChange>();
			var action = "Optional service cancelled";
			if (row.Action == OptionalServiceStopped)
			{
				var stop = ReadAssignmentHistory<OptionalServiceStopEvidence>(row.ContextJson, ref incomplete, OptionalServiceAuditJson);
				if (stop != null)
				{
					details.Add(new("Fee head", null, names.GetValueOrDefault(stop.FeeHeadId, "Optional fee"), null));
					details.Add(new("Cancellation effective date", null, FormatHistoryDate(stop.LastServiceDate.AddDays(1)), "date"));
					details.Add(new("Installments cancelled", null, stop.Result.InstallmentCount, null));
					details.Add(new("Amount cancelled", null, stop.Result.CancelledAmount, "money"));
					details.Add(new("Outstanding reduction", null, stop.Result.OutstandingReduction, "money"));
					details.Add(new("Prepaid service credit", null, stop.Result.PrepaidCredit, "money"));
				}
				incomplete |= stop == null;
			}
			else
			{
				action = row.Action == OptionalServiceEnabled ? "Optional service added" : "Optional service restarted";
				var restart = serviceRestarts[row.Id];
				if (restart != null)
				{
					var result = restart.Result;
					details.Add(new("Fee head", null, names.GetValueOrDefault(restart.FeeHeadId, "Optional fee"), null));
					details.Add(new("Service", restart.PreviousCancellationEffectiveDate.HasValue ? "Cancelled" : "Not selected", "Enabled", null));
					details.Add(new("Service start", null, FormatHistoryDate(result.StartDate), "date"));
					details.Add(new("Service end", null, FormatHistoryDate(result.EndDate), "date"));
					details.Add(new("Monthly rate", null, result.MonthlyRate, "money"));
					details.Add(new("Proration policy", null, PolicyName(restart.ProrationPolicyVersionId), null));
					details.Add(new("Payment term", null, termVersions.GetValueOrDefault(restart.PaymentTermVersionId), null));
					details.Add(new("New assigned amount", null, result.AssignedTotal, "money"));
					details.Add(new("Proration reduction", null, result.ProrationReduction, "money"));
					foreach (var line in result.Lines)
						details.Add(new($"{line.InstallmentLabel} · new charge", null, line.ProratedAmount, "money"));
					foreach (var line in result.ExistingInstallments)
						details.Add(new($"{line.InstallmentLabel} · existing charge", line.BilledAmount, line.BilledAmount, "money"));
				}
				incomplete |= restart == null;
			}
			activity.Add(new(row.Id.ToString(), action, UtcTimestamp(row.OccurredAt), row.ActorUserId, row.ActorNameSnapshot, row.Reason, details));
		}

		var page = activity.OrderByDescending(item => item.OccurredAt).ThenBy(item => item.Id, StringComparer.Ordinal)
			.Skip(Math.Max(0, input.SkipCount)).Take(Math.Clamp(input.MaxResultCount, 1, 50)).ToList();
		var actorIds = page.Where(item => item.ActorName == null).Select(item => item.ActorId).OfType<Guid>().Distinct().ToList();
		var users = actorIds.Count == 0 ? [] : await LazyServiceProvider.LazyGetRequiredService<IRepository<IdentityUser, Guid>>()
			.GetListAsync(user => user.TenantId == CurrentTenant.Id && actorIds.Contains(user.Id));
		var actorNames = users.ToDictionary(user => user.Id, user => string.IsNullOrWhiteSpace(user.Name) ? user.UserName : user.Name);
		return new(activity.Count, page.Select(item => new AssignmentHistoryEntry(item.Id, item.Action, item.OccurredAt,
			item.ActorName ?? (item.ActorId is Guid id ? actorNames.GetValueOrDefault(id, "Staff member") : null), item.Reason, item.Changes)).ToList(),
			snapshot?.Currency ?? "INR", incomplete);
	}

	private static Dictionary<string, AssignmentHistoryValue> DescribeAssignment(StudentFeeAssignmentPreviewDto? snapshot)
	{
		var values = new Dictionary<string, AssignmentHistoryValue>();
		if (snapshot == null) return values;
		values["structure"] = new("Fee structure", $"{snapshot.StructureName} · v{snapshot.RevisionNumber}");
		values["start"] = new("Charge start", FormatHistoryDate(snapshot.ChargeStartDate), "date");
		values["end"] = new("Charge end", FormatHistoryDate(snapshot.ChargeEndDate), "date");
		values["joining"] = new("Joining date", FormatHistoryDate(snapshot.RegistrationJoiningDate), "date");
		values["scheduled"] = new("Scheduled amount", snapshot.ScheduledTotal, "money");
		values["assigned"] = new("Assigned amount", snapshot.ProratedTotal, "money");
		values["reduction"] = new("Proration reduction", snapshot.ReductionTotal, "money");
		values["policyReason"] = new("Policy override reason", snapshot.PolicyOverrideReason);
		foreach (var policy in snapshot.Policies)
			values[$"policy:{policy.Family.ToLowerInvariant()}"] = new($"{PolicyFamilyLabel(policy.Family)} policy", $"{policy.Name} · v{policy.Version}");
		foreach (var head in snapshot.Heads)
		{
			var key = $"head:{head.FeeHeadId}";
			var name = head.FeeHeadName;
			AssignmentHistoryValue ForHead(string detail, object? value, string? format = null) =>
				new($"{name} · {detail}", value, format, name, detail);
			values[$"{key}:included"] = ForHead("selection", head.Included ? "Included" : "Not selected");
			if (!head.Included) continue;
			values[$"{key}:amount"] = ForHead("assigned amount", head.Obligation, "money");
			values[$"{key}:rate"] = ForHead(head.AmountBasis == "MonthlyRate" ? "monthly rate" : "base amount", head.EffectiveAmount, "money");
			values[$"{key}:term"] = ForHead("payment term", head.PaymentTermVersionNumber is int version
				? $"{head.PaymentTermName} · v{version}" : head.PaymentTermName);
			values[$"{key}:start"] = ForHead("service start", FormatHistoryDate(head.ServicePeriod?.StartDate), "date");
			values[$"{key}:end"] = ForHead("service end", FormatHistoryDate(head.ServicePeriod?.EndDate), "date");
			values[$"{key}:reason"] = ForHead("service date reason", head.ServicePeriod?.Reason);
			values[$"{key}:zero"] = ForHead("zero amount reason", head.ZeroAmountReason);
			values[$"{key}:basis"] = ForHead("amount basis", head.AmountBasis == "MonthlyRate" ? "Monthly rate" : "Annual total");
		}
		foreach (var line in snapshot.Lines)
		{
			var key = $"installment:{line.FeeHeadId}:{line.Sequence}";
			var name = $"{line.FeeHeadName} · {line.InstallmentLabel}";
			AssignmentHistoryValue ForInstallment(string detail, object? value, string? format = null) =>
				new($"{name} · {detail}", value, format, line.FeeHeadName, detail, line.InstallmentLabel);
			values[$"{key}:label"] = new($"{line.FeeHeadName} · installment {line.Sequence}", line.InstallmentLabel,
				FeeHead: line.FeeHeadName, Detail: "installment name", Installment: line.InstallmentLabel);
			values[$"{key}:due"] = ForInstallment("due date", FormatHistoryDate(line.DueDate), "date");
			values[$"{key}:demand"] = ForInstallment("demand date", FormatHistoryDate(line.DemandDate), "date");
			values[$"{key}:amount"] = ForInstallment("installment amount", line.ProratedAmount, "money");
			values[$"{key}:rule"] = ForInstallment("proration rule", line.RuleType);
		}
		return values;
	}

	private static List<AssignmentHistoryChange> HistoryDiff(Dictionary<string, AssignmentHistoryValue> before,
		Dictionary<string, AssignmentHistoryValue> after) =>
		before.Keys.Union(after.Keys).Select(key => (Before: before.GetValueOrDefault(key), After: after.GetValueOrDefault(key)))
			.Where(item => !Equals(item.Before?.Value, item.After?.Value))
			.Select(item =>
			{
				var field = item.After ?? item.Before!;
				return new AssignmentHistoryChange(field.Label, item.Before?.Value, item.After?.Value,
					field.Format, field.FeeHead, field.Detail, field.Installment);
			}).ToList();

	private static T? ReadAssignmentHistory<T>(string? json, ref bool incomplete, JsonSerializerOptions? options = null) where T : class
	{
		if (string.IsNullOrWhiteSpace(json)) return null;
		try { return JsonSerializer.Deserialize<T>(json, options); }
		catch (JsonException) { incomplete = true; return null; }
	}

	private static DateTimeOffset UtcTimestamp(DateTime value) =>
		new(value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime());
	private static string? FormatHistoryDate(DateTime? value) => !value.HasValue || value.Value == default
		? null : FormatHistoryDate(DateOnly.FromDateTime(value.Value));
	private static string FormatHistoryDate(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
	private static string PolicyFamilyLabel(string family) => family.Length == 0 ? "Assigned" : char.ToUpperInvariant(family[0]) + family[1..];

	private sealed record AssignmentHistoryValue(string Label, object? Value, string? Format = null,
		string? FeeHead = null, string? Detail = null, string? Installment = null);
	private sealed record AssignmentHistoryActivity(string Id, string Action, DateTimeOffset OccurredAt,
		Guid? ActorId, string? ActorName, string? Reason, IReadOnlyList<AssignmentHistoryChange> Changes);
	private sealed record AssignmentCorrection(string BeforeSnapshotJson, string AfterSnapshotJson, AssignmentPolicyPins? BeforePolicies,
		string Reason, Guid ActorId, DateTimeOffset OccurredAt);
	private sealed record AssignmentPolicyChange(AssignmentPolicyPins Before, AssignmentPolicyPins After,
		string Reason, Guid ActorId, DateTimeOffset OccurredAt);
	private sealed record AssignmentPolicyPins(Guid? ConcessionPolicyVersionId, Guid? CollectionPolicyVersionId,
		Guid? RefundPolicyVersionId, Guid? ProrationPolicyVersionId)
	{
		public (string Family, Guid? Id)[] Entries() =>
			[("Concession", ConcessionPolicyVersionId), ("Collection", CollectionPolicyVersionId),
				("Refund", RefundPolicyVersionId), ("Proration", ProrationPolicyVersionId)];
		public IEnumerable<Guid?> VersionIds() => Entries().Select(item => item.Id);
	}
}
