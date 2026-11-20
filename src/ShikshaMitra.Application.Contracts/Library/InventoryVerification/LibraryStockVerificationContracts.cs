using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Shared;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Library.InventoryVerification;

public interface ILibraryStockVerificationAppService : IApplicationService
{
	Task<LibraryStockVerificationOptionsDto> GetOptionsAsync();
	Task<LibraryStockCountPreviewDto> PreviewAsync(LibraryStockCountInput input);
	Task<LibraryStockVerificationSessionDto> OpenAsync(LibraryOpenStockCountCommand input);
	Task<LibraryStockVerificationSessionDto> GetAsync(Guid sessionId);
	Task<PaginatedResponse<LibraryStockVerificationSessionDto>> GetListAsync(int page = 1, int pageSize = 20);
	Task<PaginatedResponse<LibraryStockBaselineDto>> GetBaselineAsync(Guid sessionId, int page = 1, int pageSize = 20);
	Task<PaginatedResponse<LibraryStockObservationDto>> GetObservationsAsync(Guid sessionId, int page = 1, int pageSize = 20);
	Task<PaginatedResponse<LibraryStockReconciliationRowDto>> GetReconciliationAsync(Guid sessionId,
		LibraryStockReconciliationSource source = LibraryStockReconciliationSource.Expected, int page = 1, int pageSize = 20);
	Task<LibraryStockObservationDto> RecordObservationAsync(Guid sessionId, LibraryRecordStockObservationCommand input);
	Task<LibraryStockTransitionDto> BeginReviewAsync(Guid sessionId, LibraryBeginStockReviewCommand input);
	Task<LibraryStockTransitionDto> CancelAsync(Guid sessionId, LibraryCancelStockCountCommand input);
	Task<LibraryStockDecisionDto> SaveDecisionAsync(Guid sessionId, LibrarySaveStockDecisionCommand input);
	Task<PaginatedResponse<LibraryStockDecisionDto>> GetDecisionsAsync(Guid sessionId, int page = 1, int pageSize = 20);
	Task<LibraryStockApprovalPreviewDto> GetApprovalPreviewAsync(Guid sessionId);
	Task<LibraryStockTransitionDto> FinalizeAsync(Guid sessionId, LibraryFinalizeStockCountCommand input);
	Task<LibraryStockPublishedResultDto> GetPublishedResultAsync(Guid sessionId,
		LibraryStockReconciliationSource source = LibraryStockReconciliationSource.Expected, int page = 1, int pageSize = 20);
}

public sealed record LibraryStockVerificationOptionsDto(bool CanCountWholeSchool, DateOnly? SchoolDate, string SchoolDateSource,
	IReadOnlyList<LibraryCopyMoveSectionDto> Sections);

public sealed record LibraryStockCountInput(string Name, Guid? SectionId, Guid? ShelfId, DateOnly? SchoolDate);

public sealed record LibraryOpenStockCountCommand(LibraryStockCountInput Count, string Revision, string IdempotencyKey);

public sealed record LibraryRecordStockObservationCommand(string Scan, Guid? SectionId, Guid? ShelfId, DateOnly? SchoolDate, string IdempotencyKey);

public sealed record LibraryBeginStockReviewCommand(int? ExpectedObservationCount, string IdempotencyKey);

public sealed record LibraryCancelStockCountCommand(int? ExpectedObservationCount, LibraryStockVerificationStatus? ExpectedStatus, string Reason, string IdempotencyKey);

public sealed record LibraryStockTransitionDto(Guid SessionId, LibraryStockVerificationStatus Status, int ObservationCount);

public sealed record LibraryStockCountPreviewDto(DateOnly SchoolDate, string SchoolDateSource, int ExpectedCopyCount,
	IReadOnlyList<LibraryStockCountStatusTotal> StatusTotals, string Revision);

public sealed record LibraryStockCountStatusTotal(BookCopyStatus CopyStatus, int Count);

public sealed record LibraryStockVerificationSessionDto(Guid Id, string Name, Guid? SectionId, Guid? ShelfId, Guid? AcademicYearId,
	DateOnly SchoolDate, int ExpectedCopyCount, int ObservationCount, LibraryStockVerificationStatus Status,
	Guid OpenedBy, DateTimeOffset OpenedAt, Guid? ReviewStartedBy, DateTimeOffset? ReviewStartedAt, Guid? FinalizedBy,
	DateTimeOffset? FinalizedAt, Guid? CancelledBy, DateTimeOffset? CancelledAt, string? CancellationReason);

public sealed record LibraryStockBaselineDto(Guid CopyId, Guid BookId, string AccessionNumber, Guid SectionId, Guid ShelfId, BookCopyStatus CopyStatus);

public sealed record LibraryStockObservationDto(Guid Id, int Sequence, string AccessionNumber, Guid? CopyId,
	Guid? ObservedSectionId, Guid? ObservedShelfId, DateOnly SchoolDate, Guid ObservedBy, DateTimeOffset ObservedAt);

public sealed record LibraryStockReconciliationRowDto(Guid Id, string AccessionNumber, LibraryStockFinding Finding,
	bool RequiresReview, BookCopyStatus? BaselineStatus, BookCopyStatus? CurrentStatus,
	Guid? ExpectedSectionId, Guid? ExpectedShelfId, Guid? ObservedSectionId, Guid? ObservedShelfId, int? ObservationSequence)
{
	public string EvidenceRevision { get; init; } = string.Empty;
	public LibraryStockDecisionDto? Decision { get; init; }
	public bool DecisionIsCurrent { get; init; }
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
	public bool ReceivedAfterOpening { get; init; }
}

public sealed record LibrarySaveStockDecisionCommand(LibraryStockReconciliationSource Source, Guid RowId, string EvidenceRevision,
	Guid? ExpectedDecisionId, LibraryStockDecisionOutcome Outcome, string Reason, string IdempotencyKey);

public sealed record LibraryStockDecisionDto(Guid Id, LibraryStockReconciliationSource Source, Guid RowId, int Version,
	string AccessionNumber, LibraryStockFinding Finding, LibraryStockDecisionOutcome Outcome, string Reason,
	Guid DecidedBy, DateTimeOffset DecidedAt);

public sealed record LibraryFinalizeStockCountCommand(string Revision, string IdempotencyKey);

public sealed record LibraryStockApprovalPreviewDto(int ExpectedCopyCount, int ObservationCount, int FindingCount, int DiscrepancyCount,
	int MissingDecisionCount, int StaleDecisionCount, string Revision)
{
	public bool CanFinalize => MissingDecisionCount == 0 && StaleDecisionCount == 0;
}

public sealed record LibraryStockPublishedResultDto(DateTimeOffset PublishedAt, Guid PublishedBy, int ExpectedCopyCount, int ObservationCount,
	int FindingCount, int DiscrepancyCount, PaginatedResponse<LibraryStockPublishedRowDto> Findings);

public sealed record LibraryStockPublishedRowDto(Guid Id, string AccessionNumber, LibraryStockFinding Finding,
	BookCopyStatus? BaselineStatus, BookCopyStatus? CurrentStatus, string? ExpectedLocation, string? ObservedLocation,
	int? ObservationSequence, LibraryStockDecisionDto? Decision,
	[property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ReceivedAfterOpening = false);
