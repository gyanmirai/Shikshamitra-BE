using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.Setting.GeneralSetting.BookCategory
{
	public class CreateUpdateBookCategoryDto
	{
		[Required]
		[StringLength(128)]
		public string Name { get; set; }


		[StringLength(512)]
		public string Description { get; set; }

	}
}
