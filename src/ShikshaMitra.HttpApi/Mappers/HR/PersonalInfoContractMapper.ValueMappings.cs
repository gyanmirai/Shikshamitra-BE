using ShikshaMitra.Common;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public static partial class PersonalInfoContractMapper
{
	private static string? NormalizePan(string? panNo) =>
		string.IsNullOrWhiteSpace(panNo) ? null : panNo.Trim().ToUpperInvariant();

	private static GenderType MapGender(int gender)
	{
		return gender switch
		{
			1 => GenderType.Male,
			2 => GenderType.Female,
			_ => throw new BusinessException(HRErrorCodes.InvalidGenderValue)
		};
	}

	private static int MapGenderToInt(GenderType gender)
	{
		return gender switch
		{
			GenderType.Male => 1,
			GenderType.Female => 2,
			_ => throw new BusinessException(HRErrorCodes.InvalidGenderValue)
		};
	}

	private static CastType MapCasteType(int casteType)
	{
		return casteType switch
		{
			0 => CastType.General,
			1 => CastType.OBC,
			2 => CastType.SC,
			3 => CastType.ST,
			_ => throw new BusinessException(HRErrorCodes.InvalidCasteTypeValue)
		};
	}

	private static int MapCasteTypeToInt(CastType casteType)
	{
		return casteType switch
		{
			CastType.General => 0,
			CastType.OBC => 1,
			CastType.SC => 2,
			CastType.ST => 3,
			_ => 0
		};
	}

	private static MaritalStatusType MapMaritalStatus(int maritalStatus)
	{
		return maritalStatus switch
		{
			0 => MaritalStatusType.Unmarried,
			1 => MaritalStatusType.Married,
			2 => MaritalStatusType.Divorced,
			3 => MaritalStatusType.Widowed,
			_ => throw new BusinessException(HRErrorCodes.InvalidMaritalStatusValue)
		};
	}

	private static int MapMaritalStatusToInt(MaritalStatusType maritalStatus)
	{
		return maritalStatus switch
		{
			MaritalStatusType.Unmarried => 0,
			MaritalStatusType.Married => 1,
			MaritalStatusType.Divorced => 2,
			MaritalStatusType.Widowed => 3,
			_ => 0
		};
	}

	private static string MapBloodGroup(int bloodGroup)
	{
		return bloodGroup switch
		{
			1 => "A+",
			2 => "A-",
			3 => "B+",
			4 => "B-",
			5 => "AB+",
			6 => "AB-",
			7 => "O+",
			8 => "O-",
			_ => throw new BusinessException(HRErrorCodes.InvalidBloodGroupValue)
		};
	}

	private static int MapBloodGroupToInt(string bloodGroup)
	{
		return bloodGroup?.ToUpperInvariant() switch
		{
			"A+" => 1,
			"A-" => 2,
			"B+" => 3,
			"B-" => 4,
			"AB+" => 5,
			"AB-" => 6,
			"O+" => 7,
			"O-" => 8,
			_ => throw new BusinessException(HRErrorCodes.InvalidBloodGroupValue)
		};
	}
}
