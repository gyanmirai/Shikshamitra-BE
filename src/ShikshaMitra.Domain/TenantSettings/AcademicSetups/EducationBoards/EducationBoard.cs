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
	public DateTime StartDate { get; private set; }
	public DateTime EndDate { get; private set; }

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
		TenantId = tenantId;  // Allow null for host-level data
		AcademicYearId = Check.NotNull(academicYearId, nameof(academicYearId));
		SetEducationBoardType(educationBoardType);
		StartDate = DateTime.UtcNow;
		EndDate = DateTime.UtcNow.AddYears(1);
	}

	internal EducationBoard(
		Guid id,
		Guid? tenantId,
		EducationBoardType educationBoardType,
		Guid academicYearId,
		DateTime startDate,
		DateTime endDate
	)
		: base(id)
	{
		TenantId = tenantId;  // Allow null for host-level data
		AcademicYearId = Check.NotNull(academicYearId, nameof(academicYearId));
		SetEducationBoardType(educationBoardType);
		SetDates(startDate, endDate);
	}

	internal EducationBoard SetEducationBoardType(EducationBoardType educationBoardType)
	{
		Type = educationBoardType;
		return this;
	}

	internal EducationBoard SetDates(DateTime startDate, DateTime endDate)
	{
		if (endDate <= startDate)
		{
			throw new ArgumentException("End date must be after start date.");
		}
		StartDate = startDate;
		EndDate = endDate;
		return this;
	}
}
