using System;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.InventoryVerification;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryStockVerificationEvidence_Tests
{
	private static readonly DateOnly SchoolDate = new(2026, 11, 19);
	private static readonly DateTimeOffset OpenedAt = new(2026, 11, 19, 3, 0, 0, TimeSpan.Zero);

	[Fact]
	public void Baseline_Captures_The_Copy_And_Its_Recorded_Location_And_Physical_State()
	{
		var tenant = Guid.NewGuid();
		var section = Guid.NewGuid();
		var shelf = Guid.NewGuid();
		var session = Start(tenant, section, shelf);
		var copy = Copy(tenant, section, shelf) with { Status = BookCopyStatus.Issued };
		var expected = new LibraryStockVerificationBaseline(Guid.NewGuid(), session, copy);

		Assert.Equal(tenant, expected.TenantId);
		Assert.Equal(session.Id, expected.SessionId);
		Assert.Equal(copy.CopyId, expected.CopyId);
		Assert.Equal(copy.BookId, expected.BookId);
		Assert.Equal("00042", expected.AccessionNumber);
		Assert.Equal(BookCopyStatus.Issued, expected.CopyStatus);
		Assert.Equal(section, expected.SectionId);
		Assert.Equal(shelf, expected.ShelfId);
		Assert.Equal(0, session.ObservationCount);
	}

	[Fact]
	public void A_Baseline_Uses_The_Selected_School_And_Shelf()
	{
		var tenant = Guid.NewGuid();
		var section = Guid.NewGuid();
		var shelf = Guid.NewGuid();
		var session = Start(tenant, section, shelf);
		foreach (var copy in new[] { Copy(Guid.NewGuid(), section, shelf), Copy(tenant, section, Guid.NewGuid()) })
		{
			Assert.Equal(LibraryErrorCodes.StockVerificationInvalidInput,
				Assert.Throws<BusinessException>(() => new LibraryStockVerificationBaseline(Guid.NewGuid(), session, copy)).Code);
		}
	}

	[Fact]
	public void An_Unknown_Accession_Retains_What_Was_Observed_And_The_Actual_Actor()
	{
		var tenant = Guid.NewGuid();
		var session = Start(tenant);
		var actor = Guid.NewGuid();
		var observation = new LibraryStockVerificationObservation(Guid.NewGuid(), session, "  00887  ",
			null, null, null, SchoolDate, actor, OpenedAt.AddMinutes(10));

		Assert.Equal(tenant, observation.TenantId);
		Assert.Equal(session.Id, observation.SessionId);
		Assert.Equal("00887", observation.AccessionNumber);
		Assert.Null(observation.CopyId);
		Assert.Null(observation.ObservedShelfId);
		Assert.Equal(actor, observation.ObservedBy);
		Assert.Equal(SchoolDate, observation.SchoolDate);
		Assert.Equal(1, session.ObservationCount);
	}

	[Fact]
	public void Observation_Records_An_Actual_Alternate_Location_And_Leaves_Copy_Evidence_Intact()
	{
		var tenant = Guid.NewGuid();
		var section = Guid.NewGuid();
		var shelf = Guid.NewGuid();
		var actualShelf = Guid.NewGuid();
		var session = Start(tenant, section, shelf);
		var copy = Copy(tenant, section, shelf);
		var observation = new LibraryStockVerificationObservation(Guid.NewGuid(), session, "00042",
			copy, section, actualShelf, SchoolDate, Guid.NewGuid(), OpenedAt.AddMinutes(10));
		Assert.Equal(copy.CopyId, observation.CopyId);
		Assert.Equal(actualShelf, observation.ObservedShelfId);
		Assert.Equal(shelf, copy.ShelfId);
	}

	[Fact]
	public void Invalid_Observation_Is_Rejected_Before_It_Changes_The_Session_Count()
	{
		var tenant = Guid.NewGuid();
		var session = Start(tenant);
		var foreignCopy = Copy(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
		Assert.Equal(LibraryErrorCodes.StockVerificationInvalidInput,
			Assert.Throws<BusinessException>(() => new LibraryStockVerificationObservation(Guid.NewGuid(), session,
				"00042", foreignCopy, null, null, SchoolDate, Guid.NewGuid(), OpenedAt.AddMinutes(10))).Code);
		Assert.Equal(0, session.ObservationCount);
	}

	private static LibraryStockVerificationSession Start(Guid tenant, Guid? section = null, Guid? shelf = null) =>
		new(Guid.NewGuid(), tenant, "School stock count", section, shelf, SchoolDate, 1, Guid.NewGuid(), OpenedAt);

	private static LibraryCopySnapshot Copy(Guid tenant, Guid section, Guid shelf) =>
		new(Guid.NewGuid(), Guid.NewGuid(), tenant, "00042", BookCopyStatus.Available, section, shelf, null);
}
