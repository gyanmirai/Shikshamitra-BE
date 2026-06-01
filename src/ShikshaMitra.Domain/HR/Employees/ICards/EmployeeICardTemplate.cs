using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees.ICards;

public class EmployeeICardTemplate : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
	public Guid? TenantId { get; private set; }
	public Guid AcademicYearId { get; private set; }
	public Guid? MediumStageId { get; private set; }
	public string Name { get; private set; } = string.Empty;
	public int Width { get; private set; }
	public int Height { get; private set; }
	public string? BackgroundColor { get; private set; }
	public string? BackgroundImageBlobName { get; private set; }
	public string? LogoBlobName { get; private set; }
	public string? SchoolName { get; private set; }
	public string? SchoolAddress { get; private set; }
	public string? ThemeColor { get; private set; }
	public string Orientation { get; private set; } = "Horizontal";
	public string? PresetKey { get; private set; }
	public bool QrCodeEnabled { get; private set; }
	public bool BarcodeEnabled { get; private set; }
	public bool BackSideEnabled { get; private set; }
	public int SchemaVersion { get; private set; } = 1;
	public string LayoutJson { get; private set; } = string.Empty;
	public string PrintSettingsJson { get; private set; } = string.Empty;
	public string FieldsJson { get; private set; } = "[]";
	public bool IsActive { get; private set; }

	protected EmployeeICardTemplate()
	{
	}

	public EmployeeICardTemplate(
		Guid id,
		Guid? tenantId,
		Guid academicYearId,
		Guid? mediumStageId,
		string name,
		int width,
		int height,
		string? backgroundColor,
		string? backgroundImageBlobName,
		string? logoBlobName,
		string? schoolName,
		string? schoolAddress,
		string? themeColor,
		string orientation,
		string? presetKey,
		bool qrCodeEnabled,
		bool barcodeEnabled,
		bool backSideEnabled,
		int schemaVersion,
		string? layoutJson,
		string? printSettingsJson,
		string fieldsJson,
		bool isActive) : base(id)
	{
		TenantId = tenantId;
		Update(academicYearId, mediumStageId, name, width, height, backgroundColor, backgroundImageBlobName, logoBlobName, schoolName, schoolAddress, themeColor, orientation, presetKey, qrCodeEnabled, barcodeEnabled, backSideEnabled, schemaVersion, layoutJson, printSettingsJson, fieldsJson, isActive);
	}

	public void Update(
		Guid academicYearId,
		Guid? mediumStageId,
		string name,
		int width,
		int height,
		string? backgroundColor,
		string? backgroundImageBlobName,
		string? logoBlobName,
		string? schoolName,
		string? schoolAddress,
		string? themeColor,
		string orientation,
		string? presetKey,
		bool qrCodeEnabled,
		bool barcodeEnabled,
		bool backSideEnabled,
		int schemaVersion,
		string? layoutJson,
		string? printSettingsJson,
		string fieldsJson,
		bool isActive)
	{
		AcademicYearId = academicYearId;
		MediumStageId = mediumStageId;
		Name = name.Trim();
		Width = width;
		Height = height;
		BackgroundColor = string.IsNullOrWhiteSpace(backgroundColor) ? null : backgroundColor.Trim();
		BackgroundImageBlobName = string.IsNullOrWhiteSpace(backgroundImageBlobName) ? null : backgroundImageBlobName.Trim();
		LogoBlobName = string.IsNullOrWhiteSpace(logoBlobName) ? null : logoBlobName.Trim();
		SchoolName = string.IsNullOrWhiteSpace(schoolName) ? null : schoolName.Trim();
		SchoolAddress = string.IsNullOrWhiteSpace(schoolAddress) ? null : schoolAddress.Trim();
		ThemeColor = string.IsNullOrWhiteSpace(themeColor) ? null : themeColor.Trim();
		Orientation = string.IsNullOrWhiteSpace(orientation) ? "Horizontal" : orientation.Trim();
		PresetKey = string.IsNullOrWhiteSpace(presetKey) ? null : presetKey.Trim();
		QrCodeEnabled = qrCodeEnabled;
		BarcodeEnabled = barcodeEnabled;
		BackSideEnabled = backSideEnabled;
		SchemaVersion = schemaVersion <= 0 ? 1 : schemaVersion;
		LayoutJson = string.IsNullOrWhiteSpace(layoutJson) ? string.Empty : layoutJson;
		PrintSettingsJson = string.IsNullOrWhiteSpace(printSettingsJson) ? string.Empty : printSettingsJson;
		FieldsJson = string.IsNullOrWhiteSpace(fieldsJson) ? "[]" : fieldsJson;
		IsActive = isActive;
	}
}
