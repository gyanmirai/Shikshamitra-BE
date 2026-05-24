using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using ShikshaMitra.Common;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using ShikshaMitra.HR.AttendanceAndLeaves.Import;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.TenantSettings;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceImportAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IAttendanceImportAppService _attendanceImportAppService;
	private readonly IEmployeePersonalInfoAppService _employeePersonalInfoAppService;
	private readonly IEmployeeAttendanceRepository _employeeAttendanceRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
	private readonly IGuidGenerator _guidGenerator;
	private readonly ICurrentTenant _currentTenant;

	public AttendanceImportAppService_Tests()
	{
		_attendanceImportAppService = GetRequiredService<IAttendanceImportAppService>();
		_employeePersonalInfoAppService = GetRequiredService<IEmployeePersonalInfoAppService>();
		_employeeAttendanceRepository = GetRequiredService<IEmployeeAttendanceRepository>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_shiftRepository = GetRequiredService<IRepository<ShiftManagement, Guid>>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Should_Preview_And_Confirm_Csv_Import()
	{
		var employee = await CreateEmployeeAsync("csv");
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"11-04-2026,{employee.EmployeeCode},09:00 AM,05:30 PM,,Regular shift,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("attendance.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeTrue();
		preview.PreviewToken.ShouldNotBeNullOrWhiteSpace();
		preview.Summary.TotalRows.ShouldBe(1);
		preview.Summary.CreateCount.ShouldBe(1);
		preview.Rows.Count.ShouldBe(1);
		preview.Rows[0].Action.ShouldBe("create");
		preview.Rows[0].Errors.ShouldBeEmpty();

		var confirm = await _attendanceImportAppService.ConfirmAsync(new AttendanceImportConfirmRequestDto
		{
			PreviewToken = preview.PreviewToken!
		});

		confirm.ProcessedRows.ShouldBe(1);
		confirm.CreatedCount.ShouldBe(1);
		confirm.ReplacedCount.ShouldBe(0);

		await WithUnitOfWorkAsync(async () =>
		{
			var query = await _employeeAttendanceRepository.GetQueryableAsync();
			var attendance = query.Single(x => x.EmployeeId == employee.Id && x.AttendanceDate == new DateOnly(2026, 4, 11));
			attendance.StatusType.ShouldBe(EmployeeAttendanceStatusType.Present);
			attendance.TimeIn.ShouldNotBeNull();
			attendance.TimeOut.ShouldNotBeNull();
		});
	}

	[Fact]
	public async Task Should_Preview_Xlsx_Import()
	{
		var employee = await CreateEmployeeAsync("xlsx");
		var xlsx = BuildXlsxFile((
			"11-04-2026",
			employee.EmployeeCode,
			string.Empty,
			string.Empty,
			"FullDayLeave",
			"Approved leave",
			employee.FullName,
			employee.MobileNo));

		var preview = await _attendanceImportAppService.PreviewAsync("attendance.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx);

		preview.CanConfirm.ShouldBeTrue();
		preview.Summary.ValidRows.ShouldBe(1);
		preview.Rows.Single().Action.ShouldBe("create");
		preview.Rows.Single().Status.ShouldBe("FullDayLeave");
	}

	[Fact]
	public async Task Should_Replace_Existing_Attendance_On_Confirm()
	{
		var employee = await CreateEmployeeAsync("replace");
		await WithUnitOfWorkAsync(async () =>
		{
			await _employeeAttendanceRepository.InsertAsync(new EmployeeAttendance
			{
				TenantId = _currentTenant.Id,
				EmployeeId = employee.Id,
				AttendanceDate = new DateOnly(2026, 4, 11),
				StatusType = EmployeeAttendanceStatusType.Present,
				TimeIn = new DateTimeOffset(2026, 4, 11, 9, 0, 0, TimeSpan.Zero),
				TimeOut = new DateTimeOffset(2026, 4, 11, 17, 0, 0, TimeSpan.Zero)
			}, autoSave: true);
		});

		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"11-04-2026,{employee.EmployeeCode},,,HalfDayLeave,Updated by import,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("replace.csv", "text/csv", csv);
		preview.Rows.Single().Action.ShouldBe("replace");

		var confirm = await _attendanceImportAppService.ConfirmAsync(new AttendanceImportConfirmRequestDto
		{
			PreviewToken = preview.PreviewToken!
		});

		confirm.CreatedCount.ShouldBe(0);
		confirm.ReplacedCount.ShouldBe(1);

		await WithUnitOfWorkAsync(async () =>
		{
			var query = await _employeeAttendanceRepository.GetQueryableAsync();
			var attendance = query.Single(x => x.EmployeeId == employee.Id && x.AttendanceDate == new DateOnly(2026, 4, 11));
			attendance.StatusType.ShouldBe(EmployeeAttendanceStatusType.HalfDayLeave);
			attendance.TimeIn.ShouldBeNull();
			attendance.TimeOut.ShouldBeNull();
		});
	}

	[Fact]
	public async Task Should_Reject_Duplicate_Rows_In_Same_File()
	{
		var employee = await CreateEmployeeAsync("duplicate");
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"11-04-2026,{employee.EmployeeCode},09:00 AM,05:00 PM,,Morning,{employee.FullName},{employee.MobileNo}",
			$"11-04-2026,{employee.EmployeeCode},09:15 AM,05:15 PM,,Duplicate,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("duplicate.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeFalse();
		preview.PreviewToken.ShouldBeNull();
		preview.Summary.InvalidRows.ShouldBe(2);
		preview.Rows.All(x => x.Errors.Count > 0).ShouldBeTrue();
	}

	private async Task<EmployeePersonalInfoDto> CreateEmployeeAsync(string suffix)
	{
		var roleId = await EnsureRoleExistsAsync();
		var shiftId = await EnsureShiftExistsAsync();
		var organizationId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		var unique = Guid.NewGuid().ToString("N")[..6];

		return await _employeePersonalInfoAppService.CreateAsync(new CreateEmployeePersonalInfoDto
		{
			FirstName = $"Emp{suffix}{unique}",
			LastName = "Import",
			MobileNo = BuildPhoneNumber(),
			Email = BuildEmail(suffix),
			DateOfBirth = new DateOnly(1990, 5, 15),
			GenderType = GenderType.Male,
			DateOfJoining = new DateOnly(2024, 1, 1),
			EmployeeStatusType = EmployeeStatusType.Active,
			RoleId = roleId,
			ShiftId = shiftId,
			OrganizationIds = [organizationId]
		});
	}

	private async Task<Guid> EnsureRoleExistsAsync()
	{
		var existing = (await _roleRepository.GetListAsync()).FirstOrDefault();
		if (existing != null)
		{
			return existing.Id;
		}

		var id = _guidGenerator.Create();
		await _roleRepository.InsertAsync(new IdentityRole(id, $"TestRole-{id:N}", _currentTenant.Id), autoSave: true);
		return id;
	}

	private async Task<Guid> EnsureShiftExistsAsync()
	{
		var existing = (await _shiftRepository.GetListAsync()).FirstOrDefault();
		if (existing != null)
		{
			return existing.Id;
		}

		var shift = new ShiftManagement
		{
			ShiftName = "Attendance Import Test Shift",
			StartTime = new TimeOnly(8, 0),
			EndTime = new TimeOnly(16, 0)
		};

		await _shiftRepository.InsertAsync(shift, autoSave: true);
		return shift.Id;
	}

	private static string BuildPhoneNumber() => $"90{Random.Shared.Next(10000000, 99999999)}";

	private static string BuildEmail(string prefix) => $"{prefix}.{Guid.NewGuid():N}@test.com";

	private static byte[] BuildCsv(params string[] lines) => Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines));

	private static byte[] BuildXlsxFile(params (string AttendanceDate, string EmployeeCode, string TimeIn, string TimeOut, string Status, string Remarks, string EmployeeName, string ContactNo)[] rows)
	{
		using var workbook = new XLWorkbook();
		var sheet = workbook.Worksheets.Add("AttendanceImport");
		sheet.Cell(1, 1).Value = "Attendance Date";
		sheet.Cell(1, 2).Value = "Employee Code";
		sheet.Cell(1, 3).Value = "Time In";
		sheet.Cell(1, 4).Value = "Time Out";
		sheet.Cell(1, 5).Value = "Status";
		sheet.Cell(1, 6).Value = "Remarks";
		sheet.Cell(1, 7).Value = "Employee Name";
		sheet.Cell(1, 8).Value = "Contact No";

		for (var index = 0; index < rows.Length; index++)
		{
			var row = rows[index];
			var target = index + 2;
			sheet.Cell(target, 1).Value = row.AttendanceDate;
			sheet.Cell(target, 2).Value = row.EmployeeCode;
			sheet.Cell(target, 3).Value = row.TimeIn;
			sheet.Cell(target, 4).Value = row.TimeOut;
			sheet.Cell(target, 5).Value = row.Status;
			sheet.Cell(target, 6).Value = row.Remarks;
			sheet.Cell(target, 7).Value = row.EmployeeName;
			sheet.Cell(target, 8).Value = row.ContactNo;
		}

		using var stream = new System.IO.MemoryStream();
		workbook.SaveAs(stream);
		return stream.ToArray();
	}
}






