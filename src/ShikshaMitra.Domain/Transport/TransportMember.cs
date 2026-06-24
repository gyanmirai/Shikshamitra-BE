using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportMember : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public TransportMemberType MemberType { get; private set; }
	public Guid? StudentId { get; private set; }
	public Guid? EmployeeId { get; private set; }
	public TransportMemberStatus Status { get; private set; }
	public Guid? CurrentRouteId { get; private set; }
	public Guid? CurrentStopId { get; private set; }
	public DateTime? EffectiveFrom { get; private set; }
	public DateTime? EffectiveTo { get; private set; }
	public string DisplayName { get; private set; } = string.Empty;
	public string? MemberCode { get; private set; }
	public string? Role { get; private set; }
	public string? ClassName { get; private set; }
	public string? DivisionName { get; private set; }
	public string? PhoneNo { get; private set; }
	public string? PhotoUrl { get; private set; }
	public string? Remarks { get; private set; }

	private TransportMember()
	{
	}

	public TransportMember(
		Guid id,
		Guid? tenantId,
		Guid academicMediumId,
		TransportMemberType memberType,
		Guid? studentId,
		Guid? employeeId,
		string displayName,
		string? memberCode,
		string? role,
		string? className,
		string? divisionName,
		string? phoneNo,
		string? photoUrl,
		string? remarks) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		MemberType = memberType;
		StudentId = studentId;
		EmployeeId = employeeId;
		Status = TransportMemberStatus.Active;
		ValidateSource();
		UpdateSnapshot(displayName, memberCode, role, className, divisionName, phoneNo, photoUrl);
		Remarks = NormalizeNullable(remarks);
	}

	public Guid SourceId => MemberType == TransportMemberType.Student
		? StudentId!.Value
		: EmployeeId!.Value;

	public void UpdateSnapshot(
		string displayName,
		string? memberCode,
		string? role,
		string? className,
		string? divisionName,
		string? phoneNo,
		string? photoUrl)
	{
		DisplayName = Check.NotNullOrWhiteSpace(displayName, nameof(displayName)).Trim();
		MemberCode = NormalizeNullable(memberCode);
		Role = NormalizeNullable(role);
		ClassName = NormalizeNullable(className);
		DivisionName = NormalizeNullable(divisionName);
		PhoneNo = NormalizeNullable(phoneNo);
		PhotoUrl = NormalizeNullable(photoUrl);
	}

	public void Activate(Guid routeId, Guid stopId, DateTime effectiveFrom, string? remarks)
	{
		Status = TransportMemberStatus.Active;
		CurrentRouteId = routeId;
		CurrentStopId = stopId;
		EffectiveFrom = effectiveFrom;
		EffectiveTo = null;
		Remarks = NormalizeNullable(remarks) ?? Remarks;
	}

	public void Deactivate(DateTime effectiveTo, string? remarks)
	{
		Status = TransportMemberStatus.Inactive;
		CurrentRouteId = null;
		CurrentStopId = null;
		EffectiveTo = effectiveTo;
		Remarks = NormalizeNullable(remarks) ?? Remarks;
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
