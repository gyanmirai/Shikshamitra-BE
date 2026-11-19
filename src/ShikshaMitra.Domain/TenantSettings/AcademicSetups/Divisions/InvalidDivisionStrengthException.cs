using Microsoft.Extensions.Logging;
using System;
using Volo.Abp;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Divisions;

public class InvalidDivisionStrengthException : BusinessException
{
	public InvalidDivisionStrengthException(
		string? code,
		string? message,
		string? details,
		Exception? innerException,
		LogLevel logLevel)
		: base(code, message, details, innerException, logLevel)
	{
	}

	public InvalidDivisionStrengthException() : base(ShikshaMitraDomainErrorCodes.MustBeGreaterThanZero) { }
}
