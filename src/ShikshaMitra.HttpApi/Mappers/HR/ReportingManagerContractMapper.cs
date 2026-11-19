using ShikshaMitra.HR.Employees.ReportingManager;
using System;
using System.Linq;

namespace ShikshaMitra.HR.Employees.ReportingManagerInfo;

/// <summary>
/// Maps between API contract DTOs and internal application DTOs
/// </summary>
public static class ReportingManagerContractMapper
{
	/// <summary>
	/// Converts API request to internal DTO
	/// </summary>
	public static AssignReportingManagerDto ToAssignDto(ReportingManagerRequest request)
	{
		return new AssignReportingManagerDto
		{
			ReportingManagerId = Guid.Parse(request.ReportingManagerId!),
			ReportingManagerKind = request.ReportingManagerKind
		};
	}

	/// <summary>
	/// Converts internal DTO to API response
	/// </summary>
	public static ReportingManagerResponse ToResponse(EmployeeReportingResponseDto dto)
	{
		return new ReportingManagerResponse
		{
			EmployeeId = dto.EmployeeId,
			ReportingManager = dto.ReportingManager != null
				? ToManagerInfo(dto.ReportingManager)
				: null
		};
	}

	/// <summary>
	/// Converts internal DTO to employees under manager response
	/// </summary>
	public static EmployeesUnderManagerResponse ToEmployeesUnderManagerResponse(EmployeesByManagerResponseDto dto)
	{
		return new EmployeesUnderManagerResponse
		{
			Manager = ToManagerInfo(dto.Manager),
			Employees = dto.Employees.Select(ToManagerEmployeeInfo).ToList()
		};
	}

	private static ManagerInfo ToManagerInfo(EmployeeBasicDto dto)
	{
		return new ManagerInfo
		{
			Kind = dto.Kind,
			Email = dto.Email,
			Status = dto.Status,
			Id = dto.Id,
			EmployeeIdentifier = dto.EmployeeCode ?? string.Empty,
			FullName = dto.FullName ?? string.Empty,
			Designation = dto.Designation ?? string.Empty
		};
	}

	private static ManagerEmployeeInfo ToManagerEmployeeInfo(EmployeeBasicDto dto)
	{
		return new ManagerEmployeeInfo
		{
			Id = dto.Id,
			EmployeeIdentifier = dto.EmployeeCode ?? string.Empty,
			FullName = dto.FullName ?? string.Empty,
			Designation = dto.Designation ?? string.Empty
		};
	}
}
