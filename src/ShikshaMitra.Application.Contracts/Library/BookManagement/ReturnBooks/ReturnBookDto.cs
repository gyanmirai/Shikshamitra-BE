using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Library.BookManagement.ReturnBook
{
	public class ReturnBookDto : AuditedEntityDto<Guid>
	{
		public Guid BookId { get; set; }
		public string BookTitle { get; set; }
		public Guid MemberId { get; set; }
		public string MemberName { get; set; }
		public DateTime DueDate { get; set; }
		public DateTime ReturnDate { get; set; }
		public string Condition { get; set; }
		public decimal FineAmount { get; set; }
		public string FineReason { get; set; }
		public decimal ReceivedFineAmount { get; set; }
		public string PaymentMode { get; set; }
		public DateTime? NotificationSentOn { get; set; }
		public string NotificationStatus { get; set; }
		public string Action { get; set; }
	}
}
