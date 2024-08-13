using ShikshaMitra.TenantSettings.AcademicSetups;
using Volo.Abp;

namespace ShikshaMitra.Onboarding.AcademicYears;

public class AlreadyExistsException : BusinessException
{
	public AlreadyExistsException(string entityName, string name)
		: base(TenantSettingsDomainErrorCodes.AcademicYearAlreadyExists)
	{
		WithData("entityName", entityName);
		WithData("name", name);
	}
}