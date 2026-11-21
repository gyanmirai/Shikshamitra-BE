using System;
using System.Collections.Generic;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Policies;

public class LibraryPolicyScopeOptionDto
{
	public Guid Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public Guid ParentId { get; set; }
	public string ParentName { get; set; } = string.Empty;
}

public class LibraryPolicyOptionsDto
{
	public List<NamedIdDto> BookCategories { get; set; } = new();
	public List<NamedIdDto> Sections { get; set; } = new();
	public List<LibraryPolicyScopeOptionDto> Stages { get; set; } = new();
	public List<LibraryPolicyScopeOptionDto> Standards { get; set; } = new();
	public bool CanManageGlobalPolicies { get; set; }
}
