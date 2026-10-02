using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Collections;

public sealed record StudentTransitionFinanceClearance(bool Cleared, string Detail);

/// <summary>Finance owns the calculations; Records receives a clearance decision, never a balance formula.</summary>
[RemoteService(false)]
public class StudentTransitionFinanceClearanceReader(IRepository<StudentFeeStructure, Guid> assignments,
	IRepository<DueFee, Guid> dues, IRepository<FeeRecordMetadata, Guid> collections,
	StudentFeeDetailsQueryService details) : ApplicationService, ITransientDependency
{
	public async Task<StudentTransitionFinanceClearance> GetAsync(Guid studentId, Guid yearId)
	{
		var assignment = await assignments.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.AcademicYearId == yearId);
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
		if (form.Ledger.SelectMany(x => x.Items).Any(x => x.Due > 0 || x.ReconciliationAmount > 0) || form.Charges.Any(x => x.PendingAmount > 0))
			return new(false, "Finance has outstanding fees, charges, late fees, or payments awaiting reconciliation.");
		if (await collections.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.AcademicYearId == yearId && x.Status == FeeCollectionStatus.InReconciliation))
			return new(false, "A source-year payment is awaiting Finance reconciliation.");
		return new(true, "Finance source-year obligations and payment reconciliation are clear.");
	}
}
