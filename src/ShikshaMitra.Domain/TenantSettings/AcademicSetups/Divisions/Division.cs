using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using System;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.Divisions;

public class Division : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; }
	public string Name { get; internal set; }

	public int Strength { get; internal set; }

	public string RoomNumber { get; internal set; }

	// Foreign key
	public Guid StandardId { get; }

	// Navigation property
	public Standard Standard { get; }


	private Division()
	{
		/* This constructor is for deserialization / ORM purpose */
	}

	internal Division(
		Guid id,
		Guid? tenantId,
		string name,
		Guid standardId,
		int strength,
		string roomNumber)
		: base(id)
	{
		TenantId = Check.NotNull(tenantId, nameof(tenantId));
		StandardId = Check.NotNull(standardId, nameof(standardId));
		SetName(name);
		SetStrength(strength);
		RoomNumber = roomNumber;
	}

	internal Division SetName(string name)
	{
		Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: DivisionConsts.MaxNameLength);
		return this;
	}

	internal Division SetStrength(int strength)
	{
		if (strength <= 0)
		{
			throw new InvalidDivisionStrengthException();
		}

		Strength = strength;
		return this;
	}
}