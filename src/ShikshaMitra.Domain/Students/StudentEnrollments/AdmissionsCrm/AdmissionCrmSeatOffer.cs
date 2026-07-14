using System;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSeatOffer : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid SeatPoolId { get; private set; }
	public Guid? RegistrationApplicationId { get; private set; }
	public Guid? PlaybookId { get; private set; }
	public Guid? PreviousOfferId { get; private set; }
	public Guid? ReservationId { get; private set; }
	public string OfferNumber { get; private set; } = string.Empty;
	public int Version { get; private set; }
	public AdmissionCrmSeatOfferType OfferType { get; private set; }
	public AdmissionCrmSeatOfferStatus Status { get; private set; }
	public string TermsSnapshotJson { get; private set; } = "{}";
	public string Language { get; private set; } = "en";
	public DateTime ValidFrom { get; private set; }
	public DateTime ValidUntil { get; private set; }
	public Guid? SubmittedByUserId { get; private set; }
	public DateTime? SubmittedAt { get; private set; }
	public Guid? ApprovedByUserId { get; private set; }
	public DateTime? ApprovedAt { get; private set; }
	public DateTime? IssuedAt { get; private set; }
	public DateTime? OpenedAt { get; private set; }
	public DateTime? RespondedAt { get; private set; }
	public string? ResponseSource { get; private set; }
	public string? ResponseReason { get; private set; }
	public string ResponseSnapshotJson { get; private set; } = "{}";

	protected AdmissionCrmSeatOffer()
	{
	}

	public AdmissionCrmSeatOffer(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid leadId,
		Guid seatPoolId,
		Guid? registrationApplicationId,
		Guid? playbookId,
		Guid? previousOfferId,
		string offerNumber,
		int version,
		AdmissionCrmSeatOfferType offerType,
		string termsSnapshotJson,
		string language,
		DateTime validFrom,
		DateTime validUntil) : base(id)
	{
		var normalizedValidFrom = validFrom.ToUniversalTime();
		var normalizedValidUntil = validUntil.ToUniversalTime();
		if (version <= 0 || normalizedValidUntil <= normalizedValidFrom)
		{
			throw new BusinessException("AdmissionCrm:InvalidSeatOffer");
		}

		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		SeatPoolId = seatPoolId;
		RegistrationApplicationId = registrationApplicationId;
		PlaybookId = playbookId;
		PreviousOfferId = previousOfferId;
		OfferNumber = Check.NotNullOrWhiteSpace(offerNumber, nameof(offerNumber), AdmissionCrmConsts.MaxOfferNumberLength);
		Version = version;
		OfferType = offerType;
		TermsSnapshotJson = ValidateTerms(termsSnapshotJson);
		Language = Check.NotNullOrWhiteSpace(language, nameof(language), AdmissionCrmConsts.MaxLanguageCodeLength);
		ValidFrom = normalizedValidFrom;
		ValidUntil = normalizedValidUntil;
		Status = AdmissionCrmSeatOfferStatus.Draft;
	}

	public void SubmitForApproval(Guid userId, DateTime submittedAt)
	{
		EnsureStatus(AdmissionCrmSeatOfferStatus.Draft);
		SubmittedByUserId = userId;
		SubmittedAt = submittedAt.ToUniversalTime();
		Status = AdmissionCrmSeatOfferStatus.ApprovalPending;
	}

	public void Approve(Guid userId, DateTime approvedAt)
	{
		EnsureStatus(AdmissionCrmSeatOfferStatus.ApprovalPending);
		if (SubmittedByUserId == userId)
		{
			throw new BusinessException("AdmissionCrm:DistinctOfferApproverRequired");
		}
		ApprovedByUserId = userId;
		ApprovedAt = approvedAt.ToUniversalTime();
		Status = AdmissionCrmSeatOfferStatus.Approved;
	}

	public void Issue(Guid reservationId, DateTime issuedAt)
	{
		EnsureStatus(AdmissionCrmSeatOfferStatus.Approved);
		var normalizedIssuedAt = issuedAt.ToUniversalTime();
		if (normalizedIssuedAt < ValidFrom)
		{
			throw new BusinessException("AdmissionCrm:SeatOfferNotYetValid");
		}
		if (normalizedIssuedAt > ValidUntil)
		{
			throw new BusinessException("AdmissionCrm:SeatOfferExpiredBeforeIssue");
		}
		ReservationId = reservationId;
		IssuedAt = normalizedIssuedAt;
		Status = AdmissionCrmSeatOfferStatus.Issued;
	}

	public void Accept(string responseSource, DateTime respondedAt, string responseSnapshotJson = "{}")
	{
		EnsureIssuedAndValid(respondedAt);
		Status = AdmissionCrmSeatOfferStatus.Accepted;
		RespondedAt = respondedAt.ToUniversalTime();
		ResponseSource = Check.NotNullOrWhiteSpace(responseSource, nameof(responseSource), AdmissionCrmConsts.MaxNameLength);
		ResponseSnapshotJson = ValidateTerms(responseSnapshotJson);
	}

	public bool MarkOpened(DateTime openedAt)
	{
		if (Status == AdmissionCrmSeatOfferStatus.Opened) return false;
		EnsureStatus(AdmissionCrmSeatOfferStatus.Issued);
		OpenedAt = openedAt.ToUniversalTime();
		Status = AdmissionCrmSeatOfferStatus.Opened;
		return true;
	}

	public void Decline(string responseSource, string? reason, DateTime respondedAt, string responseSnapshotJson = "{}")
	{
		EnsureIssuedAndValid(respondedAt);
		Status = AdmissionCrmSeatOfferStatus.Declined;
		RespondedAt = respondedAt.ToUniversalTime();
		ResponseSource = Check.NotNullOrWhiteSpace(responseSource, nameof(responseSource), AdmissionCrmConsts.MaxNameLength);
		ResponseReason = Normalize(reason);
		ResponseSnapshotJson = ValidateTerms(responseSnapshotJson);
	}

	public void Expire(DateTime expiredAt)
	{
		EnsureIssuedOrOpened();
		var normalizedExpiredAt = expiredAt.ToUniversalTime();
		if (normalizedExpiredAt < ValidUntil)
		{
			throw new BusinessException("AdmissionCrm:SeatOfferNotExpired");
		}
		Status = AdmissionCrmSeatOfferStatus.Expired;
		RespondedAt = normalizedExpiredAt;
		ResponseReason = "Offer expired.";
	}

	public void Revoke(string reason, DateTime revokedAt)
	{
		if (Status is not (AdmissionCrmSeatOfferStatus.Approved or AdmissionCrmSeatOfferStatus.Issued or AdmissionCrmSeatOfferStatus.Opened))
		{
			throw new BusinessException("AdmissionCrm:SeatOfferNotRevocable");
		}
		Status = AdmissionCrmSeatOfferStatus.Revoked;
		RespondedAt = revokedAt.ToUniversalTime();
		ResponseReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
	}

	public void CancelAccepted(string reason, DateTime cancelledAt)
	{
		EnsureStatus(AdmissionCrmSeatOfferStatus.Accepted);
		Status = AdmissionCrmSeatOfferStatus.Cancelled;
		RespondedAt = cancelledAt.ToUniversalTime();
		ResponseReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
	}

	public void MarkSuperseded(string reason, DateTime supersededAt)
	{
		if (Status is AdmissionCrmSeatOfferStatus.Accepted or AdmissionCrmSeatOfferStatus.Declined or
			AdmissionCrmSeatOfferStatus.Expired or AdmissionCrmSeatOfferStatus.Revoked or
			AdmissionCrmSeatOfferStatus.Superseded or AdmissionCrmSeatOfferStatus.Cancelled)
		{
			throw new BusinessException("AdmissionCrm:SeatOfferNotSupersedable");
		}
		Status = AdmissionCrmSeatOfferStatus.Superseded;
		RespondedAt = supersededAt.ToUniversalTime();
		ResponseReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
	}

	private void EnsureIssuedAndValid(DateTime at)
	{
		EnsureIssuedOrOpened();
		if (at.ToUniversalTime() > ValidUntil)
		{
			throw new BusinessException("AdmissionCrm:SeatOfferExpired");
		}
	}

	private void EnsureIssuedOrOpened()
	{
		if (Status is not (AdmissionCrmSeatOfferStatus.Issued or AdmissionCrmSeatOfferStatus.Opened))
		{
			throw new BusinessException("AdmissionCrm:InvalidSeatOfferStatus")
				.WithData("Expected", "Issued or Opened")
				.WithData("Actual", Status);
		}
	}

	private void EnsureStatus(AdmissionCrmSeatOfferStatus expected)
	{
		if (Status != expected)
		{
			throw new BusinessException("AdmissionCrm:InvalidSeatOfferStatus")
				.WithData("Expected", expected)
				.WithData("Actual", Status);
		}
	}

	private static string ValidateTerms(string json)
	{
		var normalized = Check.NotNullOrWhiteSpace(json, nameof(json), AdmissionCrmConsts.MaxOfferTermsJsonLength);
		using var document = JsonDocument.Parse(normalized);
		if (document.RootElement.ValueKind != JsonValueKind.Object)
		{
			throw new BusinessException("AdmissionCrm:InvalidSeatOfferTerms");
		}
		return normalized;
	}

	private static string? Normalize(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : Check.Length(value.Trim(), nameof(value), AdmissionCrmConsts.MaxNotesLength);
}
