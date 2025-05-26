using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.BookManagement.ReturnBooks
{
	public class ReturnBook : AuditedAggregateRoot<Guid>, IMultiTenant
	{
		public Guid? TenantId { get; set; }

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

		public string PaymentMode { get; set; } // e.g., Cash, Card, Online

		// Notification and UI status info
		public DateTime? NotificationSentOn { get; set; } // When notification was sent
		public string NotificationStatus { get; set; }    // e.g., "Notified", "Pending"
		public string Action { get; set; }                // e.g., "Send Reminder", "Mark as Received"

		private ReturnBook() { }

		public ReturnBook(
			Guid id,
			Guid bookId,
			string bookTitle,
			Guid memberId,
			string memberName,
			DateTime dueDate,
			DateTime returnDate,
			string condition,
			decimal fineAmount,
			string fineReason,
			decimal receivedFineAmount,
			string paymentMode,
			DateTime? notificationSentOn,
			string notificationStatus,
			string action,
			Guid? tenantId)
			: base(id)
		{
			BookId = bookId;
			BookTitle = bookTitle;
			MemberId = memberId;
			MemberName = memberName;
			DueDate = dueDate;
			ReturnDate = returnDate;
			Condition = condition;
			FineAmount = fineAmount;
			FineReason = fineReason;
			ReceivedFineAmount = receivedFineAmount;
			PaymentMode = paymentMode;
			NotificationSentOn = notificationSentOn;
			NotificationStatus = notificationStatus;
			Action = action;
			TenantId = tenantId;
		}
	}
}