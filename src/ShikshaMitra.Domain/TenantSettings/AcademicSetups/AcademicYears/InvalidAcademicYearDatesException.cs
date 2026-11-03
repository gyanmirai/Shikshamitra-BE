using Microsoft.Extensions.Logging;
using System;
using Volo.Abp;

namespace ShikshaMitra.Onboarding.AcademicYears;

public class InvalidAcademicYearDatesException : BusinessException
{
	public InvalidAcademicYearDatesException(
		string? code,
		string? message,
		string? details,
		Exception? innerException,
		LogLevel logLevel)
		: base(code, message, details, innerException, logLevel)
	{
	}

	public InvalidAcademicYearDatesException(DateTime startDate, DateTime endDate) : base(ShikshaMitraDomainErrorCodes
		.EndDateMustBeGraterThanStartDate)
	{
		WithData("startDate", startDate);
		WithData("endDate", endDate);
	}
}