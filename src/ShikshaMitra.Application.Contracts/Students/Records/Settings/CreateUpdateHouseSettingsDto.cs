using System;

namespace ShikshaMitra.Students.Records.Settings;

public class CreateUpdateHouseSettingsDto
{
	public required string Name { get; set; }
	public required string Color { get; set; }
	public string? LogoUrl { get; set; }
	public string? Description { get; set; }
}
