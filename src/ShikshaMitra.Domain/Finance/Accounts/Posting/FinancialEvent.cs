using System;
using System.Security.Cryptography;
using System.Text;
using Volo.Abp.Domain.Entities;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Accounts.Posting;

/// <summary>
/// Durable, immutable source evidence. Capture is separate from deciding whether an
/// accepted source action is eligible for a journal under the school's accounting basis.
/// </summary>
public class FinancialEvent : Entity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid? AcademicYearId { get; private set; }
	public string EventKey { get; private set; } = string.Empty;
	public string SourceType { get; private set; } = string.Empty;
	public Guid SourceId { get; private set; }
	public string Transition { get; private set; } = string.Empty;
	public int TransitionVersion { get; private set; }
	public DateTimeOffset OccurredAt { get; private set; }
	public DateOnly BusinessDate { get; private set; }
	public string Currency { get; private set; } = string.Empty;
	public decimal Amount { get; private set; }
	public string SnapshotJson { get; private set; } = string.Empty;
	public string SnapshotHash { get; private set; } = string.Empty;

	protected FinancialEvent() { }

	public FinancialEvent(Guid id, Guid? tenantId, Guid? academicYearId,
		string sourceType, Guid sourceId, string transition, int transitionVersion,
		DateTimeOffset occurredAt, DateOnly businessDate, string currency, decimal amount,
		string snapshotJson) : base(id)
	{
		if (id == Guid.Empty || sourceId == Guid.Empty || transitionVersion < 1 ||
			string.IsNullOrWhiteSpace(sourceType) || sourceType.Length > 100 ||
			string.IsNullOrWhiteSpace(transition) || transition.Length > 100 ||
			string.IsNullOrWhiteSpace(snapshotJson) ||
			string.IsNullOrWhiteSpace(currency) || currency.Length != 3 ||
			amount <= 0 || decimal.Round(amount, 2) != amount)
			throw new ArgumentException("A financial event needs a stable source, transition, currency, and snapshot.");

		TenantId = tenantId;
		AcademicYearId = academicYearId;
		SourceType = sourceType.Trim();
		SourceId = sourceId;
		Transition = transition.Trim();
		TransitionVersion = transitionVersion;
		OccurredAt = occurredAt;
		BusinessDate = businessDate;
		Currency = currency.ToUpperInvariant();
		Amount = amount;
		SnapshotJson = snapshotJson;
		SnapshotHash = Hash(snapshotJson);
		EventKey = Hash($"{tenantId?.ToString("N") ?? "host"}|{SourceType}|{sourceId:N}|{Transition}|{transitionVersion}");
	}

	private static string Hash(string value) =>
		Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
