using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.Collections;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Accounts.Incomes;

/// <summary>
/// Represents a legacy Accounts income or expense transaction entry; this is not a posted journal.
/// </summary>
public class Transaction : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? CashCollectionId { get; private set; }
	public void LinkCashCollection(Guid id, bool pendingIncome = false, Guid? studentId = null, Guid? financeFeeAccountId = null)
	{
		if (CashCollectionId.HasValue || PaymentMethod != 0 || id == Guid.Empty || pendingIncome && TransactionType != TransactionType.Income)
			throw new BusinessException(ShikshaMitra.Finance.Accounts.CashManagement.CashManagementErrorCodes.InvalidSource);
		CashCollectionId = id;
		StudentId = studentId; FinanceFeeAccountId = financeFeeAccountId;
		if (pendingIncome) IsConfirmed = false;
	}
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid CategoryId { get; private set; }
 public string? SourceKey { get; private set; }
 public Guid? SourceId { get; private set; }
 public Guid? StudentId { get; private set; }
 public Guid? FinanceFeeAccountId { get; private set; }
 public void LinkStudent(Guid studentId) => StudentId = ShikshaMitra.Finance.Fees.FeeCollection.FinanceFeePayerIdentity.LinkStudent(StudentId, studentId);
 public Guid? BankAccountId { get; private set; }
 public string? CategoryNameSnapshot { get; private set; }
 public string? BankLabelSnapshot { get; private set; }
 public bool IsConfirmed { get; private set; } = true;
 public bool IsAutomatic => SourceKey != null;
 public void Confirm() => IsConfirmed = true;
	public const string OriginalCashGrossAmountKey = "OriginalCashGrossAmount";
	public decimal GetOriginalCashIncomeAmount() => ExtraProperties.TryGetValue(OriginalCashGrossAmountKey, out var gross)
		? gross is decimal amount ? amount : decimal.Parse(gross!.ToString()!, System.Globalization.CultureInfo.InvariantCulture) : Amount;
	public void ApproveCashIncome(Guid bankId, string bankLabel, decimal? depositedAmount = null)
	{
		if (PaymentMethod != 0 || TransactionType != TransactionType.Income || IsConfirmed ||
			ExtraProperties.ContainsKey(OriginalCashGrossAmountKey) || bankId == Guid.Empty || string.IsNullOrWhiteSpace(bankLabel))
			throw new BusinessException(ShikshaMitra.Finance.Accounts.CashManagement.CashManagementErrorCodes.InvalidState);
		var net = depositedAmount ?? Amount;
		if (net < 0 || net > Amount || decimal.Round(net, 2) != net)
			throw new BusinessException(ShikshaMitra.Finance.Accounts.CashManagement.CashManagementErrorCodes.InvalidAmount);
		ExtraProperties[OriginalCashGrossAmountKey] = Amount;
		Amount = net; BankAccountId = bankId; BankLabelSnapshot = bankLabel;
		// Retain zero shares as suppressed audit evidence; an approved source is closed
		// even though its zero Income rows must stay out of all confirmed register reads.
		IsConfirmed = net > 0;
	}
 public void SetManualRouting(string categoryName, Guid? bankId, string? bankLabel, string? reference)
 {
   if (IsAutomatic) throw new BusinessException("Finance:AutomaticEntryImmutable");
   if (PaymentMethod != 0 && !bankId.HasValue) throw new BusinessException(AccountsRegisterErrorCodes.BankRequired);
   if (PaymentMethod == 0 && bankId.HasValue) throw new BusinessException(AccountsRegisterErrorCodes.InvalidCashBank);
   CategoryNameSnapshot = categoryName; BankAccountId = bankId; BankLabelSnapshot = bankLabel;
   ExtraProperties["PaymentReference"] = reference?.Trim();
 }

 public static Transaction Automatic(Guid id, Guid? tenantId, Guid yearId, Guid categoryId,
   string categoryName, Guid? bankId, string? bankLabel, string name, decimal amount,
   int method, DateTime at, TransactionType type, string key, Guid sourceId, Guid? studentId,
   bool confirmed = true, bool correction = false, Guid? financeFeeAccountId = null)
 {
   var entry = new Transaction(id, tenantId, yearId, categoryId, name, null, amount, method, at, null, type);
   entry.SourceKey = Check.NotNullOrWhiteSpace(key, nameof(key));
   entry.SourceId = sourceId; entry.StudentId = studentId; entry.BankAccountId = bankId;
   entry.FinanceFeeAccountId = financeFeeAccountId;
   entry.CategoryNameSnapshot = categoryName; entry.BankLabelSnapshot = bankLabel;
   entry.IsConfirmed = confirmed; if (correction) entry.Amount = -amount;
   return entry;
 }
	public string Name { get; private set; }

	private List<Tag> _categoryTags = new List<Tag>();

	/// <summary>
	/// Category tags associated with this transaction
	/// </summary>
	public IReadOnlyCollection<Tag> CategoryTags => _categoryTags.AsReadOnly();

	public decimal Amount { get; private set; }

	/// <summary>
	/// Uses the shared Fee Collections method values: 0=Cash, 1=Cheque, 2=DemandDraft,
	/// 3=BankTransfer, 4=CreditCard, 5=DebitCard, 6=UPI.
	/// </summary>
	public int PaymentMethod { get; private set; }

	public DateTime PaymentDate { get; private set; }
	public string? Notes { get; private set; }
	public TransactionType TransactionType { get; private set; }

	protected Transaction()
	{
		// EF materializes these required columns after this ORM constructor.
		Name = null!;

		/* ORM / deserialization */
	}

	public Transaction(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid categoryId,
		string name,
		IEnumerable<string>? categoryTags,
		decimal amount,
		int paymentMethod,
		DateTime paymentDate,
		string? notes = null,
		TransactionType transactionType = TransactionType.Income)
		: base(id)
	{
		EnsureValidMoneyAndMethod(amount, paymentMethod);
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		CategoryId = categoryId;
		Name = name;
		Amount = amount;
		PaymentMethod = paymentMethod;
		PaymentDate = paymentDate;
		Notes = notes;
		TransactionType = transactionType;

		if (categoryTags != null)
			foreach (var tag in categoryTags)
				AddCategoryTag(tag);
	}

	public void AddCategoryTag(string value)
	{
		var tag = new Tag(value);
		if (_categoryTags.All(t => t.Value != tag.Value))
			_categoryTags.Add(tag);
	}

	public void RemoveCategoryTag(string value)
	{
		var normalized = value.Trim().ToLowerInvariant();
		_categoryTags.RemoveAll(t => t.Value == normalized);
	}

	public void ClearCategoryTags() => _categoryTags.Clear();

	public void SetCategoryTags(IEnumerable<string> tags)
	{
		_categoryTags.Clear();
		foreach (var tag in tags)
			AddCategoryTag(tag);
	}

	public void Update(
		Guid categoryId,
		string name,
		IEnumerable<string>? categoryTags,
		decimal amount,
		int paymentMethod,
		DateTime paymentDate,
		string? notes,
		string? categoryNameSnapshot = null)
	{
		if (IsAutomatic) throw new BusinessException("Finance:AutomaticEntryImmutable");
		if (CashCollectionId.HasValue) throw new BusinessException(ShikshaMitra.Finance.Accounts.CashManagement.CashManagementErrorCodes.Immutable);
		EnsureValidMoneyAndMethod(amount, paymentMethod);
		CategoryId = categoryId;
		Name = name;
		Amount = amount;
		PaymentMethod = paymentMethod;
		PaymentDate = paymentDate;
		Notes = notes;
		if (categoryNameSnapshot != null) CategoryNameSnapshot = categoryNameSnapshot;

		if (categoryTags != null)
			SetCategoryTags(categoryTags);
	}

	public static void EnsureValidMoneyAndMethod(decimal amount, int paymentMethod)
	{
		if (amount is < 0.01m or > 999999999999.99m || decimal.Round(amount, 2) != amount)
			throw new BusinessException(TransactionErrorCodes.InvalidAmount);
		if (!Enum.IsDefined(typeof(PaymentMethodEnum), paymentMethod))
			throw new BusinessException(TransactionErrorCodes.InvalidPaymentMethod);
	}
}
