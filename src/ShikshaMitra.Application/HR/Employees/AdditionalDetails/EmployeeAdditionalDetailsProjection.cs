namespace ShikshaMitra.HR.Employees.AdditionalDetails;

internal static class EmployeeAdditionalDetailsProjection
{
	public static EmployeeAdditionalDetailsDto ToEmptyResponse(Employee employee)
	{
		return new EmployeeAdditionalDetailsDto
		{
			EmployeeId = employee.Id,
			CreatedAt = employee.CreationTime,
			LastModifiedAt = employee.LastModificationTime ?? employee.CreationTime
		};
	}
}
