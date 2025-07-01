using System;
using System.Collections.Generic;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.EducationBoards;

public class EducationBoard : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; }
	public EducationBoardType Type { get; private set; }

	// Foreign key for AcademicYear
	public Guid AcademicYearId { get; }

	// Navigation properties
	public AcademicYear AcademicYear { get; }
	public ICollection<EducationMedium> EducationMediums { get; }

	private EducationBoard()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	internal EducationBoard(
		Guid id,
		Guid? tenantId,
		EducationBoardType educationBoardType,
		Guid academicYearId
	)
		: base(id)
	{
		TenantId = Check.NotNull(tenantId, nameof(tenantId));
		AcademicYearId = Check.NotNull(academicYearId, nameof(academicYearId));
		SetEducationBoardType(educationBoardType);
	}

	internal EducationBoard SetEducationBoardType(EducationBoardType educationBoardType)
	{
		Type = educationBoardType;
		return this;
	}
}
