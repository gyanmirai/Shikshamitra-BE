using System;
using System.Collections.Generic;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Standards;

public sealed class StandardLookupDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = "";
	public List<StandardDivisionLookupDto> Divisions { get; set; } = [];
}

public sealed class StandardDivisionLookupDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = "";
}
