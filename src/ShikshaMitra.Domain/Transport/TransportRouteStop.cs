using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Transport;

public class TransportRouteStop : FullAuditedEntity<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid RouteId { get; private set; }
	public Guid StopId { get; private set; }
	public int StopSequence { get; private set; }
	public DateTimeOffset? ExpectedArrivalTime { get; private set; }
	public DateTimeOffset? ExpectedDepartureTime { get; private set; }
	public decimal? DistanceFromPrevious { get; private set; }

	private TransportRouteStop()
	{
	}

	public TransportRouteStop(
		Guid id,
		Guid? tenantId,
		Guid routeId,
		Guid stopId,
		int stopSequence,
		DateTimeOffset? expectedArrivalTime,
		DateTimeOffset? expectedDepartureTime,
		decimal? distanceFromPrevious) : base(id)
	{
		TenantId = tenantId;
		RouteId = routeId;
		StopId = stopId;
		StopSequence = stopSequence;
		ExpectedArrivalTime = expectedArrivalTime;
		ExpectedDepartureTime = expectedDepartureTime;
		DistanceFromPrevious = distanceFromPrevious;
	}
}
