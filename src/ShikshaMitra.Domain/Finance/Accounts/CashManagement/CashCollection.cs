using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Accounts.CashManagement;

/// <summary>One collected cash instrument. Pending expenses reduce final net deposit approval; legacy transit retains credit evidence.</summary>
public class CashCollection : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid SourceId { get; private set; }
	public Guid? PaymentInstrumentId { get; private set; }
	public string IncomeSourceKeyPrefix { get; private set; } = "";
	public Guid? IncomeTransactionId { get; private set; }
	public string Reference { get; private set; } = "";
	public string PayerName { get; private set; } = "";
	public Guid? StudentId { get; private set; }
	public Guid? FinanceFeeAccountId { get; private set; }
	public Guid CollectedBy { get; private set; }
	public DateTimeOffset CollectedAt { get; private set; }
	public decimal Amount { get; private set; }
	public decimal SpentAmount { get; private set; }
	public decimal DepositedAmount { get; private set; }
	public decimal InTransitAmount { get; private set; }
	public decimal RemainingAmount => Amount - SpentAmount - DepositedAmount - InTransitAmount;
	public int Status { get; private set; }
	public Guid? BankAccountId { get; private set; }
	public string? BankLabel { get; private set; }
	public Guid? ApprovedBy { get; private set; }
	public DateTimeOffset? ApprovedAt { get; private set; }
	public string? Notes { get; private set; }
	public List<CashDepositRecord> Deposits { get; private set; } = [];
	public List<CashExpenseRecord> Expenses { get; private set; } = [];
	public List<CashCommandRecord> Commands { get; private set; } = [];
	private CashCollection() { }
	public CashCollection(Guid id, Guid? tenantId, Guid yearId, Guid sourceId, string incomePrefix,
		Guid? incomeTransactionId, string reference, string payerName, decimal amount, DateTimeOffset at,
		Guid actor, Guid? studentId, Guid? feeAccountId, Guid? paymentInstrumentId = null) : base(id)
	{
		EnsureMoney(amount);
		if (id == Guid.Empty || yearId == Guid.Empty || sourceId == Guid.Empty || actor == Guid.Empty || at == default)
			throw new BusinessException(CashManagementErrorCodes.InvalidSource);
		TenantId = tenantId; AcademicYearId = yearId; SourceId = sourceId;
		PaymentInstrumentId = paymentInstrumentId;
		IncomeSourceKeyPrefix = Evidence(incomePrefix, 200); IncomeTransactionId = incomeTransactionId;
		Reference = Evidence(reference, 200); PayerName = Evidence(payerName, 500);
		Amount = amount; CollectedAt = at; CollectedBy = actor;
		StudentId = studentId; FinanceFeeAccountId = feeAccountId;
	}
	public void Approve(Guid actor, Guid bankId, string bankLabel, decimal countedAmount, DateTimeOffset at, string? notes,
		bool allowSelfApproval = false)
	{
		if (countedAmount < 0 || countedAmount > 999999999999.99m || decimal.Round(countedAmount, 2) != countedAmount)
			throw new BusinessException(CashManagementErrorCodes.InvalidAmount);
		if (Status != 0) throw new BusinessException(CashManagementErrorCodes.InvalidState);
		Independent(actor, CollectedBy, allowSelfApproval);
		if (bankId == Guid.Empty) throw new BusinessException(CashManagementErrorCodes.InvalidEvidence);
		if (countedAmount != RemainingAmount) throw new BusinessException(CashManagementErrorCodes.CountMismatch);
		if (at == default || at < CollectedAt) throw new BusinessException(CashManagementErrorCodes.InvalidEvidence);
		var label = Evidence(bankLabel, 500); var cleanNotes = OptionalNotes(notes);
		BankAccountId = bankId; BankLabel = label; ApprovedBy = actor; ApprovedAt = at;
		Notes = cleanNotes; Status = 1;
		if (countedAmount > 0)
		{
			var deposit = new CashDepositRecord(Guid.NewGuid(), actor, countedAmount, at, Reference, cleanNotes);
			deposit.Credit(actor, countedAmount, at, Reference, cleanNotes);
			Deposits.Add(deposit); DepositedAmount += countedAmount;
		}
	}
	public void Spend(Guid transactionId, string name, decimal amount, DateTimeOffset paymentDate)
	{
		Available(amount, requiredStatus: 0);
		if (paymentDate == default || paymentDate < CollectedAt) throw new BusinessException(CashManagementErrorCodes.InvalidEvidence);
		if (transactionId == Guid.Empty || Expenses.Any(x => x.Id == transactionId))
			throw new BusinessException(CashManagementErrorCodes.RetryConflict);
		Expenses.Add(new CashExpenseRecord(transactionId, Evidence(name, 200), amount, paymentDate));
		SpentAmount += amount;
	}
	public CashDepositRecord Dispatch(Guid id, Guid actor, decimal amount, DateTimeOffset at, string reference, string? notes)
	{
		Available(amount);
		if (actor == Guid.Empty || id == Guid.Empty || at == default || at < CollectedAt)
			throw new BusinessException(CashManagementErrorCodes.InvalidEvidence);
		var deposit = new CashDepositRecord(id, actor, amount, at, Evidence(reference, 200), OptionalNotes(notes));
		Deposits.Add(deposit); InTransitAmount += amount; return deposit;
	}
	public void Credit(Guid depositId, Guid actor, decimal amount, DateTimeOffset at, string reference, string? notes)
	{
		EnsureMoney(amount);
		var deposit = Deposits.SingleOrDefault(x => x.Id == depositId)
			?? throw new BusinessException(CashManagementErrorCodes.InvalidSource);
		Independent(actor, deposit.CreatedBy);
		if (at < deposit.DepositDate || at == default || amount > deposit.Amount - deposit.CreditedAmount)
			throw new BusinessException(CashManagementErrorCodes.InvalidEvidence);
		deposit.Credit(actor, amount, at, Evidence(reference, 200), OptionalNotes(notes));
		DepositedAmount += amount; InTransitAmount -= amount;
	}
	public void EnsureStamp(string stamp)
	{
		if (string.IsNullOrWhiteSpace(stamp) || stamp != ConcurrencyStamp)
			throw new BusinessException(CashManagementErrorCodes.Stale);
	}
	public string? Replay(Guid key, string fingerprint)
	{
		if (key == Guid.Empty) throw new BusinessException(CashManagementErrorCodes.RetryConflict);
		var prior = Commands.SingleOrDefault(x => x.Id == key);
		if (prior == null) return null;
		if (prior.Fingerprint != fingerprint) throw new BusinessException(CashManagementErrorCodes.RetryConflict);
		return prior.ResultJson;
	}
	public void SaveCommand(Guid key, string fingerprint, string result)
	{
		if (key == Guid.Empty || Commands.Any(x => x.Id == key)) throw new BusinessException(CashManagementErrorCodes.RetryConflict);
		Commands.Add(new CashCommandRecord(key, fingerprint, result));
	}
	private void Available(decimal amount, int requiredStatus = 1)
	{
		EnsureMoney(amount);
		if (Status != requiredStatus) throw new BusinessException(CashManagementErrorCodes.InvalidState);
		if (amount > RemainingAmount) throw new BusinessException(CashManagementErrorCodes.InsufficientCash);
	}
	public static void EnsureMoney(decimal amount)
	{
		if (amount is <= 0 or > 999999999999.99m || decimal.Round(amount, 2) != amount)
			throw new BusinessException(CashManagementErrorCodes.InvalidAmount);
	}
	private static void Independent(Guid actor, Guid maker, bool allowSameActor = false)
	{
		if (actor == Guid.Empty || actor == maker && !allowSameActor) throw new BusinessException(CashManagementErrorCodes.IndependentChecker);
	}
	private static string Evidence(string value, int max)
	{
		if (string.IsNullOrWhiteSpace(value) || value.Length > max) throw new BusinessException(CashManagementErrorCodes.InvalidEvidence);
		return value.Trim();
	}
	private static string? OptionalNotes(string? notes)
	{
		if (notes?.Length > 2000) throw new BusinessException(CashManagementErrorCodes.InvalidEvidence);
		return notes?.Trim();
	}
	// ABP owns ConcurrencyStamp rotation when saving. It uses the current value as
	// the UPDATE predicate's original value, so domain mutations must retain it.
}

public class CashDepositRecord : Entity<Guid>
{
	public Guid CreatedBy { get; private set; }
	public decimal Amount { get; private set; }
	public decimal CreditedAmount { get; private set; }
	public string Reference { get; private set; } = "";
	public DateTimeOffset DepositDate { get; private set; }
	public DateTimeOffset? CreditDate { get; private set; }
	public string? Notes { get; private set; }
	public List<CashCreditRecord> Credits { get; private set; } = [];
	private CashDepositRecord() { }
	internal CashDepositRecord(Guid id, Guid actor, decimal amount, DateTimeOffset at, string reference, string? notes) : base(id)
	{ CreatedBy = actor; Amount = amount; DepositDate = at; Reference = reference; Notes = notes; }
	internal void Credit(Guid actor, decimal amount, DateTimeOffset at, string reference, string? notes)
	{
		Credits.Add(new CashCreditRecord(Guid.NewGuid(), actor, amount, at, reference, notes));
		CreditedAmount += amount; CreditDate = at;
	}
}
public class CashCreditRecord : Entity<Guid>
{
	public Guid CreatedBy { get; private set; }
	public decimal Amount { get; private set; }
	public DateTimeOffset CreditDate { get; private set; }
	public string Reference { get; private set; } = "";
	public string? Notes { get; private set; }
	private CashCreditRecord() { }
	internal CashCreditRecord(Guid id, Guid actor, decimal amount, DateTimeOffset at, string reference, string? notes) : base(id)
	{ CreatedBy = actor; Amount = amount; CreditDate = at; Reference = reference; Notes = notes; }
}
public class CashExpenseRecord : Entity<Guid>
{
	public string Name { get; private set; } = "";
	public decimal Amount { get; private set; }
	public DateTimeOffset PaymentDate { get; private set; }
	private CashExpenseRecord() { }
	internal CashExpenseRecord(Guid id, string name, decimal amount, DateTimeOffset at) : base(id)
	{ Name = name; Amount = amount; PaymentDate = at; }
}
public class CashCommandRecord : Entity<Guid>
{
	public string Fingerprint { get; private set; } = "";
	public string ResultJson { get; private set; } = "";
	private CashCommandRecord() { }
	internal CashCommandRecord(Guid id, string fingerprint, string result) : base(id)
	{ Fingerprint = fingerprint; ResultJson = result; }
}
