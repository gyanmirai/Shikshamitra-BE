using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Setting;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures;

public class FeeStructureMetadata : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid? AcademicYearId { get; private set; }
	public Guid? AcademicBoardId { get; private set; }
	public Guid? AcademicMediumId { get; private set; }
	public Guid? ReceiptBookId { get; private set; }

	public string? Name { get; private set; }

	// Navigation Properties
	public AcademicYear AcademicYear { get; private set; }
	public EducationBoard AcademicBoard { get; private set; }
	public EducationMedium AcademicMedium { get; private set; }
	public ReceiptBook ReceiptBook { get; private set; }

	protected FeeStructureMetadata()
	{
	}

	public FeeStructureMetadata(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid boardId,
		Guid mediumId,
		Guid? receiptBookId,
		string name) : base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		AcademicBoardId = boardId;
		AcademicMediumId = mediumId;
		ReceiptBookId = receiptBookId;
		Name = name;
	}
}
