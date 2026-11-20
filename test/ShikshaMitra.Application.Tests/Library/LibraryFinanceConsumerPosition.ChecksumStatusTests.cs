using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Library.Charges;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

// Corrupted-storage controls preserve the original accepted facts and money.
public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Consumer_Evidence_Should_Review_A_Checksum_Mismatch_That_The_Canonical_Source_Rejects()
	{
		var context = await SeedReasonParitySourceAsync("Explicit checksum control reason.");
		var owner = context.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var original = await ConsumerPositionAsync(owner);
		original.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		original.Original.ShouldBe(100.75m);
		(await WithUnitOfWorkAsync(() => Service(owner, useStoredSource: true).GetAsync(context.Post.Source)))
			.FinanceChargeId.ShouldBe(context.Posted.FinanceChargeId);
		var originalJson = await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>();
			var link = await repository.GetAsync(original.SourceLinkId!.Value);
			var json = link.SourceSnapshotJson;
			var corrupt = new string(link.SourceFingerprint == new string('f', 64) ? 'e' : 'f', 64);
			// Controlled persisted corruption; source facts/snapshot/version stay exact.
			typeof(FinanceExternalReceivableSourceLink).GetProperty(nameof(link.SourceFingerprint))!.SetValue(link, corrupt);
			await repository.UpdateAsync(link, true);
			return json;
		});
		(await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			Service(owner, useStoredSource: true).GetAsync(context.Post.Source)))).Code.ShouldBe(LibraryFinanceErrorCodes.SourceChanged);
		var row = await ConsumerPositionAsync(owner);
		row.Evidence.ShouldBe(FinanceExternalPositionEvidence.NeedsReview);
		row.AssessmentOriginal.ShouldBe(100.75m);
		row.Original.ShouldBeNull(); row.Settled.ShouldBeNull(); row.Collectible.ShouldBeNull();
		await AssertConsumerDetailParityAsync(owner, row);
		await WithUnitOfWorkAsync(async () =>
		{
			var query = await ConsumerPositionService(owner).GetQueryAsync(new(LibraryChargePositionReadPurpose.ChargeHistory,
				owner.YearId, [owner.AssessmentId]));
			(await GetRequiredService<IAsyncQueryableExecuter>().CountAsync(query.Where(value =>
				value.Evidence == FinanceExternalPositionEvidence.Verified))).ShouldBe(0);
			var link = await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>().GetAsync(original.SourceLinkId!.Value);
			link.SourceSnapshotJson.ShouldBe(originalJson); link.SourceVersion.ShouldBe(context.Post.Source.SourceVersion);
			(await GetRequiredService<IRepository<Charge, Guid>>().GetAsync(context.Posted.FinanceChargeId)).Amount.ShouldBe(100.75m);
		});
	}

	[Fact]
	public async Task Consumer_Evidence_Should_Review_Unknown_Stored_Approval_Status_Without_Confirming_Zero_Debt()
	{
		var context = await SeedReasonParitySourceAsync("Known approval status positive control.");
		var owner = context.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var known = await ConsumerPositionAsync(owner);
		known.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		known.ApprovalStatus.ShouldBe(ChargeStatus.Approved);
		known.Collectible.ShouldBe(100.75m);
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<Charge, Guid>>();
			var charge = await repository.GetAsync(context.Posted.FinanceChargeId);
			typeof(Charge).GetProperty(nameof(charge.Status))!.SetValue(charge, (ChargeStatus)999);
			await repository.UpdateAsync(charge, true);
		});
		var unknown = await ConsumerPositionAsync(owner);
		unknown.Evidence.ShouldBe(FinanceExternalPositionEvidence.NeedsReview);
		unknown.AssessmentOriginal.ShouldBe(100.75m);
		unknown.ApprovalStatus.ShouldBeNull(); unknown.Collectible.ShouldBeNull(); unknown.FinancialOutstanding.ShouldBeNull();
		await AssertConsumerDetailParityAsync(owner, unknown);
	}
}
