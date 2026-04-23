using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Finance.Fees.Settings.FeeStructures;

public class FeeStructure : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	public Guid AcademicYearId { get; private set; }
	public Guid AcademicBoardId { get; private set; }
	public Guid AcademicMediumId { get; private set; }
	public Guid ReceiptBookId { get; private set; }
	public Guid StandardId { get; private set; }
	public Guid FeeStructureMetadataId { get; private set; }

	public string Name { get; private set; }
	public ICollection<FeeStructureDetail> FeeStructureDetails { get; private set; } = new List<FeeStructureDetail>();

	protected FeeStructure()
	{
	}

	public FeeStructure(
		Guid id,
		Guid? tenantId,
		Guid feeStructureMetadataId,
		Guid standardId,
		Guid academicYearId,
		Guid boardId,
		Guid mediumId,
		Guid receiptBookId,
		string name) : base(id)
	{
		TenantId = tenantId;
		FeeStructureMetadataId = feeStructureMetadataId;
		StandardId = standardId;
		AcademicYearId = academicYearId;
		AcademicBoardId = boardId;
		AcademicMediumId = mediumId;
		ReceiptBookId = receiptBookId;
		Name = name;
		FeeStructureDetails = new List<FeeStructureDetail>();
	}

	public void AddDetail(FeeStructureDetail detail)
	{
		FeeStructureDetails.Add(detail);
	}

	public void ClearDetails()
	{
		FeeStructureDetails.Clear();
	}
}
