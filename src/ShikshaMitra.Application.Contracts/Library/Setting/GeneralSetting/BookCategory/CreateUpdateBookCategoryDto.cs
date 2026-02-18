using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;

public class BookCategoryCreateRequest
{
	[Required]
	public string CategoryName { get; set; }

	public string Description { get; set; }
}

public class BookCategoryUpdateRequest
{
	[Required]
	public string CategoryName { get; set; }

	public string Description { get; set; }
}
