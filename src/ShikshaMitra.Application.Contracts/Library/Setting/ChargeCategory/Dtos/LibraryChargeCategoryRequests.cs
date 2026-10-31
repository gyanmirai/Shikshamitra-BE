using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Setting.ChargeCategory.Dtos;

public class LibraryChargeCategoryCreateRequest
{
	[Required]
	public string ChargeType { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

	[Required]
	public decimal Amount { get; set; }

	public bool IsFixed { get; set; }

	public string Description { get; set; } = null!;
}

public class LibraryChargeCategoryUpdateRequest
{
	[Required]
	public string ChargeType { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

	[Required]
	public decimal Amount { get; set; }

	public bool IsFixed { get; set; }

	public string Description { get; set; } = null!;
}
