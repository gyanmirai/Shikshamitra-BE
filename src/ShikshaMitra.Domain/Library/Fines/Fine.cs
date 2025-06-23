using ShikshaMitra.Library.BooksAssignments;
using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.Fines;

public class Fine : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }
	public Guid BooksAssignmentId { get; set; }
        public Guid MemberId { get; set; }
	public decimal FineAmount { get; set; }
	public PaymentMode PaymentMode { get; set; }
        public decimal PenaltyAmount { get; set; }
	public string? PenaltyReason { get; set; }
	public string? BankAccountNumber { get; set; }
	public string? IFSCCode { get; set; }
	public string? AccountHolderName { get; set; }

	private Fine() { }

	public Fine(
	   Guid id,
	   Guid? tenantId,
	   Guid booksAssignmentId,
	   Guid memberId,
	   decimal FineAmount,
	   PaymentMode paymentMode,
	   decimal penaltyAmount,
	   string pentaltyReason,
	   string bankAccountNumber,
           string ifscCode,
	   string accountHolderName)
       : base(id)
	{
		TenantId = TenantId;
		BooksAssignmentId = booksAssignmentId;
		MemberId = memberId;
		FineAmount = FineAmount;
		PaymentMode = paymentMode;
		PenaltyAmount = penaltyAmount;
		PenaltyReason = pentaltyReason;
		BankAccountNumber = bankAccountNumber;
		IFSCCode = ifscCode;
		AccountHolderName = accountHolderName;
	}
}
