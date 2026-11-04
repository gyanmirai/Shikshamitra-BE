using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;
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
	IRepository<StudentFeeStructure, Guid> assignmentRepository) : ITransientDependency
{
	public async Task<Dictionary<Guid, FeeHeadDependenciesDto>> GetAsync(IEnumerable<Guid> feeHeadIds)
	{
		var ids = feeHeadIds.Distinct().ToArray();
		if (ids.Length == 0) return [];
		var idSet = ids.ToHashSet();
		var detailQuery = await detailRepository.GetQueryableAsync();
		var details = await detailRepository.AsyncExecuter.ToListAsync(detailQuery
			.Where(x => Enumerable.Contains(ids, x.FeeHeadId))
			.Select(x => new { x.FeeHeadId, x.FeeStructureId, x.PaymentTermId }));
		var detailsByHead = details.ToLookup(x => x.FeeHeadId);
		var detailsByStructure = details.ToLookup(x => x.FeeStructureId);
		var query = await dueRepository.GetQueryableAsync();
		var references = await dueRepository.AsyncExecuter.ToListAsync(query
			.Where(x => Enumerable.Contains(ids, x.FeeHeadId))
			.Select(x => new { x.FeeHeadId, x.StudentId, x.StudentFeeStructureId }).Distinct());
		var referencesByHead = references.ToLookup(x => x.FeeHeadId);
		var structureIds = details.Select(x => x.FeeStructureId).Distinct().ToArray();
		var assignments = await assignmentRepository.GetQueryableAsync();
		var assignmentsUsingStructures = await assignmentRepository.AsyncExecuter.ToListAsync(assignments
			.Where(a => Enumerable.Contains(structureIds, a.FeeStructureStandardId))
			.Select(a => new { a.Id, a.StudentId, a.FeeStructureStandardId, a.AssignmentSnapshotJson }));
		var explicitReferences = new List<(Guid FeeHeadId, Guid? StudentId, Guid? AssignmentId)>();
		foreach (var assignment in assignmentsUsingStructures)
		{
			// Zero-valued selected heads may intentionally generate no dues. Count their assignment evidence too.
			List<Guid>? included = null;
			if (assignment.AssignmentSnapshotJson != null)
			{
				try { included = JsonSerializer.Deserialize<StudentFeeAssignmentPreviewDto>(assignment.AssignmentSnapshotJson)?.Heads?.Where(h => h.Included).Select(h => h.FeeHeadId).ToList(); }
				catch (JsonException) { /* Preserve conservative references for legacy/corrupt evidence. */ }
			}
			foreach (var id in included ?? detailsByStructure[assignment.FeeStructureStandardId].Select(d => d.FeeHeadId).ToList())
				if (idSet.Contains(id)) explicitReferences.Add((id, assignment.StudentId, assignment.Id));
		}
		var explicitByHead = explicitReferences.ToLookup(x => x.FeeHeadId);
		return ids.ToDictionary(id => id, id => {
			var dues = referencesByHead[id];
			var heads = detailsByHead[id];
			var explicitForHead = explicitByHead[id];
			var explicitStudents = explicitForHead.Select(x => x.StudentId).ToHashSet();
			var assigned = explicitForHead
				.Select(x => (x.StudentId, x.AssignmentId)).Concat(dues
					.Where(x => x.StudentFeeStructureId.HasValue || !explicitStudents.Contains(x.StudentId))
					.Select(x => (x.StudentId, x.StudentFeeStructureId))).Distinct().ToList();
			return new FeeHeadDependenciesDto {
				StudentCount = assigned.Where(x => x.StudentId.HasValue).Select(x => x.StudentId).Distinct().Count(),
				// Legacy dues without an assignment ID still represent an assigned obligation.
				AssignmentCount = assigned.Count,
				FeeStructureCount = heads.Select(x => x.FeeStructureId).Distinct().Count(),
				PaymentTermCount = heads.Where(x => x.PaymentTermId.HasValue).Select(x => x.PaymentTermId).Distinct().Count()
			};
		});
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
