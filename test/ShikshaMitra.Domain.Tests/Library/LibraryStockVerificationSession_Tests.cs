using System;
using ShikshaMitra.Library.InventoryVerification;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryStockVerificationSession_Tests
{
	private static readonly DateOnly SchoolDate = new(2026, 11, 19);
	private static readonly DateTimeOffset OpenedAt = new(2026, 11, 19, 8, 30, 0, TimeSpan.FromHours(5.5));

	[Fact]
	public void Start_Captures_The_Physical_Scope_And_School_Date()
	{
		var tenant = Guid.NewGuid();
		var opener = Guid.NewGuid();
		var section = Guid.NewGuid();
		var shelf = Guid.NewGuid();
		var session = new LibraryStockVerificationSession(Guid.NewGuid(), tenant, "  November count  ",
			section, shelf, SchoolDate, 500, opener, OpenedAt);

		Assert.Equal(tenant, session.TenantId);
		Assert.Equal(section, session.SectionId);
		Assert.Equal(shelf, session.ShelfId);
		Assert.Equal("November count", session.Name);
		Assert.Equal(SchoolDate, session.SchoolDate);
		Assert.Equal(500, session.ExpectedCopyCount);
		Assert.Equal(opener, session.OpenedBy);
		Assert.Equal(TimeSpan.Zero, session.OpenedAt.Offset);
		Assert.Equal(OpenedAt.UtcDateTime, session.OpenedAt.UtcDateTime);
		Assert.Equal(LibraryStockVerificationStatus.Counting, session.Status);
	}

	[Fact]
	public void Review_Closes_Scanning_And_Seals_The_Approved_Result()
	{
		var session = Start();
		var scanner = Guid.NewGuid();
		var reviewer = Guid.NewGuid();
		var fingerprint = new string('A', 64);
		Assert.Equal(LibraryErrorCodes.StockVerificationInvalidState,
			Assert.Throws<BusinessException>(() => session.FinalizeReview(reviewer, OpenedAt.AddHours(1), fingerprint)).Code);
		session.RecordObservation(scanner, OpenedAt.AddMinutes(2));
		session.RecordObservation(scanner, OpenedAt.AddMinutes(3));
		session.StartReview(scanner, OpenedAt.AddMinutes(4));
		Assert.Equal(LibraryStockVerificationStatus.InReview, session.Status);
		Assert.Equal(2, session.ObservationCount);
		Assert.Equal(scanner, session.ReviewStartedBy);
		Assert.Equal(LibraryErrorCodes.StockVerificationInvalidState,
			Assert.Throws<BusinessException>(() => session.RecordObservation(scanner, OpenedAt.AddMinutes(5))).Code);

		session.FinalizeReview(reviewer, OpenedAt.AddHours(1), fingerprint);
		Assert.Equal(LibraryStockVerificationStatus.Finalized, session.Status);
		Assert.Equal(reviewer, session.FinalizedBy);
		Assert.Equal(OpenedAt.AddHours(1).UtcDateTime, session.FinalizedAt!.Value.UtcDateTime);
		Assert.Equal(fingerprint, session.FinalizedEvidenceFingerprint);
		Assert.Equal(500, session.ExpectedCopyCount);
		Assert.Equal(LibraryErrorCodes.StockVerificationInvalidState,
			Assert.Throws<BusinessException>(() => session.Cancel(reviewer, OpenedAt.AddHours(2), "Duplicate count")).Code);
	}

	[Fact]
	public void Cancellation_Records_The_Actor_And_Leaves_The_Original_Scope_And_Count_Available()
	{
		var session = Start();
		var actor = Guid.NewGuid();
		Assert.Equal(LibraryErrorCodes.StockVerificationInvalidInput,
			Assert.Throws<BusinessException>(() => session.Cancel(actor, OpenedAt.AddMinutes(5), " ")).Code);
		session.Cancel(actor, OpenedAt.AddMinutes(5), "  Shelves were moved before counting.  ");
		Assert.Equal(actor, session.CancelledBy);
		Assert.Equal("Shelves were moved before counting.", session.CancellationReason);
		Assert.Equal(500, session.ExpectedCopyCount);
		Assert.Equal(LibraryStockVerificationStatus.Cancelled, session.Status);
		Assert.Equal(LibraryErrorCodes.StockVerificationInvalidState,
			Assert.Throws<BusinessException>(() => session.StartReview(actor, OpenedAt.AddMinutes(6))).Code);
	}

	[Fact]
	public void Start_Requires_Stable_Tenant_And_Location_Identity()
	{
		var tenant = Guid.NewGuid();
		var actor = Guid.NewGuid();
		Assert.Equal(LibraryErrorCodes.StockVerificationInvalidInput,
			Assert.Throws<BusinessException>(() => new LibraryStockVerificationSession(Guid.NewGuid(), Guid.Empty,
				"Invalid school", null, null, SchoolDate, 10, actor, OpenedAt)).Code);
		Assert.Equal(LibraryErrorCodes.StockVerificationInvalidInput,
			Assert.Throws<BusinessException>(() => new LibraryStockVerificationSession(Guid.NewGuid(), tenant,
				"Unbound shelf", null, Guid.NewGuid(), SchoolDate, 10, actor, OpenedAt)).Code);
		var emptyLibrary = new LibraryStockVerificationSession(Guid.NewGuid(), tenant,
			"First library setup", null, null, SchoolDate, 0, actor, OpenedAt);
		Assert.Equal(0, emptyLibrary.ExpectedCopyCount);
	}

	[Fact]
	public void Review_Requires_Chronological_Evidence_And_A_Stable_Fingerprint()
	{
		var session = Start();
		var actor = Guid.NewGuid();
		session.RecordObservation(actor, OpenedAt.AddMinutes(5));
		Assert.Equal(LibraryErrorCodes.StockVerificationInvalidInput,
			Assert.Throws<BusinessException>(() => session.StartReview(actor, OpenedAt.AddMinutes(4))).Code);
		session.StartReview(actor, OpenedAt.AddMinutes(6));
		Assert.Equal(LibraryErrorCodes.StockVerificationInvalidInput,
			Assert.Throws<BusinessException>(() => session.FinalizeReview(actor, OpenedAt.AddMinutes(7), "short")).Code);
		Assert.Equal(LibraryStockVerificationStatus.InReview, session.Status);
	}

	private static LibraryStockVerificationSession Start() => new(Guid.NewGuid(), Guid.NewGuid(), "School count",
		null, null, SchoolDate, 500, Guid.NewGuid(), OpenedAt);
}
