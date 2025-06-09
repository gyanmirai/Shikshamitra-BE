using ShikshaMitra.Library.BooksAssignments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Fines
{
	public class Fine : AuditedAggregateRoot<Guid>, IMultiTenant
	{
		public Guid? TenantId { get; set; }
        public Guid MemberId { get; set; }
		public Guid BookAssignmentId { get; set; }
		public decimal TotalFineAmount { get; set; }
		public string Status { get; set; }
		public PaymentMode PaymentMode { get; set; }

		private Fine() { }

		public Fine(
		   Guid id,
		   Guid memberId,
		   Guid bookAssignmentId,
		   decimal totalFineAmount,
		   string status,
		   PaymentMode paymentMode,
		   Guid? tenantId
	   ) : base(id)
		{
			MemberId = memberId;
			BookAssignmentId = bookAssignmentId;
			TotalFineAmount = totalFineAmount;
			Status = status;
			PaymentMode = paymentMode;
			TenantId = tenantId;
		}
	}
}
