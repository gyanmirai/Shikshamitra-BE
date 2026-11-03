using Microsoft.Extensions.Logging;
using System;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Volo.Abp;

namespace ShikshaMitra.Onboarding.AcademicYears;

public class AlreadyExistsException : BusinessException
{
	public AlreadyExistsException(
		string? code,
		string? message,
		string? details,
		Exception? innerException,
		LogLevel logLevel)
		: base(code, message, details, innerException, logLevel)
	{
	}

	public AlreadyExistsException(string entityName, string name)
		: base(TenantSettingsDomainErrorCodes.AcademicYearAlreadyExists)
	{
		WithData("entityName", entityName);
		WithData("name", name);
	}
}