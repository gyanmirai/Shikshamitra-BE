using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Library;

public partial class LibraryFinanceExternalReceivable_Tests
{
	// M13: an infrastructure/evidence read failure must not become a successful
	// empty/zero/Unposted response over an actually verified financial position.
	[Fact]
	public async Task Member_Charge_History_Should_Propagate_The_Canonical_Detail_Reader_Failure()
	{
		var fixture = await SeedTypedRefundAsync(FinanceExternalPayerKind.Employee);
		var owner = fixture.Settlement.Owner;
		using var tenant = GetRequiredService<ICurrentTenant>().Change(owner.TenantId);
		using var actor = SignIn(owner);
		var denied = await RestrictMemberChargeReadAsync(owner);
		await AssertMemberChargeReadAuthorityAsync(owner, denied);
		await AssertMemberChargeTargetAsync(owner);
		var actual = await MemberChargePositionAsync(owner);
		actual.SourceId.ShouldBe(owner.AssessmentId);
		actual.Evidence.ShouldBe(FinanceExternalPositionEvidence.Verified);
		actual.Settled.ShouldBe(40.25m); actual.FinancialOutstanding.ShouldBe(60.50m);
		var expected = new InvalidOperationException($"Canonical member-charge detail failure {Guid.NewGuid():N}");
		var failure = new FailingMemberChargeDetailReader(expected);

		var error = await Should.ThrowAsync<InvalidOperationException>(() => WithUnitOfWorkAsync(() =>
			MemberChargeReadService(owner, detailOverride: failure).GetMemberChargesAsync(owner.PersonId, 1, 1)));

		error.ShouldBeSameAs(expected);
		failure.Calls.ShouldBe(1);
		failure.SourceIds.ShouldHaveSingleItem().ShouldBe(owner.AssessmentId);
		failure.TenantIds.ShouldHaveSingleItem().ShouldBe(owner.TenantId);
	}

	private sealed class FailingMemberChargeDetailReader(InvalidOperationException failure) : IFinanceExternalPositionReader
	{
		public int Calls { get; private set; }
		public Guid[] SourceIds { get; private set; } = [];
		public Guid[] TenantIds { get; private set; } = [];

		public Task<IReadOnlyList<FinanceExternalSourcePosition>> ReadAsync(
			IReadOnlyList<FinanceExternalPositionSource> authorizedSources, CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();
			Calls++;
			SourceIds = authorizedSources.Select(source => source.SourceId).ToArray();
			TenantIds = authorizedSources.Select(source => source.TenantId).Distinct().ToArray();
			return Task.FromException<IReadOnlyList<FinanceExternalSourcePosition>>(failure);
		}
	}
}

