using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Dashboard;

public class DashboardLayout : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid UserId { get; set; }
	public string Layouts { get; set; } // Will store JSON layout configuration
	public string VisibleWidgets { get; set; } // Will store JSON of visible widget settings
	public Guid? TenantId { get; set; } 

	protected DashboardLayout() { }

	public DashboardLayout(Guid id, Guid userId, string layouts, string visibleWidgets, Guid? tenantId) : base(id)
	{
		TenantId = tenantId;
		UserId = userId;
		Layouts = layouts;
		VisibleWidgets = visibleWidgets;
	}
}
