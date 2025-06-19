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
	public Guid BooksAssignmentId { get; set; }
	public Guid MemberId { get; set; }
    public decimal FineAmount { get; set; }
	public PaymentMode PaymentMode { get; set; }

	public decimal PenaltyAmount { get; set; }

	public string? PenaltyReason { get; set; }

	public string? BankAccountNumber { get; set; }

	public string? IFSCCode { get; set; }

	public string? AccountHolderName { get; set; }
}
