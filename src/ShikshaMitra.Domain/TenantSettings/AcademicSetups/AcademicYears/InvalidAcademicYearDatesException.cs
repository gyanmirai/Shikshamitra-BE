using System;
using Volo.Abp;

namespace ShikshaMitra.Onboarding.AcademicYears;

public class InvalidAcademicYearDatesException : BusinessException
{
	public InvalidAcademicYearDatesException(DateTime startDate, DateTime endDate) : base(ShikshaMitraDomainErrorCodes
		.EndDateMustBeGraterThanStartDate)
	{
		WithData("startDate", startDate);
		WithData("endDate", endDate);
	}
}