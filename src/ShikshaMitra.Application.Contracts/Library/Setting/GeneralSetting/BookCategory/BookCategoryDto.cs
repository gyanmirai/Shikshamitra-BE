using System;

namespace ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;

public class BookCategoryDto
{
	public Guid Id { get; set; }
	public string CategoryName { get; set; } = null!; // The response mapper populates these members after construction.
	public string Description { get; set; } = null!;
}
