using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Security.OrganizationUnits;

public class PagingBase : IValidatableObject
{
	public const int MaxPageSize = 100000;

	public int PageIndex { get; set; } = 1;

	public int PageSize { get; set; } = 10;


	public int SkipCount => (PageIndex - 1) * PageSize;

	protected PagingBase()
	{
	}

	public PagingBase(int pageIndex = 1, int pageSize = 10)
	{
		PageIndex = pageIndex;
		PageSize = pageSize;
	}

	public virtual IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (PageIndex < 1)
		{
			yield return new ValidationResult(
				"PageIndex"
			);
		}

		if (PageSize > MaxPageSize)
		{
			yield return new ValidationResult(
				"PageSize"
			);
		}
	}
}