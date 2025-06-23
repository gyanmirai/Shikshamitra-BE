using ShikshaMitra.Library.BooksAssignments;
using System;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Library.Fines;

public class FineDto : EntityDto<Guid>
{
	public Guid Id { get; set; }
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
