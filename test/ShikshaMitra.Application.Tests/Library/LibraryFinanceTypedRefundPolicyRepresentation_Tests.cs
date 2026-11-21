using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Finance.Fees.FeeRefund;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	[Fact]
	public async Task Typed_Refund_Approval_Should_Accept_The_Unchanged_Policy_After_Jsonb_Representation_Normalization()
	{
		var fixture = await SeedSubmittedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Administrator.TenantId);
		using var actor = SignIn(fixture.Administrator);
		await SeedRefundPolicyRepresentationAsync(fixture, changeRule: false);

		var approved = await WithUnitOfWorkAsync(() => DownstreamRefundService(fixture.Administrator)
			.ApproveAsync(fixture.Submitted.Id, new() { Notes = "Reviewed the same pinned rules after provider JSON formatting." }));

		approved.Status.ShouldBe(FeeRefundStatus.Approved);
		approved.TotalAmount.ShouldBe(15.25m);
		approved.CalculatedAmount.ShouldBe(15m);
		approved.ApprovedAmount.ShouldBe(15m);
		approved.ProcessedAmount.ShouldBe(0m);
		approved.RefundPolicyVersionId.ShouldBe(fixture.Source.PolicyVersionId);
	}

	[Fact]
	public async Task Typed_Refund_Approval_Should_Reject_An_Actually_Changed_Policy_Despite_The_Same_Version_Id()
	{
		var fixture = await SeedSubmittedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		using var tenant = GetRequiredService<ICurrentTenant>().Change(fixture.Administrator.TenantId);
		using var actor = SignIn(fixture.Administrator);
		await SeedRefundPolicyRepresentationAsync(fixture, changeRule: true);

		var denied = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(() =>
			DownstreamRefundService(fixture.Administrator).ApproveAsync(fixture.Submitted.Id,
				new() { Notes = "A modified rule cannot inherit the original version authority." })));

		denied.Code.ShouldBe(FeeRefundErrorCodes.PolicyVersionInvalid);
		await AssertTypedRefundStillSubmittedAsync(fixture);
	}

	private Task SeedRefundPolicyRepresentationAsync(DownstreamRefundFixture fixture, bool changeRule) => WithUnitOfWorkAsync(async () =>
	{
		var versionId = fixture.Source.PolicyVersionId ?? throw new InvalidOperationException("The pinned source policy version is required.");
		var version = await GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().GetAsync(versionId);
		var original = JsonNode.Parse(version.SnapshotJson)?.AsObject()
			?? throw new InvalidOperationException("The real recorded policy needs an object snapshot.");
		var normalized = new JsonObject();
		foreach (var field in original.Reverse()) normalized.Add(field.Key, field.Value?.DeepClone());
		if (changeRule) normalized[nameof(RefundPolicySnapshot.PercentageValue)] = 75m;
		var representation = normalized.ToJsonString(new() { WriteIndented = true });
		representation.ShouldNotBe(version.SnapshotJson);
		using var before = JsonDocument.Parse(version.SnapshotJson);
		using var after = JsonDocument.Parse(representation);
		JsonElement.DeepEquals(before.RootElement, after.RootElement).ShouldBe(!changeRule);
		// Simulate jsonb's storage representation on the SQLite test provider. Only
		// key order/spacing changes in the positive case; version and money stay exact.
		var database = (await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync()).Database;
		(await database.ExecuteSqlInterpolatedAsync($"UPDATE \"Finance_FeeRefundRequests\" SET \"PolicySnapshotJson\" = {representation} WHERE \"Id\" = {fixture.Submitted.Id}"))
			.ShouldBe(1);
	});
}
