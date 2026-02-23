using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Expenses;

/// <summary>
/// Represents an expense transaction entry
/// </summary>
public class Expense : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid CategoryId { get; private set; }
	public string Name { get; private set; }

	/// <summary>
	/// Category tags stored as comma-separated values
	/// </summary>
	public string CategoryTags { get; private set; }

	public decimal Amount { get; private set; }

	/// <summary>
	/// Payment method (0=CHEQUE, 1=DEMAND_DRAFT, 2=BANK_TRANSFER, 3=CREDIT_CARD, 4=UPI, 5=ONLINE_PAYMENT_GATEWAY, 6=WALLET)
	/// </summary>
	public int PaymentMethod { get; private set; }

	public DateTime PaymentDate { get; private set; }
	public string? Notes { get; private set; }

	protected Expense()
	{
		/* ORM / deserialization */
	}

	public Expense(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid categoryId,
		string name,
		string categoryTags,
		decimal amount,
		int paymentMethod,
		DateTime paymentDate,
		string? notes = null)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		CategoryId = categoryId;
		Name = name;
		CategoryTags = categoryTags;
		Amount = amount;
		PaymentMethod = paymentMethod;
		PaymentDate = paymentDate;
		Notes = notes;
	}

	public void Update(
		Guid categoryId,
		string name,
		string categoryTags,
		decimal amount,
		int paymentMethod,
		DateTime paymentDate,
		string? notes)
	{
		CategoryId = categoryId;
		Name = name;
		CategoryTags = categoryTags;
		Amount = amount;
		PaymentMethod = paymentMethod;
		PaymentDate = paymentDate;
		Notes = notes;
	}
}
