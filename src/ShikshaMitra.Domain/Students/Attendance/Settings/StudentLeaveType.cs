using System.Diagnostics.CodeAnalysis;
using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.Attendance.Settings;

/// <summary>
/// Represents a leave type configuration for students (e.g., Sick, Casual, etc.)
/// Separate from HR leave policies to handle student-specific leave types
/// </summary>
public class StudentLeaveType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }

	/// <summary>
	/// Name of the leave type (e.g., "Sick", "Casual Leave", "Medical Leave")
	/// </summary>
	public string LeaveTypeName { get; private set; }

	/// <summary>
	/// Color code for UI representation (hex color code)
	/// </summary>
	public string Color { get; private set; }

	/// <summary>
	/// Indicates if this leave type is active and can be used
	/// </summary>
	public bool IsActive { get; private set; }

	private StudentLeaveType()
	{
		// EF materializes these required columns after this ORM constructor.
		Color = null!;
		LeaveTypeName = null!;

		/* This constructor is for deserialization / ORM purpose */
	}

	internal StudentLeaveType(
		Guid id,
		Guid? tenantId,
		string leaveTypeName,
		string color,
		bool isActive = true)
		: base(id)
	{
		TenantId = tenantId;
		SetLeaveTypeName(leaveTypeName);
		SetColor(color);
		IsActive = isActive;
	}

	/// <summary>
	/// Updates the leave type name with validation
	/// </summary>
	[MemberNotNull(nameof(LeaveTypeName))]
	public void SetLeaveTypeName(string leaveTypeName)
	{
		LeaveTypeName = Check.NotNullOrWhiteSpace(leaveTypeName, nameof(leaveTypeName), StudentLeaveTypeConsts.MaxLeaveTypeNameLength);

		if (leaveTypeName.Length < StudentLeaveTypeConsts.MinLeaveTypeNameLength)
		{
			throw new BusinessException("SM:LeaveTypeNameTooShort")
				.WithData("MinLength", StudentLeaveTypeConsts.MinLeaveTypeNameLength)
				.WithData("ActualLength", leaveTypeName.Length);
		}
	}

	/// <summary>
	/// Updates the color with validation
	/// </summary>
	[MemberNotNull(nameof(Color))]
	public void SetColor(string color)
	{
		Color = Check.NotNullOrWhiteSpace(color, nameof(color), StudentLeaveTypeConsts.MaxColorLength);

		// Validate hex color format
		if (!System.Text.RegularExpressions.Regex.IsMatch(color, @"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$"))
		{
			throw new BusinessException("SM:InvalidColorFormat")
				.WithData("Color", color);
		}
	}

	/// <summary>
	/// Activates the leave type
	/// </summary>
	public void Activate()
	{
		IsActive = true;
	}

	/// <summary>
	/// Deactivates the leave type
	/// </summary>
	public void Deactivate()
	{
		IsActive = false;
	}
}

/// <summary>
/// Constants for StudentLeaveType entity
/// </summary>
public static class StudentLeaveTypeConsts
{
	public const int MinLeaveTypeNameLength = 2;
	public const int MaxLeaveTypeNameLength = 100;
	public const int MaxColorLength = 7; // #RRGGBB format
}
