using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

/// <summary>Read-only integration. The caller must authorize the student's academic scope.</summary>
public class OnboardingFeeReadinessReader(
	IRepository<StudentFeeStructure, Guid> assignments,
	IRepository<DueFee, Guid> dues,
	IRepository<FeeCollectionDueFeeAllocation, Guid> allocations,
	IRepository<FeeRecordMetadata, Guid> collections) : ITransientDependency
{
	public Task<bool> HasAssignmentAsync(Guid studentId, Guid academicYearId) => assignments.AnyAsync(a => a.StudentId == studentId && a.AcademicYearId == academicYearId);
	public async Task<OnboardingFeeReadiness> GetAsync(Guid studentId, Guid academicYearId)
	{
		var assignment = await assignments.FindAsync(a => a.StudentId == studentId && a.AcademicYearId == academicYearId);
		if (assignment == null) return new(false, false, "Assign a fee structure in Finance.");
		if (string.IsNullOrWhiteSpace(assignment.AssignmentSnapshotJson))
			return new(true, false, "The legacy fee assignment needs Finance reconciliation before first-payment readiness can be verified.");
		StudentFeeAssignmentPreviewDto? snapshot;
		try { snapshot = JsonSerializer.Deserialize<StudentFeeAssignmentPreviewDto>(assignment.AssignmentSnapshotJson); }
		catch (JsonException) { return new(true, false, "The fee assignment evidence needs Finance reconciliation."); }
		if (snapshot == null || snapshot.StudentId != studentId || snapshot.AcademicYearId != academicYearId)
			return new(true, false, "The fee assignment evidence does not match this student and academic year.");
		var lines = await dues.GetListAsync(d => d.StudentFeeStructureId == assignment.Id && d.StudentId == studentId);
		var expected = snapshot.Lines.Select(l => l.DueFeeId).ToList();
		if (expected.Any(id => !id.HasValue) || expected.Count != expected.Distinct().Count() ||
			lines.Count != expected.Count || lines.Any(line => !expected.Contains(line.Id)))
			return new(true, false, "Fee obligations are incomplete. Ask Finance to reconcile the assignment.");
		if (lines.Count == 0)
			return new(true, false, snapshot.ProratedTotal == 0m
				? "The saved fee assignment has no payable obligation or first payment. Ask Finance to review the assignment."
				: "Fee obligations are missing.");
		var firstDate = lines.Min(l => l.DueDate.Date);
		var firstInstallment = lines.Where(l => l.DueDate.Date == firstDate).ToList();
		var settled = firstInstallment.All(l => l.OutstandingAmount == 0m);
		if (settled && !firstInstallment.Any(l => l.PaidAmount > 0m))
			return new(true, false, "The first installment has no payment. Ask Finance to review the fee schedule or collect the first payment.");
		if (settled)
		{
			var dueIds = firstInstallment.Select(l => l.Id).ToArray();
			var applied = await allocations.GetListAsync(a => Enumerable.Contains(dueIds, a.DueFeeId));
			var collectionIds = applied.Select(a => a.FeeRecordMetadataId).Distinct().ToArray();
			var confirmed = (await collections.GetListAsync(c => Enumerable.Contains(collectionIds, c.Id) &&
				c.StudentId == studentId && c.AcademicYearId == academicYearId &&
				(c.Status == FeeCollectionStatus.Paid || c.Status == FeeCollectionStatus.Partial)))
				.Select(c => c.Id).ToHashSet();
			// Finance reduces outstanding even while an instrument awaits reconciliation.
			// Require confirmed receipt evidence, not just a zero displayed balance.
			if (firstInstallment.Any(line => decimal.Round(applied.Where(a => a.DueFeeId == line.Id && confirmed.Contains(a.FeeRecordMetadataId))
				.Sum(a => (decimal)a.Amount), 2) < line.PaidAmount))
				return new(true, false, "The first installment payment is awaiting Finance confirmation or reconciliation.");
		}
		return new(true, settled, settled ? "First installment settled in Finance." :
			$"Collect the full first installment (due {firstDate:dd MMM yyyy}) in Finance.");
	}
}

public sealed record OnboardingFeeReadiness(bool Assigned, bool FirstInstallmentSettled, string Detail);
