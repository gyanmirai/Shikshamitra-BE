using System;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Library.Setting.ChargeCategory;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Reason_Origin_Should_Reject_A_Bound_Raw_Remarks_Change_Without_Rewriting_Accepted_Evidence()
	{
		var context = await SeedReasonParitySourceAsync(remarks: null);
		var owner = context.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var before = await CaptureReasonParityEvidenceAsync(context);
		var valid = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(context.Post.Source));
		valid.CanCollect.ShouldBeTrue();
		const string changed = "Different raw source meaning after acceptance.";
		await WithUnitOfWorkAsync(async () =>
		{
			var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var source = await charges.GetAsync(owner.AssessmentId);
			source.Remarks = changed;
			await charges.UpdateAsync(source, true);
		});

		(await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			Service(owner, useStoredSource: true).GetAsync(context.Post.Source)))).Code.ShouldBe(LibraryFinanceErrorCodes.SourceChanged);
		(await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			TypedService(owner).CollectAsync(CashCollectionInput(context, valid.Revision))))).Code.ShouldBe(LibraryFinanceErrorCodes.SourceChanged);
		(await CaptureReasonParityEvidenceAsync(context)).ShouldBe(before with { RawRemarks = changed });
		await AssertNoTypedCollectionsAsync();
	}

	[Fact]
	public async Task Reason_Origin_Should_Reject_An_Actual_Category_Id_Change_Even_When_Explicit_Reason_Is_Unchanged()
	{
		const string reason = "The explicit original assessment meaning is unchanged.";
		var context = await SeedReasonParitySourceAsync(reason);
		var owner = context.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var before = await CaptureReasonParityEvidenceAsync(context);
		var valid = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(context.Post.Source));
		valid.CanCollect.ShouldBeTrue();
		await WithUnitOfWorkAsync(async () =>
		{
			var charges = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var source = await charges.GetAsync(owner.AssessmentId);
			var originalCategoryId = source.ChargeCategoryId;
			var category = new LibraryChargeCategory(Guid.NewGuid(), owner.TenantId, owner.YearId,
				"Another actual Library category", 100m, false, "A distinct legal school category used only for source-corruption setup.");
			await GetRequiredService<IRepository<LibraryChargeCategory, Guid>>().InsertAsync(category, true);
			source.ChargeCategoryId = category.Id;
			await charges.UpdateAsync(source, true);
			source.ChargeCategoryId.ShouldNotBe(originalCategoryId);
			source.Remarks.ShouldBe(reason);
			source.GetAssessmentVersion().ShouldBe(context.Post.Source.SourceVersion);
		});

		(await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			Service(owner, useStoredSource: true).GetAsync(context.Post.Source)))).Code.ShouldBe(LibraryFinanceErrorCodes.SourceChanged);
		(await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			TypedService(owner).CollectAsync(CashCollectionInput(context, valid.Revision))))).Code.ShouldBe(LibraryFinanceErrorCodes.SourceChanged);
		(await CaptureReasonParityEvidenceAsync(context)).ShouldBe(before);
		await AssertNoTypedCollectionsAsync();
	}

	[Fact]
	public async Task Reason_Origin_Should_Keep_Genuinely_Old_No_Origin_Posts_On_Strict_Comparison_After_Category_Rename()
	{
		var fixture = await SeedLegacyOriginPostAsync();
		var owner = fixture.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var before = await ReadLegacyOriginLinkAsync(owner);
		using (var json = JsonDocument.Parse(before.SourceSnapshotJson))
			json.RootElement.TryGetProperty("ReasonOrigin", out _).ShouldBeFalse("The legacy producer must omit origin during initial persistence, not patch immutable history.");
		(await WithUnitOfWorkAsync(() => Service(owner, useStoredSource: true).GetAsync(fixture.Post.Source)))
			.FinanceChargeId.ShouldBe(fixture.Posted.FinanceChargeId);
		await RenameLegacyOriginCategoryAsync(owner);

		(await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			Service(owner, useStoredSource: true).GetAsync(fixture.Post.Source)))).Code.ShouldBe(LibraryFinanceErrorCodes.SourceChanged);
		var after = await ReadLegacyOriginLinkAsync(owner);
		after.SourceSnapshotJson.ShouldBe(before.SourceSnapshotJson);
		after.SourceFingerprint.ShouldBe(before.SourceFingerprint);
		after.SourceVersion.ShouldBe(before.SourceVersion);
		after.FinanceChargeId.ShouldBe(before.FinanceChargeId);
		await WithUnitOfWorkAsync(async () =>
			(await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetAsync(owner.AssessmentId)).Remarks.ShouldBeNull());
		await AssertNoTypedCollectionsAsync();
	}
}
