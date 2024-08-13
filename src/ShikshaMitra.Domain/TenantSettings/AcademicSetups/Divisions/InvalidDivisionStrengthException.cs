using Volo.Abp;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Divisions;

public class InvalidDivisionStrengthException :   BusinessException
{
	public InvalidDivisionStrengthException() : base(ShikshaMitraDomainErrorCodes.MustBeGreaterThanZero)
	{
	}
}