using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task External_Post_Should_Respect_Append_Only_Database_Guards()
	{
		var fixture = await SeedAsync(FinanceExternalPayerKind.Student, departed: true);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.TenantId);
		using var actor = SignIn(fixture);
		fixture = await ConfirmStoredAssessmentAsync(fixture);
		await SetFinanceAppendOnlyGuardsAsync(enabled: true);
		try
		{
			var input = Post(fixture);
			var posted = await WithUnitOfWorkAsync(() => Service(fixture, useStoredSource: true).PostAsync(input));
			var replay = await WithUnitOfWorkAsync(() => Service(fixture, useStoredSource: true).PostAsync(input));
			replay.FinanceChargeId.ShouldBe(posted.FinanceChargeId);
			posted.OriginalAmount.ShouldBe(100m);
			await WithUnitOfWorkAsync(async () =>
			{
				var link = await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>()
					.GetAsync(row => row.SourceId == fixture.AssessmentId);
				link.CreationTime.ShouldBeGreaterThan(DateTime.MinValue);
				link.CreatorId.ShouldBe(fixture.ActorId);
			});
		}
		finally { await SetFinanceAppendOnlyGuardsAsync(enabled: false); }
	}

	[Fact]
	public async Task Typed_Collection_And_Waiver_Should_Respect_Append_Only_Database_Guards()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		await SetFinanceAppendOnlyGuardsAsync(enabled: true);
		try
		{
			await ConfigureAndApproveTypedAsync(context);
			using var actor = SignIn(context.Owner);
			var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
			var collected = await WithUnitOfWorkAsync(() => TypedService(context.Owner)
				.CollectAsync(CashCollectionInput(context, form.Revision)));
			collected.AcceptedAmount.ShouldBe(40.25m);
			form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
			var waived = await WithUnitOfWorkAsync(() => TypedService(context.Owner).RequestWaiverAsync(new()
			{
				Source = context.Post.Source, Revision = form.Revision, Amount = 10.10m,
				Reason = "Reviewed partial school assistance.", IdempotencyKey = Guid.NewGuid().ToString("N")
			}));
			form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
			var reversed = await WithUnitOfWorkAsync(() => TypedService(context.Owner).ReverseWaiverAsync(new()
			{
				Source = context.Post.Source, Revision = form.Revision, WaiverApplicationId = waived.ApplicationId!.Value,
				Amount = 2.05m, Reason = "Corrected the assistance evidence.", IdempotencyKey = Guid.NewGuid().ToString("N")
			}));
			reversed.Receivable.AcceptedPaymentAmount.ShouldBe(40.25m);
			reversed.Receivable.WaivedAmount.ShouldBe(8.05m);
			reversed.Receivable.OutstandingAmount.ShouldBe(52.45m);
		}
		finally { await SetFinanceAppendOnlyGuardsAsync(enabled: false); }
	}

	[Fact]
	public async Task Typed_Waiver_Reversal_Should_Preserve_An_Append_Only_Original_Application()
	{
		var context = await SeedTypedSettlementAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(context.Owner.TenantId);
		await ConfigureAndApproveTypedAsync(context);
		using var actor = SignIn(context.Owner);
		var form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
		var waived = await WithUnitOfWorkAsync(() => TypedService(context.Owner).RequestWaiverAsync(new()
		{
			Source = context.Post.Source, Revision = form.Revision, Amount = 10.10m,
			Reason = "Reviewed partial assistance.", IdempotencyKey = Guid.NewGuid().ToString("N")
		}));
		const string legacyStamp = "retained-original-waiver-stamp";
		const string legacyJson = "{ \"legacy-opaque\" : [\"original\", 7] }\n";
		await WithUnitOfWorkAsync(async () =>
		{
			var database = (await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync()).Database;
			await database.ExecuteSqlInterpolatedAsync($"UPDATE \"Finance_ChargeWaiverApplications\" SET \"ConcurrencyStamp\" = {legacyStamp}, \"ExtraProperties\" = {legacyJson} WHERE \"Id\" = {waived.ApplicationId!.Value}");
		});
		await SetFinanceAppendOnlyGuardsAsync(enabled: true, waiverOnly: true);
		try
		{
			form = await WithUnitOfWorkAsync(() => TypedService(context.Owner).GetCollectFormAsync(context.Post.Source));
			var reversed = await WithUnitOfWorkAsync(() => TypedService(context.Owner).ReverseWaiverAsync(new()
			{
				Source = context.Post.Source, Revision = form.Revision, WaiverApplicationId = waived.ApplicationId!.Value,
				Amount = 2.05m, Reason = "Corrected reviewed assistance.", IdempotencyKey = Guid.NewGuid().ToString("N")
			}));
			reversed.AppliedAmount.ShouldBe(10.10m);
			reversed.ReversedAmount.ShouldBe(2.05m);
			reversed.Receivable.OutstandingAmount.ShouldBe(92.70m);
			await WithUnitOfWorkAsync(async () =>
			{
				var original = await GetRequiredService<IRepository<ChargeWaiverApplication, Guid>>()
					.GetAsync(waived.ApplicationId!.Value);
				original.LegacyConcurrencyStamp.ShouldBe(legacyStamp);
				original.LegacyExtraPropertiesJson.ShouldBe(legacyJson);
				original.CreationTime.ShouldBeGreaterThan(DateTime.MinValue);
				original.CreatorId.ShouldBe(context.Owner.ActorId);
			});
		}
		finally { await SetFinanceAppendOnlyGuardsAsync(enabled: false, waiverOnly: true); }
	}

	private Task SetFinanceAppendOnlyGuardsAsync(bool enabled, bool waiverOnly = false) => WithUnitOfWorkAsync(async () =>
	{
		var database = (await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync()).Database;
		var tables = waiverOnly ? new[] { "Finance_ChargeWaiverApplications", "Finance_ChargeWaiverReversals" }
			: new[] { "Finance_ExternalReceivableSourceLinks", "Finance_ExternalReceivableCommands",
				"Finance_ExternalSettlementPolicies", "Finance_ChargeWaiverApplications", "Finance_ChargeWaiverReversals" };
		foreach (var table in tables)
		{
			var trigger = "LibraryTest_AppendOnly_" + table;
			var sql = enabled
				? $"CREATE TRIGGER \"{trigger}\" BEFORE UPDATE ON \"{table}\" BEGIN SELECT RAISE(ABORT, 'Finance evidence is append only.'); END;"
				: $"DROP TRIGGER IF EXISTS \"{trigger}\";";
			await database.ExecuteSqlRawAsync(sql);
		}
	});
}
