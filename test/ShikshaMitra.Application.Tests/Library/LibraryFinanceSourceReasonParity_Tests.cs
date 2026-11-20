using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	// Approved new-post behavior: the original accepted source meaning survives
	// a category label rename; existing evidence is never rewritten.
	[Fact]
	public async Task Posted_Source_Category_Rename_Should_Preserve_The_Frozen_Reason_For_Read_And_Settlement()
	{
		var context = await SeedReasonParitySourceAsync(remarks: null);
		var owner = context.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var before = await CaptureReasonParityEvidenceAsync(context);
		var original = await WithUnitOfWorkAsync(() => Service(owner, useStoredSource: true).GetAsync(context.Post.Source));
		var ready = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(context.Post.Source));
		ready.CanCollect.ShouldBeTrue();
		var oldFacts = await ReadReasonParityFactsAsync(context);
		await RenameReasonParityCategoryAsync(context);
		var newFacts = await ReadReasonParityFactsAsync(context);
		(await ReadReasonParityCategoryNameAsync(context)).ShouldNotBe(oldFacts.Reason.Trim());
		newFacts.Source.SourceVersion.ShouldBe(oldFacts.Source.SourceVersion);
		newFacts.Amount.ShouldBe(oldFacts.Amount);
		newFacts.AcademicYearId.ShouldBe(oldFacts.AcademicYearId);

		FinanceExternalReceivableDto? detail = null;
		var readError = await Record.ExceptionAsync(async () =>
			detail = await WithUnitOfWorkAsync(() => Service(owner, useStoredSource: true).GetAsync(context.Post.Source)));
		(await CaptureReasonParityEvidenceAsync(context)).ShouldBe(before);
		readError.ShouldBeNull("A current category label rename must not replace or invalidate the accepted source evidence of this new post.");
		detail.ShouldNotBeNull();
		detail.FinanceChargeId.ShouldBe(original.FinanceChargeId);
		detail.OriginalAmount.ShouldBe(100.75m);
		detail.OutstandingAmount.ShouldBe(100.75m);
		var form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(context.Post.Source));
		var paid = await WithUnitOfWorkAsync(() => TypedService(owner).CollectAsync(CashCollectionInput(context, form.Revision)));
		paid.AcceptedAmount.ShouldBe(40.25m);
		paid.Receivable.OutstandingAmount.ShouldBe(60.50m);
		(await CaptureReasonParityEvidenceAsync(context)).ShouldBe(before);
	}

	[Fact]
	public async Task Posted_Source_With_Explicit_Remarks_Should_Keep_Its_Current_Scalar_Read_And_Settlement_After_Category_Rename()
	{
		const string reason = "Reviewed the actual damaged copy before departure.";
		var context = await SeedReasonParitySourceAsync(reason);
		var owner = context.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var before = await CaptureReasonParityEvidenceAsync(context);
		(await ReadReasonParityFactsAsync(context)).Reason.ShouldBe(reason);
		await RenameReasonParityCategoryAsync(context);
		(await ReadReasonParityFactsAsync(context)).Reason.ShouldBe(reason);

		var detail = await WithUnitOfWorkAsync(() => Service(owner, useStoredSource: true).GetAsync(context.Post.Source));
		detail.OriginalAmount.ShouldBe(100.75m);
		detail.OutstandingAmount.ShouldBe(100.75m);
		var form = await WithUnitOfWorkAsync(() => TypedService(owner).GetCollectFormAsync(context.Post.Source));
		form.CanCollect.ShouldBeTrue();
		var paid = await WithUnitOfWorkAsync(() => TypedService(owner).CollectAsync(CashCollectionInput(context, form.Revision)));
		paid.AcceptedAmount.ShouldBe(40.25m);
		paid.Receivable.OutstandingAmount.ShouldBe(60.50m);
		(await CaptureReasonParityEvidenceAsync(context)).ShouldBe(before);
	}
}
