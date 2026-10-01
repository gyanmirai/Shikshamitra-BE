using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Accounts.Posting;

/// <summary>A balanced, source-linked accounting fact. Corrections use a new entry.</summary>
public class JournalEntry : AggregateRoot<Guid>, IMultiTenant
{
	private readonly List<JournalLine> _lines = new();
	public Guid? TenantId { get; private set; }
	public Guid FinancialEventId { get; private set; }
	public Guid AccountingPeriodId { get; private set; }
	public Guid? CorrectsJournalEntryId { get; private set; }
	public DateOnly PostingDate { get; private set; }
	public DateTimeOffset PostedAt { get; private set; }
	public string Currency { get; private set; } = string.Empty;
	public string JournalNumber { get; private set; } = string.Empty;
	public Guid PostedByUserId { get; private set; }
	public IReadOnlyCollection<JournalLine> Lines => _lines.AsReadOnly();
	public decimal TotalDebits => _lines.Sum(x => x.Debit);
	public decimal TotalCredits => _lines.Sum(x => x.Credit);

	protected JournalEntry() { }

	public JournalEntry(Guid id, Guid? tenantId, Guid financialEventId,
		Guid accountingPeriodId, Guid? correctsJournalEntryId, DateOnly postingDate,
		DateTimeOffset postedAt, string currency, string journalNumber,
		Guid postedByUserId, IEnumerable<JournalLine> lines) : base(id)
	{
		if (id == Guid.Empty || financialEventId == Guid.Empty || accountingPeriodId == Guid.Empty ||
			postedByUserId == Guid.Empty || string.IsNullOrWhiteSpace(journalNumber) ||
			journalNumber.Length > 50 || string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
			throw new ArgumentException("A journal needs a source, period, number, actor, and currency.");
		if (correctsJournalEntryId == id)
			throw new ArgumentException("A journal cannot correct itself.");
		if (lines is null)
			throw new ArgumentNullException(nameof(lines));

		_lines.AddRange(lines);
		if (_lines.Count < 2 || _lines.Any(x => x is null || x.JournalEntryId != id) ||
			TotalDebits <= 0 || TotalDebits != TotalCredits)
			throw new ArgumentException("A journal needs at least two valid lines with exactly balanced debit and credit totals.");

		TenantId = tenantId;
		FinancialEventId = financialEventId;
		AccountingPeriodId = accountingPeriodId;
		CorrectsJournalEntryId = correctsJournalEntryId;
		PostingDate = postingDate;
		PostedAt = postedAt;
		Currency = currency.ToUpperInvariant();
		JournalNumber = journalNumber.Trim();
		PostedByUserId = postedByUserId;
	}
}

public class JournalLine : Entity<Guid>
{
	public Guid JournalEntryId { get; private set; }
	public Guid AccountHeadId { get; private set; }
	public Guid? SourceAllocationId { get; private set; }
	public decimal Debit { get; private set; }
	public decimal Credit { get; private set; }

	protected JournalLine() { }

	public JournalLine(Guid id, Guid journalEntryId, Guid accountHeadId,
		Guid? sourceAllocationId, decimal debit, decimal credit) : base(id)
	{
		if (id == Guid.Empty || journalEntryId == Guid.Empty || accountHeadId == Guid.Empty ||
			debit < 0 || credit < 0 || (debit > 0) == (credit > 0) ||
			decimal.Round(debit, 2) != debit || decimal.Round(credit, 2) != credit)
			throw new ArgumentException("A journal line needs one positive whole-paise side and a valid account.");
		JournalEntryId = journalEntryId;
		AccountHeadId = accountHeadId;
		SourceAllocationId = sourceAllocationId;
		Debit = debit;
		Credit = credit;
	}
}
