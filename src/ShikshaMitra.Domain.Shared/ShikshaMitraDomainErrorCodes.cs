namespace ShikshaMitra;

public static class ShikshaMitraDomainErrorCodes
{
	public const string EndDateMustBeGraterThanStartDate = "ShikshaMitra:00001";
	public const string MustBeGreaterThanZero = "ShikshaMitra:00002";

	/* You can add your business exception error codes here, as constants */
	public const string AuthorAlreadyExists = "BookStore:00001";


	public const string OrganizationUnitNotExist = "ShikshaMitra:00003";

	// Reporting Manager error codes
	public const string EmployeeCannotReportToSelf = "ShikshaMitra:00004";
	public const string CircularReportingNotAllowed = "ShikshaMitra:00005";
}