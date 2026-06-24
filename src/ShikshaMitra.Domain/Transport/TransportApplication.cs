using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportApplication : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public TransportApplicationType ApplicationType { get; private set; }
	public TransportApplicationStatus Status { get; private set; }
	public TransportMemberType MemberType { get; private set; }
	public Guid? MemberId { get; private set; }
	public Guid? StudentId { get; private set; }
	public Guid? EmployeeId { get; private set; }
	public Guid? RequestedStopId { get; private set; }
	public Guid? AssignedRouteId { get; private set; }
	public Guid? AssignedStopId { get; private set; }
	public string? PickupType { get; private set; }
	public string? Landmark { get; private set; }
	public string? AreaLocality { get; private set; }
	public string? AddressLine1 { get; private set; }
	public string? Description { get; private set; }
	public decimal? EstimatedFeeAmount { get; private set; }
	public string ApplicantName { get; private set; } = string.Empty;
	public string? ApplicantCode { get; private set; }
	public string? ApplicantRole { get; private set; }
	public string? ApplicantClassName { get; private set; }
	public string? ApplicantDivisionName { get; private set; }
	public string? ApplicantPhoneNo { get; private set; }
	public string? ApplicantPhotoUrl { get; private set; }
	public string? StatusRemarks { get; private set; }
	public DateTime? ReviewedAt { get; private set; }
	public Guid? ReviewedById { get; private set; }

	private TransportApplication()
	{
	}

	public TransportApplication(
		Guid id,
		Guid? tenantId,
		Guid academicMediumId,
		TransportApplicationType applicationType,
		TransportMemberType memberType,
		Guid? memberId,
		Guid? studentId,
		Guid? employeeId,
		Guid? requestedStopId,
		string? pickupType,
		string? landmark,
		string? areaLocality,
		string? addressLine1,
		string? description,
		string applicantName,
		string? applicantCode,
		string? applicantRole,
		string? applicantClassName,
		string? applicantDivisionName,
		string? applicantPhoneNo,
		string? applicantPhotoUrl) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		ApplicationType = applicationType;
		MemberType = memberType;
		MemberId = memberId;
		StudentId = studentId;
		EmployeeId = employeeId;
		Status = TransportApplicationStatus.Pending;
		ValidateSource();
		UpdateRequest(requestedStopId, pickupType, landmark, areaLocality, addressLine1, description);
		UpdateApplicantSnapshot(
			applicantName,
			applicantCode,
			applicantRole,
			applicantClassName,
			applicantDivisionName,
			applicantPhoneNo,
			applicantPhotoUrl);
	}

	public Guid SourceId => MemberType == TransportMemberType.Student
		? StudentId!.Value
		: EmployeeId!.Value;

	public void UpdateRequest(
		Guid? requestedStopId,
		string? pickupType,
		string? landmark,
		string? areaLocality,
		string? addressLine1,
		string? description)
	{
		RequestedStopId = requestedStopId;
		PickupType = NormalizeNullable(pickupType);
		Landmark = NormalizeNullable(landmark);
		AreaLocality = NormalizeNullable(areaLocality);
		AddressLine1 = NormalizeNullable(addressLine1);
		Description = NormalizeNullable(description);
	}

	public void UpdateApplicantSnapshot(
		string applicantName,
		string? applicantCode,
		string? applicantRole,
		string? applicantClassName,
		string? applicantDivisionName,
		string? applicantPhoneNo,
		string? applicantPhotoUrl)
	{
		ApplicantName = Check.NotNullOrWhiteSpace(applicantName, nameof(applicantName)).Trim();
		ApplicantCode = NormalizeNullable(applicantCode);
		ApplicantRole = NormalizeNullable(applicantRole);
		ApplicantClassName = NormalizeNullable(applicantClassName);
		ApplicantDivisionName = NormalizeNullable(applicantDivisionName);
		ApplicantPhoneNo = NormalizeNullable(applicantPhoneNo);
		ApplicantPhotoUrl = NormalizeNullable(applicantPhotoUrl);
	}

	public void AssignRouteStop(Guid routeId, Guid stopId, decimal feeAmount)
	{
		AssignedRouteId = routeId;
		AssignedStopId = stopId;
		EstimatedFeeAmount = feeAmount;
	}

	public void ChangeStatus(TransportApplicationStatus status, Guid? reviewerId, string? remarks)
	{
		Status = status;
		StatusRemarks = NormalizeNullable(remarks);
		if (status != TransportApplicationStatus.Pending)
		{
			ReviewedAt = DateTime.UtcNow;
			ReviewedById = reviewerId;
		}
	}

	public void LinkMember(Guid memberId)
	{
		MemberId = memberId;
	}

	private void ValidateSource()
	{
		if (MemberType == TransportMemberType.Student)
		{
			if (!StudentId.HasValue || EmployeeId.HasValue)
			{
				throw new BusinessException(TransportErrorCodes.MemberSourceInvalid);
			}

			return;
		}

		if (!EmployeeId.HasValue || StudentId.HasValue)
		{
			throw new BusinessException(TransportErrorCodes.MemberSourceInvalid);
		}
	}

	private static string? NormalizeNullable(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
