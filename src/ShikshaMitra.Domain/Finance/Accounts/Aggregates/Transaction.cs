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
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid CategoryId { get; private set; }
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
		string? notes)
	{
		EnsureValidMoneyAndMethod(amount, paymentMethod);
		CategoryId = categoryId;
		Name = name;
		Amount = amount;
		PaymentMethod = paymentMethod;
		PaymentDate = paymentDate;
		Notes = notes;

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
