using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.Dtos;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Settings;

public class FeeHeadDependencyQuery(
	IRepository<FeeStructureDetail, Guid> detailRepository,
	IRepository<DueFee, Guid> dueRepository,
	IRepository<StudentFeeStructure, Guid> assignmentRepository,
	IRepository<ShikshaMitra.Domain.Finance.Fees.Settings.FeeHead, Guid> headRepository) : ITransientDependency
{
	public async Task<Dictionary<Guid, FeeHeadDependenciesDto>> GetAsync(IEnumerable<Guid> feeHeadIds)
	{
		var ids = feeHeadIds.Distinct().ToArray();
		if (ids.Length == 0) return [];
		var details = (await detailRepository.GetQueryableAsync()).Where(x => Enumerable.Contains(ids, x.FeeHeadId));
		var structures = await detailRepository.AsyncExecuter.ToListAsync(details.GroupBy(x => x.FeeHeadId).Select(group => new {
			FeeHeadId = group.Key,
			StructureCount = group.Select(x => x.FeeStructureId).Distinct().Count(),
			TermCount = group.Where(x => x.PaymentTermId.HasValue).Select(x => x.PaymentTermId).Distinct().Count()
		}));
		var assignments = await assignmentRepository.GetQueryableAsync();
		// The small normalized evidence includes selected zero-value heads. NULL is deliberately
		// conservative for legacy/corrupt snapshots, using that assignment's structure heads.
		// Expand database head rows, never a primitive collection SelectMany (unsupported
		// by one or both providers). Scalar membership retains PostgreSQL's array index path.
		var requestedHeads = (await headRepository.GetQueryableAsync()).Where(x => Enumerable.Contains(ids, x.Id));
		var normalizedReferences = from assignment in assignments.Where(x => x.IncludedFeeHeadIds != null && x.IncludedFeeHeadIds.Any(headId => Enumerable.Contains(ids, headId)))
			from head in requestedHeads
			where Enumerable.Contains(assignment.IncludedFeeHeadIds!, head.Id)
			select new { FeeHeadId = head.Id, StudentId = (Guid?)assignment.StudentId, AssignmentId = (Guid?)assignment.Id };
		var legacyReferences = from detail in details
			join assignment in assignments.Where(x => x.IncludedFeeHeadIds == null) on detail.FeeStructureId equals assignment.FeeStructureStandardId
			select new { detail.FeeHeadId, StudentId = (Guid?)assignment.StudentId, AssignmentId = (Guid?)assignment.Id };
		var explicitReferences = normalizedReferences.Union(legacyReferences);
		var dues = (await dueRepository.GetQueryableAsync()).Where(x => Enumerable.Contains(ids, x.FeeHeadId));
		// An unbound legacy due is one assignment only when no explicit assignment for the
		// same student/head exists. Bound dues retain their independent assignment identity.
		var dueReferences = dues.Where(due => due.StudentFeeStructureId.HasValue || !explicitReferences.Any(reference =>
			reference.FeeHeadId == due.FeeHeadId && reference.StudentId == due.StudentId))
			.Select(due => new { due.FeeHeadId, due.StudentId, AssignmentId = due.StudentFeeStructureId });
		var counts = await assignmentRepository.AsyncExecuter.ToListAsync(explicitReferences.Union(dueReferences)
			.GroupBy(x => x.FeeHeadId).Select(group => new {
				FeeHeadId = group.Key, AssignmentCount = group.Count(),
				StudentCount = group.Where(x => x.StudentId.HasValue).Select(x => x.StudentId).Distinct().Count()
			}));
		var result = ids.ToDictionary(id => id, _ => new FeeHeadDependenciesDto());
		foreach (var row in structures) { result[row.FeeHeadId].FeeStructureCount = row.StructureCount; result[row.FeeHeadId].PaymentTermCount = row.TermCount; }
		foreach (var row in counts) { result[row.FeeHeadId].StudentCount = row.StudentCount; result[row.FeeHeadId].AssignmentCount = row.AssignmentCount; }
		return result;
	}

	public static BusinessException Blocked(string code, string name, FeeHeadDependenciesDto dependencies) =>
		new BusinessException(code)
			.WithData("Name", name)
			.WithData("Students", Count(dependencies.StudentCount, "student"))
			.WithData("Assignments", Count(dependencies.AssignmentCount, "assignment"))
			.WithData("FeeStructures", Count(dependencies.FeeStructureCount, "fee structure"))
			.WithData("PaymentTerms", Count(dependencies.PaymentTermCount, "payment term"))
			.WithData("StudentCount", dependencies.StudentCount)
			.WithData("AssignmentCount", dependencies.AssignmentCount)
			.WithData("FeeStructureCount", dependencies.FeeStructureCount)
			.WithData("PaymentTermCount", dependencies.PaymentTermCount);
	private static string Count(int value, string singular) => $"{value} {singular}{(value == 1 ? string.Empty : "s")}";
}
