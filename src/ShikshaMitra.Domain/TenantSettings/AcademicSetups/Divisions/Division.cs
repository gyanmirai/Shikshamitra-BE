using System.Diagnostics.CodeAnalysis;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using System;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.TenantSettings.Divisions;

public partial class Division : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; }
	public string Name { get; internal set; }

	public int Strength { get; internal set; }
	public int SortOrder { get; private set; }

	public string RoomNumber { get; internal set; }
	public Guid? RoomId { get; private set; }

	internal void AssignRoom(string roomNumber, Guid? roomId)
	{
		RoomNumber = roomNumber;
		RoomId = roomId;
	}

	internal void SetSortOrder(int sortOrder) => SortOrder = Check.Range(sortOrder, nameof(sortOrder), 0, int.MaxValue);

	// Foreign key
	public Guid StandardId { get; }

	// Navigation property
	public Standard Standard { get; } = null!; // Populated by EF relationship materialization.


	private Division()
	{
		// EF materializes these required columns after this ORM constructor.
		Name = null!;
		RoomNumber = null!;

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
		RoomNumber = roomNumber ?? string.Empty;
	}

	[MemberNotNull(nameof(Name))]
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
