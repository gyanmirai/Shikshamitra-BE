using ShikshaMitra.Finance.Charges;
using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.Charges;

/// <summary>
/// Aggregate root for a batch charge request applied to one or more students.
/// </summary>
public class ChargeRequest : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public const int MaxChargeNameLength = 100;

	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string ChargeName { get; private set; } = string.Empty;
	public Guid ChargeCategoryId { get; private set; }
	public string PurposeDescription { get; private set; } = string.Empty;
	public decimal Amount { get; private set; }
	public DateTime DueDate { get; private set; }
	public ChargePriority Priority { get; private set; }
	public Guid BoardId { get; private set; }
	public Guid EducationMediumId { get; private set; }
	public string? AdditionalNotes { get; private set; }
	public Guid RequestorId { get; private set; }
	public DateTime AppliedOn { get; private set; }

	/// <summary>JSON snapshot of <see cref="ChargeAudience"/> for audit and edit-form rehydration.</summary>
	public string AudienceSnapshot { get; private set; } = string.Empty;

	private ChargeRequest()
	{
	}

	public ChargeRequest(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string chargeName,
		Guid chargeCategoryId,
		string purposeDescription,
		decimal amount,
		DateTime dueDate,
		ChargePriority priority,
		Guid boardId,
		Guid educationMediumId,
		Guid requestorId,
		string audienceSnapshot,
		string? additionalNotes = null)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = Check.NotNull(academicYearId, nameof(academicYearId));
		ChargeName = Check.NotNullOrWhiteSpace(chargeName, nameof(chargeName));
		if (ChargeName.Length > MaxChargeNameLength)
		{
			throw new BusinessException(FinanceChargeErrorCodes.InvalidChargeName)
				.WithData("Message", $"Charge name cannot exceed {MaxChargeNameLength} characters");
		}

		ChargeCategoryId = Check.NotNull(chargeCategoryId, nameof(chargeCategoryId));
		PurposeDescription = Check.NotNullOrWhiteSpace(purposeDescription, nameof(purposeDescription));
		Amount = amount;
		DueDate = dueDate;
		Priority = priority;
		BoardId = Check.NotNull(boardId, nameof(boardId));
		EducationMediumId = Check.NotNull(educationMediumId, nameof(educationMediumId));
		RequestorId = Check.NotNull(requestorId, nameof(requestorId));
		AudienceSnapshot = Check.NotNullOrWhiteSpace(audienceSnapshot, nameof(audienceSnapshot));
		AdditionalNotes = additionalNotes;
		AppliedOn = DateTime.UtcNow;

		ValidateAmount();
		ValidateDueDate();
	}

	public void Update(
		string? chargeName = null,
		Guid? chargeCategoryId = null,
		string? purposeDescription = null,
		decimal? amount = null,
		DateTime? dueDate = null,
		ChargePriority? priority = null,
		string? additionalNotes = null,
		string? audienceSnapshot = null)
	{
		if (!string.IsNullOrWhiteSpace(chargeName))
		{
			if (chargeName.Length > MaxChargeNameLength)
			{
				throw new BusinessException(FinanceChargeErrorCodes.InvalidChargeName)
					.WithData("Message", $"Charge name cannot exceed {MaxChargeNameLength} characters");
			}

			ChargeName = chargeName;
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

		if (!string.IsNullOrWhiteSpace(audienceSnapshot))
		{
			AudienceSnapshot = audienceSnapshot;
		}
	}

	private void ValidateAmount()
	{
		if (Amount <= 0)
		{
			throw new BusinessException(FinanceChargeErrorCodes.InvalidChargeAmount)
				.WithData("Amount", Amount);
		}
	}

	private void ValidateDueDate()
	{
		if (DueDate.Date < DateTime.UtcNow.Date)
		{
			throw new BusinessException(FinanceChargeErrorCodes.InvalidDueDate)
				.WithData("DueDate", DueDate);
		}
	}
}
