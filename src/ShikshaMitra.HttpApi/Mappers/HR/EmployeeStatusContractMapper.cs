using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees.Status;

namespace ShikshaMitra.HR.Employees;

public static class EmployeeStatusContractMapper
{
	public static ChangeEmployeeStatusDto ToChangeDto(EmployeeStatusUpdateRequest request)
	{
		return new ChangeEmployeeStatusDto
		{
			EmployeeStatusType = (EmployeeStatusType)request.EmployeeStatusType,
			Remarks = request.Remarks
		};
	}

	public static EmployeeStatusUpdateResponse ToResponse(EmployeeStatusResponseDto dto)
	{
		return new EmployeeStatusUpdateResponse
		{
			EmployeeId = dto.EmployeeId,
			PreviousStatus = dto.PreviousStatus.HasValue ? (int)dto.PreviousStatus.Value : null,
			CurrentStatus = (int)dto.CurrentStatus,
			UpdatedAt = dto.UpdatedAt
		};
	}
}
