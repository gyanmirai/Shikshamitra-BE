using System;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.InventoryVerification;
using ShikshaMitra.Library.Setting.Sections;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStockVerificationCommands_Tests
{
	[Fact]
	public async Task Approval_Should_Require_Current_Decisions_And_Preserve_The_Published_Result()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = CreateInput(seed);
		var opening = await App.PreviewAsync(input);
		var count = await App.OpenAsync(new(input, opening.Revision, "open-final"));
		await App.BeginReviewAsync(count.Id, new(0, "review-final"));
		var missing = await App.GetApprovalPreviewAsync(count.Id);
		missing.MissingDecisionCount.ShouldBe(1);
		missing.CanFinalize.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => App.FinalizeAsync(count.Id, new(missing.Revision, "pending"))))
			.Code.ShouldBe(LibraryErrorCodes.StockVerificationDecisionsRequired);

		var finding = (await App.GetReconciliationAsync(count.Id)).Items.ShouldHaveSingleItem();
		var decision = new LibrarySaveStockDecisionCommand(LibraryStockReconciliationSource.Expected, finding.Id,
			finding.EvidenceRevision, null, LibraryStockDecisionOutcome.FollowUp, "Check the reading room.", "approve-decision-1");
		var first = await App.SaveDecisionAsync(count.Id, decision);
		var reviewed = await App.GetApprovalPreviewAsync(count.Id);
		reviewed.CanFinalize.ShouldBeTrue();
		await ChangeCopyStatusAsync(seed.CopyId, BookCopyStatus.Damaged);
		(await Should.ThrowAsync<BusinessException>(() => App.FinalizeAsync(count.Id, new(reviewed.Revision, "old-snapshot"))))
			.Code.ShouldBe(LibraryErrorCodes.StockVerificationPlanChanged);
		(await App.GetApprovalPreviewAsync(count.Id)).StaleDecisionCount.ShouldBe(1);

		finding = (await App.GetReconciliationAsync(count.Id)).Items.ShouldHaveSingleItem();
		var latest = await App.SaveDecisionAsync(count.Id, decision with { EvidenceRevision = finding.EvidenceRevision,
			ExpectedDecisionId = first.Id, Reason = "Check the repair register.", IdempotencyKey = "approve-decision-2" });
		var permissions = GetRequiredService<IPermissionChecker>();
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.InventoryVerification.Update).Returns(false);
		var ready = await App.GetApprovalPreviewAsync(count.Id);
		var command = new LibraryFinalizeStockCountCommand(ready.Revision, "approve-count");
		var finalized = await App.FinalizeAsync(count.Id, command);
		finalized.Status.ShouldBe(LibraryStockVerificationStatus.Finalized);
		(await App.FinalizeAsync(count.Id, command)).ShouldBe(finalized);
		await ChangeCopyStatusAsync(seed.CopyId, BookCopyStatus.Available);

		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.InventoryVerification.Approve).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.FinalizeAsync(count.Id, command));
		var published = await App.GetPublishedResultAsync(count.Id);
		published.DiscrepancyCount.ShouldBe(1);
		published.Findings.Items.ShouldHaveSingleItem().CurrentStatus.ShouldBe(BookCopyStatus.Damaged);
		published.Findings.Items[0].Decision!.Id.ShouldBe(latest.Id);
		published.Findings.Items[0].Decision!.Reason.ShouldBe("Check the repair register.");
		await LimitToYearAsync(seed.YearId);
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.GetPublishedResultAsync(count.Id));
	}

	[Fact]
	public async Task Approval_Should_Require_Exact_Action_And_Publish_An_Empty_Count()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		await ChangeCopyStatusAsync(seed.CopyId, BookCopyStatus.Discarded);
		var input = CreateInput(seed);
		var opening = await App.PreviewAsync(input);
		var count = await App.OpenAsync(new(input, opening.Revision, "open-empty"));
		await App.BeginReviewAsync(count.Id, new(0, "review-empty"));
		var permissions = GetRequiredService<IPermissionChecker>();
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.InventoryVerification.Approve).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.GetApprovalPreviewAsync(count.Id));
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.InventoryVerification.Approve).Returns(true);
		var ready = await App.GetApprovalPreviewAsync(count.Id);
		ready.CanFinalize.ShouldBeTrue();
		await App.FinalizeAsync(count.Id, new(ready.Revision, "approve-empty"));
		var result = await App.GetPublishedResultAsync(count.Id);
		result.FindingCount.ShouldBe(0);
		result.Findings.Items.ShouldBeEmpty();
		result.PublishedBy.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public async Task Approval_Should_Use_The_Same_Recorded_Year_For_Broad_And_Scoped_Staff()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = CreateInput(seed);
		var opening = await App.PreviewAsync(input);
		var count = await App.OpenAsync(new(input, opening.Revision, "open-scoped-review"));
		await App.BeginReviewAsync(count.Id, new(0, "scoped-review"));
		await WithUnitOfWorkAsync(async () =>
		{
			var sections = GetRequiredService<IRepository<LibrarySection, Guid>>();
			var shelves = GetRequiredService<IRepository<LibraryShelf, Guid>>();
			var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
			var copy = await copies.GetAsync(seed.CopyId);
			var section = await sections.InsertAsync(new(Guid.NewGuid(), copy.TenantId, seed.YearId, "Later-year stock", 2), true);
			var shelf = await shelves.InsertAsync(new(Guid.NewGuid(), section.Id, "Later shelf"), true);
			copy.SectionId = section.Id;
			copy.ShelfId = shelf.Id;
			await copies.UpdateAsync(copy, true);
		});
		var broad = await App.GetApprovalPreviewAsync(count.Id);
		var broadFinding = (await App.GetReconciliationAsync(count.Id)).Items.ShouldHaveSingleItem();
		broadFinding.Finding.ShouldBe(LibraryStockFinding.CurrentCopyUnavailable);
		broadFinding.CurrentStatus.ShouldBeNull();
		await LimitToYearAsync(seed.OriginYearId);
		var scoped = await App.GetApprovalPreviewAsync(count.Id);
		scoped.Revision.ShouldBe(broad.Revision);
		(await App.GetReconciliationAsync(count.Id)).Items.ShouldHaveSingleItem().EvidenceRevision.ShouldBe(broadFinding.EvidenceRevision);
	}

	[Fact]
	public async Task Approval_Should_Publish_All_Findings_Across_Batches()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		await ChangeCopyStatusAsync(seed.CopyId, BookCopyStatus.UnderRepair);
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<BookCopy, Guid>>();
			var tenant = GetRequiredService<ICurrentTenant>().Id;
			await repository.InsertManyAsync(Enumerable.Range(2, 257)
				.Select(number => new BookCopy(Guid.NewGuid(), seed.BookId, number, $"BATCH-{number:D4}",
					seed.SectionId, seed.ShelfId, tenant) { Status = BookCopyStatus.UnderRepair }), true);
		});
		var input = CreateInput(seed);
		var opening = await App.PreviewAsync(input);
		var count = await App.OpenAsync(new(input, opening.Revision, "open-batches"));
		await App.BeginReviewAsync(count.Id, new(0, "review-batches"));
		var ready = await App.GetApprovalPreviewAsync(count.Id);
		ready.FindingCount.ShouldBe(258);
		ready.CanFinalize.ShouldBeTrue();
		await App.FinalizeAsync(count.Id, new(ready.Revision, "approve-batches"));
		var last = await App.GetPublishedResultAsync(count.Id, page: 3, pageSize: 100);
		last.FindingCount.ShouldBe(258);
		last.Findings.Items.Count.ShouldBe(58);
		last.Findings.Items.ShouldAllBe(row => row.Finding == LibraryStockFinding.InRepair);
	}

	private Task ChangeCopyStatusAsync(Guid copyId, BookCopyStatus status) => WithUnitOfWorkAsync(async () =>
	{
		var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
		var copy = await copies.GetAsync(copyId);
		copy.Status = status;
		await copies.UpdateAsync(copy, true);
	});
}
