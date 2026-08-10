using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRegistrationApplication : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid LeadId { get; private set; }
	public Guid RegistrationTemplateId { get; private set; }
	public int RegistrationTemplateVersion { get; private set; }
	public Guid? PlaybookId { get; private set; }
	public string FormNumber { get; private set; } = string.Empty;
	public AdmissionCrmRegistrationProvenance Provenance { get; private set; }
	public AdmissionCrmRegistrationDataEntryState DataEntryState { get; private set; }
	public string? PhysicalFormNumber { get; private set; }
	public DateTime? PhysicalFormReceivedDate { get; private set; }
	public Guid? PhysicalFormReceivedByUserId { get; private set; }
	public string? ScannedFormAttachmentReference { get; private set; }
	public AdmissionCrmRegistrationStatus Status { get; private set; }
	public string TemplateSnapshotJson { get; private set; } = "{}";
	public string PrefillJson { get; private set; } = "{}";
	public string ResponseJson { get; private set; } = "{}";
	public string CorrectionJson { get; private set; } = "[]";
	public decimal CompletionPercentage { get; private set; }
	public DateTime IssuedAt { get; private set; }
	public DateTime LinkExpiresAt { get; private set; }
	public DateTime? OpenedAt { get; private set; }
	public DateTime? LastSavedAt { get; private set; }
	public DateTime? SubmittedAt { get; private set; }
	public DateTime? VerifiedAt { get; private set; }
	public DateTime? RevokedAt { get; private set; }
	public string? LifecycleReason { get; private set; }
	public decimal FeeAmount { get; private set; }
	public AdmissionCrmRegistrationFeeStatus FeeStatus { get; private set; }
	public string? PaymentReference { get; private set; }

	protected AdmissionCrmRegistrationApplication()
	{
	}

	public AdmissionCrmRegistrationApplication(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid leadId,
		Guid registrationTemplateId,
		int registrationTemplateVersion,
		Guid? playbookId,
		string formNumber,
		string templateSnapshotJson,
		string prefillJson,
		DateTime issuedAt,
		DateTime linkExpiresAt,
		decimal feeAmount,
		AdmissionCrmRegistrationProvenance provenance = AdmissionCrmRegistrationProvenance.OnlineParent)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		LeadId = leadId;
		RegistrationTemplateId = registrationTemplateId;
		RegistrationTemplateVersion = registrationTemplateVersion;
		PlaybookId = playbookId;
		FormNumber = Check.NotNullOrWhiteSpace(formNumber, nameof(formNumber), AdmissionCrmConsts.MaxRegistrationFormNumberLength);
		TemplateSnapshotJson = Check.Length(templateSnapshotJson, nameof(templateSnapshotJson), AdmissionCrmConsts.MaxRegistrationTemplateJsonLength);
		PrefillJson = Check.Length(prefillJson, nameof(prefillJson), AdmissionCrmConsts.MaxRegistrationPrefillJsonLength);
		IssuedAt = issuedAt;
		LinkExpiresAt = linkExpiresAt;
		Provenance = provenance;
		DataEntryState = AdmissionCrmRegistrationDataEntryState.NotStarted;
		Status = AdmissionCrmRegistrationStatus.Issued;
		FeeAmount = feeAmount;
		FeeStatus = feeAmount > 0 ? AdmissionCrmRegistrationFeeStatus.Pending : AdmissionCrmRegistrationFeeStatus.NotRequired;
	}

	public void RecordPaperReceipt(
		string physicalFormNumber,
		DateTime receivedDate,
		Guid receivedByUserId,
		string? scannedFormAttachmentReference)
	{
		if (Provenance != AdmissionCrmRegistrationProvenance.StaffEnteredFromPaper)
		{
			throw new BusinessException("AdmissionCrm:PaperReceiptRequiresPaperRegistration");
		}

		PhysicalFormNumber = Check.NotNullOrWhiteSpace(
			physicalFormNumber,
			nameof(physicalFormNumber),
			AdmissionCrmConsts.MaxRegistrationFormNumberLength).Trim().ToUpperInvariant();
		PhysicalFormReceivedDate = receivedDate;
		PhysicalFormReceivedByUserId = receivedByUserId;
		ScannedFormAttachmentReference = string.IsNullOrWhiteSpace(scannedFormAttachmentReference)
			? null
			: Check.Length(scannedFormAttachmentReference.Trim(), nameof(scannedFormAttachmentReference), AdmissionCrmConsts.MaxDocumentBlobNameLength);
		DataEntryState = AdmissionCrmRegistrationDataEntryState.InProgress;
	}

	public void MarkOpened(DateTime openedAt)
	{
		EnsureAccessible(openedAt);
		OpenedAt ??= openedAt;
		if (Status == AdmissionCrmRegistrationStatus.Issued)
		{
			Status = AdmissionCrmRegistrationStatus.Opened;
		}
	}

	public void Save(string responseJson, decimal completionPercentage, DateTime savedAt)
	{
		EnsureAccessible(savedAt);
		if (Status is AdmissionCrmRegistrationStatus.Submitted or AdmissionCrmRegistrationStatus.Verified or AdmissionCrmRegistrationStatus.AdmissionReady)
		{
			throw new BusinessException("AdmissionCrm:RegistrationSubmissionIsReadOnly");
		}
		ResponseJson = Check.Length(responseJson, nameof(responseJson), AdmissionCrmConsts.MaxRegistrationResponseJsonLength);
		CompletionPercentage = Math.Clamp(completionPercentage, 0, 100);
		LastSavedAt = savedAt;
		OpenedAt ??= savedAt;
		DataEntryState = AdmissionCrmRegistrationDataEntryState.InProgress;
		Status = CompletionPercentage > 0
			? AdmissionCrmRegistrationStatus.PartiallyCompleted
			: AdmissionCrmRegistrationStatus.Started;
	}

	public void Submit(string responseJson, DateTime submittedAt, bool isResubmission)
	{
		EnsureAccessible(submittedAt);
		if (Status is AdmissionCrmRegistrationStatus.Verified or AdmissionCrmRegistrationStatus.AdmissionReady)
		{
			throw new BusinessException("AdmissionCrm:RegistrationSubmissionIsReadOnly");
		}
		ResponseJson = Check.Length(responseJson, nameof(responseJson), AdmissionCrmConsts.MaxRegistrationResponseJsonLength);
		CompletionPercentage = 100;
		LastSavedAt = submittedAt;
		SubmittedAt = submittedAt;
		CorrectionJson = "[]";
		DataEntryState = AdmissionCrmRegistrationDataEntryState.Submitted;
		Status = isResubmission
			? AdmissionCrmRegistrationStatus.Resubmitted
			: AdmissionCrmRegistrationStatus.Submitted;
	}

	public void EnsureResponseVersion(string expectedConcurrencyStamp)
	{
		if (string.IsNullOrWhiteSpace(expectedConcurrencyStamp))
		{
			throw new BusinessException("AdmissionCrm:RegistrationConcurrencyStampRequired");
		}
		if (!string.Equals(ConcurrencyStamp, expectedConcurrencyStamp, StringComparison.Ordinal))
		{
			throw new BusinessException("AdmissionCrm:RegistrationConcurrencyConflict");
		}
	}

	public void ReturnForCorrection(string correctionJson, string reason)
	{
		if (Status is not (AdmissionCrmRegistrationStatus.Submitted or AdmissionCrmRegistrationStatus.Resubmitted))
		{
			throw new BusinessException("AdmissionCrm:RegistrationCannotBeReturnedForCorrection");
		}
		CorrectionJson = Check.Length(correctionJson, nameof(correctionJson), AdmissionCrmConsts.MaxRegistrationCorrectionJsonLength);
		LifecycleReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		Status = AdmissionCrmRegistrationStatus.ReturnedForCorrection;
	}

	public void Verify(DateTime verifiedAt)
	{
		if (Status is not (AdmissionCrmRegistrationStatus.Submitted or AdmissionCrmRegistrationStatus.Resubmitted))
		{
			throw new BusinessException("AdmissionCrm:RegistrationMustBeSubmittedBeforeVerification");
		}
		VerifiedAt = verifiedAt;
		DataEntryState = AdmissionCrmRegistrationDataEntryState.Verified;
		Status = AdmissionCrmRegistrationStatus.Verified;
	}

	public void MarkAdmissionReady()
	{
		if (Status != AdmissionCrmRegistrationStatus.Verified)
		{
			throw new BusinessException("AdmissionCrm:RegistrationMustBeVerifiedBeforeAdmissionReadiness");
		}
		Status = AdmissionCrmRegistrationStatus.AdmissionReady;
	}

	public void Extend(DateTime newExpiry, string reason, DateTime now)
	{
		if (newExpiry <= now || newExpiry > now.AddDays(365))
		{
			throw new BusinessException("AdmissionCrm:InvalidRegistrationLinkExpiry");
		}
		if (Status == AdmissionCrmRegistrationStatus.Revoked)
		{
			throw new BusinessException("AdmissionCrm:RevokedRegistrationCannotBeExtended");
		}
		LinkExpiresAt = newExpiry;
		LifecycleReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		if (Status == AdmissionCrmRegistrationStatus.Expired)
		{
			Status = AdmissionCrmRegistrationStatus.PartiallyCompleted;
		}
	}

	public void Revoke(string reason, DateTime revokedAt)
	{
		LifecycleReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		RevokedAt = revokedAt;
		Status = AdmissionCrmRegistrationStatus.Revoked;
	}

	public void Withdraw(string reason)
	{
		LifecycleReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		Status = AdmissionCrmRegistrationStatus.Withdrawn;
	}

	public void Reissue(DateTime newExpiry, string reason, DateTime reissuedAt)
	{
		if (Status != AdmissionCrmRegistrationStatus.Revoked && LinkExpiresAt > reissuedAt)
		{
			throw new BusinessException("AdmissionCrm:RegistrationReissueNotRequired");
		}
		if (newExpiry <= reissuedAt || newExpiry > reissuedAt.AddDays(365))
		{
			throw new BusinessException("AdmissionCrm:InvalidRegistrationLinkExpiry");
		}
		LinkExpiresAt = newExpiry;
		RevokedAt = null;
		LifecycleReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		Status = CompletionPercentage > 0
			? AdmissionCrmRegistrationStatus.PartiallyCompleted
			: AdmissionCrmRegistrationStatus.Issued;
	}

	public void RecordPaymentReference(string paymentReference)
	{
		PaymentReference = Check.NotNullOrWhiteSpace(paymentReference, nameof(paymentReference), AdmissionCrmConsts.MaxPaymentReferenceLength);
		FeeStatus = AdmissionCrmRegistrationFeeStatus.ReferenceRecorded;
	}

	public void WaiveFee(string reason)
	{
		LifecycleReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), AdmissionCrmConsts.MaxNotesLength);
		FeeStatus = AdmissionCrmRegistrationFeeStatus.Waived;
	}

	private void EnsureAccessible(DateTime now)
	{
		if (Status == AdmissionCrmRegistrationStatus.Revoked)
		{
			throw new BusinessException("AdmissionCrm:RegistrationLinkRevoked");
		}
		if (LinkExpiresAt <= now)
		{
			Status = AdmissionCrmRegistrationStatus.Expired;
			throw new BusinessException("AdmissionCrm:RegistrationLinkExpired");
		}
	}

}
