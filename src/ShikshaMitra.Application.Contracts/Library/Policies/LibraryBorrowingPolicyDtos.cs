using System;
using System.ComponentModel.DataAnnotations;

namespace ShikshaMitra.Library.Policies;

public class LibraryBorrowingTermsDto
{
	[Range(0, 1)] public int DueDateMode { get; set; } = 1;
	[Range(0, 1000)] public int MaxBooksAtTime { get; set; }
	[Range(0, 1000)] public int ReservationLimit { get; set; }
	[Range(0, 3660)] public int LoanDays { get; set; }
	[Range(0, 1)] public int DayCountingMode { get; set; }
	[Range(0, 100)] public int MaxRenewals { get; set; }
	[Range(0, 3660)] public int RenewalDays { get; set; }
	[Range(0, 365)] public int PickupWindowDays { get; set; }
	public bool MoveDueDateToNextOpenDay { get; set; } = true;
}

public class LibraryBorrowingPolicyDraftRequest
{
	public Guid? PolicyId { get; set; }
	[Required, MaxLength(160)] public string Name { get; set; } = string.Empty;
	[Range(0, 1)] public int Role { get; set; }
	public DateOnly EffectiveFrom { get; set; }
	public DateOnly? EffectiveTo { get; set; }
	public Guid? BookCategoryId { get; set; }
	public Guid? SectionId { get; set; }
	public Guid? EducationMediumStageId { get; set; }
	public Guid? StandardId { get; set; }
	[Range(0, 1000)] public int Priority { get; set; }
	[Required] public LibraryBorrowingTermsDto Terms { get; set; } = new();
	public string? ConcurrencyStamp { get; set; }
}

public class LibraryPolicyPublishRequest
{
	[Required] public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class LibraryPolicyArchiveRequest : LibraryPolicyPublishRequest
{
	public DateOnly EffectiveOn { get; set; }
	[Required, MaxLength(500)] public string Reason { get; set; } = string.Empty;
}

public class LibraryBorrowingPolicyVersionDto : LibraryBorrowingPolicyDraftRequest
{
	public Guid Id { get; set; }
	public int Version { get; set; }
	public int Status { get; set; }
	public DateOnly? ArchivedFrom { get; set; }
	public string? ArchiveReason { get; set; }
}

public class LibraryPolicyResolveRequest
{
	public Guid AcademicYearId { get; set; }
	[Range(0, 1)] public int Role { get; set; }
	public Guid? BookId { get; set; }
	public Guid? CopyId { get; set; }
	public Guid? MemberId { get; set; }
	public DateOnly? AsOfDate { get; set; }
}

public class LibraryBorrowingPolicySnapshotTermsDto
{
	public int? DueDateMode { get; set; }
	public int MaxBooksAtTime { get; set; }
	public int ReservationLimit { get; set; }
	public int? LoanDays { get; set; }
	public int? DayCountingMode { get; set; }
	public int? MaxRenewals { get; set; }
	public int? RenewalDays { get; set; }
	public int? PickupWindowDays { get; set; }
	public bool? MoveDueDateToNextOpenDay { get; set; }
}

public class LibraryBorrowingPolicySnapshotDto
{
	public Guid? PolicyVersionId { get; set; }
	public Guid? PolicyId { get; set; }
	public int? Version { get; set; }
	public bool IsLegacyManualDate { get; set; }
	public int Role { get; set; }
	public LibraryBorrowingPolicySnapshotTermsDto Terms { get; set; } = new();
	public DateOnly? EffectiveFrom { get; set; }
	public DateOnly? EffectiveTo { get; set; }
	public string? TimeZoneId { get; set; }
	public string TimeZoneSource { get; set; } = "Unconfigured";
	public bool NeedsCalendarConfiguration { get; set; }
}
