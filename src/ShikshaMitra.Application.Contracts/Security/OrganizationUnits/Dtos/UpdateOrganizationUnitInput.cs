using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class UpdateOrganizationUnitInput : IValidatableObject
{
	public string DisplayName { get; set; }

	public Guid Id { get; set; }

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (DisplayName.IsNullOrWhiteSpace())
		{
			yield return new ValidationResult(
				nameof(DisplayName)
			);
		}
	}
}