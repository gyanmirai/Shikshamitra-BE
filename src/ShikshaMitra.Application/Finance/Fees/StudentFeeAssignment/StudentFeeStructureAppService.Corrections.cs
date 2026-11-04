using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeAdjustment.Entities;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Finance.Admissions;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
	private const string PaymentEditBlocker = "A payment has been recorded for this student and year, including reversed or refunded payments. Only assigned policies can be edited; use the existing financial correction workflows for amounts.";
	private const string DownstreamEditBlocker = "A concession, adjustment, waiver, withdrawal or late-fee assessment already uses these obligations. Review that financial workflow before correcting the assignment.";
	private const string EvidenceEditBlocker = "The saved assignment and its obligations need reconciliation before the structure can be corrected.";
	private const string InactiveEditBlocker = "Activate the student's enrollment for this year before correcting the structure.";

	private static StudentFeeAssignmentPreviewDto? SavedAssignment(StudentFeeStructure assignment)
	{
		try
		{
			var saved = assignment.AssignmentSnapshotJson == null ? null : JsonSerializer.Deserialize<StudentFeeAssignmentPreviewDto>(assignment.AssignmentSnapshotJson);
			return saved?.Heads == null || saved.Lines == null || saved.Policies == null ? null : saved;
		}
		catch (JsonException) { return null; }
	}

	// Batch all dependency checks for the scoped page. Only soft-delete filtering is
	// disabled for historical evidence; the tenant filter remains enabled throughout.
	private async Task<Dictionary<Guid, string?>> StructureEditBlockersAsync(IReadOnlyList<StudentFeeStructure> assignments, List<DueFee>? dues = null)
	{
		var result = assignments.ToDictionary(a => a.Id, _ => (string?)null);
		if (assignments.Count == 0) return result;
		var assignmentIds = assignments.Select(a => a.Id).ToArray();
		dues ??= await dueFeeRepository.GetListAsync(d => d.StudentFeeStructureId.HasValue && Enumerable.Contains(assignmentIds, d.StudentFeeStructureId.Value));
		var dueIds = dues.Select(d => d.Id).ToArray();
		var students = assignments.Select(a => a.StudentId).ToArray();
		var years = assignments.Select(a => a.AcademicYearId).Distinct().ToArray();
		var accountIds = assignments.Where(a => a.FinanceFeeAccountId.HasValue).Select(a => a.FinanceFeeAccountId!.Value).ToArray();
		var accounts = await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>()
			.GetListAsync(a => Enumerable.Contains(accountIds, a.Id));
		using var history = LazyServiceProvider.LazyGetRequiredService<IDataFilter>().Disable<ISoftDelete>();
		var accountApplicationIds = accounts.Select(a => a.ApplicationId).ToArray();
		var applicationQuery = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionApplication, Guid>>().GetQueryableAsync();
		var offerings = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionOffering, Guid>>().GetQueryableAsync();
		var cycles = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionCycle, Guid>>().GetQueryableAsync();
		var applications = await AsyncExecuter.ToListAsync(from a in applicationQuery
			join offering in offerings on a.AdmissionOfferingId equals offering.Id
			join cycle in cycles on offering.AdmissionCycleId equals cycle.Id
			where a.TenantId == CurrentTenant.Id && offering.TenantId == CurrentTenant.Id && cycle.TenantId == CurrentTenant.Id &&
				Enumerable.Contains(years, cycle.AcademicYearId) && (Enumerable.Contains(accountApplicationIds, a.Id) ||
					a.StudentId.HasValue && Enumerable.Contains(students, a.StudentId.Value) || a.EnrolledStudentId.HasValue && Enumerable.Contains(students, a.EnrolledStudentId.Value))
			select a);
		var applicationIds = applications.Select(a => a.Id).ToArray();
		var admissionPayments = await LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionPaymentTransaction, Guid>>()
			.GetListAsync(p => Enumerable.Contains(applicationIds, p.ApplicationId) && p.Amount > 0m);
		var payments = await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeRecordMetadata, Guid>>().GetListAsync(p =>
			Enumerable.Contains(years, p.AcademicYearId) && p.TotalPaidFee > 0 &&
			(p.StudentId.HasValue && Enumerable.Contains(students, p.StudentId.Value) || p.FinanceFeeAccountId.HasValue && Enumerable.Contains(accountIds, p.FinanceFeeAccountId.Value)));
		var paidDues = (await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>()
			.GetListAsync(a => Enumerable.Contains(dueIds, a.DueFeeId))).Select(a => a.DueFeeId).ToHashSet();
		paidDues.UnionWith((await LazyServiceProvider.LazyGetRequiredService<IRepository<LateFeeCollectionAllocation, Guid>>()
			.GetListAsync(a => Enumerable.Contains(dueIds, a.DueFeeId))).Select(a => a.DueFeeId));
		var protectedDues = (await LazyServiceProvider.LazyGetRequiredService<IRepository<ConcessionDueFeeAllocation, Guid>>()
			.GetListAsync(a => Enumerable.Contains(dueIds, a.DueFeeId))).Select(a => a.DueFeeId).ToHashSet();
		protectedDues.UnionWith((await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeAdjustmentSourceLine, Guid>>()
			.GetListAsync(a => Enumerable.Contains(dueIds, a.DueFeeId))).Select(a => a.DueFeeId));
		protectedDues.UnionWith((await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeAdjustmentTargetAllocation, Guid>>()
			.GetListAsync(a => Enumerable.Contains(dueIds, a.DueFeeId))).Select(a => a.DueFeeId));
		protectedDues.UnionWith((await LazyServiceProvider.LazyGetRequiredService<IRepository<LateFeeWaiverApplication, Guid>>()
			.GetListAsync(a => Enumerable.Contains(dueIds, a.DueFeeId))).Select(a => a.DueFeeId));
		// Bulk waiver applications use the same persisted per-due evidence above,
		// including BulkWaiverRequestId. BulkWaiverRequest itself has no EF mapping
		// in this module; it cannot provide durable pending-request evidence.
		var refundRequests = await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeRefundRequest, Guid>>().GetQueryableAsync();
		var refunds = await AsyncExecuter.ToListAsync(refundRequests.SelectMany(request => request.Lines).Where(a =>
			a.DueFeeId.HasValue && Enumerable.Contains(dueIds, a.DueFeeId.Value) || a.LateFeeDueFeeId.HasValue && Enumerable.Contains(dueIds, a.LateFeeDueFeeId.Value))
			.Select(a => new { a.DueFeeId, a.LateFeeDueFeeId }));
		protectedDues.UnionWith(refunds.SelectMany(a => new[] { a.DueFeeId, a.LateFeeDueFeeId }).Where(id => id.HasValue).Select(id => id!.Value));
		var concessions = await LazyServiceProvider.LazyGetRequiredService<IRepository<ConcessionRequest, Guid>>().GetListAsync(a =>
			Enumerable.Contains(students, a.StudentId) && Enumerable.Contains(years, a.AcademicYearId));
		foreach (var assignment in assignments)
		{
			var rows = dues.Where(d => d.StudentFeeStructureId == assignment.Id).ToList();
			var snapshot = SavedAssignment(assignment);
			var account = accounts.FirstOrDefault(a => a.Id == assignment.FinanceFeeAccountId);
			var sources = applications.Where(a => a.Id == account?.ApplicationId || a.StudentId == assignment.StudentId || a.EnrolledStudentId == assignment.StudentId).ToList();
			if (account?.HasPaymentEvidence == true || sources.Any(a => a.HasFinanceFeePayment || admissionPayments.Any(p => p.ApplicationId == a.Id)) ||
				rows.Any(d => d.HasPaymentEvidence() || paidDues.Contains(d.Id)) ||
				payments.Any(p => p.AcademicYearId == assignment.AcademicYearId && (p.StudentId == assignment.StudentId || assignment.FinanceFeeAccountId.HasValue && p.FinanceFeeAccountId == assignment.FinanceFeeAccountId)))
				result[assignment.Id] = PaymentEditBlocker;
			else if (rows.Any(d => d.HasProtectedAssignmentUse() || protectedDues.Contains(d.Id)) ||
				concessions.Any(c => c.StudentId == assignment.StudentId && c.AcademicYearId == assignment.AcademicYearId))
				result[assignment.Id] = DownstreamEditBlocker;
			else if (snapshot == null || snapshot.StudentId != assignment.StudentId || snapshot.AcademicYearId != assignment.AcademicYearId ||
				snapshot.StructureId != assignment.FeeStructureStandardId || snapshot.Heads == null || snapshot.Lines == null || snapshot.RevisionNumber < 1 || snapshot.Lines.Count == 0 || snapshot.Lines.Count != rows.Count ||
				snapshot.Lines.Select(l => l.DueFeeId).Distinct().Count() != rows.Count ||
				snapshot.Lines.Any(l => !rows.Any(d => d.Id == l.DueFeeId && d.StudentId == assignment.StudentId && d.OriginalAmount == l.ProratedAmount &&
					d.ScheduledAmount == l.ScheduledAmount && d.FeeHeadId == l.FeeHeadId && d.OutstandingAmount == d.OriginalAmount)) ||
				assignment.FinanceFeeAccountId.HasValue && (account == null || account.StudentFeeStructureId != assignment.Id || account.StudentId != assignment.StudentId))
				result[assignment.Id] = EvidenceEditBlocker;
		}
		return result;
	}

	private async Task<StudentFeeStructure> RequireCorrectionTargetAsync(AssignStudentFeeStructureDto input)
	{
		var assignment = await studentFeeStructureRepository.FindAsync(a => a.StudentId == input.StudentId && a.AcademicYearId == input.AcademicYearId);
		if (assignment == null || assignment.Id != input.EditAssignmentId || assignment.ConcurrencyStamp != input.AssignmentConcurrencyStamp)
			throw new BusinessException(AssignmentErrorCodes.StalePreview);
		var blocker = (await StructureEditBlockersAsync([assignment]))[assignment.Id];
		if (blocker != null) throw new BusinessException(AssignmentErrorCodes.StructureEditBlocked, blocker).WithData("Reason", blocker);
		return assignment;
	}

	private async Task<PreparedAssignment> PrepareCorrectionAsync(AssignStudentFeeStructureDto input)
	{
		var assignment = await RequireCorrectionTargetAsync(input);
		var enrollment = await RequireEnrollmentAsync(input.StudentId, input.AcademicYearId, true);
		var facts = await ReadAssignmentAdmissionAsync(input.StudentId, input.AcademicYearId, enrollment.StandardId);
		PreparedAssignment prepared;
		if (!assignment.FinanceFeeAccountId.HasValue)
		{
			if (facts != null) throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
			prepared = await PrepareForContextAsync(input, null, isCorrection: true);
		}
		else
		{
			if (facts?.Account == null || facts.Account.Id != assignment.FinanceFeeAccountId)
				throw new BusinessException(AssignmentErrorCodes.SourceUnavailable);
			if (facts.Application.HasFinanceFeePayment)
				throw new BusinessException(AssignmentErrorCodes.StructureEditBlocked, PaymentEditBlocker);
			var proration = await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Finance.Policies.AdmissionProrationPolicyResolver>().ResolveAsync(input.AcademicYearId);
			prepared = await PrepareForContextAsync(input, new(facts.Account.Id, input.StudentId, enrollment.StandardId!.Value,
				facts.Offering.BoardId, facts.Offering.MediumId, null, facts.Revision, facts.JoiningDate, proration, facts.Application.Id, IsFinanceAssignment: true), isCorrection: true);
			prepared.Preview.EnrollmentId = enrollment.Id;
			prepared = prepared with { Account = facts.Account };
		}
		var oldDues = await dueFeeRepository.GetListAsync(d => d.StudentFeeStructureId == assignment.Id);
		return prepared with { Fingerprint = Hash(new { Prepared = prepared.Fingerprint, assignment.ConcurrencyStamp,
			Dues = oldDues.OrderBy(d => d.Id).Select(d => new { d.Id, d.ConcurrencyStamp }), AccountRevision = prepared.Account?.ConcurrencyStamp,
			AdmissionRevision = facts?.Revision }) };
	}

	private async Task<AssignmentCommitResultDto> CommitCorrectionAsync(AssignStudentFeeStructureDto input, PreparedAssignment prepared,
		StudentFeeStructure existing, StudentFeeStructure replacement, string fingerprint)
	{
		var oldDues = await dueFeeRepository.GetListAsync(d => d.StudentFeeStructureId == existing.Id);
		var before = SavedAssignment(existing) ?? throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
		var beforeDues = JsonSerializer.Serialize(oldDues);
		for (var i = 0; i < prepared.Dues.Count; i++)
		{
			prepared.Dues[i].BindAssignment(existing.Id);
			prepared.Preview.Lines[i].DueFeeId = prepared.Dues[i].Id;
		}
		var snapshot = JsonSerializer.Serialize(prepared.Preview);
		existing.CorrectUnpaidAssignment(replacement, fingerprint, snapshot, before.RevisionNumber, beforeDues,
			input.EditReason!, CurrentUser.Id!.Value, new DateTimeOffset(Clock.Now).ToUniversalTime());
		// ABP DeleteAsync reloads a soft-deleted entity and its concurrency stamp.
		// Use a named transition plus normal UPDATE to retain the stamp observed by
		// eligibility/preview and contend with collection on the canonical due.
		// Flush before INSERT to free filtered unique schedule keys in this transaction.
		foreach (var due in oldDues)
		{
			due.SupersedeUnusedAssignment(existing.Id);
			await dueFeeRepository.UpdateAsync(due, autoSave: false);
		}
		await unitOfWorkManager.Current!.SaveChangesAsync();
		await dueFeeRepository.InsertManyAsync(prepared.Dues, autoSave: false);
		await studentFeeStructureRepository.UpdateAsync(existing, autoSave: false);
		if (prepared.Account != null)
		{
			prepared.Account.CorrectUnpaidAssignment(existing.Id, prepared.Structure.Id, prepared.Preview.RevisionNumber, snapshot, fingerprint);
			await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceFeeAccount, Guid>>().UpdateAsync(prepared.Account, false);
			var applications = LazyServiceProvider.LazyGetRequiredService<IRepository<AdmissionApplication, Guid>>();
			var application = await applications.GetAsync(prepared.Account.ApplicationId);
			var selection = application.GetFeeSelection();
			application.CorrectUnpaidFinanceSelection(prepared.Account.Id, new(prepared.Structure.Id, prepared.Preview.StructureName,
				prepared.Preview.RevisionNumber, existing.AcademicYearId, prepared.Account.StandardId,
				selection?.OwnerStageId ?? prepared.Account.MediumStageId ?? throw new BusinessException(AssignmentErrorCodes.SourceUnavailable),
				prepared.Account.Currency, prepared.Preview.Heads.Where(h => h.Included).Select(h => new AdmissionFeeSelectionHead(
					h.FeeHeadId, h.FeeHeadName, h.ConfiguredAmount, h.IsOptional, h.IsOneTime, h.PaymentTermId, h.PaymentTermVersionId, h.IsVariable)).ToList(),
				CurrentUser.Id!.Value, new DateTimeOffset(Clock.Now).ToUniversalTime(), selection?.ConfirmedBy, selection?.OnboardingConfirmedAt), input.EditReason!);
			await applications.UpdateAsync(application, false);
		}
		var student = await studentRepository.GetForFeeAssignmentAsync(input.StudentId) ?? throw new BusinessException(AssignmentErrorCodes.EnrollmentRequired);
		if (student.AcademicYearId == input.AcademicYearId) student.AssignFeeStructure(prepared.Structure.Id);
		return new() { AssignmentId = existing.Id, Assignment = prepared.Preview };
	}

	private async Task ValidateBulkCorrectionGroupAsync(IReadOnlyList<AssignStudentFeeStructureDto> items)
	{
		if (!items.Any(i => i.EditAssignmentId.HasValue)) return;
		if (items.Any(i => !i.EditAssignmentId.HasValue)) throw new BusinessException(AssignmentErrorCodes.MixedOriginalStructures);
		var originals = new HashSet<(Guid, int)>();
		foreach (var input in items)
		{
			// Preserve normal per-item failures. Only verified accessible originals
			// participate in the cross-item compatibility check; invalid items fail
			// individually again at preview/commit without exposing their records.
			try { ValidateInput(input); await RequireEnrollmentAsync(input.StudentId, input.AcademicYearId, false); }
			catch (BusinessException) { continue; }
			var assignment = await studentFeeStructureRepository.FindAsync(a => a.StudentId == input.StudentId && a.AcademicYearId == input.AcademicYearId);
			if (assignment == null || assignment.Id != input.EditAssignmentId) continue;
			var snapshot = SavedAssignment(assignment);
			if (snapshot == null) continue;
			var original = (assignment.FeeStructureStandardId, snapshot.RevisionNumber);
			if (assignment.AssignmentRequestFingerprint == CommandFingerprint(input) && assignment.ExtraProperties.TryGetValue("AssignmentCorrectionHistory", out var history))
			{
				using var entries = JsonDocument.Parse(history!.ToString()!);
				var saved = entries.RootElement.EnumerateArray().Last(e => e.GetProperty("CommandFingerprint").GetString() == assignment.AssignmentRequestFingerprint);
				original = (saved.GetProperty("FromStructureId").GetGuid(), saved.GetProperty("FromRevision").GetInt32());
			}
			originals.Add(original);
		}
		if (originals.Count > 1 || items.Select(i => i.AcademicYearId).Distinct().Count() != 1)
			throw new BusinessException(AssignmentErrorCodes.MixedOriginalStructures);
	}
}
