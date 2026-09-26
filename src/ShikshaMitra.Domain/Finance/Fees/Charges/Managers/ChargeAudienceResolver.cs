using ShikshaMitra.Students;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;

namespace ShikshaMitra.Finance.Fees.Charges;

/// <summary>
/// Resolves a <see cref="ChargeAudience"/> to distinct enrolled student IDs that have a
/// fee-structure assignment for the requested academic year.
/// </summary>
public class ChargeAudienceResolver(
	IStudentRepository studentRepository,
	IRepository<StudentFeeStructure, Guid> studentFeeStructureRepository) : DomainService
{
	public const int MaxBatchSize = 500;

	public async Task<IReadOnlyList<Guid>> ResolveAsync(ChargeAudience audience, Guid academicYearId)
	{
		var candidates = await studentRepository.GetActiveStudentsInScopeAsync(
			academicYearId,
			audience.Scope.BoardId,
			audience.Scope.EducationMediumId,
			audience.Scope.ClassIds,
			audience.Scope.DivisionIds);

		if (candidates.Count == 0)
		{
			throw new BusinessException(FinanceChargeErrorCodes.EmptyChargeAudience)
				.WithData("Message", "No enrolled students match the selected academic scope");
		}

		var scopedCandidateIds = candidates.Select(s => s.Id).ToHashSet();
		var assignedStudentIds = (await studentFeeStructureRepository.GetListAsync(assignment =>
			assignment.AcademicYearId == academicYearId
			&& scopedCandidateIds.Contains(assignment.StudentId)))
			.Select(assignment => assignment.StudentId)
			.ToHashSet();
		var eligibleCandidates = candidates.Where(student => assignedStudentIds.Contains(student.Id)).ToList();

		IReadOnlyList<Guid> resolved;

		if (audience.Selection.Mode == StudentSelectionMode.AllInScope)
		{
			resolved = assignedStudentIds.ToList();
		}
		else
		{
			var explicitIds = audience.Selection.StudentIds!;
			var invalid = explicitIds.Where(id => !scopedCandidateIds.Contains(id)).ToList();
			if (invalid.Count > 0)
			{
				throw new BusinessException(FinanceChargeErrorCodes.StudentNotInChargeScope)
					.WithData("StudentIds", string.Join(",", invalid))
					.WithData("Message", "One or more selected students are outside the academic scope");
			}

			var unassigned = explicitIds.Where(id => !assignedStudentIds.Contains(id)).ToList();
			if (unassigned.Count > 0)
			{
				throw new BusinessException(FinanceChargeErrorCodes.StudentFeeStructureNotAssigned)
					.WithData("StudentIds", string.Join(",", unassigned))
					.WithData("AcademicYearId", academicYearId)
					.WithData("Message", "A fee structure must be assigned before a charge can be created for the student");
			}

			resolved = explicitIds.Distinct().ToList();
		}

		if (resolved.Count == 0)
		{
			throw new BusinessException(FinanceChargeErrorCodes.EmptyChargeAudience)
				.WithData("Message", "No students with an assigned fee structure match the selected academic scope");
		}

		if (resolved.Count > MaxBatchSize)
		{
			throw new BusinessException(FinanceChargeErrorCodes.ChargeBatchTooLarge)
				.WithData("Count", resolved.Count)
				.WithData("Max", MaxBatchSize);
		}

		foreach (var student in eligibleCandidates.Where(s => resolved.Contains(s.Id)))
		{
			if (student.StandardId is null || student.DivisionId is null)
			{
				throw new BusinessException(FinanceChargeErrorCodes.StudentMissingAcademicScope)
					.WithData("StudentId", student.Id);
			}
		}

		return resolved;
	}
}
