using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class AdmissionFeeCollectionAppService
{
    [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View)]
    public async Task<FeeCollectFormDto> GetCollectFormAsync(Guid applicationId)
    {
        var source = await contextReader.GetAsync(applicationId);
        var account = await accounts.FirstOrDefaultAsync(a => a.ApplicationId == applicationId && a.AcademicYearId == source.AcademicYearId)
            ?? throw new BusinessException(AdmissionFeeCollectionErrorCodes.NotPrepared);
        await scope.EnsureFeeAccountAccessibleAsync(account);
        if (account.AssignmentSnapshotJson == null)
            throw new BusinessException(AdmissionFeeCollectionErrorCodes.NotPrepared);
        var preview = ReadAssignment(account);
        if (!SelectionMatches(source, preview))
            throw new BusinessException(AdmissionFeeCollectionErrorCodes.SelectionChanged);
        return await BuildCollectFormAsync(source, account, preview);
    }

    private async Task<FeeCollectFormDto> BuildCollectFormAsync(AdmissionFinanceContext source, FinanceFeeAccount account,
        StudentFeeAssignmentPreviewDto preview, List<DueFee>? preparedRows = null)
    {
        if (account.StudentFeeStructureId.HasValue && account.StudentId.HasValue)
        {
            var actual = await studentDetails.GetCollectFormAsync(account.StudentId.Value, account.AcademicYearId);
            actual.Student.FinanceFeeAccountId = account.Id;
            return actual;
        }
        var rows = preparedRows ?? await dues.GetListAsync(d => d.FinanceFeeAccountId == account.Id);
        var metadata = await transactions.GetListAsync(t => t.FinanceFeeAccountId == account.Id && t.Status == FeeCollectionStatus.InReconciliation);
        var transactionIds = metadata.Select(t => t.Id).ToList();
        var pending = await allocations.GetListAsync(a => transactionIds.Contains(a.FeeRecordMetadataId));
        var reconciliation = pending.GroupBy(a => a.DueFeeId).ToDictionary(g => g.Key, g => g.Sum(a => (double)a.Amount));
        var balances = await lateFees.GetBalancesAsync(rows, account.AcademicYearId, Clock.Now.Date);
        var rules = await policies.ResolveEffectiveRulesForAccountAsync(account.Id);
        var names = preview.Heads.ToDictionary(h => h.FeeHeadId, h => h.FeeHeadName);
        var proration = preview.Policies.FirstOrDefault(p => p.Family == "proration");
        var ledger = rows.GroupBy(d => (d.Year, d.Month)).OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new FeeLedgerMonthDto
            {
                Month = g.Key.Month.ToString(), Year = g.Key.Year,
                Items = g.OrderBy(d => d.ScheduleDueDate ?? d.DueDate).ThenBy(d => d.InstallmentSequence)
                    .Select(d => AccountLedgerLine(d, names.GetValueOrDefault(d.FeeHeadId, string.Empty),
                        reconciliation.GetValueOrDefault(d.Id), Clock.Now.Date, proration)).ToList()
            }).ToList();
        foreach (var month in ledger) month.TotalDue = month.Items.Sum(i => i.CollectionAmount > 0 ? i.CollectionAmount : i.Due);
        var lateCharges = rows.Select(d => (Due: d, Balance: balances[d.Id]))
            .Where(x => x.Balance.Total > 0m)
            .Select(x => AccountLateFeeCharge(x.Due, names.GetValueOrDefault(x.Due.FeeHeadId, string.Empty), x.Balance)).ToList();
        var board = await LazyServiceProvider.LazyGetRequiredService<IReadOnlyRepository<EducationBoard, Guid>>().GetAsync(source.BoardId);
        var medium = await LazyServiceProvider.LazyGetRequiredService<IReadOnlyRepository<EducationMedium, Guid>>().GetAsync(source.MediumId);
        var division = source.DivisionId.HasValue
            ? await LazyServiceProvider.LazyGetRequiredService<IReadOnlyRepository<Division, Guid>>().GetAsync(source.DivisionId.Value) : null;
        return new FeeCollectFormDto
        {
            Student = new StudentInfoDto
            {
                StudentId = account.StudentId, FinanceFeeAccountId = account.Id, StudentName = source.ApplicantName,
                BoardId = source.BoardId, BoardName = board.Type.ToString(), EducationMediumId = source.MediumId,
                EducationMediumName = medium.Name, StandardId = source.StandardId, StandardName = source.StandardName,
                DivisionId = source.DivisionId ?? Guid.Empty, DivisionName = division?.Name ?? string.Empty,
                FeeStructureName = preview.StructureName, IsFeeStructureAssigned = true,
            },
            Summary = new FeeSummaryDto
            {
                TotalFee = (double)rows.Sum(d => d.OriginalAmount), PaidFee = (double)rows.Sum(d => d.PaidAmount),
                PendingFee = (double)rows.Sum(d => d.OutstandingAmount), ReconciliationAmount = reconciliation.Values.Sum(),
                TotalCollectionAmount = (double)rows.Sum(d => Math.Ceiling(d.OutstandingAmount)),
                TotalRoundingAdjustment = (double)rows.Sum(d => Math.Ceiling(d.OutstandingAmount) - d.OutstandingAmount),
            },
            Ledger = ledger, Charges = lateCharges, AllowOnlineCollection = rules.Policy?.AllowOnlineCollection ?? true,
        };
    }

    // Read projection only: amounts/status come from canonical dues and the shared balance services.
    private static FeeLedgerLineDto AccountLedgerLine(DueFee due, string name, double reconciliation,
        DateTime asOf, AssignmentPolicyDto? proration) => new()
    {
        DueFeeId = due.Id, FeeHeadId = due.FeeHeadId, FeeHeadName = name,
        InstallmentLabel = due.GetDisplayInstallmentLabel(), DueDate = due.ScheduleDueDate ?? due.DueDate,
        CoverageStartDate = due.CoverageStartDate, CoverageEndDate = due.CoverageEndDate,
        ScheduleSource = due.ScheduleSource.ToString(), Total = (double)due.OriginalAmount, ScheduledTotal = (double)due.ScheduledAmount,
        Paid = (double)due.PaidAmount, Due = (double)due.OutstandingAmount,
        CollectionAmount = (double)Math.Ceiling(due.OutstandingAmount),
        RoundingAmount = (double)(Math.Ceiling(due.OutstandingAmount) - due.OutstandingAmount),
        ReconciliationAmount = reconciliation, ConcessionAmount = (double)due.ConcessionAmount,
        RefundAmount = (double)due.RefundedAmount, Status = DueFeeCollectionStatusResolver.Resolve(due, asOf, reconciliation),
        ProrationPolicyId = due.ProrationPolicyId, ProrationPolicyVersionId = due.ProrationPolicyVersionId,
        ProrationPolicyName = proration != null && proration.VersionId == due.ProrationPolicyVersionId ? proration.Name : null,
        ProrationPolicyVersionNumber = proration != null && proration.VersionId == due.ProrationPolicyVersionId ? proration.Version : null,
        ProrationFactor = due.ProrationFactor, ProrationRuleType = due.ProrationRuleType?.ToString(),
        ProrationRoundingMode = due.ProrationRoundingMode?.ToString(), ProrationEligibleStartDate = due.ProrationEligibleStartDate,
        ProrationEligibleEndDate = due.ProrationEligibleEndDate, ProrationCoverageDays = due.ProrationCoverageDays,
        ProrationEligibleDays = due.ProrationEligibleDays, ProrationChargedDays = due.ProrationChargedDays,
        ProrationExplanation = due.ProrationExplanation,
    };

    private static ChargeDetailDto AccountLateFeeCharge(DueFee due, string name, LateFeeBalanceService.Balance balance) => new()
    {
        ChargeId = due.Id, SourceId = due.Id, SourceType = "LateFee", IsSystemGenerated = true,
        Name = $"Late Fee - {name} ({due.GetDisplayInstallmentLabel()})", Amount = (double)balance.Total,
        PaidAmount = (double)balance.PaidAmount, PendingAmount = (double)balance.PendingAmount,
        WaivedAmount = (double)balance.WaivedAmount, ReconciliationAmount = balance.ReconciliationAmount,
        Explanation = balance.Explanation, CanWaive = balance.PendingAmount > 0m,
        Status = balance.ReconciliationAmount > 0m ? FeeCollectionStatus.InReconciliation
            : balance.PendingAmount == 0m ? balance.WaivedAmount > 0m ? FeeCollectionStatus.Waived : FeeCollectionStatus.Paid
            : balance.PaidAmount > 0m ? FeeCollectionStatus.Partial : FeeCollectionStatus.Pending,
    };
}
