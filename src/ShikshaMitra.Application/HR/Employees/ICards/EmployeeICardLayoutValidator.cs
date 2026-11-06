using ShikshaMitra.Common.IdentityCards;
using Volo.Abp;

namespace ShikshaMitra.HR.Employees.ICards;

internal static class EmployeeICardLayoutValidator
{
	private static readonly string[] Fields = ["photo", "name", "employeeCode", "designation", "department", "organization", "contactNo", "bloodGroup", "validityYear", "address", "schoolName", "schoolAddress", "static"];

	public static IdentityCardLayout ParseLayout(string json, int schemaVersion = 1, bool backEnabled = false)
	{
		try { return IdentityCardSchema.ParseLayout(json, Fields, schemaVersion, backEnabled); }
		catch (BusinessException ex) when (ex.Code == IdentityCardGeometry.InvalidConfiguration)
		{
			throw new BusinessException(HRErrorCodes.EmployeeICardTemplateInvalidFieldsJson).WithData("detail", ex.Data["detail"] ?? "Invalid identity card configuration.");
		}
	}
}
