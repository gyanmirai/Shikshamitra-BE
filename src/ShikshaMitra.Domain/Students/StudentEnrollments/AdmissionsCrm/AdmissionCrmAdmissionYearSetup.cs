using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAdmissionYearSetup : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public int Version { get; private set; }
	public AdmissionCrmAdmissionYearSetupStatus Status { get; private set; }
	public DateTime InquiryOpenDate { get; private set; }
	public DateTime InquiryCloseDate { get; private set; }
	public DateTime RegistrationCloseDate { get; private set; }
	public DateTime ActivationTargetDate { get; private set; }
	public bool EntranceExamRequired { get; private set; }
	public bool FinanceVerificationRequired { get; private set; }
	public AdmissionCrmPlacementTiming PlacementTiming { get; private set; }
	public Guid? DefaultOwnerUserId { get; private set; }
	public string TargetStandardIdsJson { get; private set; } = "[]";
	public string RouteTypesJson { get; private set; } = "[]";
	public string StepOwnersJson { get; private set; } = "{}";
	public string? PublishedSnapshotJson { get; private set; }
	public DateTime? PublishedAt { get; private set; }
	public Guid? PublishedByUserId { get; private set; }
	public DateTime? ClosedAt { get; private set; }
	public Guid? ClosedByUserId { get; private set; }
	public string? CloseReason { get; private set; }

	protected AdmissionCrmAdmissionYearSetup()
	{
	}

	public AdmissionCrmAdmissionYearSetup(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		int version,
		DateTime inquiryOpenDate,
		DateTime inquiryCloseDate,
		DateTime registrationCloseDate,
		DateTime activationTargetDate,
		bool entranceExamRequired,
		bool financeVerificationRequired,
		AdmissionCrmPlacementTiming placementTiming,
		Guid? defaultOwnerUserId,
		string targetStandardIdsJson,
		string routeTypesJson,
		string stepOwnersJson)
		: base(id)
	{
		if (academicYearId == Guid.Empty)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearAcademicYearRequired");
		}

		if (version <= 0)
		{
			throw new BusinessException("AdmissionCrm:InvalidAdmissionYearSetupVersion")
				.WithData("Version", version);
		}

		TenantId = tenantId;
		AcademicYearId = academicYearId;
		Version = version;
		Status = AdmissionCrmAdmissionYearSetupStatus.Draft;
		Update(
			inquiryOpenDate,
			inquiryCloseDate,
			registrationCloseDate,
			activationTargetDate,
			entranceExamRequired,
			financeVerificationRequired,
			placementTiming,
			defaultOwnerUserId,
			targetStandardIdsJson,
			routeTypesJson,
			stepOwnersJson);
	}

	public void Update(
		DateTime inquiryOpenDate,
		DateTime inquiryCloseDate,
		DateTime registrationCloseDate,
		DateTime activationTargetDate,
		bool entranceExamRequired,
		bool financeVerificationRequired,
		AdmissionCrmPlacementTiming placementTiming,
		Guid? defaultOwnerUserId,
		string targetStandardIdsJson,
		string routeTypesJson,
		string stepOwnersJson)
	{
		EnsureDraft();
		ValidateDates(
			inquiryOpenDate,
			inquiryCloseDate,
			registrationCloseDate,
			activationTargetDate);
		if (!Enum.IsDefined(placementTiming))
		{
			throw new BusinessException("AdmissionCrm:InvalidPlacementTiming")
				.WithData("PlacementTiming", placementTiming);
		}

		InquiryOpenDate = inquiryOpenDate.Date;
		InquiryCloseDate = inquiryCloseDate.Date;
		RegistrationCloseDate = registrationCloseDate.Date;
		ActivationTargetDate = activationTargetDate.Date;
		EntranceExamRequired = entranceExamRequired;
		FinanceVerificationRequired = financeVerificationRequired;
		PlacementTiming = placementTiming;
		DefaultOwnerUserId = defaultOwnerUserId;
		TargetStandardIdsJson = NormalizeJson(
			targetStandardIdsJson,
			nameof(targetStandardIdsJson));
		RouteTypesJson = NormalizeJson(routeTypesJson, nameof(routeTypesJson));
		StepOwnersJson = NormalizeJson(stepOwnersJson, nameof(stepOwnersJson));
	}

	public void Publish(string snapshotJson, Guid userId, DateTime publishedAt)
	{
		EnsureDraft();
		PublishedSnapshotJson = Check.NotNullOrWhiteSpace(
			snapshotJson,
			nameof(snapshotJson),
			AdmissionCrmConsts.MaxAdmissionYearSetupSnapshotJsonLength);
		PublishedByUserId = userId;
		PublishedAt = publishedAt.ToUniversalTime();
		Status = AdmissionCrmAdmissionYearSetupStatus.Published;
	}

	public void Supersede()
	{
		if (Status != AdmissionCrmAdmissionYearSetupStatus.Published)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearSetupNotPublished")
				.WithData("SetupId", Id);
		}

		Status = AdmissionCrmAdmissionYearSetupStatus.Superseded;
	}

	public void Close(string reason, Guid userId, DateTime closedAt)
	{
		if (Status != AdmissionCrmAdmissionYearSetupStatus.Published)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearSetupNotPublished")
				.WithData("SetupId", Id);
		}

		CloseReason = Check.NotNullOrWhiteSpace(
			reason,
			nameof(reason),
			AdmissionCrmConsts.MaxNotesLength);
		ClosedByUserId = userId;
		ClosedAt = closedAt.ToUniversalTime();
		Status = AdmissionCrmAdmissionYearSetupStatus.Closed;
	}

	public void EnsureInquiryCaptureAllowed(DateTime now)
	{
		if (Status != AdmissionCrmAdmissionYearSetupStatus.Published)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearNotOpen")
				.WithData("AcademicYearId", AcademicYearId)
				.WithData("Status", Status);
		}

		var date = now.Date;
		if (date < InquiryOpenDate || date > InquiryCloseDate)
		{
			throw new BusinessException("AdmissionCrm:InquiryWindowClosed")
				.WithData("AcademicYearId", AcademicYearId)
				.WithData("InquiryOpenDate", InquiryOpenDate)
				.WithData("InquiryCloseDate", InquiryCloseDate);
		}
	}

	private void EnsureDraft()
	{
		if (Status != AdmissionCrmAdmissionYearSetupStatus.Draft)
		{
			throw new BusinessException("AdmissionCrm:AdmissionYearSetupNotEditable")
				.WithData("SetupId", Id)
				.WithData("Status", Status);
		}
	}

	private static void ValidateDates(
		DateTime inquiryOpenDate,
		DateTime inquiryCloseDate,
		DateTime registrationCloseDate,
		DateTime activationTargetDate)
	{
		if (inquiryOpenDate.Date > inquiryCloseDate.Date ||
			inquiryCloseDate.Date > registrationCloseDate.Date ||
			registrationCloseDate.Date > activationTargetDate.Date)
		{
			throw new BusinessException("AdmissionCrm:InvalidAdmissionYearDates")
				.WithData("InquiryOpenDate", inquiryOpenDate)
				.WithData("InquiryCloseDate", inquiryCloseDate)
				.WithData("RegistrationCloseDate", registrationCloseDate)
				.WithData("ActivationTargetDate", activationTargetDate);
		}
	}

	private static string NormalizeJson(string value, string name)
	{
		return Check.NotNullOrWhiteSpace(
			value,
			name,
			AdmissionCrmConsts.MaxAdmissionYearSetupSnapshotJsonLength);
	}
}
