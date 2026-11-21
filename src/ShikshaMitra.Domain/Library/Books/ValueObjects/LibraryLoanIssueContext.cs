using System;

namespace ShikshaMitra.Library.Books;

public sealed record LibraryLoanIssueContext(Guid AcademicYearId, Guid? BoardId, Guid? MediumId,
	Guid? StageId, Guid? StandardId, Guid? DivisionId, string PolicySnapshotJson, string BorrowerSnapshotJson,
	string CalendarSnapshotJson, Guid ActorUserId, DateTimeOffset RecordedAt);
