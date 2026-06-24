using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportRoute : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public string RouteName { get; private set; } = string.Empty;
	public string RouteCode { get; private set; } = string.Empty;
	public string RouteType { get; private set; } = string.Empty;
	public decimal Distance { get; private set; }
	public string? Duration { get; private set; }
	public string? Description { get; private set; }
	public string StartPoint { get; private set; } = string.Empty;
	public string EndPoint { get; private set; } = string.Empty;
	public DateTimeOffset? PickupStartTime { get; private set; }
	public DateTimeOffset? PickupEndTime { get; private set; }
	public DateTimeOffset? DropoffStartTime { get; private set; }
	public DateTimeOffset? DropoffEndTime { get; private set; }
	public TransportRecordStatus Status { get; private set; }

	private TransportRoute()
	{
	}

	public TransportRoute(
		Guid id,
		Guid? tenantId,
		Guid academicMediumId,
		string routeName,
		string routeCode,
		string routeType,
		decimal distance,
		string? duration,
		string? description,
		string startPoint,
		string endPoint,
		DateTimeOffset? pickupStartTime,
		DateTimeOffset? pickupEndTime,
		DateTimeOffset? dropoffStartTime,
		DateTimeOffset? dropoffEndTime) : base(id)
	{
		TenantId = tenantId;
		AcademicMediumId = academicMediumId;
		Status = TransportRecordStatus.Active;
		UpdateDetails(
			routeName,
			routeCode,
			routeType,
			distance,
			duration,
			description,
			startPoint,
			endPoint,
			pickupStartTime,
			pickupEndTime,
			dropoffStartTime,
			dropoffEndTime);
	}

	public void UpdateDetails(
		string routeName,
		string routeCode,
		string routeType,
		decimal distance,
		string? duration,
		string? description,
		string startPoint,
		string endPoint,
		DateTimeOffset? pickupStartTime,
		DateTimeOffset? pickupEndTime,
		DateTimeOffset? dropoffStartTime,
		DateTimeOffset? dropoffEndTime)
	{
		RouteName = routeName.Trim();
		RouteCode = routeCode.Trim().ToUpperInvariant();
		RouteType = routeType.Trim();
		Distance = distance;
		Duration = NormalizeNullable(duration);
		Description = NormalizeNullable(description);
		StartPoint = startPoint.Trim();
		EndPoint = endPoint.Trim();
		PickupStartTime = pickupStartTime;
		PickupEndTime = pickupEndTime;
		DropoffStartTime = dropoffStartTime;
		DropoffEndTime = dropoffEndTime;
	}

	public void ChangeStatus(TransportRecordStatus status)
	{
		Status = status;
	}

	private static string? NormalizeNullable(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}
}
