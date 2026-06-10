using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Finance.Charges;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Charges;

/// <summary>
/// Value object describing who a charge request targets.
/// </summary>
public class ChargeAudience
{
	public AcademicScope Scope { get; private set; } = null!;
	public StudentSelection Selection { get; private set; } = null!;

	private ChargeAudience()
	{
	}

	public ChargeAudience(AcademicScope scope, StudentSelection selection)
	{
		Scope = Check.NotNull(scope, nameof(scope));
		Selection = Check.NotNull(selection, nameof(selection));
	}
}

public class AcademicScope
{
	public Guid BoardId { get; private set; }
	public Guid EducationMediumId { get; private set; }
	public IReadOnlyList<Guid> ClassIds { get; private set; } = [];
	public IReadOnlyList<Guid>? DivisionIds { get; private set; }

	private AcademicScope()
	{
	}

	public AcademicScope(
		Guid boardId,
		Guid educationMediumId,
		IReadOnlyList<Guid> classIds,
		IReadOnlyList<Guid>? divisionIds = null)
	{
		BoardId = Check.NotNull(boardId, nameof(boardId));
		EducationMediumId = Check.NotNull(educationMediumId, nameof(educationMediumId));
		ClassIds = classIds?.Where(id => id != Guid.Empty).Distinct().ToList()
			?? throw new ArgumentException("ClassIds cannot be null", nameof(classIds));

		if (ClassIds.Count == 0)
		{
			throw new BusinessException(FinanceChargeErrorCodes.EmptyChargeAudience)
				.WithData("Message", "At least one class must be specified in the charge audience scope");
		}

		if (divisionIds is { Count: > 0 })
		{
			if (ClassIds.Count != 1)
			{
				throw new BusinessException(FinanceChargeErrorCodes.InvalidChargeAudienceScope)
					.WithData("Message", "Division filters are only allowed when exactly one class is selected");
			}

			DivisionIds = divisionIds.Where(id => id != Guid.Empty).Distinct().ToList();
		}
	}
}

public class StudentSelection
{
	public StudentSelectionMode Mode { get; private set; }
	public IReadOnlyList<Guid>? StudentIds { get; private set; }

	private StudentSelection()
	{
	}

	public StudentSelection(StudentSelectionMode mode, IReadOnlyList<Guid>? studentIds = null)
	{
		Mode = mode;

		if (mode == StudentSelectionMode.Explicit)
		{
			StudentIds = studentIds?.Where(id => id != Guid.Empty).Distinct().ToList()
				?? throw new BusinessException(FinanceChargeErrorCodes.EmptyChargeAudience)
					.WithData("Message", "StudentIds are required when selection mode is Explicit");

			if (StudentIds.Count == 0)
			{
				throw new BusinessException(FinanceChargeErrorCodes.EmptyChargeAudience)
					.WithData("Message", "At least one student must be selected");
			}
		}
	}
}
