using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Library.BookManagement.BookOverviews
{
	public class BookOverviewDto : AuditedEntityDto<Guid>
	{   
		public string ISBN { get; set; }
		public string Name { get; set; }
		public int Borrowed { get; set; }
		public int Overdue { get; set; }
		public int Reserved { get; set; }
	}
}
