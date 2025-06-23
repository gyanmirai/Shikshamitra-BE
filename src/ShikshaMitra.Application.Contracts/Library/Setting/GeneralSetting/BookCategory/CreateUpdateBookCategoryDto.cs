using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;

public class CreateUpdateBookCategoryDto
{
	[Required]
	[StringLength(128)]
	public string Name { get; set; }
	public string Description { get; set; }

}
