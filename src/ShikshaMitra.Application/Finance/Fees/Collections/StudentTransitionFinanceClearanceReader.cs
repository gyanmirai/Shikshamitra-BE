using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using Microsoft.Extensions.Localization;
using ShikshaMitra.Localization;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Collections;

public sealed record StudentTransitionFinanceClearance(bool Cleared, string Detail, FeeProrationResultDto? Proration = null,
	bool OutstandingOnly = false, decimal? OutstandingAmount = null, string Evidence = "", bool AssignmentMissing = false);

/// <summary>Finance owns the calculations; Records receives a clearance decision, never a balance formula.</summary>
[RemoteService(false)]
public class StudentTransitionFinanceClearanceReader(IRepository<StudentFeeStructure, Guid> assignments,
	IRepository<DueFee, Guid> dues, IRepository<FeeRecordMetadata, Guid> collections,
	StudentFeeDetailsQueryService details, WithdrawalProrationService proration, IStringLocalizer<ShikshaMitraResource> messages) : ApplicationService, ITransientDependency
{
	public async Task<StudentTransitionFinanceClearance> GetAsync(Guid studentId, Guid yearId, DateTime? withdrawalDate = null, bool requireClearance = true, bool inspectExisting = false)
	{
		WithdrawalProrationPlan? plan = null;
		if (withdrawalDate.HasValue)
		{
			try { plan = await proration.PreviewAsync(studentId, yearId, withdrawalDate.Value); }
			catch (BusinessException error)
			{
				return new(false, error.Code == ProrationPolicyErrorCodes.InvalidChargePeriod
					? "The actual leaving date cannot be before the academic-year start or the registration joining date."
					: messages[error.Code ?? ProrationPolicyErrorCodes.WithdrawalEvidenceInvalid]);
			}
		}
		if (!requireClearance && !inspectExisting) return new(true, "Finance withdrawal calculation is ready.", plan?.Result);
		var assignment = await assignments.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.AcademicYearId == yearId);
		if (assignment == null && !requireClearance)
		{
			var unassigned = await details.GetCollectFormAsync(studentId, yearId);
			if (unassigned.Ledger.Any(x => x.Items.Count > 0) || unassigned.Charges.Any(x => x.PendingAmount > 0 || x.ReconciliationAmount > 0) ||
				await collections.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.AcademicYearId == yearId && x.Status == FeeCollectionStatus.InReconciliation))
				return new(false, "Finance must reconcile source-year obligations without a matching fee assignment.", plan?.Result);
			return new(true, "No source-year Finance assignment is available; no clearance is asserted.", plan?.Result, AssignmentMissing: true);
		}
		if (assignment == null || string.IsNullOrWhiteSpace(assignment.AssignmentSnapshotJson))
			return new(false, "Finance must assign or reconcile the source-year fee structure before clearance can be verified.");
		StudentFeeAssignmentPreviewDto? evidence;
		try { evidence = JsonSerializer.Deserialize<StudentFeeAssignmentPreviewDto>(assignment.AssignmentSnapshotJson); }
		catch (JsonException) { return new(false, "Finance assignment evidence is unavailable or invalid."); }
		if (evidence == null || evidence.StudentId != studentId || evidence.AcademicYearId != yearId)
			return new(false, "Finance assignment evidence does not match this student and year.");
		var expected = evidence.Lines.Select(x => x.DueFeeId).ToList();
		var actual = await dues.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentFeeStructureId == assignment.Id && x.StudentId == studentId);
		if (expected.Any(x => !x.HasValue) || expected.Distinct().Count() != expected.Count || actual.Count != expected.Count || actual.Any(x => !expected.Contains(x.Id)))
			return new(false, "Finance must reconcile missing or mismatched obligations before clearance.");
		var form = await details.GetCollectFormAsync(studentId, yearId);
		var credits = plan?.Lines.ToDictionary(x => x.DueFeeId, x => x.Credit) ?? new();
		if (form.Ledger.SelectMany(x => x.Items).Any(x => x.ReconciliationAmount > 0) || form.Charges.Any(x => x.ReconciliationAmount > 0) ||
			await collections.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.AcademicYearId == yearId && x.Status == FeeCollectionStatus.InReconciliation))
			return new(false, "A source-year payment is awaiting Finance reconciliation.");
		var pending = form.Ledger.SelectMany(x => x.Items).Select(x => new { Id = x.DueFeeId, Amount = Math.Max(0m, Convert.ToDecimal(x.Due) - credits.GetValueOrDefault(x.DueFeeId)) }).Where(x => x.Amount > 0).OrderBy(x => x.Id).ToList();
		var charges = form.Charges.Where(x => x.PendingAmount > 0).OrderBy(x => x.ChargeId).Select(x => new { Id = x.ChargeId, Amount = Convert.ToDecimal(x.PendingAmount) }).ToList();
		var amount = pending.Sum(x => x.Amount) + charges.Sum(x => x.Amount);
		if (amount > 0) return new(false, "Outstanding fees and charges remain payable in Finance.", plan?.Result, true, amount, JsonSerializer.Serialize(new { pending, charges }));
		return new(true, "Finance source-year obligations and payment reconciliation are clear.", plan?.Result, OutstandingAmount: 0);
	}
}
