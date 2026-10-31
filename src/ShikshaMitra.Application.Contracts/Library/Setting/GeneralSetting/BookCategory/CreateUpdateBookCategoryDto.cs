using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;

public class BookCategoryCreateRequest
{
	[Required]
	public string CategoryName { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

	public string Description { get; set; } = null!;
}

public class BookCategoryUpdateRequest
{
	[Required]
	public string CategoryName { get; set; } = null!; // Request binding defers initialization; retain the null default and existing validation.

	public string Description { get; set; } = null!;
}
