using System;
using System.Threading.Tasks;
using NSubstitute;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library.InventoryVerification;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryStockVerificationCommands_Tests
{
	[Fact]
	public async Task Review_Should_Lock_Scanning_At_The_Confirmed_Sequence_And_Replay_Exactly()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = CreateInput(seed);
		var preview = await App.PreviewAsync(input);
		var session = await App.OpenAsync(new(input, preview.Revision, "open"));
		var scan = new LibraryRecordStockObservationCommand("UNKNOWN-LABEL", null, null, input.SchoolDate, "scan-before-review");
		var observation = await App.RecordObservationAsync(session.Id, scan);

		(await Should.ThrowAsync<BusinessException>(() => App.BeginReviewAsync(session.Id, new(0, "stale-review"))))
			.Code.ShouldBe(LibraryErrorCodes.StockVerificationPlanChanged);
		var command = new LibraryBeginStockReviewCommand(1, "begin-review");
		var accepted = await App.BeginReviewAsync(session.Id, command);
		accepted.Status.ShouldBe(LibraryStockVerificationStatus.InReview);
		accepted.ObservationCount.ShouldBe(1);
		(await App.GetAsync(session.Id)).ReviewStartedBy.ShouldNotBeNull();
		(await App.BeginReviewAsync(session.Id, command)).ShouldBe(accepted);
		(await Should.ThrowAsync<BusinessException>(() => App.BeginReviewAsync(session.Id, command with { ExpectedObservationCount = 0 })))
			.Code.ShouldBe(LibraryErrorCodes.StockVerificationCommandConflict);
		(await Should.ThrowAsync<BusinessException>(() => App.RecordObservationAsync(session.Id, scan with { IdempotencyKey = "new-scan" })))
			.Code.ShouldBe(LibraryErrorCodes.StockVerificationInvalidState);
		(await App.RecordObservationAsync(session.Id, scan)).Id.ShouldBe(observation.Id);

		var cancel = new LibraryCancelStockCountCommand(1, LibraryStockVerificationStatus.InReview, "  Room reassigned  ", "cancel-review");
		(await App.CancelAsync(session.Id, cancel)).Status.ShouldBe(LibraryStockVerificationStatus.Cancelled);
		(await App.GetAsync(session.Id)).CancellationReason.ShouldBe("Room reassigned");
		(await App.BeginReviewAsync(session.Id, command)).ShouldBe(accepted);
		var permissions = GetRequiredService<IPermissionChecker>();
		permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.Library.InventoryVerification.Update).Returns(false);
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.BeginReviewAsync(session.Id, command));
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.CancelAsync(session.Id, cancel));
	}

	[Fact]
	public async Task Cancel_Should_Require_A_Reason_And_A_Fresh_State_And_Keep_The_Evidence()
	{
		using var school = LibraryCopyLifecycleTestFixture.EnterSchool(ServiceProvider);
		var seed = await WithUnitOfWorkAsync(() => LibraryPolicyTestFixture.SeedAsync(ServiceProvider));
		var input = CreateInput(seed);
		var preview = await App.PreviewAsync(input);
		var session = await App.OpenAsync(new(input, preview.Revision, "open"));
		var command = new LibraryCancelStockCountCommand(0, LibraryStockVerificationStatus.Counting, "Interrupted count", "cancel-count");
		(await Should.ThrowAsync<BusinessException>(() => App.CancelAsync(session.Id, command with { Reason = " " })))
			.Code.ShouldBe(LibraryErrorCodes.StockVerificationInvalidInput);
		(await Should.ThrowAsync<BusinessException>(() => App.BeginReviewAsync(session.Id, new(null, "no-counter"))))
			.Code.ShouldBe(LibraryErrorCodes.StockVerificationInvalidInput);
		(await Should.ThrowAsync<BusinessException>(() => App.CancelAsync(session.Id, command with { ExpectedStatus = LibraryStockVerificationStatus.InReview })))
			.Code.ShouldBe(LibraryErrorCodes.StockVerificationPlanChanged);
		var accepted = await App.CancelAsync(session.Id, command);
		accepted.Status.ShouldBe(LibraryStockVerificationStatus.Cancelled);
		(await App.CancelAsync(session.Id, command)).ShouldBe(accepted);
		(await Should.ThrowAsync<BusinessException>(() => App.CancelAsync(session.Id, command with { Reason = "Another reason" })))
			.Code.ShouldBe(LibraryErrorCodes.StockVerificationCommandConflict);
		(await App.GetBaselineAsync(session.Id)).Items.ShouldHaveSingleItem().CopyId.ShouldBe(seed.CopyId);
		(await App.GetAsync(session.Id)).CancelledBy.ShouldNotBeNull();

		await LimitToYearAsync(seed.YearId);
		await Should.ThrowAsync<AbpAuthorizationException>(() => App.CancelAsync(session.Id, command));
	}
}
