using System;
using System.Collections.Generic;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShikshaMitra.TenantSettings.EducationBoards;

public class EducationBoard : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; }
	public EducationBoardType Type { get; private set; }
	[NotMapped] public string? CustomName => this.GetProperty<string?>("CustomName");
	[NotMapped] public string Name => EducationBoardNames.DisplayName(Type);
	public DateTime StartDate { get; private set; }
	public DateTime EndDate { get; private set; }

	// Foreign key for AcademicYear
	public Guid AcademicYearId { get; }

	// Navigation properties
	public AcademicYear AcademicYear { get; } = null!; // Populated by EF relationship materialization.
	public ICollection<EducationMedium> EducationMediums { get; } = null!; // Populated by EF relationship materialization.

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
		if (!Enum.IsDefined(educationBoardType)) throw new BusinessException(EducationBoardNames.InvalidType, "Select a valid education board.");
		Type = educationBoardType;
		return this;
	}

	internal EducationBoard SetCustomName(string? customName)
	{
		var normalized = EducationBoardNames.NormalizeCustomName(Type, customName);
		if (normalized == null) this.RemoveProperty("CustomName");
		else this.SetProperty("CustomName", normalized);
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
