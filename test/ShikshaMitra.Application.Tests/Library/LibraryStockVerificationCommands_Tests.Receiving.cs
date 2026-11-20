using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.InventoryVerification;
using ShikshaMitra.Library.Receiving;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStockVerificationCommands_Tests
{
	[Fact]
	public async Task Stock_Count_Should_Distinguish_Proven_Receipts_After_Opening_In_The_Published_Result()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = CreateInput(seed);
		var opening = await App.PreviewAsync(input);
		var count = await App.OpenAsync(new(input, opening.Revision, "open-receiving-count"));
		var original = (await App.GetBaselineAsync(count.Id)).Items.ShouldHaveSingleItem();
		var manual = await AddCopyAsync(seed, 2, "MANUAL-EXTRA", BookCopyStatus.Available);
		var receiving = GetRequiredService<ILibraryReceivingAppService>();
		var receipt = new LibraryReceivingInput
		{
			ReceivedOn = input.SchoolDate!.Value.AddDays(-1),
			Source = LibraryStockSource.Donation,
			SourceName = "Local donor",
			Lines = [new() { BookId = seed.BookId, SectionId = seed.SectionId, ShelfId = seed.ShelfId, Quantity = 1, Accessions = ["DONATION-NEW"] }]
		};
		var prepared = await receiving.PreviewAsync(receipt);
		prepared.CanReceive.ShouldBeTrue();
		await receiving.ReceiveAsync(new() { Receipt = receipt, Revision = prepared.Revision!, IdempotencyKey = "receive-count" });
		await App.RecordObservationAsync(count.Id, new(original.AccessionNumber, seed.SectionId, seed.ShelfId, input.SchoolDate, "original"));
		await App.RecordObservationAsync(count.Id, new(manual.AccessionNumber, seed.SectionId, seed.ShelfId, input.SchoolDate, "manual"));
		await App.RecordObservationAsync(count.Id, new("DONATION-NEW", seed.SectionId, seed.ShelfId, input.SchoolDate, "donated"));
		await App.BeginReviewAsync(count.Id, new(3, "review-receiving"));
		const LibraryStockReconciliationSource source = LibraryStockReconciliationSource.OutsideBaseline;
		var findings = (await App.GetReconciliationAsync(count.Id, source)).Items;
		findings.Single(row => row.AccessionNumber == "DONATION-NEW").ReceivedAfterOpening.ShouldBeTrue();
		findings.Single(row => row.AccessionNumber == "MANUAL-EXTRA").ReceivedAfterOpening.ShouldBeFalse();
		foreach (var row in findings)
			await App.SaveDecisionAsync(count.Id, new(source, row.Id, row.EvidenceRevision, null,
				LibraryStockDecisionOutcome.EvidenceVerified, "Reviewed inventory evidence.", $"decision-{row.Id}"));
		var approval = await App.GetApprovalPreviewAsync(count.Id);
		await App.FinalizeAsync(count.Id, new(approval.Revision, "approve-receiving"));
		var published = (await App.GetPublishedResultAsync(count.Id, source)).Findings.Items;
		published.Single(row => row.AccessionNumber == "DONATION-NEW").ReceivedAfterOpening.ShouldBeTrue();
		published.Single(row => row.AccessionNumber == "MANUAL-EXTRA").ReceivedAfterOpening.ShouldBeFalse();
	}
}
