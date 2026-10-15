using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Accounts.Incomes;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.FeeRefund;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Collections;

[ExposeServices(typeof(AdmissionFeeCollectionAppService), typeof(IAdmissionFeeCollectionAppService), typeof(IAdmissionFinanceIntegration))]
public partial class AdmissionFeeCollectionAppService : IAdmissionFinanceIntegration
{
    public async Task LinkFinalizedStudentAsync(Guid applicationId, Guid studentId)
    {
        await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.AdmissionApplications.Finalize);
        var account = await accounts.FindAsync(a => a.ApplicationId == applicationId);
        if (account == null) return;
        var source = await contextReader.GetAsync(applicationId);
        EnsureHandoffContext(source, account, studentId);
        await LinkAccountStudentAsync(source, account, studentId);
    }

    public async Task BindActivatedStudentAsync(Guid applicationId, Guid studentId, Guid enrollmentId)
    {
        await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Activate);
        var account = await accounts.FindAsync(a => a.ApplicationId == applicationId);
        if (account == null) return;
        var source = await contextReader.GetAsync(applicationId);
        EnsureHandoffContext(source, account, studentId);
        if (enrollmentId == Guid.Empty || source.EnrollmentId != enrollmentId)
            throw new BusinessException(AdmissionFeeCollectionErrorCodes.InvalidState);
        await BindEnrolledStudentAsync(source, account);
    }

    private void EnsureHandoffContext(AdmissionFinanceContext source, FinanceFeeAccount account, Guid studentId)
    {
        if (CurrentUnitOfWork == null || studentId == Guid.Empty || source.StudentId != studentId ||
            account.TenantId != CurrentTenant.Id || account.ApplicationId != source.ApplicationId ||
            account.ApplicantId != source.ApplicantId || account.AcademicYearId != source.AcademicYearId ||
            account.BoardId != source.BoardId || account.EducationMediumId != source.MediumId ||
            account.MediumStageId != source.StageId || account.StandardId != source.StandardId)
            throw new BusinessException(AdmissionFeeCollectionErrorCodes.InvalidState);
    }

    private async Task LinkAccountStudentAsync(AdmissionFinanceContext source, FinanceFeeAccount account, Guid studentId)
    {
        if (account.StudentId.HasValue && account.StudentId != studentId)
            throw new BusinessException(AdmissionFeeCollectionErrorCodes.AssignmentConflict);
        if (!account.StudentLinkedAt.HasValue || account.DivisionId != source.DivisionId)
        {
            account.LinkStudent(studentId, new DateTimeOffset(Clock.Now.ToUniversalTime()));
            account.SetDivision(source.DivisionId);
            await accounts.UpdateAsync(account, false);
        }
        var accountDues = await dues.GetListAsync(d => d.FinanceFeeAccountId == account.Id && d.StudentId != studentId);
        foreach (var due in accountDues) due.LinkStudent(studentId);
        await dues.UpdateManyAsync(accountDues, false);
        var metadata = await transactions.GetListAsync(t => t.FinanceFeeAccountId == account.Id && t.StudentId != studentId);
        foreach (var row in metadata) row.LinkStudent(studentId);
        await transactions.UpdateManyAsync(metadata, false);
        var feeRowsRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<FeeCollectionRecord, Guid>>();
        var feeRows = await feeRowsRepository.GetListAsync(r => r.FinanceFeeAccountId == account.Id && r.StudentId != studentId);
        foreach (var row in feeRows) row.LinkStudent(studentId);
        await feeRowsRepository.UpdateManyAsync(feeRows, false);
        var methodRows = await paymentMethods.GetListAsync(p => p.FinanceFeeAccountId == account.Id && p.StudentId != studentId);
        foreach (var row in methodRows) row.LinkStudent(studentId);
        await paymentMethods.UpdateManyAsync(methodRows, false);
        var receiptRows = await receipts.GetListAsync(r => r.FinanceFeeAccountId == account.Id && r.StudentId != studentId);
        foreach (var row in receiptRows) row.LinkStudent(studentId);
        await receipts.UpdateManyAsync(receiptRows, false);
        var incomeRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<Transaction, Guid>>();
        var incomeRows = await incomeRepository.GetListAsync(t => t.FinanceFeeAccountId == account.Id && t.StudentId != studentId);
        foreach (var row in incomeRows) row.LinkStudent(studentId);
        await incomeRepository.UpdateManyAsync(incomeRows, false);
        var refundRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<FeeRefundRequest, Guid>>();
        var refundRows = await refundRepository.GetListAsync(r => r.FinanceFeeAccountId == account.Id && r.StudentId != studentId);
        foreach (var row in refundRows) row.LinkStudent(studentId);
        await refundRepository.UpdateManyAsync(refundRows, false);
        // Receipt bytes, amount/date evidence, allocations, retry fingerprint and result JSON are immutable.
    }

    private async Task BindEnrolledStudentAsync(AdmissionFinanceContext source, FinanceFeeAccount account)
    {
        if (!source.StudentId.HasValue || !source.EnrollmentId.HasValue)
            throw new BusinessException(AdmissionFeeCollectionErrorCodes.InvalidState);
        EnsureHandoffContext(source, account, source.StudentId.Value);
        var assignmentRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<StudentFeeStructure, Guid>>();
        var assignment = await assignmentRepository.FindAsync(a => a.StudentId == source.StudentId.Value && a.AcademicYearId == source.AcademicYearId);
        if (account.StudentFeeStructureId.HasValue)
        {
            if (assignment == null || assignment.Id != account.StudentFeeStructureId || assignment.FinanceFeeAccountId != account.Id)
                throw new BusinessException(AdmissionFeeCollectionErrorCodes.AssignmentConflict);
            await LinkAccountStudentAsync(source, account, source.StudentId.Value);
            return; // Historical retries must not replace the student's later academic placement.
        }
        if (account.AssignmentSnapshotJson == null)
        {
            await LinkAccountStudentAsync(source, account, source.StudentId.Value);
            return;
        }
        var studentRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<Student, Guid>>();
        var student = await studentRepository.GetAsync(source.StudentId.Value);
        if (student.TenantId != CurrentTenant.Id || student.Status != StudentLifeCycleStatusEnum.Enrolled ||
            student.AcademicYearId != source.AcademicYearId || student.StandardId != source.StandardId ||
            student.AcademicBoardId != source.BoardId || student.AcademicMediumId != source.MediumId)
            throw new BusinessException(AdmissionFeeCollectionErrorCodes.InvalidState);
        await LinkAccountStudentAsync(source, account, student.Id);
        var saved = ReadAssignment(account);
        if (!SelectionMatches(source, saved))
        {
            // Opening and cancelling collection is optional. A never-used draft must
            // not block activation after the operator confirms another structure.
            await ResetUnusedPreparationAsync(account);
            await accounts.UpdateAsync(account, false);
            return;
        }
        var existingDues = await dues.GetListAsync(d => d.FinanceFeeAccountId == account.Id);
        if (assignment != null)
        {
            if (assignment.FinanceFeeAccountId != account.Id || assignment.FeeStructureStandardId != saved.StructureId ||
                account.StudentFeeStructureId.HasValue && account.StudentFeeStructureId != assignment.Id)
                throw new BusinessException(AdmissionFeeCollectionErrorCodes.AssignmentConflict);
        }
        else
        {
            if (account.StudentFeeStructureId.HasValue) throw new BusinessException(AdmissionFeeCollectionErrorCodes.AssignmentConflict);
            var policyReferences = saved.Policies.ToDictionary(p => p.Family);
            if (!policyReferences.TryGetValue("collection", out var collection))
                throw new BusinessException(AdmissionFeeCollectionErrorCodes.NotPrepared);
            var concession = policyReferences.GetValueOrDefault("concession");
            var version = concession == null ? null : await LazyServiceProvider.LazyGetRequiredService<IRepository<FinancePolicyVersion, Guid>>().GetAsync(concession.VersionId);
            var concessionSnapshot = version == null ? null : JsonSerializer.Deserialize<ConcessionPolicySnapshot>(version.SnapshotJson);
            assignment = new StudentFeeStructure(GuidGenerator.Create(), CurrentTenant.Id, student.Id, saved.StructureId,
                source.AcademicYearId, concession?.PolicyId, concession?.VersionId,
                concessionSnapshot?.FeeHeadScope ?? ConcessionFeeHeadScope.EntireFeeStructure, concessionSnapshot?.ApplicableFeeHeadIds ?? [],
                collection.PolicyId, collection.VersionId, policyReferences.GetValueOrDefault("refund")?.PolicyId,
                policyReferences.GetValueOrDefault("refund")?.VersionId, policyReferences.GetValueOrDefault("proration")?.PolicyId,
                policyReferences.GetValueOrDefault("proration")?.VersionId, saved.ChargeStartDate, saved.ChargeEndDate,
                financeFeeAccountId: account.Id);
            saved.StudentId = student.Id;
            saved.EnrollmentId = source.EnrollmentId;
            assignment.CaptureAssignment(account.AssignmentRequestFingerprint!, JsonSerializer.Serialize(saved));
            await assignmentRepository.InsertAsync(assignment, false);
        }
        var unbound = existingDues.Where(d => d.StudentFeeStructureId != assignment.Id).ToList();
        foreach (var due in unbound) due.BindAssignment(assignment.Id);
        await dues.UpdateManyAsync(unbound, false);
        if (account.StudentFeeStructureId != assignment.Id)
        {
            account.BindStudentAssignment(assignment.Id);
            await accounts.UpdateAsync(account, false);
        }
        if (student.FeeStructureId != saved.StructureId)
        {
            student.AssignFeeStructure(saved.StructureId);
            await studentRepository.UpdateAsync(student, false);
        }
    }

    private async Task ResetUnusedPreparationAsync(FinanceFeeAccount account)
    {
        if (account.HasPaymentEvidence || account.StudentFeeStructureId.HasValue ||
            await transactions.AnyAsync(t => t.FinanceFeeAccountId == account.Id ||
                account.StudentId.HasValue && t.StudentId == account.StudentId && t.AcademicYearId == account.AcademicYearId) ||
            account.StudentId.HasValue && await LazyServiceProvider.LazyGetRequiredService<IRepository<StudentFeeStructure, Guid>>()
                .AnyAsync(a => a.StudentId == account.StudentId && a.AcademicYearId == account.AcademicYearId))
            throw new BusinessException(AdmissionFeeCollectionErrorCodes.SelectionChanged);
        var draftRows = await dues.GetListAsync(d => d.FinanceFeeAccountId == account.Id);
        var ids = draftRows.Select(d => d.Id).ToList();
        if (draftRows.Any(d => d.PaidAmount != 0m || d.RefundedAmount != 0m || d.NetAmount != 0m || d.StudentFeeStructureId.HasValue) ||
            await allocations.AnyAsync(a => ids.Contains(a.DueFeeId)))
            throw new BusinessException(AdmissionFeeCollectionErrorCodes.AssignmentConflict);
        account.ResetUnpaidPreparation();
        await dues.DeleteManyAsync(draftRows, false);
        await CurrentUnitOfWork!.SaveChangesAsync();
    }
}
