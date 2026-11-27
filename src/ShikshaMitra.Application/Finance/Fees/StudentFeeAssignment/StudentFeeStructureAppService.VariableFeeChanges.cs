using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Fees.Settings.PaymentTerms.Services;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Audit;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment;

public partial class StudentFeeStructureAppService
{
    private const string VariableFeeChanged = "variable_fee_changed";
    private const string OverrideChargePeriodPermission = ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.OverrideChargePeriod;
    private IDataProtector VariableFeeChangeProtector => protectionProvider.CreateProtector("Finance.VariableFeeChange.v1");

    private static bool SupportsVariableFeeChange(StudentFeeStructure assignment, AssignmentHeadDto head, StudentFeeAssignmentPreviewDto snapshot) =>
        head is { IsVariable: true, Included: true, IsOneTime: false } &&
        head.PaymentTermVersionId.HasValue &&
        (head.AmountBasis == "MonthlyRate" || HasRecordedMonthlyInstallments(snapshot, head.FeeHeadId)) &&
        head.ServicePeriod?.StartDate != null && head.ServicePeriod.EndDate != null &&
        !assignment.GetOptionalServiceEndDate(head.FeeHeadId).HasValue &&
        assignment.GetOptionalServicePeriods(head.FeeHeadId).Count == 0;

    private static bool HasRecordedMonthlyInstallments(StudentFeeAssignmentPreviewDto snapshot, Guid headId)
    {
        var lines = snapshot.Lines.Where(line => line.FeeHeadId == headId).ToList();
        return lines.Count > 1 && lines.All(line =>
            line.CoverageStartDate is { } start && line.CoverageEndDate is { } end &&
            start.Year == end.Year && start.Month == end.Month) &&
            lines.Select(line => (line.CoverageStartDate!.Value.Year, line.CoverageStartDate.Value.Month)).Distinct().Count() == lines.Count;
    }

    [Authorize(ViewPermission)]
    [Authorize(AssignPermission)]
    [Authorize(OverrideChargePeriodPermission)]
    public async Task<VariableFeeChangeResult> PreviewVariableFeeChangeAsync(Guid studentId, VariableFeeChangeInput input)
    {
        await RequireEnrollmentAsync(studentId, input.AcademicYearId, true);
        var command = VariableFeeChangeCommand(studentId, input);
        var plan = await PlanVariableFeeChangeAsync(studentId, input);
        var assertion = new PreviewAssertion(CurrentTenant.Id, CurrentUser.Id, command, plan.Fingerprint, Clock.Now.AddMinutes(20));
        return plan.Result with { PreviewToken = VariableFeeChangeProtector.Protect(JsonSerializer.Serialize(assertion)) };
    }

    [Authorize(ViewPermission)]
    [Authorize(AssignPermission)]
    [Authorize(OverrideChargePeriodPermission)]
    public async Task<VariableFeeChangeResult> CommitVariableFeeChangeAsync(Guid studentId, VariableFeeChangeInput input)
    {
        var acceptedAt = Clock.Now;
        var command = VariableFeeChangeCommand(studentId, input);
        try
        {
            return await SaveVariableFeeChangeAsync(studentId, input, command, acceptedAt);
        }
        catch (Exception ex) when (ex.GetType().Name is "DbUpdateException" or "AbpDbConcurrencyException" ||
            ex.GetType().Name == "PostgresException" && ex.GetType().GetProperty("SqlState")?.GetValue(ex) as string == "40001")
        {
            using var read = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
            await RequireEnrollmentAsync(studentId, input.AcademicYearId, true);
            return await ReadVariableFeeChangeReplayAsync(studentId, input, command)
                ?? throw new BusinessException(AssignmentErrorCodes.StalePreview);
        }
    }

    private async Task<VariableFeeChangeResult> SaveVariableFeeChangeAsync(
        Guid studentId, VariableFeeChangeInput input, string command, DateTime acceptedAt)
    {
        using var uow = unitOfWorkManager.Begin(new AbpUnitOfWorkOptions
            { IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
        await RequireEnrollmentAsync(studentId, input.AcademicYearId, true);
        if (await ReadVariableFeeChangeReplayAsync(studentId, input, command) is { } replay) return replay;

        PreviewAssertion? assertion;
        try { assertion = JsonSerializer.Deserialize<PreviewAssertion>(VariableFeeChangeProtector.Unprotect(input.PreviewToken ?? "")); }
        catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException)
        { throw new BusinessException(AssignmentErrorCodes.StalePreview); }
        if (assertion == null || assertion.TenantId != CurrentTenant.Id || assertion.UserId != CurrentUser.Id ||
            assertion.Command != command || assertion.Expires <= acceptedAt)
            throw new BusinessException(AssignmentErrorCodes.StalePreview);

        var plan = await PlanVariableFeeChangeAsync(studentId, input);
        if (plan.Fingerprint != assertion.Prepared ||
            !await LazyServiceProvider.LazyGetRequiredService<IPaymentTermReferenceLock>().AcquireAsync([plan.PaymentTermId]))
            throw new BusinessException(AssignmentErrorCodes.StalePreview);

        foreach (var due in plan.Replaced)
        {
            due.SupersedeUnusedAssignment(plan.Assignment.Id);
            await dueFeeRepository.UpdateAsync(due, autoSave: false);
        }
        // Release the filtered schedule keys inside this transaction before writing replacement dues.
        await CurrentUnitOfWork!.SaveChangesAsync();
        await dueFeeRepository.InsertManyAsync(plan.Added, autoSave: false);

        var before = plan.Assignment.AssignmentSnapshotJson!;
        var result = plan.Result with
        {
            NewInstallments = plan.Added.Select(due => OptionalServiceRestartLine(due, plan.Head.FeeHeadName, true)).ToList(),
        };
        var replacedIds = plan.Replaced.Select(due => (Guid?)due.Id).ToHashSet();
        plan.Snapshot.Lines.RemoveAll(line => replacedIds.Contains(line.DueFeeId));
        plan.Snapshot.Lines.AddRange(result.NewInstallments);
        plan.Snapshot.Lines = plan.Snapshot.Lines.OrderBy(line => line.FeeHeadId).ThenBy(line => line.Sequence).ToList();
        plan.Head.EffectiveAmount = input.MonthlyRate;
        plan.Head.AmountBasis = "MonthlyRate";
        plan.Head.ZeroAmountReason = input.MonthlyRate == 0 ? input.Reason.Trim() : null;
        plan.Head.MonthlyRateEffectiveDate = input.EffectiveDate;
        plan.Head.ServicePeriod = new()
        {
            FeeHeadId = input.FeeHeadId,
            Source = "ManualOverride",
            StartDate = DateTime.SpecifyKind(plan.ServiceStart, DateTimeKind.Utc),
            EndDate = DateTime.SpecifyKind(input.ServiceEndDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
            Reason = input.Reason.Trim(),
        };
        plan.Head.Obligation = plan.Snapshot.Lines.Where(line => line.FeeHeadId == input.FeeHeadId).Sum(line => line.ProratedAmount);
        plan.Snapshot.ScheduledTotal = plan.Snapshot.Lines.Sum(line => line.ScheduledAmount);
        plan.Snapshot.ProratedTotal = plan.Snapshot.Lines.Sum(line => line.ProratedAmount);
        plan.Snapshot.ReductionTotal = plan.Snapshot.ScheduledTotal - plan.Snapshot.ProratedTotal;
        var after = JsonSerializer.Serialize(plan.Snapshot);
        plan.Assignment.ReplaceFutureVariableFeeSnapshot(after);
        await studentFeeStructureRepository.UpdateAsync(plan.Assignment, autoSave: false);
        await LazyServiceProvider.LazyGetRequiredService<FinanceAuditWriter>().WriteAsync(
            input.AcademicYearId, FinanceAuditEntityTypes.FeeAdjustment, plan.Assignment.Id,
            "Variable fee", VariableFeeChanged, input.Reason.Trim(),
            new() { StudentId = studentId },
            context: new VariableFeeChangeEvidence(command, input.FeeHeadId, input.EffectiveDate, before, after, result),
            eventId: input.RequestId);
        await uow.CompleteAsync();
        return result;
    }

    private async Task<VariableFeeChangePlan> PlanVariableFeeChangeAsync(Guid studentId, VariableFeeChangeInput input)
    {
        var assignment = await studentFeeStructureRepository.FindAsync(row =>
            row.StudentId == studentId && row.AcademicYearId == input.AcademicYearId)
            ?? throw new BusinessException(AssignmentErrorCodes.NotAssigned);
        if (input.ConcurrencyStamp != assignment.ConcurrencyStamp)
            throw new BusinessException(AssignmentErrorCodes.StalePreview);
        var snapshot = SavedAssignment(assignment);
        var head = snapshot?.Heads.SingleOrDefault(item => item.FeeHeadId == input.FeeHeadId);
        var savedPeriod = head?.ServicePeriod;
        if (snapshot == null || snapshot.StudentId != studentId || snapshot.AcademicYearId != input.AcademicYearId ||
            snapshot.StructureId != assignment.FeeStructureStandardId || head == null ||
            savedPeriod?.StartDate == null || !SupportsVariableFeeChange(assignment, head, snapshot))
            throw VariableFeeUnavailable("Choose an active recurring variable fee with a recorded service period.");
        if (head.MonthlyRateEffectiveDate is { } previous && input.EffectiveDate < previous)
            throw VariableFeeUnavailable("Choose a date on or after the latest rate change.");

        var start = input.EffectiveDate.ToDateTime(TimeOnly.MinValue);
        var end = input.ServiceEndDate.ToDateTime(TimeOnly.MinValue);
        var year = await academicYearRepository.GetAsync(input.AcademicYearId);
        var structure = await feeStructureRepository.GetAsync(assignment.FeeStructureStandardId);
        var metadata = await feeStructureMetadataRepository.GetAsync(structure.FeeStructureMetadataId);
        var board = metadata.AcademicBoardId is Guid boardId ? await LoadAssignmentBoardAsync(boardId) : null;
        if (start < Clock.Now.Date || start < year.StartDate.Date || end > year.EndDate.Date ||
            start < snapshot.RegistrationJoiningDate?.Date ||
            board != null && HasAssignmentBoardDates(board, year) && (start < board.StartDate.Date || end > board.EndDate.Date))
            throw VariableFeeUnavailable("Choose future dates within the student's service and academic year.");

        var detail = await feeStructureDetailRepository.FindAsync(row => row.FeeStructureId == structure.Id && row.FeeHeadId == input.FeeHeadId)
            ?? throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
        var terms = await LoadAssignmentTermsAsync([detail]);
        var schedule = LazyServiceProvider.LazyGetRequiredService<PaymentTermScheduleResolver>().ResolveFromTerms(detail, terms);
        if (!schedule.IsValid || schedule.PaymentTermId is not Guid paymentTermId ||
            schedule.PaymentTermVersionId != head.PaymentTermVersionId ||
            !MonthlyFeePricing.IsMonthlySchedule(schedule.Installments))
            throw VariableFeeUnavailable("This fee needs a saved monthly payment schedule.");
        var scheduleRange = schedule.Installments.Where(row => row.CoverageStartDate.Date <= end && row.CoverageEndDate.Date >= start)
            .OrderBy(row => row.CoverageStartDate).ToList();
        var coveredThrough = start.AddDays(-1);
        foreach (var row in scheduleRange)
        {
            if (row.CoverageStartDate.Date > coveredThrough.AddDays(1)) break;
            coveredThrough = row.CoverageEndDate.Date;
        }
        if (coveredThrough < end)
            throw VariableFeeUnavailable("Choose dates covered by the fee's payment term.");

        var dues = await dueFeeRepository.GetListAsync(row => row.StudentFeeStructureId == assignment.Id && row.StudentId == studentId && row.FeeHeadId == input.FeeHeadId);
        var lines = snapshot.Lines.Where(line => line.FeeHeadId == input.FeeHeadId).ToList();
        if (lines.Count != dues.Count || lines.Select(line => line.DueFeeId).Distinct().Count() != dues.Count ||
            dues.Any(due => due.CoverageStartDate == null || due.CoverageEndDate == null || due.PaymentTermVersionId != head.PaymentTermVersionId ||
                !lines.Any(line => line.DueFeeId == due.Id && line.ProratedAmount == due.OriginalAmount && line.ScheduledAmount == due.ScheduledAmount)))
            throw VariableFeeUnavailable("Reconcile the recorded installments before changing this fee.");
        if (dues.Any(due => due.CoverageStartDate!.Value.Date < start && due.CoverageEndDate!.Value.Date >= start))
            throw VariableFeeUnavailable("The effective date must begin a new installment.");

        var replaced = dues.Where(due => due.CoverageStartDate!.Value.Date >= start).OrderBy(due => due.InstallmentSequence).ToList();
        if (replaced.Any(due => due.HasPaymentEvidence() || due.HasProtectedAssignmentUse() || due.OutstandingAmount != due.OriginalAmount))
            throw VariableFeeUnavailable("An installment in the selected period already has financial activity.");
        if (replaced.Count > 0)
        {
            var ids = replaced.Select(due => due.Id).ToArray();
            var protectedIds = await OptionalServiceProtectedDueIdsAsync(studentId, input.AcademicYearId, ids);
            using var history = LazyServiceProvider.LazyGetRequiredService<IDataFilter>().Disable<ISoftDelete>();
            if (protectedIds.Overlaps(ids) || await LazyServiceProvider.LazyGetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>()
                .AnyAsync(line => Enumerable.Contains(ids, line.DueFeeId)))
                throw VariableFeeUnavailable("An installment in the selected period has a collection or adjustment record.");
        }

        var policyId = snapshot.ProrationPolicyId;
        var policyVersionId = snapshot.ProrationPolicyVersionId;
        var policy = await LoadVersionAsync("proration", policyId, policyVersionId, year.Id, required: false);
        var rules = policy == null ? null : JsonSerializer.Deserialize<ProrationPolicySnapshot>(policy.SnapshotJson)
            ?? throw new BusinessException(AssignmentErrorCodes.ConfigurationInvalid);
        var added = dueFeeManager.GenerateVariableFeeChangeDues(assignment, detail, schedule, input.MonthlyRate,
            rules, policyVersionId, start, end, year);
        if (added.Count == 0 || added.Any(due => due.CoverageStartDate!.Value.Date < start))
            throw VariableFeeUnavailable("Choose a date at the beginning of a covered month.");

        var result = new VariableFeeChangeResult(replaced.Sum(due => due.OriginalAmount), added.Sum(due => due.OriginalAmount),
            replaced.Select(due => OptionalServiceRestartLine(due, head.FeeHeadName, true)).ToList(),
            added.Select(due => OptionalServiceRestartLine(due, head.FeeHeadName, false)).ToList());
        var reportStart = dues.Count > replaced.Count && savedPeriod.StartDate.Value.Date < start
            ? savedPeriod.StartDate.Value.Date : start;
        return new(assignment, snapshot, head, paymentTermId, replaced, added, reportStart, result,
            Hash(new { assignment.Id, assignment.ConcurrencyStamp, Result = result, Term = schedule.PaymentTermVersionId,
                Replaced = replaced.Select(due => new { due.Id, due.ConcurrencyStamp }), Policy = policy?.SnapshotJson }));
    }

    private async Task<VariableFeeChangeResult?> ReadVariableFeeChangeReplayAsync(Guid studentId, VariableFeeChangeInput input, string command)
    {
        var row = await LazyServiceProvider.LazyGetRequiredService<IRepository<FinanceAuditEvent, Guid>>()
            .FindAsync(item => item.Id == input.RequestId && item.TenantId == CurrentTenant.Id);
        if (row == null) return null;
        var evidence = row.Action == VariableFeeChanged && row.StudentId == studentId &&
            row.AcademicYearId == input.AcademicYearId && row.ContextJson != null
            ? JsonSerializer.Deserialize<VariableFeeChangeEvidence>(row.ContextJson, OptionalServiceAuditJson) : null;
        return evidence?.Command == command ? evidence.Result : throw new BusinessException(AssignmentErrorCodes.StalePreview);
    }

    private string VariableFeeChangeCommand(Guid studentId, VariableFeeChangeInput input)
    {
        if (input.RequestId == Guid.Empty || input.FeeHeadId == Guid.Empty || input.AcademicYearId == Guid.Empty ||
            string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || string.IsNullOrWhiteSpace(input.Reason) ||
            input.Reason.Trim().Length > 500 || input.MonthlyRate is < 0m or > 9999999999.99m ||
            decimal.Round(input.MonthlyRate, 2) != input.MonthlyRate ||
            input.EffectiveDate == default || input.EffectiveDate.Day != 1 || input.ServiceEndDate < input.EffectiveDate)
            throw VariableFeeUnavailable("Enter a monthly amount, a first-of-month effective date, an end date and a reason.");
        return Hash(new { CurrentUser.Id, StudentId = studentId, Input = input with { PreviewToken = null } });
    }

    private static BusinessException VariableFeeUnavailable(string reason) =>
        new BusinessException(AssignmentErrorCodes.StructureEditBlocked, reason).WithData("Reason", reason);

    private sealed record VariableFeeChangeEvidence(string Command, Guid FeeHeadId, DateOnly EffectiveDate,
        string BeforeSnapshotJson, string AfterSnapshotJson, VariableFeeChangeResult Result);
    private sealed record VariableFeeChangePlan(StudentFeeStructure Assignment, StudentFeeAssignmentPreviewDto Snapshot,
        AssignmentHeadDto Head, Guid PaymentTermId, List<DueFee> Replaced, List<DueFee> Added,
        DateTime ServiceStart, VariableFeeChangeResult Result, string Fingerprint);
}
