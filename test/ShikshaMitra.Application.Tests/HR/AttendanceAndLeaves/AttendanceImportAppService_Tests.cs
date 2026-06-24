using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using ShikshaMitra.Common;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using ShikshaMitra.HR.AttendanceAndLeaves.Import;
using ShikshaMitra.HR.AttendanceAndLeaves.TakeAttendance;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.AttendanceImports;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class AttendanceImportAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IAttendanceImportAppService _attendanceImportAppService;
	private readonly IEmployeePersonalInfoAppService _employeePersonalInfoAppService;
	private readonly IRepository<Employee, Guid> _employeeRepository;
	private readonly IRepository<EmployeeOrganizationAssignment, Guid> _employeeOrganizationAssignmentRepository;
	private readonly IEmployeeAttendanceRepository _employeeAttendanceRepository;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
	private readonly IRepository<AcademicYear, Guid> _academicYearRepository;
	private readonly IRepository<WorkCalendarPolicy, Guid> _workCalendarPolicyRepository;
	private readonly IRepository<PayrollPolicy, Guid> _payrollPolicyRepository;
	private readonly IRepository<AttendancePolicy, Guid> _attendancePolicyRepository;
	private readonly IEducationBoardRepository _educationBoardRepository;
	private readonly IEducationMediumRepository _educationMediumRepository;
	private readonly IEducationMediumStageRepository _educationMediumStageRepository;
	private readonly EducationBoardManager _educationBoardManager;
	private readonly EducationMediumManager _educationMediumManager;
	private readonly EducationMediumStageManager _educationMediumStageManager;
	private readonly IGuidGenerator _guidGenerator;
	private readonly ICurrentTenant _currentTenant;

	public AttendanceImportAppService_Tests()
	{
		_attendanceImportAppService = GetRequiredService<IAttendanceImportAppService>();
		_employeePersonalInfoAppService = GetRequiredService<IEmployeePersonalInfoAppService>();
		_employeeRepository = GetRequiredService<IRepository<Employee, Guid>>();
		_employeeOrganizationAssignmentRepository = GetRequiredService<IRepository<EmployeeOrganizationAssignment, Guid>>();
		_employeeAttendanceRepository = GetRequiredService<IEmployeeAttendanceRepository>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_shiftRepository = GetRequiredService<IRepository<ShiftManagement, Guid>>();
		_academicYearRepository = GetRequiredService<IRepository<AcademicYear, Guid>>();
		_workCalendarPolicyRepository = GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>();
		_payrollPolicyRepository = GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		_attendancePolicyRepository = GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		_educationBoardRepository = GetRequiredService<IEducationBoardRepository>();
		_educationMediumRepository = GetRequiredService<IEducationMediumRepository>();
		_educationMediumStageRepository = GetRequiredService<IEducationMediumStageRepository>();
		_educationBoardManager = GetRequiredService<EducationBoardManager>();
		_educationMediumManager = GetRequiredService<EducationMediumManager>();
		_educationMediumStageManager = GetRequiredService<EducationMediumStageManager>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
	}

	[Fact]
	public async Task Should_Preview_And_Confirm_Csv_Import()
	{
		var employee = await CreateEmployeeAsync("csv");
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		var attendanceDateText = attendanceDate.ToString("dd-MM-yyyy");
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDateText},{employee.EmployeeCode},09:00 AM,05:30 PM,,Regular shift,{employee.FullName},{employee.MobileNo}");

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
			var attendance = query.Single(x => x.EmployeeId == employee.Id && x.AttendanceDate == attendanceDate);
			attendance.StatusType.ShouldBe(EmployeeAttendanceStatusType.Present);
			attendance.TimeIn.ShouldNotBeNull();
			attendance.TimeOut.ShouldNotBeNull();
		});
	}

	[Fact]
	public async Task Should_Preview_Xlsx_Import()
	{
		var employee = await CreateEmployeeAsync("xlsx");
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		var xlsx = BuildXlsxFile((
			attendanceDate.ToString("dd-MM-yyyy"),
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
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		var attendanceDateText = attendanceDate.ToString("dd-MM-yyyy");
		await WithUnitOfWorkAsync(async () =>
		{
			await _employeeAttendanceRepository.InsertAsync(new EmployeeAttendance
			{
				TenantId = _currentTenant.Id,
				EmployeeId = employee.Id,
				AttendanceDate = attendanceDate,
				StatusType = EmployeeAttendanceStatusType.Present,
				TimeIn = new DateTimeOffset(attendanceDate.Year, attendanceDate.Month, attendanceDate.Day, 9, 0, 0, TimeSpan.Zero),
				TimeOut = new DateTimeOffset(attendanceDate.Year, attendanceDate.Month, attendanceDate.Day, 17, 0, 0, TimeSpan.Zero)
			}, autoSave: true);
		});

		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDateText},{employee.EmployeeCode},,,HalfDayLeave,Updated by import,{employee.FullName},{employee.MobileNo}");

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
			var attendance = query.Single(x => x.EmployeeId == employee.Id && x.AttendanceDate == attendanceDate);
			attendance.StatusType.ShouldBe(EmployeeAttendanceStatusType.HalfDayLeave);
			attendance.TimeIn.ShouldBeNull();
			attendance.TimeOut.ShouldBeNull();
		});
	}

	[Fact]
	public async Task Should_Reject_Duplicate_Rows_In_Same_File()
	{
		var employee = await CreateEmployeeAsync("duplicate");
		var attendanceDateText = (await GetCoveredAttendanceDateAsync()).ToString("dd-MM-yyyy");
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDateText},{employee.EmployeeCode},09:00 AM,05:00 PM,,Morning,{employee.FullName},{employee.MobileNo}",
			$"{attendanceDateText},{employee.EmployeeCode},09:15 AM,05:15 PM,,Duplicate,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("duplicate.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeFalse();
		preview.PreviewToken.ShouldBeNull();
		preview.Summary.InvalidRows.ShouldBe(2);
		preview.Rows.All(x => x.Errors.Count > 0).ShouldBeTrue();
	}

	[Fact]
	public async Task Should_Reject_Duplicate_FileName_In_Same_Payroll_Month()
	{
		var employee = await CreateEmployeeAsync("duplicatefile");
		var attendanceDateText = (await GetCoveredAttendanceDateAsync()).ToString("dd-MM-yyyy");
		var fileName = $"duplicate-file-{Guid.NewGuid():N}.csv";
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDateText},{employee.EmployeeCode},09:00 AM,05:00 PM,,First upload,{employee.FullName},{employee.MobileNo}");

		await _attendanceImportAppService.PreviewAsync(fileName, "text/csv", csv);

		var duplicate = await Should.ThrowAsync<BusinessException>(() =>
			_attendanceImportAppService.PreviewAsync(
				$"  {fileName.ToUpperInvariant()}  ",
				"text/csv",
				csv));

		duplicate.Code.ShouldBe(HRErrorCodes.AttendanceImportDuplicateFileName);
	}

	[Fact]
	public async Task Should_Allow_Same_FileName_In_Different_Payroll_Month()
	{
		var employee = await CreateEmployeeAsync("samefilemonth");
		var firstAttendanceDate = await GetCoveredAttendanceDateAsync();
		var secondAttendanceDate = await GetCoveredAttendanceDateAsync(monthOffset: 1);
		var fileName = $"same-file-different-month-{Guid.NewGuid():N}.csv";
		var aprilCsv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{firstAttendanceDate:dd-MM-yyyy},{employee.EmployeeCode},09:00 AM,05:00 PM,,May upload,{employee.FullName},{employee.MobileNo}");
		var mayCsv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{secondAttendanceDate:dd-MM-yyyy},{employee.EmployeeCode},09:00 AM,05:00 PM,,June upload,{employee.FullName},{employee.MobileNo}");

		await _attendanceImportAppService.PreviewAsync(fileName, "text/csv", aprilCsv);
		var mayPreview = await _attendanceImportAppService.PreviewAsync(fileName, "text/csv", mayCsv);

		mayPreview.CanConfirm.ShouldBeTrue();
		mayPreview.Summary.ValidRows.ShouldBe(1);
	}

	[Fact]
	public async Task Should_Auto_Mark_Half_Day_When_Worked_Time_Is_Below_Full_Day_Threshold()
	{
		var shiftId = await EnsureFullDayShiftExistsAsync();
		var employee = await CreateEmployeeAsync("lowwork", shiftId);
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDate:dd-MM-yyyy},{employee.EmployeeCode},03:00 PM,05:30 PM,,Low worked time,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("low-worked-time.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeTrue();
		var row = preview.Rows.Single();
		row.Status.ShouldBe(EmployeeAttendanceStatusType.HalfDayLeave.ToString());
		row.Warnings.ShouldContain(x => x.Contains("Auto-marked Half Day", StringComparison.OrdinalIgnoreCase));

		await _attendanceImportAppService.ConfirmAsync(new AttendanceImportConfirmRequestDto
		{
			PreviewToken = preview.PreviewToken!
		});

		await WithUnitOfWorkAsync(async () =>
		{
			var query = await _employeeAttendanceRepository.GetQueryableAsync();
			var attendance = query.Single(x => x.EmployeeId == employee.Id && x.AttendanceDate == attendanceDate);
			attendance.StatusType.ShouldBe(EmployeeAttendanceStatusType.HalfDayLeave);
		});
	}

	[Fact]
	public async Task Should_Auto_Mark_Half_Day_When_Late_Arrival_Exceeds_Grace_Time()
	{
		var shiftId = await EnsureEightToFiveShiftExistsAsync();
		var employee = await CreateEmployeeAsync("latehalfday", shiftId);
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.DeductHalfDay);
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDate:dd-MM-yyyy},{employee.EmployeeCode},08:35 AM,05:30 PM,,Late beyond grace,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("late-half-day.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeTrue();
		var row = preview.Rows.Single();
		row.Status.ShouldBe(EmployeeAttendanceStatusType.HalfDayLeave.ToString());
		row.Warnings.ShouldContain(x => x.Contains("Auto-marked Half Day", StringComparison.OrdinalIgnoreCase));

		await _attendanceImportAppService.ConfirmAsync(new AttendanceImportConfirmRequestDto
		{
			PreviewToken = preview.PreviewToken!
		});

		await WithUnitOfWorkAsync(async () =>
		{
			var query = await _employeeAttendanceRepository.GetQueryableAsync();
			var attendance = query.Single(x => x.EmployeeId == employee.Id && x.AttendanceDate == attendanceDate);
			attendance.StatusType.ShouldBe(EmployeeAttendanceStatusType.HalfDayLeave);
		});
	}

	[Fact]
	public async Task Take_Attendance_Entry_Writer_Should_Auto_Mark_Half_Day_When_Late_Arrival_Exceeds_Grace_Time()
	{
		var shiftId = await EnsureEightToFiveShiftExistsAsync();
		var employee = await CreateEmployeeAsync("manualsave", shiftId);
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.DeductHalfDay);

		await WithUnitOfWorkAsync(async () =>
		{
			var writer = GetRequiredService<TakeAttendanceEntryWriter>();
			await writer.SaveEntryAsync(
				new ParsedTakeAttendanceEntry(
					new TakeAttendanceSaveEntryDto
					{
						EmployeeId = employee.Id,
						StatusWire = "PRESENT",
						InTime = "08:35:00",
						OutTime = "17:30:00",
						Remarks = "Late beyond grace from mark attendance"
					},
					EmployeeAttendanceStatusType.Present),
				attendanceDate,
				new UserScopeContext { IsAdmin = true, UserId = _guidGenerator.Create(), TenantId = _currentTenant.Id },
				new Dictionary<Guid, Guid?>());
		});

		await WithUnitOfWorkAsync(async () =>
		{
			var query = await _employeeAttendanceRepository.GetQueryableAsync();
			var attendance = query.Single(x => x.EmployeeId == employee.Id && x.AttendanceDate == attendanceDate);
			attendance.StatusType.ShouldBe(EmployeeAttendanceStatusType.HalfDayLeave);
		});
	}

	[Fact]
	public async Task Take_Attendance_Entry_Writer_Should_Reject_Present_When_No_Punches_In_Time_Based_Mode()
	{
		var employee = await CreateEmployeeAsync("presentnopunch");
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.None);

		await WithUnitOfWorkAsync(async () =>
		{
			var writer = GetRequiredService<TakeAttendanceEntryWriter>();
			var exception = await Should.ThrowAsync<BusinessException>(() =>
				writer.SaveEntryAsync(
					new ParsedTakeAttendanceEntry(
						new TakeAttendanceSaveEntryDto
						{
							EmployeeId = employee.Id,
							StatusWire = "PRESENT",
							InTime = null,
							OutTime = null,
							Remarks = "Invalid present without punches"
						},
						EmployeeAttendanceStatusType.Present),
					attendanceDate,
					new UserScopeContext { IsAdmin = true, UserId = _guidGenerator.Create(), TenantId = _currentTenant.Id },
					new Dictionary<Guid, Guid?>()));

			exception.Code.ShouldBe(HRErrorCodes.AttendancePolicyEvaluationInvalidPunches);
		});
	}

	[Fact]
	public async Task Take_Attendance_Entry_Writer_Should_Save_Explicit_Absent_When_No_Punches_In_Time_Based_Mode()
	{
		var employee = await CreateEmployeeAsync("absentnopunch");
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.None);

		await WithUnitOfWorkAsync(async () =>
		{
			var writer = GetRequiredService<TakeAttendanceEntryWriter>();
			await writer.SaveEntryAsync(
				new ParsedTakeAttendanceEntry(
					new TakeAttendanceSaveEntryDto
					{
						EmployeeId = employee.Id,
						StatusWire = "ABSENT",
						InTime = null,
						OutTime = null,
						Remarks = "Absent without punches"
					},
					EmployeeAttendanceStatusType.FullDayLeave),
				attendanceDate,
				new UserScopeContext { IsAdmin = true, UserId = _guidGenerator.Create(), TenantId = _currentTenant.Id },
				new Dictionary<Guid, Guid?>());
		});

		await WithUnitOfWorkAsync(async () =>
		{
			var query = await _employeeAttendanceRepository.GetQueryableAsync();
			var attendance = query.Single(x => x.EmployeeId == employee.Id && x.AttendanceDate == attendanceDate);
			attendance.StatusType.ShouldBe(EmployeeAttendanceStatusType.FullDayLeave);
			attendance.TimeIn.ShouldBeNull();
			attendance.TimeOut.ShouldBeNull();
		});
	}

	[Fact]
	public async Task Integration_Writer_Should_Skip_Time_Based_Evaluation_In_Manual_Status_Mode()
	{
		var shiftId = await EnsureEightToFiveShiftExistsAsync();
		var employee = await CreateEmployeeAsync("integrationmanual", shiftId);
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.DeductHalfDay,
			captureMode: AttendanceCaptureMode.ManualStatus);

		await WithUnitOfWorkAsync(async () =>
		{
			var writer = GetRequiredService<AttendanceIntegrationRunAttendanceWriter>();
			var outcome = await writer.UpsertAttendanceAndBuildImportRowsAsync(
				[
					new ParsedIntegrationRow(
						new AttendanceIntegrationRunRowDto
						{
							EmployeeId = employee.Id,
							AttendanceDate = attendanceDate.ToString("dd-MM-yyyy"),
							TimeIn = new DateTimeOffset(attendanceDate.Year, attendanceDate.Month, attendanceDate.Day, 8, 35, 0, TimeSpan.Zero).ToString("o"),
							TimeOut = new DateTimeOffset(attendanceDate.Year, attendanceDate.Month, attendanceDate.Day, 17, 30, 0, TimeSpan.Zero).ToString("o"),
							Status = EmployeeAttendanceStatusType.Present,
							Remarks = "Manual mode integration row"
						},
						attendanceDate)
				],
				_guidGenerator.Create());

			outcome.Created.ShouldBe(1);
			outcome.ImportRows.Single().Status.ShouldBe(EmployeeAttendanceStatusType.Present.ToString());
			outcome.ImportRows.Single().Warnings.ShouldBeEmpty();
		});

		await WithUnitOfWorkAsync(async () =>
		{
			var query = await _employeeAttendanceRepository.GetQueryableAsync();
			var attendance = query.Single(x => x.EmployeeId == employee.Id && x.AttendanceDate == attendanceDate);
			attendance.StatusType.ShouldBe(EmployeeAttendanceStatusType.Present);
			attendance.TimeIn.ShouldBeNull();
			attendance.TimeOut.ShouldBeNull();
		});
	}

	[Fact]
	public async Task Integration_Writer_Should_Clear_Previous_Punches_When_Replacement_Row_Has_No_Times()
	{
		var shiftId = await EnsureEightToFiveShiftExistsAsync();
		var employee = await CreateEmployeeAsync("integrationclearpunch", shiftId);
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.None,
			captureMode: AttendanceCaptureMode.ManualStatus);
		await InsertAttendanceAsync(
			employee.Id,
			attendanceDate,
			EmployeeAttendanceStatusType.Present,
			new TimeOnly(8, 0),
			new TimeOnly(17, 30));

		await WithUnitOfWorkAsync(async () =>
		{
			var writer = GetRequiredService<AttendanceIntegrationRunAttendanceWriter>();
			var outcome = await writer.UpsertAttendanceAndBuildImportRowsAsync(
				[
					new ParsedIntegrationRow(
						new AttendanceIntegrationRunRowDto
						{
							EmployeeId = employee.Id,
							AttendanceDate = attendanceDate.ToString("dd-MM-yyyy"),
							TimeIn = null,
							TimeOut = null,
							Status = EmployeeAttendanceStatusType.FullDayLeave,
							Remarks = "Replacement row intentionally has no punches"
						},
						attendanceDate)
				],
				_guidGenerator.Create());

			outcome.Created.ShouldBe(0);
			outcome.Replaced.ShouldBe(1);
		});

		await WithUnitOfWorkAsync(async () =>
		{
			var query = await _employeeAttendanceRepository.GetQueryableAsync();
			var attendance = query.Single(x => x.EmployeeId == employee.Id && x.AttendanceDate == attendanceDate);
			attendance.StatusType.ShouldBe(EmployeeAttendanceStatusType.FullDayLeave);
			attendance.TimeIn.ShouldBeNull();
			attendance.TimeOut.ShouldBeNull();
		});
	}

	[Fact]
	public async Task Should_Keep_Present_When_Late_Arrival_Equals_Grace_Time()
	{
		var shiftId = await EnsureEightToFiveShiftExistsAsync();
		var employee = await CreateEmployeeAsync("lateboundary", shiftId);
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.DeductHalfDay);
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDate:dd-MM-yyyy},{employee.EmployeeCode},08:30 AM,05:30 PM,,Late at grace,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("late-boundary.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeTrue();
		var row = preview.Rows.Single();
		row.Status.ShouldBe(EmployeeAttendanceStatusType.Present.ToString());
		row.Warnings.ShouldNotContain(x => x.Contains("late arrival", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Should_Keep_Present_When_Late_Penalty_Is_None()
	{
		var shiftId = await EnsureEightToFiveShiftExistsAsync();
		var employee = await CreateEmployeeAsync("latenopenalty", shiftId);
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.None);
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDate:dd-MM-yyyy},{employee.EmployeeCode},08:35 AM,05:30 PM,,Late no penalty,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("late-no-penalty.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeTrue();
		var row = preview.Rows.Single();
		row.Status.ShouldBe(EmployeeAttendanceStatusType.Present.ToString());
	}

	[Theory]
	[InlineData(AttendancePenaltyType.Warning, "warning action")]
	[InlineData(AttendancePenaltyType.SalaryDeduction, "salary deduction marker")]
	public async Task Should_Keep_Present_And_Warn_When_Late_Penalty_Does_Not_Change_Status(
		AttendancePenaltyType penaltyType,
		string warningText)
	{
		var shiftId = await EnsureEightToFiveShiftExistsAsync();
		var employee = await CreateEmployeeAsync($"late{penaltyType}", shiftId);
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: penaltyType);
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDate:dd-MM-yyyy},{employee.EmployeeCode},08:35 AM,05:30 PM,,Late non-status penalty,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("late-non-status-penalty.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeTrue();
		var row = preview.Rows.Single();
		row.Status.ShouldBe(EmployeeAttendanceStatusType.Present.ToString());
		row.Warnings.ShouldContain(x => x.Contains(warningText, StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Should_Warn_But_Keep_Present_When_Late_Count_Is_Below_Trigger()
	{
		var shiftId = await EnsureEightToFiveShiftExistsAsync();
		var employee = await CreateEmployeeAsync("latebelowtrigger", shiftId);
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.DeductHalfDay,
			maxLateAllowedPerMonth: 1);
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDate:dd-MM-yyyy},{employee.EmployeeCode},08:35 AM,05:30 PM,,First late below trigger,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("late-below-trigger.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeTrue();
		var row = preview.Rows.Single();
		row.Status.ShouldBe(EmployeeAttendanceStatusType.Present.ToString());
		row.Warnings.ShouldContain(x => x.Contains("late arrival", StringComparison.OrdinalIgnoreCase));
		row.Warnings.ShouldNotContain(x => x.Contains("Auto-marked Half Day", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Should_Apply_Late_Penalty_When_Monthly_Late_Count_Reaches_Trigger()
	{
		var shiftId = await EnsureEightToFiveShiftExistsAsync();
		var employee = await CreateEmployeeAsync("latetrigger", shiftId);
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.DeductHalfDay,
			maxLateAllowedPerMonth: 1);
		await InsertAttendanceAsync(
			employee.Id,
			attendanceDate.AddDays(-1),
			EmployeeAttendanceStatusType.Present,
			new TimeOnly(8, 35),
			new TimeOnly(17, 30));
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDate:dd-MM-yyyy},{employee.EmployeeCode},08:35 AM,05:30 PM,,Second late triggers,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("late-trigger.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeTrue();
		var row = preview.Rows.Single();
		row.Status.ShouldBe(EmployeeAttendanceStatusType.HalfDayLeave.ToString());
		row.Warnings.ShouldContain(x => x.Contains("Auto-marked Half Day", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Should_Apply_Late_Penalty_To_Second_Late_In_Same_Import_Batch()
	{
		var shiftId = await EnsureEightToFiveShiftExistsAsync();
		var employee = await CreateEmployeeAsync("latebatch", shiftId);
		var firstAttendanceDate = await GetCoveredAttendanceDateAsync();
		var secondAttendanceDate = firstAttendanceDate.AddDays(1);
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.DeductHalfDay,
			maxLateAllowedPerMonth: 1);
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{firstAttendanceDate:dd-MM-yyyy},{employee.EmployeeCode},08:35 AM,05:30 PM,,First batch late,{employee.FullName},{employee.MobileNo}",
			$"{secondAttendanceDate:dd-MM-yyyy},{employee.EmployeeCode},08:35 AM,05:30 PM,,Second batch late,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("late-batch-trigger.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeTrue();
		var firstRow = preview.Rows.Single(x => x.AttendanceDate == firstAttendanceDate.ToString("dd-MM-yyyy"));
		var secondRow = preview.Rows.Single(x => x.AttendanceDate == secondAttendanceDate.ToString("dd-MM-yyyy"));
		firstRow.Status.ShouldBe(EmployeeAttendanceStatusType.Present.ToString());
		firstRow.Warnings.ShouldContain(x => x.Contains("late arrival", StringComparison.OrdinalIgnoreCase));
		firstRow.Warnings.ShouldNotContain(x => x.Contains("Auto-marked Half Day", StringComparison.OrdinalIgnoreCase));
		secondRow.Status.ShouldBe(EmployeeAttendanceStatusType.HalfDayLeave.ToString());
		secondRow.Warnings.ShouldContain(x => x.Contains("Auto-marked Half Day", StringComparison.OrdinalIgnoreCase));

		await _attendanceImportAppService.ConfirmAsync(new AttendanceImportConfirmRequestDto
		{
			PreviewToken = preview.PreviewToken!
		});

		await WithUnitOfWorkAsync(async () =>
		{
			var query = await _employeeAttendanceRepository.GetQueryableAsync();
			var attendance = query
				.Where(x => x.EmployeeId == employee.Id && (x.AttendanceDate == firstAttendanceDate || x.AttendanceDate == secondAttendanceDate))
				.ToDictionary(x => x.AttendanceDate);
			attendance[firstAttendanceDate].StatusType.ShouldBe(EmployeeAttendanceStatusType.Present);
			attendance[secondAttendanceDate].StatusType.ShouldBe(EmployeeAttendanceStatusType.HalfDayLeave);
		});
	}

	[Fact]
	public async Task Should_Not_Upgrade_Lower_Worked_Time_Status_When_Late_Penalty_Is_Less_Severe()
	{
		var shiftId = await EnsureEightToFiveShiftExistsAsync();
		var employee = await CreateEmployeeAsync("latenoupgrade", shiftId);
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.DeductHalfDay,
			absentThresholdPercentage: 50);
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDate:dd-MM-yyyy},{employee.EmployeeCode},08:35 AM,08:45 AM,,Late but absent by worked time,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("late-no-upgrade.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeTrue();
		var row = preview.Rows.Single();
		row.Status.ShouldBe(EmployeeAttendanceStatusType.FullDayLeave.ToString());
		row.Warnings.ShouldContain(x => x.Contains("Auto-marked Absent", StringComparison.OrdinalIgnoreCase));
		row.Warnings.ShouldContain(x => x.Contains("late arrival", StringComparison.OrdinalIgnoreCase));
		row.Warnings.ShouldNotContain(x => x.Contains("Auto-marked Half Day", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Should_Mark_Half_Day_When_One_Punch_And_Missing_Punch_Policy_Is_Half_Day()
	{
		var employee = await CreateEmployeeAsync("missinghalf");
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.None,
			missingPunchBehavior: AttendanceMissingPunchBehavior.MarkHalfDay);
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDate:dd-MM-yyyy},{employee.EmployeeCode},08:00 AM,,,One punch half day,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("missing-half-day.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeTrue();
		var row = preview.Rows.Single();
		row.Status.ShouldBe(EmployeeAttendanceStatusType.HalfDayLeave.ToString());
		row.Warnings.ShouldContain(x => x.Contains("missing punch", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Should_Mark_Absent_When_One_Punch_And_Missing_Punch_Policy_Is_Absent()
	{
		var employee = await CreateEmployeeAsync("missingabsent");
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.None,
			missingPunchBehavior: AttendanceMissingPunchBehavior.MarkAbsent);
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDate:dd-MM-yyyy},{employee.EmployeeCode},08:00 AM,,,One punch absent,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("missing-absent.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeTrue();
		var row = preview.Rows.Single();
		row.Status.ShouldBe(EmployeeAttendanceStatusType.FullDayLeave.ToString());
		row.Warnings.ShouldContain(x => x.Contains("missing punch", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Should_Return_Row_Error_When_One_Punch_And_Missing_Punch_Policy_Requires_Review()
	{
		var employee = await CreateEmployeeAsync("missingreview");
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.None,
			missingPunchBehavior: AttendanceMissingPunchBehavior.RequiresReview);
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDate:dd-MM-yyyy},{employee.EmployeeCode},08:00 AM,,,One punch review,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("missing-review.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeFalse();
		preview.Rows.Single().Errors.ShouldContain(x => x.Contains("Time In and Time Out", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Should_Not_Warn_When_Early_Exit_Is_Within_Grace()
	{
		var shiftId = await EnsureEightToFiveShiftExistsAsync();
		var employee = await CreateEmployeeAsync("earlywithin", shiftId);
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.None,
			earlyExitEnabled: true,
			earlyExitGraceTime: 15);
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDate:dd-MM-yyyy},{employee.EmployeeCode},08:00 AM,05:20 PM,,Within early grace,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("early-within-grace.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeTrue();
		var row = preview.Rows.Single();
		row.Status.ShouldBe(EmployeeAttendanceStatusType.Present.ToString());
		row.Warnings.ShouldNotContain(x => x.Contains("early exit", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Should_Warn_When_Early_Exit_Exceeds_Grace_Without_Downgrading_Status()
	{
		var shiftId = await EnsureEightToFiveShiftExistsAsync();
		var employee = await CreateEmployeeAsync("earlybeyond", shiftId);
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			penaltyType: AttendancePenaltyType.None,
			earlyExitEnabled: true,
			earlyExitGraceTime: 15);
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDate:dd-MM-yyyy},{employee.EmployeeCode},08:00 AM,05:00 PM,,Beyond early grace,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("early-beyond-grace.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeTrue();
		var row = preview.Rows.Single();
		row.Status.ShouldBe(EmployeeAttendanceStatusType.Present.ToString());
		row.Warnings.ShouldContain(x => x.Contains("early exit", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Should_Not_Apply_Late_Arrival_Penalty_To_Early_Exit_Warnings()
	{
		var shiftId = await EnsureEightToFiveShiftExistsAsync();
		var employee = await CreateEmployeeAsync("earlypenalty", shiftId);
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await SetDefaultAttendancePolicyRulesAsync(
			graceTime: 30,
			maxLateAllowedPerMonth: 0,
			penaltyType: AttendancePenaltyType.DeductFullDay,
			penaltyCountThreshold: 1,
			earlyExitEnabled: true,
			earlyExitGraceTime: 15);
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDate:dd-MM-yyyy},{employee.EmployeeCode},08:00 AM,05:00 PM,,Early exit only,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("early-exit-penalty.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeTrue();
		var row = preview.Rows.Single();
		row.Status.ShouldBe(EmployeeAttendanceStatusType.Present.ToString());
		row.Warnings.ShouldContain(x => x.Contains("early exit", StringComparison.OrdinalIgnoreCase));
		row.Warnings.ShouldNotContain(x => x.Contains("Auto-marked", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Should_Return_Row_Error_When_Employee_Has_No_Shift_For_Worked_Time_Evaluation()
	{
		var employee = await CreateEmployeeAsync("noshift");
		var attendanceDate = await GetCoveredAttendanceDateAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var entity = await _employeeRepository.GetAsync(employee.Id);
			entity.ShiftId = null;
			await _employeeRepository.UpdateAsync(entity, autoSave: true);
		});
		var csv = BuildCsv(
			"Attendance Date,Employee Code,Time In,Time Out,Status,Remarks,Employee Name,Contact No",
			$"{attendanceDate:dd-MM-yyyy},{employee.EmployeeCode},09:00 AM,05:00 PM,,Missing shift,{employee.FullName},{employee.MobileNo}");

		var preview = await _attendanceImportAppService.PreviewAsync("missing-shift.csv", "text/csv", csv);

		preview.CanConfirm.ShouldBeFalse();
		preview.Summary.InvalidRows.ShouldBe(1);
		preview.Rows.Single().Errors.ShouldContain(x => x.Contains("Employee shift is required", StringComparison.OrdinalIgnoreCase));
	}

	private async Task<EmployeePersonalInfoDto> CreateEmployeeAsync(string suffix, Guid? shiftIdOverride = null)
	{
		var roleId = await EnsureRoleExistsAsync();
		var shiftId = shiftIdOverride ?? await EnsureShiftExistsAsync();
		var academicYear = await EnsurePolicyBaselineAsync();
		var organizationId = await EnsurePolicyBaselineStageAsync(academicYear.Id);
		var unique = Guid.NewGuid().ToString("N")[..6];

		var employee = await _employeePersonalInfoAppService.CreateAsync(new CreateEmployeePersonalInfoDto
		{
			FirstName = $"Emp{suffix}{unique}",
			LastName = "Import",
			MobileNo = BuildPhoneNumber(),
			Email = BuildEmail(suffix),
			PanNo = UniquePan(),
			AdharNo = UniqueAdhar(),
			DateOfBirth = new DateOnly(1990, 5, 15),
			GenderType = GenderType.Male,
			DateOfJoining = new DateOnly(2024, 1, 1),
			EmployeeStatusType = EmployeeStatusType.Active,
			RoleId = roleId,
			ShiftId = shiftId
		});
		await _employeeOrganizationAssignmentRepository.InsertAsync(
			new EmployeeOrganizationAssignment(Guid.NewGuid(), _currentTenant.Id, employee.Id, organizationId),
			autoSave: true);
		return employee;
	}

	private async Task<Guid> EnsurePolicyBaselineStageAsync(Guid academicYearId)
	{
		var boards = await _educationBoardRepository.GetListAsync(x => x.AcademicYearId == academicYearId);
		foreach (var existingBoard in boards)
		{
			var existingMedium = (await _educationMediumRepository.GetListAsync(x => x.EducationBoardId == existingBoard.Id))
				.FirstOrDefault();
			if (existingMedium == null)
			{
				continue;
			}

			var existingStage = (await _educationMediumStageRepository.GetListAsync(x => x.EducationMediumId == existingMedium.Id))
				.FirstOrDefault();
			if (existingStage != null)
			{
				return existingStage.Id;
			}
		}

		var board = boards.FirstOrDefault();
		if (board == null)
		{
			board = await _educationBoardManager.CreateAsync(academicYearId, EducationBoardType.CBSE);
			await _educationBoardRepository.InsertAsync(board, autoSave: true);
		}

		var medium = (await _educationMediumRepository.GetListAsync(x => x.EducationBoardId == board.Id))
			.FirstOrDefault();
		if (medium == null)
		{
			medium = await _educationMediumManager.CreateAsync(board.Id, $"ImportMedium-{Guid.NewGuid():N}"[..24]);
			await _educationMediumRepository.InsertAsync(medium, autoSave: true);
		}

		var stage = await _educationMediumStageManager.CreateAsync(medium.Id, $"ImportStage-{Guid.NewGuid():N}"[..23]);
		await _educationMediumStageRepository.InsertAsync(stage, autoSave: true);
		return stage.Id;
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

	private async Task<Guid> EnsureFullDayShiftExistsAsync()
	{
		var existing = (await _shiftRepository.GetListAsync())
			.FirstOrDefault(x => x.ShiftName == "Attendance Import Full Day Test Shift");
		if (existing != null)
		{
			return existing.Id;
		}

		var shift = new ShiftManagement
		{
			ShiftName = "Attendance Import Full Day Test Shift",
			StartTime = new TimeOnly(7, 0),
			EndTime = new TimeOnly(17, 30)
		};

		await _shiftRepository.InsertAsync(shift, autoSave: true);
		return shift.Id;
	}

	private async Task<Guid> EnsureEightToFiveShiftExistsAsync()
	{
		var existing = (await _shiftRepository.GetListAsync())
			.FirstOrDefault(x => x.ShiftName == "Attendance Import Eight To Five Test Shift");
		if (existing != null)
		{
			return existing.Id;
		}

		var shift = new ShiftManagement
		{
			ShiftName = "Attendance Import Eight To Five Test Shift",
			StartTime = new TimeOnly(8, 0),
			EndTime = new TimeOnly(17, 30)
		};

		await _shiftRepository.InsertAsync(shift, autoSave: true);
		return shift.Id;
	}

	private async Task<DateOnly> GetCoveredAttendanceDateAsync(int monthOffset = 0)
	{
		var academicYear = await EnsurePolicyBaselineAsync();
		return DateOnly.FromDateTime(academicYear.StartDate.AddMonths(monthOffset).AddDays(14));
	}

	private async Task SetDefaultAttendancePolicyRulesAsync(
		int graceTime,
		AttendancePenaltyType penaltyType,
		int? maxLateAllowedPerMonth = null,
		int? penaltyCountThreshold = null,
		AttendanceMissingPunchBehavior? missingPunchBehavior = null,
		bool? earlyExitEnabled = null,
		int? earlyExitGraceTime = null,
		double? absentThresholdPercentage = null,
		AttendanceCaptureMode? captureMode = null)
	{
		await EnsurePolicyBaselineAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var policy = (await _attendancePolicyRepository.GetListAsync()).Single(x => x.IsDefault);
			policy.Update(
				policy.WorkCalendarPolicyId,
				policy.Name,
				policy.Status,
				policy.IsDefault,
				captureMode ?? AttendanceCaptureMode.Integrated,
				policy.FullDayThresholdPercentage,
				policy.HalfDayThresholdPercentage,
				absentThresholdPercentage ?? 0,
				missingPunchBehavior ?? AttendanceMissingPunchBehavior.RequiresReview,
				policy.ManualCorrectionAllowed,
				graceTime,
				maxLateAllowedPerMonth ?? 0,
				penaltyType,
				penaltyCountThreshold ?? 0,
				earlyExitEnabled ?? false,
				earlyExitGraceTime ?? 0,
				policy.OvertimeEnabled,
				policy.OvertimeRate,
				policy.MinOvertimeMinutes,
				policy.HolidayWorkBehavior,
				policy.PayrollPreviewEnabled,
				penaltyType == AttendancePenaltyType.SalaryDeduction ? 1m : null);
			await _attendancePolicyRepository.UpdateAsync(policy, autoSave: true);
		});
	}

	private async Task InsertAttendanceAsync(
		Guid employeeId,
		DateOnly attendanceDate,
		EmployeeAttendanceStatusType status,
		TimeOnly? timeIn,
		TimeOnly? timeOut)
	{
		await WithUnitOfWorkAsync(async () =>
		{
			await _employeeAttendanceRepository.InsertAsync(new EmployeeAttendance
			{
				TenantId = _currentTenant.Id,
				EmployeeId = employeeId,
				AttendanceDate = attendanceDate,
				StatusType = status,
				TimeIn = timeIn.HasValue
					? new DateTimeOffset(attendanceDate.Year, attendanceDate.Month, attendanceDate.Day, timeIn.Value.Hour, timeIn.Value.Minute, 0, TimeSpan.Zero)
					: null,
				TimeOut = timeOut.HasValue
					? new DateTimeOffset(attendanceDate.Year, attendanceDate.Month, attendanceDate.Day, timeOut.Value.Hour, timeOut.Value.Minute, 0, TimeSpan.Zero)
					: null
			}, autoSave: true);
		});
	}

	private async Task<AcademicYear> EnsurePolicyBaselineAsync()
	{
		const string academicYearName = "Attendance Import Policy Test 2026";
		var existing = (await _academicYearRepository.GetListAsync())
			.FirstOrDefault(x => x.Name == academicYearName);
		if (existing != null)
		{
			return existing;
		}

		var academicYear = (AcademicYear)Activator.CreateInstance(
			typeof(AcademicYear),
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
			binder: null,
			args:
			[
				_guidGenerator.Create(),
				_currentTenant.Id,
				academicYearName,
				new DateTime(2026, 1, 1),
				new DateTime(2026, 12, 31)
			],
			culture: null)!;
		await _academicYearRepository.InsertAsync(academicYear, autoSave: true);

		var calendar = new WorkCalendarPolicy(
			_guidGenerator.Create(),
			_currentTenant.Id,
			academicYear.Id,
			"Attendance Import Test Calendar",
			HrPolicyStatus.Active,
			academicYear.StartDate,
			academicYear.EndDate,
			AttendanceWeeklyOffPattern.Sunday,
			[],
			WorkCalendarHolidaySyncMode.Snapshot);
		await _workCalendarPolicyRepository.InsertAsync(calendar, autoSave: true);

		await _payrollPolicyRepository.InsertAsync(new PayrollPolicy(
			_guidGenerator.Create(),
			_currentTenant.Id,
			academicYear.Id,
			calendar.Id,
			"Attendance Import Test Payroll",
			HrPolicyStatus.Active,
			isDefault: true,
			DayCalculationType.WorkingDays,
			manualDays: null,
			manualMonthDays: [],
			SaturdayWorkPolicy.FullDay,
			holidayOverrides: [],
			lockedMonthSettings: []), autoSave: true);

		await _attendancePolicyRepository.InsertAsync(new AttendancePolicy(
			_guidGenerator.Create(),
			_currentTenant.Id,
			academicYear.Id,
			calendar.Id,
			"Attendance Import Test Attendance",
			HrPolicyStatus.Active,
			true,
			AttendanceCaptureMode.Integrated,
			75,
			50,
			0,
			AttendanceMissingPunchBehavior.RequiresReview,
			true,
			0,
			0,
			AttendancePenaltyType.None,
			0,
			false,
			0,
			false,
			null,
			null,
			AttendanceHolidayWorkBehavior.CompOff,
			true), autoSave: true);

		return academicYear;
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






