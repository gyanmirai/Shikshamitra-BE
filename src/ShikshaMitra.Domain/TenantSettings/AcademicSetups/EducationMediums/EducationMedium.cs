using System.Diagnostics.CodeAnalysis;
using System;
using System.Collections.Generic;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.EducationMediums;

public class EducationMedium : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; }

	public string Name { get; private set; }

	// Foreign key for EducationBoard
	public Guid EducationBoardId { get; private set; }

	// Navigation property
	public EducationBoard EducationBoard { get; } = null!; // Populated by EF relationship materialization.
	public ICollection<EducationMediumStage> EducationMediumStages { get; } = null!; // Populated by EF relationship materialization.

	private EducationMedium()
	{
		// EF materializes these required columns after this ORM constructor.
		Name = null!;

		/* This constructor is for deserialization / ORM purpose */
	}

	internal EducationMedium(Guid id, Guid? tenantId, string name, Guid educationBoardId)
		: base(id)
	{
		TenantId = tenantId;  // Allow null for host-level data
		EducationBoardId = Check.NotNull(educationBoardId, nameof(educationBoardId));
		SetName(name);
	}

	[MemberNotNull(nameof(Name))]
	internal EducationMedium SetName(string name)
	{
		Name = Check.NotNullOrWhiteSpace(
			name,
			nameof(name),
			maxLength: EducationMediumConsts.MaxNameLength
		);
		return this;
	}
}
