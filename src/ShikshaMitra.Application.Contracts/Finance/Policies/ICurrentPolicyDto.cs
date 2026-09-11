using System;

namespace ShikshaMitra.Domain.Finance.Policies;

/// <summary>Current attachment identity, separate from the policy's editable master identity.</summary>
public interface ICurrentPolicyDto
{
	Guid Id { get; }
	Guid AcademicYearId { get; }
	int Version { get; }
	string Status { get; }
	Guid? CurrentVersionId { get; set; }
}
