using System;
using ShikshaMitra.Finance.Accounts.Posting;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class AccountingPostingFoundation_Tests
{
	[Fact]
	public void Source_identity_is_stable_and_tenant_separated()
	{
		var tenant = Guid.NewGuid();
		var source = Guid.NewGuid();
		var first = Event(tenant, source, 1, "{\"amount\":100}");
		var replay = Event(tenant, source, 1, "{\"amount\":100}");
		var next = Event(tenant, source, 2, "{\"amount\":100}");
		var otherTenant = Event(Guid.NewGuid(), source, 1, "{\"amount\":100}");

		first.EventKey.ShouldBe(replay.EventKey);
		first.EventKey.ShouldNotBe(next.EventKey);
		first.EventKey.ShouldNotBe(otherTenant.EventKey);
		first.SnapshotHash.ShouldBe(replay.SnapshotHash);
		first.Amount.ShouldBe(100m);
	}

	[Fact]
	public void Source_event_rejects_fractional_paise()
	{
		Should.Throw<ArgumentException>(() => new FinancialEvent(
			Guid.NewGuid(), Guid.NewGuid(), null, "FeeCollection", Guid.NewGuid(),
			"Accepted", 1, DateTimeOffset.UtcNow, new DateOnly(2026, 10, 1),
			"INR", 0.001m, "{}"));
	}

	[Fact]
	public void A_journal_requires_exact_paise_balance()
	{
		var id = Guid.NewGuid();
		var debit = new JournalLine(Guid.NewGuid(), id, Guid.NewGuid(), null, 0.01m, 0);
		var credit = new JournalLine(Guid.NewGuid(), id, Guid.NewGuid(), null, 0, 0.01m);
		var entry = Journal(id, debit, credit);

		entry.TotalDebits.ShouldBe(0.01m);
		entry.TotalCredits.ShouldBe(0.01m);
		Should.Throw<ArgumentException>(() => Journal(Guid.NewGuid(), debit, credit));
		Should.Throw<ArgumentException>(() => Journal(id, debit,
			new JournalLine(Guid.NewGuid(), id, Guid.NewGuid(), null, 0, 0.02m)));
	}

	[Theory]
	[InlineData(0, 0)]
	[InlineData(-1, 0)]
	[InlineData(0.001, 0)]
	[InlineData(1, 1)]
	public void A_line_rejects_invalid_money(decimal debit, decimal credit)
	{
		Should.Throw<ArgumentException>(() => new JournalLine(
			Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, debit, credit));
	}

	private static FinancialEvent Event(Guid tenant, Guid source, int version, string snapshot) =>
		new(Guid.NewGuid(), tenant, null, "FeeCollection", source, "Accepted", version,
			DateTimeOffset.UtcNow, new DateOnly(2026, 10, 1), "INR", 100m, snapshot);

	private static JournalEntry Journal(Guid id, params JournalLine[] lines) =>
		new(id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
			new DateOnly(2026, 10, 1), DateTimeOffset.UtcNow, "INR", "J-1",
			Guid.NewGuid(), lines);
}
