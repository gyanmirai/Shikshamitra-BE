using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Library.BookManagement.IssueBook
{
	public class IssueBookDto : AuditedEntityDto<Guid>
	{
		public string Standard { get; set; }
		public string Division { get; set; }
		public string GrNumber { get; set; }
		public string StudentName { get; set; }
		public Guid BookId { get; set; }
		public string BookTitle { get; set; }
		public int EditionYear { get; set; }
		public string Publisher { get; set; }
		public DateTime IssueDate { get; set; }
		public DateTime DueDate { get; set; }
		public string MemberInfo { get; set; }
	}
}
