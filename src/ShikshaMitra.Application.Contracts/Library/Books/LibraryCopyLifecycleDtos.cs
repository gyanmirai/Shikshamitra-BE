using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Books;

public class LibraryCopyStateDto
{
	public Guid BookId { get; set; }
	public Guid CopyId { get; set; }
	public string AccessionNumber { get; set; } = string.Empty;
	public int Status { get; set; }
	public string? Remarks { get; set; }
	public Guid SectionId { get; set; }
	public Guid ShelfId { get; set; }
	public string Revision { get; set; } = string.Empty;
	public bool HasOpenLoan { get; set; }
	public Guid? ActiveLoanId { get; set; }
	public string LoanLinkSource { get; set; } = "None";
	public bool RequiresLoanReconciliation { get; set; }
	public bool CanReturn { get; set; }
	public bool CanChangeCondition { get; set; }
	public bool CanEditNotes { get; set; }
	public bool CanMove { get; set; }
	public bool CanWriteOff { get; set; }
	public List<int> AllowedConditionStatuses { get; set; } = [];
	public List<string> BlockedReasons { get; set; } = [];
}

public class LibraryCopyConditionRequest
{
	[Required]
	public BookCopyStatus? Status { get; set; }
	[Required, StringLength(500, MinimumLength = 1)]
	public string Reason { get; set; } = string.Empty;
	[Required, StringLength(128)]
	public string Revision { get; set; } = string.Empty;
}

public class LibraryCopyMoveOptionsDto
{
	public LibraryCopyStateDto State { get; set; } = new();
	public List<LibraryCopyMoveSectionDto> Sections { get; set; } = [];
}

public class LibraryCopyMoveSectionDto
{
	public Guid Id { get; set; }
	public string SectionName { get; set; } = string.Empty;
	public List<LibraryCopyMoveShelfDto> ShelfDetails { get; set; } = [];
}

public class LibraryCopyMoveShelfDto
{
	public Guid Id { get; set; }
	public string ShelfName { get; set; } = string.Empty;
}

public class LibraryCopyMoveRequest
{
	public Guid SectionId { get; set; }
	public Guid ShelfId { get; set; }
	[Required, StringLength(500, MinimumLength = 1)]
	public string Reason { get; set; } = string.Empty;
	[Required, StringLength(128)]
	public string Revision { get; set; } = string.Empty;
}

public class LibraryCopyWriteOffRequest
{
	[Required, StringLength(500, MinimumLength = 1)]
	public string Reason { get; set; } = string.Empty;
	[Required, StringLength(128)]
	public string Revision { get; set; } = string.Empty;
}

public class LibraryCopyEventDto
{
	public Guid Id { get; set; }
	public long Sequence { get; set; }
	public Guid BookId { get; set; }
	public Guid CopyId { get; set; }
	public string EventType { get; set; } = string.Empty;
	public DateTimeOffset OccurredAt { get; set; }
	public int ActorKind { get; set; }
	public Guid? ActorId { get; set; }
	public int? SystemPurpose { get; set; }
	public string Reason { get; set; } = string.Empty;
	public int PreviousStatus { get; set; }
	public int NextStatus { get; set; }
	public string? PreviousRemarks { get; set; }
	public string? NextRemarks { get; set; }
	public Guid FromSectionId { get; set; }
	public Guid FromShelfId { get; set; }
	public Guid ToSectionId { get; set; }
	public Guid ToShelfId { get; set; }
	public Guid? RelatedLoanId { get; set; }
}
