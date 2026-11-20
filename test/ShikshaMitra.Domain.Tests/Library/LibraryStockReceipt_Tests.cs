using System;
using System.Collections.Generic;
using System.Globalization;
using ShikshaMitra.Library.Receiving;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Library;

public class LibraryStockReceipt_Tests
{
	private static LibraryStockLineEvidence Line(decimal? cost = null, string? currency = null) => new(
		Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, cost, currency,
		[new(Guid.NewGuid(), "ACC-101", 1)]);

	private static LibraryStockReceipt Receive(IReadOnlyList<LibraryStockLineEvidence>? lines = null,
		LibraryStockSource source = LibraryStockSource.Donation) => new(Guid.NewGuid(), Guid.NewGuid(),
		new(2026, 11, 18), source, "Community donor", null, Guid.NewGuid(),
		new DateTimeOffset(2026, 11, 18, 13, 0, 0, TimeSpan.FromHours(5.5)), lines ?? [Line()]);

	[Fact]
	public void A_Donation_Should_Keep_Unknown_Cost_And_Seal_The_Actual_Copies()
	{
		var line = Line();
		var receipt = Receive([line]);
		Assert.Equal(LibraryStockSource.Donation, receipt.Source);
		Assert.Null(Assert.Single(receipt.Lines).UnitCost);
		Assert.Null(Assert.Single(receipt.Lines).Currency);
		Assert.Equal(line.Copies[0].CopyId, receipt.Lines[0].Copies[0].CopyId);
		Assert.Equal(1, receipt.ExpectedLineCount);
		Assert.Equal(1, receipt.ExpectedCopyCount);
	}

	[Fact]
	public void A_Known_Zero_Cost_Should_Remain_Different_From_Unknown_Cost()
	{
		var receipt = Receive([Line(0m, "INR")]);
		Assert.Equal(0m, receipt.Lines[0].UnitCost);
		Assert.Equal("INR", receipt.Lines[0].Currency);
	}

	[Fact]
	public void An_Actual_Purchase_Should_Preserve_Paise_And_Separate_School_Date_From_The_Audit_Instant()
	{
		var receipt = Receive([Line(350.50m, "INR")], LibraryStockSource.Purchase);
		Assert.Equal(350.50m, receipt.Lines[0].UnitCost);
		Assert.Equal(new DateOnly(2026, 11, 18), receipt.ReceivedOn);
		Assert.Equal(new DateTimeOffset(2026, 11, 18, 7, 30, 0, TimeSpan.Zero), receipt.RecordedAt);
		Assert.NotEqual(Guid.Empty, receipt.RecordedBy);
	}

	[Theory]
	[InlineData("-0.01", "INR")]
	[InlineData("0.001", "INR")]
	[InlineData("10000000000000000", "INR")]
	[InlineData("5.25", null)]
	[InlineData("5.25", "inr")]
	[InlineData("5.25", "R1S")]
	public void An_Invalid_Recorded_Cost_Should_Be_Rejected_Without_Rounding(string amount, string? currency)
	{
		Assert.Throws<BusinessException>(() => Receive([Line(decimal.Parse(amount, CultureInfo.InvariantCulture), currency)]));
	}

	[Fact]
	public void An_Unknown_Cost_Should_Not_Be_Given_An_Invented_Currency()
	{
		Assert.Throws<BusinessException>(() => Receive([Line(null, "INR")]));
	}

	[Fact]
	public void A_Receipt_Should_Reject_Incomplete_Line_And_Copy_Identity()
	{
		var line = Line();
		foreach (var invalid in new[] { line with { Id = Guid.Empty }, line with { BookId = Guid.Empty },
			line with { SectionId = Guid.Empty }, line with { ShelfId = Guid.Empty }, line with { ExpectedCopyCount = 2 },
			line with { Copies = [] }, line with { Copies = [line.Copies[0] with { CopyId = Guid.Empty }] },
			line with { Copies = [line.Copies[0] with { CopyNumber = 0 }] } })
			Assert.Throws<BusinessException>(() => Receive([invalid]));
	}

	[Fact]
	public void Copy_Ids_And_Normalized_Accessions_Should_Be_Unique_Across_The_Whole_Receipt()
	{
		var first = Line();
		var second = Line() with { Copies = [first.Copies[0] with { AccessionNumber = "ACC-102" }] };
		Assert.Throws<BusinessException>(() => Receive([first, second]));
		second = Line() with { Copies = [new(Guid.NewGuid(), " acc-101 ", 2)] };
		Assert.Throws<BusinessException>(() => Receive([first, second]));
		Assert.Throws<BusinessException>(() => Receive([first, first]));
	}

	[Fact]
	public void The_Committed_Receipt_Should_Not_Share_Mutable_Input_Collections()
	{
		var copies = new List<LibraryReceivedCopyEvidence> { new(Guid.NewGuid(), "ACC-101", 1) };
		var lines = new List<LibraryStockLineEvidence> { Line() with { Copies = copies } };
		var receipt = Receive(lines);
		var expectedCopyId = copies[0].CopyId;
		copies.Clear();
		lines.Clear();
		Assert.Single(receipt.Lines);
		Assert.Equal(expectedCopyId, Assert.Single(receipt.Lines[0].Copies).CopyId);
		Assert.True(((System.Collections.IList)receipt.Lines).IsReadOnly);
		Assert.True(((System.Collections.IList)receipt.Lines[0].Copies).IsReadOnly);
	}

	[Fact]
	public void A_Receipt_Requires_An_Actual_School_User_Date_And_Recognized_Source()
	{
		var id = Guid.NewGuid(); var tenant = Guid.NewGuid(); var user = Guid.NewGuid();
		var date = new DateOnly(2026, 11, 18); var at = DateTimeOffset.UtcNow;
		Assert.Throws<BusinessException>(() => new LibraryStockReceipt(Guid.Empty, tenant, date, LibraryStockSource.Other, null, null, user, at, [Line()]));
		Assert.Throws<BusinessException>(() => new LibraryStockReceipt(id, Guid.Empty, date, LibraryStockSource.Other, null, null, user, at, [Line()]));
		Assert.Throws<BusinessException>(() => new LibraryStockReceipt(id, tenant, default, LibraryStockSource.Other, null, null, user, at, [Line()]));
		Assert.Throws<BusinessException>(() => new LibraryStockReceipt(id, tenant, date, (LibraryStockSource)99, null, null, user, at, [Line()]));
		Assert.Throws<BusinessException>(() => new LibraryStockReceipt(id, tenant, date, LibraryStockSource.Other, null, null, Guid.Empty, at, [Line()]));
		Assert.Throws<BusinessException>(() => new LibraryStockReceipt(id, tenant, date, LibraryStockSource.Other, null, null, user, default, [Line()]));
		Assert.Throws<BusinessException>(() => Receive([]));
	}
}
