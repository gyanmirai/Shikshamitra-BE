using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.Charges;
using ShikshaMitra.Library.Books;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Member_Charge_Position_Should_Preserve_Initial_Legacy_Declarations_Without_Inventing_Finance()
	{
		var owner = await SeedAsync(FinanceExternalPayerKind.Employee, departed: false);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var expected = await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IRepository<LibraryCharge, Guid>>();
			var original = await repository.GetAsync(owner.AssessmentId);
			original.AcademicYearId.ShouldBeNull(); original.Currency.ShouldBeNull(); original.FinanceSourceLinkId.ShouldBeNull();
			var rows = new[]
			{
				new LibraryCharge(Guid.NewGuid(), owner.PersonId, 1, original.ChargeCategoryId, null,
					20.25m, new DateTime(2024, 6, 10), new DateTime(2024, 6, 24), owner.TenantId),
				new LibraryCharge(Guid.NewGuid(), owner.PersonId, 1, original.ChargeCategoryId, null,
					30.50m, new DateTime(2024, 6, 11), new DateTime(2024, 6, 25), owner.TenantId)
			};
			// Initial historical declarations only. No forbidden status API,
			// subcent persistence, fabricated receipt or bound source mutation.
			typeof(LibraryCharge).GetProperty(nameof(LibraryCharge.PaymentStatus))!.SetValue(rows[0], ChargePaymentStatus.Paid);
			typeof(LibraryCharge).GetProperty(nameof(LibraryCharge.PaymentStatus))!.SetValue(rows[1], ChargePaymentStatus.Waived);
			await repository.InsertManyAsync(rows, true);
			return rows.Append(original).ToDictionary(row => row.Id,
				row => (row.Amount, row.PaymentStatus, row.ChargeDate, row.DueDate));
		});
		var denied = await RestrictMemberChargeReadAsync(owner);
		await AssertMemberChargeReadAuthorityAsync(owner, denied);
		await AssertMemberChargeTargetAsync(owner);
		await AssertNoTypedCollectionsAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			(await GetRequiredService<IRepository<FinanceExternalReceivableSourceLink, Guid>>().CountAsync()).ShouldBe(0);
			(await GetRequiredService<IRepository<Charge, Guid>>().CountAsync()).ShouldBe(0);
		});

		var page = await WithUnitOfWorkAsync(() => MemberChargeReadService(owner).GetMemberChargesAsync(owner.PersonId, 1, 100));

		page.TotalCount.ShouldBe(3); page.Items.Count.ShouldBe(3);
		foreach (var item in page.Items)
		{
			var saved = expected[item.Id];
			item.Amount.ShouldBe(saved.Amount); item.PaymentStatus.ShouldBe((int)saved.PaymentStatus); item.Date.ShouldBe(saved.ChargeDate);
			item.SchoolChargeDate.ShouldBeNull(); item.SchoolDueDate.ShouldBeNull();
			var json = MemberChargeWire(item);
			json.GetProperty("amountText").GetString().ShouldBe(saved.Amount.ToString("G29", CultureInfo.InvariantCulture));
			json.GetProperty("recordedCurrency").ValueKind.ShouldBe(JsonValueKind.Null);
			json.GetProperty("academicYearId").ValueKind.ShouldBe(JsonValueKind.Null);
			var position = RequireMemberChargePosition(item);
			position.GetProperty("evidence").GetInt32().ShouldBe((int)FinanceExternalPositionEvidence.Unposted);
			position.GetProperty("currency").ValueKind.ShouldBe(JsonValueKind.Null);
			position.GetProperty("amounts").ValueKind.ShouldBe(JsonValueKind.Null);
		}
		await WithUnitOfWorkAsync(async () =>
		{
			var rows = await GetRequiredService<IRepository<LibraryCharge, Guid>>().GetListAsync();
			foreach (var row in rows)
			{
				var saved = expected[row.Id];
				(row.Amount, row.PaymentStatus, row.ChargeDate, row.DueDate).ShouldBe(saved);
				row.Currency.ShouldBeNull(); row.FinanceSourceLinkId.ShouldBeNull();
			}
			(await GetRequiredService<IRepository<Charge, Guid>>().CountAsync()).ShouldBe(0);
		});
		await AssertNoTypedCollectionsAsync();
	}

	[Fact]
	public async Task Member_Charge_Position_Should_Expose_Genuine_Old_Source_Review_Without_Zeroing_Unknown_Money()
	{
		var legacy = await SeedLegacyOriginPostAsync();
		var owner = legacy.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var original = await ConsumerPositionAsync(owner);
		original.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		var retained = await CaptureDashboardClockSourceAsync(owner);
		await RenameLegacyOriginCategoryAsync(owner);
		(await CaptureDashboardClockSourceAsync(owner)).ShouldBe(retained);
		var denied = await RestrictMemberChargeReadAsync(owner);
		await AssertMemberChargeReadAuthorityAsync(owner, denied);
		await AssertMemberChargeTargetAsync(owner);
		var actual = await MemberChargePositionAsync(owner);
		actual.Evidence.ShouldBe(FinanceExternalPositionEvidence.NeedsReview);
		actual.Original.ShouldBeNull(); actual.Settled.ShouldBeNull(); actual.Collectible.ShouldBeNull();

		var page = await WithUnitOfWorkAsync(() => MemberChargeReadService(owner).GetMemberChargesAsync(owner.PersonId));

		page.TotalCount.ShouldBe(1);
		var item = page.Items.ShouldHaveSingleItem();
		item.Amount.ShouldBe(100m); item.PaymentStatus.ShouldBe((int)ChargePaymentStatus.Pending);
		var position = RequireMemberChargePosition(item);
		position.GetProperty("evidence").GetInt32().ShouldBe((int)FinanceExternalPositionEvidence.NeedsReview);
		position.GetProperty("amounts").ValueKind.ShouldBe(JsonValueKind.Null);
		position.GetProperty("reviewIssues").GetArrayLength().ShouldBeGreaterThan(0);
		position.TryGetProperty("sourceSnapshotJson", out _).ShouldBeFalse();
		position.TryGetProperty("sourceVersion", out _).ShouldBeFalse();
		(await CaptureDashboardClockSourceAsync(owner)).ShouldBe(retained);
		await AssertNoTypedCollectionsAsync();
	}
}
