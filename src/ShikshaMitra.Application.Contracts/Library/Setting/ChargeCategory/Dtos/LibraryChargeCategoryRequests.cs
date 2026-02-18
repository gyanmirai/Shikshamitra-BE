using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Setting.ChargeCategory.Dtos;

public class LibraryChargeCategoryCreateRequest
{
	[Required]
	public string ChargeType { get; set; }

	[Required]
	public decimal Amount { get; set; }

	public bool IsFixed { get; set; }

	public string Description { get; set; }
}

public class LibraryChargeCategoryUpdateRequest
{
	[Required]
	public string ChargeType { get; set; }

	[Required]
	public decimal Amount { get; set; }

	public bool IsFixed { get; set; }

	public string Description { get; set; }
}
