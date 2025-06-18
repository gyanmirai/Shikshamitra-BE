using ShikshaMitra.Library.BooksAssignments;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Library.Fines;

public class CreateFineDto
{
	[Required]
	public Guid BooksAssignmentId { get; set; }

	[Required]
	public decimal FineAmount { get; set; }

	[Required]
	public PaymentMode PaymentMode { get; set; }

	public decimal PenaltyAmount { get; set; }

	public string? PenaltyReason { get; set; }

	public string? BankAccountNumber { get; set; }

	public string? IFSCCode { get; set; }

	public string? AccountHolderName { get; set; }
}
