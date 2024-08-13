using ShikshaMitra.Onboarding.AcademicYears;
using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.AcademicYears;

public class AcademicYear : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; }
	public string Name { get; internal set; }
	public DateTime StartDate { get; internal set; }
	public DateTime EndDate { get; internal set; }

	private AcademicYear()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	internal AcademicYear(
		Guid id,
		Guid? tenantId,
		string name,
		DateTime startDate,
		DateTime endDate)
		: base(id)
	{
		TenantId = Check.NotNull(tenantId, nameof(tenantId));
		SetName(name);
		SetDates(startDate, endDate);
	}


	internal AcademicYear SetName(string name)
	{
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: AcademicYearConsts.MaxNameLength);
		return this;
	}

	internal AcademicYear SetDates(DateTime startDate, DateTime endDate)
	{
		if (endDate <= startDate)
		{
			throw new InvalidAcademicYearDatesException(startDate, endDate);
		}

		StartDate = startDate;
		EndDate = endDate;
		return this;
	}
}