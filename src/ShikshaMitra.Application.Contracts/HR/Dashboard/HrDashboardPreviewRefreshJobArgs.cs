using System;

namespace ShikshaMitra.HR.Dashboard;

[Serializable]
public class HrDashboardPreviewRefreshJobArgs
{
	public Guid? TenantId { get; set; }
	public Guid PreviewId { get; set; }
	public string DashboardKey { get; set; } = string.Empty;
	public string ScopeKey { get; set; } = HrDashboardScopeKeys.HrWide;
	public Guid? RequestedByUserId { get; set; }
}
