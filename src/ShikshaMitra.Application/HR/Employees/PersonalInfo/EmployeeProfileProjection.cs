using System;
using System.Linq;
using System.Linq.Expressions;
using ShikshaMitra.Common;

namespace ShikshaMitra.HR.Employees.PersonalInfo;

public static class EmployeeProfileProjection
{
	// GET /employees/{id} exposes the same scalar contract as before. Do not hydrate
	// a second employee/document graph or family rows merely to discard them.
	public static Expression<Func<Employee, EmployeeDetails>> Details => employee => new EmployeeDetails
	{
		Id = employee.Id,
		UserId = employee.UserId,
		RoleName = employee.Role != null ? employee.Role.Name : null,
		FullName = JoinName(employee.FirstName, employee.LastName),
		FirstName = employee.FirstName,
		MiddleName = employee.MiddleName,
		LastName = employee.LastName,
		ThumbnailPicture = employee.ProfilePictureBlobName,
		EmployeeIdentifier = employee.EmployeeCode,
		Dob = employee.DateOfBirth.HasValue ? employee.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue) : null,
		Gender = employee.GenderType.HasValue ? employee.GenderType.Value.ToString() : null,
		Doj = employee.DateOfJoining.HasValue ? employee.DateOfJoining.Value.ToDateTime(TimeOnly.MinValue) : null,
		Email = employee.Email,
		MobileNo = employee.MobileNo,
		Department = employee.Department,
		Designation = employee.Designation,
		BloodGroup = employee.BloodGroup,
		Address = JoinAddress(employee.Address1, employee.Address2, employee.Address3),
		IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
		Shift = employee.Shift != null ? new ShiftInfo
		{
			Id = employee.Shift.Id,
			Name = employee.Shift.ShiftName ?? string.Empty,
			StartTime = employee.Shift.StartTime.ToString("HH:mm:ss"),
			EndTime = employee.Shift.EndTime.ToString("HH:mm:ss")
		} : null,
		Status = (int)(employee.EmployeeStatusType ?? EmployeeStatusType.Active)
	};

	private static string JoinName(string? first, string? last) =>
		string.Join(" ", new[] { first, last }.Where(x => !string.IsNullOrWhiteSpace(x)));

	private static string JoinAddress(string? first, string? second, string? third) =>
		string.Join(", ", new[] { first, second, third }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
