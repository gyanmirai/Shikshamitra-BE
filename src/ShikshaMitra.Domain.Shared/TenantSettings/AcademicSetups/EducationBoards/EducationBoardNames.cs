using System;
using Volo.Abp;

namespace ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;

public static class EducationBoardNames
{
	public const int MaxCustomNameLength = 128;
	public const string InvalidType = "ShikshaMitra.TenantSettings:EducationBoard:InvalidType";
	public const string InvalidCustomName = "ShikshaMitra.TenantSettings:EducationBoard:InvalidCustomName";
	public const string ReservedCustomName = "ShikshaMitra.TenantSettings:EducationBoard:ReservedCustomName";
	public static string DisplayName(EducationBoardType type) => type.ToString();
	public static string? NormalizeCustomName(EducationBoardType type, string? name)
	{
		if (!Enum.IsDefined(type)) throw new BusinessException(InvalidType, "Select a valid education board.");
		return null;
	}
}
