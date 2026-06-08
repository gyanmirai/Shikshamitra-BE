using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Common;

public static class FinanceClassDivisionFilterValidator
{
	public static void Validate(IFinanceClassDivisionFilter filter)
	{
		Normalize(filter);

		if (filter.DivisionId.HasValue && !filter.StandardId.HasValue)
		{
			throw new AbpValidationException(
				"Validation failed",
				[
					new ValidationResult(
						"StandardId is required when DivisionId is provided.",
						[nameof(IFinanceClassDivisionFilter.StandardId)])
				]);
		}
	}

	public static void Normalize(IFinanceClassDivisionFilter filter)
	{
		if (filter.StandardId is Guid standardId && standardId == Guid.Empty)
		{
			filter.StandardId = null;
		}

		if (filter.DivisionId is Guid divisionId && divisionId == Guid.Empty)
		{
			filter.DivisionId = null;
		}
	}
}
