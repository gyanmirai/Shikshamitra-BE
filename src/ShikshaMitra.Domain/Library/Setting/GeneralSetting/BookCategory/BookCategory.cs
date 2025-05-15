using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities.Auditing;

namespace ShikshaMitra.Library.Setting.GeneralSetting.BookCategory
{
	public class BookCategory : AuditedAggregateRoot<Guid>
	{
		public string Name { get; set; }

		public string Description { get; set; }

		public BookCategory(string name, string description)
		{
			Name = name;
			Description = description;
		}

	}
}
