using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Charges;

/// <summary>
/// Represents a charge applied to a student
/// </summary>
public class Charge : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid ChargeCategoryId { get; private set; }
	public Guid StudentId { get; private set; }
	public Guid BoardId { get; private set; }
	public Guid EducationMediumId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid DivisionId { get; private set; }
	public string PurposeDescription { get; private set; }
	public decimal Amount { get; private set; }
	public DateTime DueDate { get; private set; }
	public DateTime AppliedOn { get; private set; }
	public ChargePriority Priority { get; private set; }
	public ChargeStatus Status { get; private set; }
	public string? AdditionalNotes { get; private set; }
	public Guid RequestorId { get; private set; }
	public Guid? ApprovedBy { get; private set; }
	public DateTime? ApprovedOn { get; private set; }
	public string? ApprovalNotes { get; private set; }

	private Charge()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public Charge(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid chargeCategoryId,
		Guid studentId,
		Guid boardId,
		Guid educationMediumId,
		Guid standardId,
		Guid divisionId,
		string purposeDescription,
		decimal amount,
		DateTime dueDate,
		ChargePriority priority,
		ChargeStatus initialStatus,
		Guid requestorId,
		string? additionalNotes = null)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = Check.NotNull(academicYearId, nameof(academicYearId));
		ChargeCategoryId = Check.NotNull(chargeCategoryId, nameof(chargeCategoryId));
		StudentId = Check.NotNull(studentId, nameof(studentId));
		BoardId = Check.NotNull(boardId, nameof(boardId));
		EducationMediumId = Check.NotNull(educationMediumId, nameof(educationMediumId));
		StandardId = Check.NotNull(standardId, nameof(standardId));
		DivisionId = Check.NotNull(divisionId, nameof(divisionId));
		PurposeDescription = Check.NotNullOrWhiteSpace(purposeDescription, nameof(purposeDescription));
		Amount = amount;
		DueDate = dueDate;
		AppliedOn = DateTime.UtcNow;
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
		DateTime? dueDate = null,
		ChargePriority? priority = null,
		string? additionalNotes = null)
	{
		if (Status != ChargeStatus.Pending)
		{
			throw new BusinessException("ShikshaMitra:ChargeNotPending")
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
	/// </summary>
	public void Approve(Guid approvedBy, string? approvalNotes = null)
	{
		if (Status != ChargeStatus.Pending)
		{
			throw new BusinessException("ShikshaMitra:ChargeNotPending")
				.WithData("Status", Status.ToString())
				.WithData("Message", "Only pending charges can be approved");
		}

		Status = ChargeStatus.Approved;
		ApprovedBy = approvedBy;
		ApprovedOn = DateTime.UtcNow;
		ApprovalNotes = approvalNotes;
	}

	/// <summary>
	/// Rejects the charge with a mandatory reason. Only allowed for PENDING charges.
	/// </summary>
	public void Reject(Guid rejectedBy, string rejectionReason)
	{
		if (Status != ChargeStatus.Pending)
		{
			throw new BusinessException("ShikshaMitra:ChargeNotPending")
				.WithData("Status", Status.ToString())
				.WithData("Message", "Only pending charges can be rejected");
		}

		if (string.IsNullOrWhiteSpace(rejectionReason) || rejectionReason.Length < 10)
		{
			throw new BusinessException("ShikshaMitra:InvalidRejectionReason")
				.WithData("Message", "Rejection reason must be at least 10 characters");
		}

		Status = ChargeStatus.Rejected;
		ApprovedBy = rejectedBy;
		ApprovedOn = DateTime.UtcNow;
		ApprovalNotes = rejectionReason;
	}

	/// <summary>
	/// Validates if the charge can be deleted. Only allowed for PENDING charges.
	/// </summary>
	public void ValidateCanDelete()
	{
		if (Status != ChargeStatus.Pending)
		{
			throw new BusinessException("ShikshaMitra:ChargeCannotBeDeleted")
				.WithData("Status", Status.ToString())
				.WithData("Message", "Only pending charges can be deleted");
		}
	}

	private void ValidateAmount()
	{
		if (Amount <= 0)
		{
			throw new BusinessException("ShikshaMitra:InvalidChargeAmount")
				.WithData("Amount", Amount)
				.WithData("Message", "Charge amount must be greater than 0");
		}
	}

	private void ValidateDueDate()
	{
		if (DueDate.Date < DateTime.UtcNow.Date)
		{
			throw new BusinessException("ShikshaMitra:InvalidDueDate")
				.WithData("DueDate", DueDate)
				.WithData("Message", "Due date must be in the future");
		}
	}
}
