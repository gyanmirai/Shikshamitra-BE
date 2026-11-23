using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos;

public class AssignmentHistoryRequest : PagedResultRequestDto
{
	public Guid AcademicYearId { get; set; }
}

public record AssignmentHistoryChange(string Field, object? Before, object? After, string? Format,
	string? FeeHead = null, string? Detail = null, string? Installment = null);

public record AssignmentHistoryEntry(string Id, string Action, DateTimeOffset OccurredAt, string? Actor,
	string? Reason, IReadOnlyList<AssignmentHistoryChange> Changes);

public record AssignmentHistoryPage(long TotalCount, IReadOnlyList<AssignmentHistoryEntry> Items,
	string Currency, bool HasIncompleteEvidence);
