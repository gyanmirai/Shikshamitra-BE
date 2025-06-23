using System;

namespace ShikshaMitra.Library.BooksAssignments;

public class ReturnBookDto
{
	public Guid BookId { get; set; }
	public Guid MemberId { get; set; }
	public DateTime ReturnDate { get; set; }

	public string Notes { get; set; }
	public decimal FineAmount { get; set; }
	public decimal PenaltyAmount { get; set; }
	public string PenaltyReason { get; set; }

	public PaymentMode PaymentMode { get; set; }
	public string BankAccountNumber { get; set; }
	public string IFSCCode { get; set; }
	public string AccountHolderName { get; set; }
}
