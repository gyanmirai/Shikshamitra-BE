using System;
using System.Text.RegularExpressions;
using ShikshaMitra.TenantSettings.EducationBoards;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// Represents a leave type master data (e.g., "Paid Leave", "Sick Leave")
/// </summary>
public class LeaveType : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; protected set; }

	/// <summary>
	/// Organization/School identifier this leave type belongs to
	/// </summary>
	public Guid EducationMediumStageId { get; protected set; }

	/// <summary>
	/// Type/Name of the leave (e.g., "Paid Leave", "Sick Leave")
	/// </summary>
	public string Type { get; protected set; } = string.Empty;

	/// <summary>
	/// Optional description of the leave type
	/// </summary>
	public string? Description { get; protected set; }

	/// <summary>
	/// Color code for UI display (HEX format: #RRGGBB)
	/// </summary>
	public string Color { get; protected set; } = string.Empty;

	/// <summary>
	/// Whether this leave type is active
	/// </summary>
	public bool IsActive { get; protected set; }

	/// <summary>
	/// Navigation property for EducationMediumStage
	/// </summary>
	public virtual EducationMediumStage? EducationMediumStage { get; protected set; }

	// For EF Core
	protected LeaveType()
	{
	}

	public LeaveType(
		Guid id,
		Guid? tenantId,
		Guid educationMediumStageId,
		string type,
		string color,
		string? description = null) : base(id)
	{
		TenantId = tenantId;
		EducationMediumStageId = educationMediumStageId;
		SetType(type);
		SetColor(color);
		Description = description;
		IsActive = true;
	}

	public void Update(string type, string color, string? description = null)
	{
		SetType(type);
		SetColor(color);
		Description = description;
	}

	public void Activate()
	{
		IsActive = true;
	}

	public void Deactivate()
	{
		IsActive = false;
	}

	private void SetType(string type)
	{
		Check.NotNullOrWhiteSpace(type, nameof(type));
		Check.Length(type, nameof(type), LeaveTypeConsts.MaxTypeLength);
		Type = type;
	}

	private void SetColor(string color)
	{
		Check.NotNullOrWhiteSpace(color, nameof(color));
		Check.Length(color, nameof(color), LeaveTypeConsts.MaxColorLength);

		// Validate HEX color format (#RRGGBB)
		if (!IsValidHexColor(color))
		{
			throw new BusinessException("ShikshaMitra:InvalidColorCode")
				.WithData("message", "Color code must be a valid HEX value (e.g., #FF5733)");
		}

		Color = color.ToUpperInvariant(); // Normalize to uppercase
	}

	private static bool IsValidHexColor(string color)
	{
		// Regex pattern for HEX color: #RRGGBB
		var hexPattern = @"^#([A-Fa-f0-9]{6})$";
		return Regex.IsMatch(color, hexPattern);
	}
}
