namespace ShikshaMitra.HR;

public static partial class HRErrorCodes
{
	public const string EmployeeApplicationNotFound = "HR:Employee:010401";
	public const string EmployeeApplicationTokenInvalid = "HR:Employee:010402";
	public const string EmployeeApplicationTokenExpired = "HR:Employee:010403";
	public const string EmployeeApplicationAlreadySubmitted = "HR:Employee:010404";
	public const string EmployeeApplicationAlreadyFinalized = "HR:Employee:010405";
	public const string EmployeeApplicationInvalidStatusTransition = "HR:Employee:010406";
	public const string EmployeeApplicationInvalidUpload = "HR:Employee:010407";
	public const string EmployeeApplicationCannotDeleteFinalized = "HR:Employee:010408";
	public const string EmployeeApplicationEmailAlreadyInUse = "HR:Employee:010409";
	public const string EmployeeApplicationSubmissionRequired = "HR:Employee:010410";
}
