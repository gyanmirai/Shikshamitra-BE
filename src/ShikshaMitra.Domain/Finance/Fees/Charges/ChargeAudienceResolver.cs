using ShikshaMitra.Finance.Charges;
using ShikshaMitra.Students;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Finance.Fees.Charges;

/// <summary>
/// Resolves a <see cref="ChargeAudience"/> to distinct enrolled student IDs.
/// </summary>
public class ChargeAudienceResolver(IStudentRepository studentRepository) : DomainService
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

		var candidateIds = candidates.Select(s => s.Id).ToHashSet();

		IReadOnlyList<Guid> resolved;

		if (audience.Selection.Mode == StudentSelectionMode.AllInScope)
		{
			resolved = candidateIds.ToList();
		}
		else
		{
			var explicitIds = audience.Selection.StudentIds!;
			var invalid = explicitIds.Where(id => !candidateIds.Contains(id)).ToList();
			if (invalid.Count > 0)
			{
				throw new BusinessException(FinanceChargeErrorCodes.StudentNotInChargeScope)
					.WithData("StudentIds", string.Join(",", invalid))
					.WithData("Message", "One or more selected students are outside the academic scope");
			}

			resolved = explicitIds.Distinct().ToList();
		}

		if (resolved.Count > MaxBatchSize)
		{
			throw new BusinessException(FinanceChargeErrorCodes.ChargeBatchTooLarge)
				.WithData("Count", resolved.Count)
				.WithData("Max", MaxBatchSize);
		}

		foreach (var student in candidates.Where(s => resolved.Contains(s.Id)))
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
