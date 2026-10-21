using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public class RoomFeature : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public string Name { get; private set; } = null!;
	public string NormalizedName { get; private set; } = null!;

	private RoomFeature() { }

	public RoomFeature(Guid id, Guid? tenantId, string name) : base(id)
	{
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), 100).Trim();
		NormalizedName = Name.ToUpperInvariant();
		TenantId = tenantId;
	}
}
