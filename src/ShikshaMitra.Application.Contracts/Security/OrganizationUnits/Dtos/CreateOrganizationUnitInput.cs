using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Security.OrganizationUnits.Dtos;

public class CreateOrganizationUnitInput : IValidatableObject
{
	public string DisplayName { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

	public Guid? ParentId { get; set; }

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		if (DisplayName.IsNullOrWhiteSpace())
		{
			yield return new ValidationResult(
				nameof(DisplayName));
		}
	}
}