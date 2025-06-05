using ShikshaMitra.Library.BooksAssignments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Library.BooksAssignements;

public class BooksAssignment : AuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; set; }

	public Guid BookId { get; set; }
	public Guid MemberId { get; set; }
    public DateTime IssueDate { get; set; }
	public DateTime DueDate { get; set; }
	public DateTime? ReturnDate { get; set; }

	public decimal FineAmount { get; set; }
	public decimal PenaltyAmount { get; set; }
	public string PenaltyReason { get; set; }

	public PaymentMode PaymentMode { get; set; }
	public string BankAccountNumber { get; set; }
	public string IFSCCode { get; set; }
	public string AccountHolderName { get; set; }
	public string Notes { get; set; }
	public string Status { get; set; }

	private BooksAssignment() { }

	public BooksAssignment(
		Guid id,
		Guid? tenantId,
		Guid bookId,
		Guid memberId,
		DateTime issueDate,
		DateTime dueDate,
		DateTime? returnDate,
		decimal fineAmount,
		decimal penaltyAmount,
		string penaltyReason,
		PaymentMode paymentMode,
		string bankAccountNumber,
		string ifscCode,
		string accountHolderName,
		string notes,
		string status)
		: base(id)
	{
		TenantId = tenantId;
		BookId = bookId;
		MemberId = memberId;
		IssueDate = issueDate;
		DueDate = dueDate;
		ReturnDate = returnDate;
		FineAmount = fineAmount;
		PenaltyAmount = penaltyAmount;
		PenaltyReason = penaltyReason;
		PaymentMode = paymentMode;
		BankAccountNumber = bankAccountNumber;
		IFSCCode = ifscCode;
		AccountHolderName = accountHolderName;
		Notes = notes;
		Status = status;
	}
}
