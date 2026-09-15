using System;
using System.Collections.Generic;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.Library.Reports;

public class LibraryReportExecutionScope
{
	public Guid? TenantId { get; set; }
	public UserScopeContext UserScope { get; set; } = new();
	public List<Guid> AllowedAcademicYearIds { get; set; } = [];
}
