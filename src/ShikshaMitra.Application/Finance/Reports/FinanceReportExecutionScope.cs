using System;
using System.Collections.Generic;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.Finance.Reports;

public class FinanceReportExecutionScope
{
    public Guid? TenantId { get; set; }
    public UserScopeContext UserScope { get; set; } = new();
    public List<Guid> AllowedStandardIds { get; set; } = [];
}
