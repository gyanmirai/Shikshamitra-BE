using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students;

public class AcademicTerm : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public int Sequence { get; private set; }
	public bool IsCurrent { get; private set; }
	public bool IsActive { get; private set; }

	private AcademicTerm()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	public AcademicTerm(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		string name,
		int sequence,
		bool isCurrent = false,
		bool isActive = true)
		: base(id)
	{
		TenantId = tenantId;
		AcademicYearId = academicYearId;
		SetDetails(name, sequence);
		IsCurrent = isCurrent;
		IsActive = isActive;
	}

	public void SetDetails(string name, int sequence)
	{
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 100).Trim();
		Sequence = sequence;
	}

	public void MarkCurrent()
	{
		IsCurrent = true;
		IsActive = true;
	}

	public void ClearCurrent()
	{
		IsCurrent = false;
	}

	public void SetActive(bool isActive)
	{
		IsActive = isActive;
		if (!isActive)
		{
			IsCurrent = false;
		}
	}
}
