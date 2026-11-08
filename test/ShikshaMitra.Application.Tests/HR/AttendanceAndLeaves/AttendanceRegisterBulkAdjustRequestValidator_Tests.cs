using System;
using ShikshaMitra.HR.AttendanceAndLeaves.AttendanceRegister;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceRegisterBulkAdjustRequestValidator_Tests
{
	private readonly AttendanceRegisterBulkAdjustRequestValidator _validator = new();

	[Theory]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(true, true)]
	public void Should_Accept_Either_Target_Collection_Or_Both(bool hasAttendanceIds, bool hasEmployeeDates)
	{
		var input = new AttendanceRegisterBulkAdjustRequest
		{
			AttendanceIds = hasAttendanceIds ? [Guid.NewGuid()] : [],
			EmployeeDates = hasEmployeeDates ? [ValidEmployeeDate()] : []
		};

		_validator.Validate(input).IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Should_Reject_Empty_Target_Collections()
	{
		var result = _validator.Validate(new AttendanceRegisterBulkAdjustRequest());

		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(x => x.ErrorMessage == "At least one attendance ID or employee date is required.");
	}

	[Theory]
	[InlineData(true, false)]
	[InlineData(false, true)]
	[InlineData(true, true)]
	public void Should_Reject_Null_Collections_Without_Throwing(bool nullAttendanceIds, bool nullEmployeeDates)
	{
		var input = new AttendanceRegisterBulkAdjustRequest
		{
			AttendanceIds = nullAttendanceIds ? null! : [Guid.NewGuid()],
			EmployeeDates = nullEmployeeDates ? null! : [ValidEmployeeDate()]
		};

		var result = _validator.Validate(input);

		result.IsValid.ShouldBeFalse();
		if (nullAttendanceIds)
		{
			result.Errors.ShouldContain(x => x.PropertyName == "AttendanceIds");
		}
		if (nullEmployeeDates)
		{
			result.Errors.ShouldContain(x => x.PropertyName == "EmployeeDates");
		}
	}

	[Fact]
	public void Should_Reject_Empty_Attendance_Id_Even_With_Valid_Employee_Date()
	{
		var input = new AttendanceRegisterBulkAdjustRequest
		{
			AttendanceIds = [Guid.Empty],
			EmployeeDates = [ValidEmployeeDate()]
		};

		_validator.Validate(input).Errors.ShouldContain(x => x.PropertyName == "AttendanceIds[0]");
	}

	[Fact]
	public void Should_Reject_Null_Employee_Date_Even_With_Valid_Attendance_Id()
	{
		var input = new AttendanceRegisterBulkAdjustRequest
		{
			AttendanceIds = [Guid.NewGuid()],
			EmployeeDates = [null!]
		};

		_validator.Validate(input).Errors.ShouldContain(x => x.PropertyName == "EmployeeDates[0]");
	}

	[Fact]
	public void Should_Reject_Empty_Employee_Id()
	{
		var entry = ValidEmployeeDate();
		entry.EmployeeId = Guid.Empty;
		var input = new AttendanceRegisterBulkAdjustRequest { EmployeeDates = [entry] };

		_validator.Validate(input).Errors.ShouldContain(x => x.PropertyName == "EmployeeDates[0].EmployeeId");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	[InlineData("2026-04-17xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
	public void Should_Reject_Missing_Or_Overlong_Employee_Date(string? date)
	{
		var entry = ValidEmployeeDate();
		entry.AttendanceDate = date!;
		var input = new AttendanceRegisterBulkAdjustRequest { EmployeeDates = [entry] };

		_validator.Validate(input).Errors.ShouldContain(x => x.PropertyName == "EmployeeDates[0].AttendanceDate");
	}

	[Theory]
	[InlineData("StatusWire", 21)]
	[InlineData("InTime", 41)]
	[InlineData("OutTime", 41)]
	[InlineData("Remarks", 501)]
	[InlineData("Reason", 501)]
	public void Should_Preserve_Shared_Field_Length_Validation(string property, int length)
	{
		var input = new AttendanceRegisterBulkAdjustRequest { EmployeeDates = [ValidEmployeeDate()] };
		typeof(AttendanceRegisterBulkAdjustRequest).GetProperty(property)!.SetValue(input, new string('x', length));

		_validator.Validate(input).Errors.ShouldContain(x => x.PropertyName == property);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	public void Should_Require_Status_For_Employee_Date_Targets(string? status)
	{
		var input = new AttendanceRegisterBulkAdjustRequest
		{
			EmployeeDates = [ValidEmployeeDate()],
			StatusWire = status!
		};

		_validator.Validate(input).Errors.ShouldContain(x => x.PropertyName == "StatusWire");
	}

	private static EmployeeDateInput ValidEmployeeDate() => new()
	{
		EmployeeId = Guid.NewGuid(),
		AttendanceDate = "2026-04-17"
	};
}
