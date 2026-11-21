using System;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.InventoryVerification;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStockVerificationCommands_Tests
{
	[Fact]
	public async Task Review_Decisions_Should_Be_Retained_And_Reject_Changed_Evidence_Or_Another_Reviewers_Decision()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = CreateInput(seed);
		var preview = await App.PreviewAsync(input);
		var session = await App.OpenAsync(new(input, preview.Revision, "decision-count"));
		var finding = (await App.GetReconciliationAsync(session.Id)).Items.ShouldHaveSingleItem();
		finding.Finding.ShouldBe(LibraryStockFinding.NotSeen);
		var command = new LibrarySaveStockDecisionCommand(LibraryStockReconciliationSource.Expected, finding.Id,
			finding.EvidenceRevision, null, LibraryStockDecisionOutcome.FollowUp, " Recheck this shelf after class. ", "decision-1");
		(await Should.ThrowAsync<BusinessException>(() => App.SaveDecisionAsync(session.Id, command)))
			.Code.ShouldBe(LibraryErrorCodes.StockVerificationInvalidState);

		await App.BeginReviewAsync(session.Id, new(0, "decision-review"));
		finding = (await App.GetReconciliationAsync(session.Id)).Items.ShouldHaveSingleItem();
		command = command with { EvidenceRevision = finding.EvidenceRevision };
		var first = await App.SaveDecisionAsync(session.Id, command);
		first.Reason.ShouldBe("Recheck this shelf after class.");
		first.Version.ShouldBe(1);
		(await App.SaveDecisionAsync(session.Id, command)).Id.ShouldBe(first.Id);
		var read = (await App.GetReconciliationAsync(session.Id)).Items.ShouldHaveSingleItem();
		read.Decision?.Id.ShouldBe(first.Id);
		read.DecisionIsCurrent.ShouldBeTrue();

		await WithUnitOfWorkAsync(async () =>
		{
			var copies = GetRequiredService<IRepository<BookCopy, Guid>>();
			var copy = await copies.GetAsync(seed.CopyId);
			copy.Status = BookCopyStatus.Damaged;
			await copies.UpdateAsync(copy, true);
		});
		read = (await App.GetReconciliationAsync(session.Id)).Items.ShouldHaveSingleItem();
		read.Decision?.Id.ShouldBe(first.Id);
		read.DecisionIsCurrent.ShouldBeFalse();
		read.EvidenceRevision.ShouldNotBe(command.EvidenceRevision);
		var revised = command with
		{
			ExpectedDecisionId = first.Id,
			Outcome = LibraryStockDecisionOutcome.RegisterCorrection,
			Reason = "Review the condition entry with the librarian.",
			IdempotencyKey = "decision-2"
		};
		(await Should.ThrowAsync<BusinessException>(() => App.SaveDecisionAsync(session.Id, revised)))
			.Code.ShouldBe(LibraryErrorCodes.StockVerificationPlanChanged);
		var signed = revised with { EvidenceRevision = read.EvidenceRevision };
		(await Should.ThrowAsync<BusinessException>(() => App.SaveDecisionAsync(session.Id, signed with { ExpectedDecisionId = null })))
			.Code.ShouldBe(LibraryErrorCodes.StockVerificationPlanChanged);
		var second = await App.SaveDecisionAsync(session.Id, signed);
		second.Version.ShouldBe(2);
		(await App.SaveDecisionAsync(session.Id, command)).Id.ShouldBe(first.Id);

		await App.CancelAsync(session.Id, new(0, LibraryStockVerificationStatus.InReview, "Review rescheduled", "decision-cancel"));
		var retained = await App.GetDecisionsAsync(session.Id);
		retained.TotalCount.ShouldBe(2);
		retained.Items[0].Id.ShouldBe(second.Id);
		retained.Items[1].Id.ShouldBe(first.Id);
		GetRequiredService<IPermissionChecker>().IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.InventoryVerification.Update).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.SaveDecisionAsync(session.Id, signed));
		(await App.GetDecisionsAsync(session.Id)).TotalCount.ShouldBe(2);
	}
}
