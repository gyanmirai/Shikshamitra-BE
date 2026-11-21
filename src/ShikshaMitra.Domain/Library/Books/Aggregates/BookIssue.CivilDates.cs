using System;
using System.Text.Json;
using ShikshaMitra.Library.Shared;
using Volo.Abp;

namespace ShikshaMitra.Library.Books;

public partial class BookIssue
{
	public DateOnly? SchoolIssueDate { get; private set; }
	public DateOnly? SchoolDueDate { get; private set; }
	public DateOnly? OriginalSchoolDueDate { get; private set; }
	public DateOnly? SchoolReturnDate { get; private set; }
	public LibraryCivilDateProvenance IssueDateProvenance { get; private set; }
	public LibraryCivilDateProvenance DueDateProvenance { get; private set; }
	public LibraryCivilDateProvenance ReturnDateProvenance { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	public Guid? BorrowerBoardId { get; private set; }
	public Guid? BorrowerMediumId { get; private set; }
	public Guid? BorrowerStageId { get; private set; }
	public Guid? BorrowerStandardId { get; private set; }
	public Guid? BorrowerDivisionId { get; private set; }
	public string? PolicySnapshotJson { get; private set; }
	public string? BorrowerSnapshotJson { get; private set; }
	public string? OriginalCalendarSnapshotJson { get; private set; }
	public Guid? IssuedByUserId { get; private set; }
	public DateTimeOffset? IssueRecordedAt { get; private set; }
	public int RenewalCount { get; private set; }

	public static BookIssue CreateCivil(Guid id, Guid? tenantId, Guid copyId, string accession, Guid memberId,
		int role, DateOnly issueDate, DateOnly dueDate, LibraryLoanIssueContext context)
	{
		if (id == Guid.Empty || copyId == Guid.Empty || memberId == Guid.Empty || role is < 0 or > 1 ||
			context.AcademicYearId == Guid.Empty || context.ActorUserId == Guid.Empty || context.RecordedAt == default ||
			string.IsNullOrWhiteSpace(accession) || accession.Length > 50 || issueDate == default || dueDate < issueDate)
			throw new BusinessException(LibraryErrorCodes.InvalidIssueDateRange);
		ValidateSnapshot(context.PolicySnapshotJson);
		ValidateSnapshot(context.BorrowerSnapshotJson);
		ValidateSnapshot(context.CalendarSnapshotJson);
		return new BookIssue
		{
			Id = id, TenantId = tenantId, BookCopyId = copyId, AccessionNumber = accession, MemberId = memberId,
			MemberRole = role, Status = BookIssueStatus.Issued, SchoolIssueDate = issueDate, SchoolDueDate = dueDate,
			OriginalSchoolDueDate = dueDate, IssueDateProvenance = LibraryCivilDateProvenance.ExplicitSchoolDate,
			DueDateProvenance = LibraryCivilDateProvenance.ExplicitSchoolDate, AcademicYearId = context.AcademicYearId,
			BorrowerBoardId = context.BoardId, BorrowerMediumId = context.MediumId, BorrowerStageId = context.StageId,
			BorrowerStandardId = context.StandardId, BorrowerDivisionId = context.DivisionId,
			BorrowerScopeProvenance = role == 0 ? LibraryBorrowerScopeProvenance.CapturedAtOperation : LibraryBorrowerScopeProvenance.Unknown,
			PolicySnapshotJson = context.PolicySnapshotJson, BorrowerSnapshotJson = context.BorrowerSnapshotJson,
			OriginalCalendarSnapshotJson = context.CalendarSnapshotJson, IssuedByUserId = context.ActorUserId,
			IssueRecordedAt = context.RecordedAt.ToUniversalTime()
		};
	}

	private static void ValidateSnapshot(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Length > 50000)
			throw new BusinessException(LibraryErrorCodes.ContextInvalid);
		try
		{
			using var document = JsonDocument.Parse(value);
			if (document.RootElement.ValueKind != JsonValueKind.Object)
				throw new BusinessException(LibraryErrorCodes.ContextInvalid);
		}
		catch (JsonException) { throw new BusinessException(LibraryErrorCodes.ContextInvalid); }
	}
}
