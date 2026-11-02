using ShikshaMitra.Domain.Finance.Fees.Settings;
using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Data;

namespace ShikshaMitra.Finance.Fees.Charges;

/// <summary>
/// Represents a charge applied to a student
/// </summary>
public class Charge : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	private const string VoidedByProperty = "ChargeVoidedBy";
	private const string VoidedOnProperty = "ChargeVoidedOn";
	private const string VoidReasonProperty = "ChargeVoidReason";
	private const string IncomeCategoryIdProperty = "ChargeIncomeCategoryId";
	private const string IncomeAccountHeadIdProperty = "ChargeIncomeAccountHeadId";
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid ChargeRequestId { get; private set; }
	public string ChargeName { get; private set; } = string.Empty;
	public Guid ChargeCategoryId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid BoardId { get; private set; }
	public Guid EducationMediumId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid DivisionId { get; private set; }
	public string PurposeDescription { get; private set; }
	public decimal Amount { get; private set; }
	public DateTimeOffset DueDate { get; private set; }
	public DateTimeOffset AppliedOn { get; private set; }
	public ChargePriority Priority { get; private set; }
	public ChargeStatus Status { get; private set; }
	public string? AdditionalNotes { get; private set; }
	public Guid RequestorId { get; private set; }
	public Guid? ApprovedBy { get; private set; }
	public DateTimeOffset? ApprovedOn { get; private set; }
	public string? ApprovalNotes { get; private set; }
	public Guid? VoidedBy => this.GetProperty<Guid?>(VoidedByProperty);
	public DateTimeOffset? VoidedOn => this.GetProperty<DateTimeOffset?>(VoidedOnProperty);
	public string? VoidReason => this.GetProperty<string>(VoidReasonProperty);
	public string? IncomeCategoryName { get; private set; }
	public Guid? IncomeCategoryId => this.GetProperty<Guid?>(IncomeCategoryIdProperty);
	public Guid? IncomeAccountHeadId => this.GetProperty<Guid?>(IncomeAccountHeadIdProperty);

	private Charge()
	{
		// EF materializes these required columns after this ORM constructor.
		PurposeDescription = null!;

		/* This constructor is for deserialization / ORM purpose */
	}

	public Charge(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid chargeRequestId,
		string chargeName,
		Guid chargeCategoryId,
		Guid studentId,
		Guid boardId,
		Guid educationMediumId,
		Guid standardId,
		Guid divisionId,
		string purposeDescription,
		decimal amount,
		DateTimeOffset dueDate,
		ChargePriority priority,
		ChargeStatus initialStatus,
		Guid requestorId,
		string? additionalNotes = null)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = Check.NotNull(academicYearId, nameof(academicYearId));
		ChargeRequestId = Check.NotNull(chargeRequestId, nameof(chargeRequestId));
		ChargeName = Check.NotNullOrWhiteSpace(chargeName, nameof(chargeName));
		ChargeCategoryId = Check.NotNull(chargeCategoryId, nameof(chargeCategoryId));
		StudentId = Check.NotNull(studentId, nameof(studentId));
		BoardId = Check.NotNull(boardId, nameof(boardId));
		EducationMediumId = Check.NotNull(educationMediumId, nameof(educationMediumId));
		StandardId = Check.NotNull(standardId, nameof(standardId));
		DivisionId = Check.NotNull(divisionId, nameof(divisionId));
		PurposeDescription = Check.NotNullOrWhiteSpace(purposeDescription, nameof(purposeDescription));
		Amount = amount;
		DueDate = dueDate;
		AppliedOn = DateTimeOffset.UtcNow;
		Priority = priority;
		Status = initialStatus;
		RequestorId = Check.NotNull(requestorId, nameof(requestorId));
		AdditionalNotes = additionalNotes;

		ValidateAmount();
		ValidateDueDate();
	}

	/// <summary>
	/// Updates charge details. Only allowed for PENDING charges.
	/// </summary>
	public void Update(
		Guid? chargeCategoryId = null,
		string? purposeDescription = null,
		decimal? amount = null,
		DateTimeOffset? dueDate = null,
		ChargePriority? priority = null,
		string? additionalNotes = null)
	{
		if (Status != ChargeStatus.Pending)
		{
			throw new BusinessException(FinanceChargeErrorCodes.ChargeNotPending)
				.WithData("Status", Status.ToString());
		}

		if (chargeCategoryId.HasValue)
		{
			ChargeCategoryId = chargeCategoryId.Value;
		}

		if (!string.IsNullOrWhiteSpace(purposeDescription))
		{
			PurposeDescription = purposeDescription;
		}

		if (amount.HasValue)
		{
			Amount = amount.Value;
			ValidateAmount();
		}

		if (dueDate.HasValue)
		{
			DueDate = dueDate.Value;
			ValidateDueDate();
		}

		if (priority.HasValue)
		{
			Priority = priority.Value;
		}

		AdditionalNotes = additionalNotes;
	}

	/// <summary>
	/// Approves the charge. Only allowed for PENDING charges.
	/// The approver must differ from the requestor (maker-checker rule).
	/// </summary>
	public void Approve(Guid approvedBy, string? approvalNotes = null)
	{
		if (Status != ChargeStatus.Pending)
		{
			throw new BusinessException(FinanceChargeErrorCodes.ChargeNotPending)
				.WithData("Status", Status.ToString())
				.WithData("Message", "Only pending charges can be approved");
		}

		Status = ChargeStatus.Approved;
		ApprovedBy = approvedBy;
		ApprovedOn = DateTimeOffset.UtcNow;
		ApprovalNotes = approvalNotes;
	}

	/// <summary>
	/// Rejects the charge with a mandatory reason. Only allowed for PENDING charges.
	/// The rejector must differ from the requestor (maker-checker rule).
	/// </summary>
	public void Reject(Guid rejectedBy, string rejectionReason)
	{
		if (Status != ChargeStatus.Pending)
		{
			throw new BusinessException(FinanceChargeErrorCodes.ChargeNotPending)
				.WithData("Status", Status.ToString())
				.WithData("Message", "Only pending charges can be rejected");
		}

		if (string.IsNullOrWhiteSpace(rejectionReason) || rejectionReason.Length < 10)
		{
			throw new BusinessException(FinanceChargeErrorCodes.InvalidRejectionReason)
				.WithData("Message", "Rejection reason must be at least 10 characters");
		}

		Status = ChargeStatus.Rejected;
		ApprovedBy = rejectedBy;
		ApprovedOn = DateTimeOffset.UtcNow;
		ApprovalNotes = rejectionReason;
	}

	/// <summary>
	/// Automatically approves the charge when the charge category does not require approval.
	/// Unlike <see cref="Approve"/>, this bypasses the maker-checker rule since no human approver is involved.
	/// Only applies to pending charges; already-approved/rejected charges are left unchanged.
	/// </summary>
	public void AutoApprove()
	{
		if (Status != ChargeStatus.Pending)
			return;

		Status = ChargeStatus.Approved;
		ApprovedOn = DateTimeOffset.UtcNow;
	}

	/// <summary>
	/// Voids an approved obligation that has not been collected. Collection state is
	/// checked by the application service in the same serializable transaction.
	/// </summary>
	public void Void(Guid voidedBy, string reason)
	{
		if (Status != ChargeStatus.Approved)
			throw new BusinessException(FinanceChargeErrorCodes.ChargeCannotBeVoided)
				.WithData("Status", Status.ToString());
		var normalizedReason = reason?.Trim();
		if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length < 10)
			throw new BusinessException(FinanceChargeErrorCodes.ChargeCannotBeVoided)
				.WithData("Message", "Void reason must be at least 10 characters");

		Status = ChargeStatus.Voided;
		this.SetProperty(VoidedByProperty, voidedBy);
		this.SetProperty(VoidedOnProperty, DateTimeOffset.UtcNow);
		this.SetProperty(VoidReasonProperty, normalizedReason);
	}

	public void SetIncomeCategory(Guid categoryId, string categoryName)
	{
		if (Status != ChargeStatus.Pending || categoryId == Guid.Empty)
            throw new BusinessException("Finance:Routing:MappingLocked");
        SetAccountingSnapshot(categoryId);
		IncomeCategoryName = categoryName;
	}

	public void SetAccountingSnapshot(Guid incomeCategoryId, Guid? incomeAccountHeadId = null)
	{
		this.SetProperty(IncomeCategoryIdProperty, incomeCategoryId);
		this.SetProperty(IncomeAccountHeadIdProperty, incomeAccountHeadId);
	}

	/// <summary>
	/// Validates if the charge can be deleted. Only allowed for PENDING charges.
	/// </summary>
	public void ValidateCanDelete()
	{
		if (Status != ChargeStatus.Pending)
		{
			throw new BusinessException(FinanceChargeErrorCodes.ChargeCannotBeDeleted)
				.WithData("Status", Status.ToString())
				.WithData("Message", "Only pending charges can be deleted");
		}
	}

	private void ValidateAmount()
	{
		if (Amount <= 0)
		{
			throw new BusinessException(FinanceChargeErrorCodes.InvalidChargeAmount)
				.WithData("Amount", Amount)
				.WithData("Message", "Charge amount must be greater than 0");
		}

		if (!FinanceChargeAmountRules.HasSupportedScale(Amount))
		{
			throw new BusinessException(FinanceChargeErrorCodes.InvalidChargeAmountScale)
				.WithData("Amount", Amount)
				.WithData("Message", "Charge amount must have at most two decimal places");
		}

		if (Amount > FinanceChargeAmountRules.MaximumAmount)
		{
			throw new BusinessException(FinanceChargeErrorCodes.ChargeAmountExceedsMaximum)
				.WithData("Amount", Amount)
				.WithData("MaximumAmount", FinanceChargeAmountRules.MaximumAmount)
				.WithData("Message", "Charge amount exceeds the supported maximum");
		}
	}

	private void ValidateDueDate()
	{
		var dueDateDate = DueDate.Date;
		var todayInDueDateOffset = DateTimeOffset.UtcNow
			.ToOffset(DueDate.Offset)
			.Date;

		if (dueDateDate < todayInDueDateOffset)
		{
			throw new BusinessException(FinanceChargeErrorCodes.InvalidDueDate)
				.WithData("DueDate", DueDate);
		}
	}
}
