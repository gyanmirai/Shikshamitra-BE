using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportMemberRouteAssignment : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public Guid MemberId { get; private set; }
	public Guid RouteId { get; private set; }
	public Guid StopId { get; private set; }
	public Guid? ApplicationId { get; private set; }
	public DateTime EffectiveFrom { get; private set; }
	public DateTime? EffectiveTo { get; private set; }
	public decimal FeeAmountSnapshot { get; private set; }
	public string? Remarks { get; private set; }

	private TransportMemberRouteAssignment()
	{
	}

	public TransportMemberRouteAssignment(
		Guid id,
		Guid? tenantId,
		Guid academicMediumId,
		Guid memberId,
		Guid routeId,
		Guid stopId,
		Guid? applicationId,
		DateTime effectiveFrom,
		decimal feeAmountSnapshot,
		string? remarks) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		MemberId = memberId;
		RouteId = routeId;
		StopId = stopId;
		ApplicationId = applicationId;
		EffectiveFrom = effectiveFrom;
		FeeAmountSnapshot = feeAmountSnapshot;
		Remarks = NormalizeNullable(remarks);
	}

	public bool IsCurrent => EffectiveTo == null;

	public void Close(DateTime effectiveTo, string? remarks)
	{
		EffectiveTo = effectiveTo;
		Remarks = NormalizeNullable(remarks) ?? Remarks;
	}

	private static string? NormalizeNullable(string? value) =>
		string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
