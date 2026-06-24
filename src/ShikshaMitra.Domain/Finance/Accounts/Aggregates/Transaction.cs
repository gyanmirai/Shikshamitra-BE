using System;
using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.Finance.Accounts.Settings;
using ShikshaMitra.Finance.Accounts.Transactions;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Accounts.Incomes;

/// <summary>
/// Represents an income transaction entry
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
	/// Payment method (0=CHEQUE, 1=DEMAND_DRAFT, 2=BANK_TRANSFER, 3=CREDIT_CARD, 4=UPI, 5=ONLINE_PAYMENT_GATEWAY, 6=WALLET)
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
		CategoryId = categoryId;
		Name = name;
		Amount = amount;
		PaymentMethod = paymentMethod;
		PaymentDate = paymentDate;
		Notes = notes;

		if (categoryTags != null)
			SetCategoryTags(categoryTags);
	}
}
