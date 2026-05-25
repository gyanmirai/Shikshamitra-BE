using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Groups;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public class HrPolicyAppService_Tests : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly IWorkCalendarPolicyAppService _workCalendars;
	private readonly IPayrollPolicyAppService _payrollPolicies;
	private readonly IAttendancePolicyAppService _attendancePolicies;
	private readonly IHrPolicyAssignmentAppService _assignments;
	private readonly IPolicyHolidayImpactAppService _holidayImpact;
	private readonly IHrPolicySafetyAppService _policySafety;
	private readonly IHrPolicyVersionAppService _policyVersions;
	private readonly IHrAuditTrailAppService _auditTrail;
	private readonly IEmployeePersonalInfoAppService _employeePersonalInfo;
	private readonly IIdentityRoleRepository _roleRepository;
	private readonly IRepository<WorkCalendarPolicy, Guid> _workCalendarPolicyRepository;
	private readonly IRepository<ShiftManagement, Guid> _shiftRepository;
	private readonly IAcademicYearRepository _academicYearRepository;
	private readonly AcademicYearManager _academicYearManager;
	private readonly IRepository<HolidayType, Guid> _holidayTypeRepository;
	private readonly IRepository<Holiday, Guid> _holidayRepository;
	private readonly IRepository<Group, Guid> _groupRepository;
	private readonly ICurrentTenant _currentTenant;
	private readonly IUnitOfWorkManager _unitOfWorkManager;
	private readonly IGuidGenerator _guidGenerator;

	public HrPolicyAppService_Tests()
	{
		_workCalendars = GetRequiredService<IWorkCalendarPolicyAppService>();
		_payrollPolicies = GetRequiredService<IPayrollPolicyAppService>();
		_attendancePolicies = GetRequiredService<IAttendancePolicyAppService>();
		_assignments = GetRequiredService<IHrPolicyAssignmentAppService>();
		_holidayImpact = GetRequiredService<IPolicyHolidayImpactAppService>();
		_policySafety = GetRequiredService<IHrPolicySafetyAppService>();
		_policyVersions = GetRequiredService<IHrPolicyVersionAppService>();
		_auditTrail = GetRequiredService<IHrAuditTrailAppService>();
		_employeePersonalInfo = GetRequiredService<IEmployeePersonalInfoAppService>();
		_roleRepository = GetRequiredService<IIdentityRoleRepository>();
		_workCalendarPolicyRepository = GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>();
		_shiftRepository = GetRequiredService<IRepository<ShiftManagement, Guid>>();
		_academicYearRepository = GetRequiredService<IAcademicYearRepository>();
		_academicYearManager = GetRequiredService<AcademicYearManager>();
		_holidayTypeRepository = GetRequiredService<IRepository<HolidayType, Guid>>();
		_holidayRepository = GetRequiredService<IRepository<Holiday, Guid>>();
		_groupRepository = GetRequiredService<IRepository<Group, Guid>>();
		_currentTenant = GetRequiredService<ICurrentTenant>();
		_unitOfWorkManager = GetRequiredService<IUnitOfWorkManager>();
		_guidGenerator = GetRequiredService<IGuidGenerator>();
	}

	[Fact]
	public async Task WorkCalendar_Should_Reject_Manual_Holiday_Overlapping_Included_Central_Holiday()
	{
		var academicYearId = await CreateAcademicYearAsync();
		var input = BuildWorkCalendarInput(academicYearId, [
			BuildWorkCalendarHoliday(academicYearId, "Central Holiday", new DateTime(2026, 5, 10), source: "central_calendar", calendarHolidayId: Guid.NewGuid()),
			BuildWorkCalendarHoliday(academicYearId, "Manual Holiday", new DateTime(2026, 5, 10), source: "manual")
		]);

		var ex = await Should.ThrowAsync<BusinessException>(() => _workCalendars.CreateAsync(input));

		ex.Code.ShouldBe("ShikshaMitra:WorkCalendarHolidayOverlap");
		ex.Data["holidayName"].ShouldBe("Manual Holiday");
		ex.Data["conflictingHolidayName"].ShouldBe("Central Holiday");
		ex.Data["fromDate"].ShouldBe("2026-05-10");
		ex.Data["toDate"].ShouldBe("2026-05-10");
	}

	[Fact]
	public async Task WorkCalendar_Should_Reject_Overlapping_Multi_Day_Holiday_Ranges()
	{
		var academicYearId = await CreateAcademicYearAsync();
		var input = BuildWorkCalendarInput(academicYearId, [
			BuildWorkCalendarHoliday(academicYearId, "Quarter Break", new DateTime(2026, 6, 1), new DateTime(2026, 6, 3), source: "manual"),
			BuildWorkCalendarHoliday(academicYearId, "Local Closure", new DateTime(2026, 6, 3), new DateTime(2026, 6, 5), source: "manual")
		]);

		var ex = await Should.ThrowAsync<BusinessException>(() => _workCalendars.CreateAsync(input));

		ex.Code.ShouldBe("ShikshaMitra:WorkCalendarHolidayOverlap");
		ex.Data["fromDate"].ShouldBe("2026-06-03");
		ex.Data["toDate"].ShouldBe("2026-06-03");
	}

	[Fact]
	public async Task WorkCalendar_Should_Allow_Updating_Unchanged_Manual_Holiday()
	{
		var academicYearId = await CreateAcademicYearAsync();
		var input = BuildWorkCalendarInput(academicYearId, [
			BuildWorkCalendarHoliday(academicYearId, "Manual Holiday", new DateTime(2026, 7, 1), source: "manual")
		]);
		var created = await _workCalendars.CreateAsync(input);

		var updated = await _workCalendars.UpdateAsync(created.Id, input);

		updated.Holidays.Count.ShouldBe(1);
		updated.Holidays.Single().HolidayName.ShouldBe("Manual Holiday");
	}

	[Fact]
	public async Task WorkCalendar_Should_Allow_Manual_Holiday_Over_Removed_Central_Holiday()
	{
		var academicYearId = await CreateAcademicYearAsync();
		var input = BuildWorkCalendarInput(academicYearId, [
			BuildWorkCalendarHoliday(academicYearId, "Removed Central Holiday", new DateTime(2026, 8, 15), source: "central_calendar", calendarHolidayId: Guid.NewGuid(), isIncluded: false),
			BuildWorkCalendarHoliday(academicYearId, "Manual Replacement", new DateTime(2026, 8, 15), source: "manual")
		]);

		var created = await _workCalendars.CreateAsync(input);

		created.Holidays.Count.ShouldBe(2);
		created.HolidaySummary.IncludedCount.ShouldBe(1);
		created.HolidaySummary.RemovedCount.ShouldBe(1);
	}

	[Fact]
	public async Task WorkCalendar_Should_Allow_Adjacent_Non_Overlapping_Holiday_Ranges()
	{
		var academicYearId = await CreateAcademicYearAsync();
		var input = BuildWorkCalendarInput(academicYearId, [
			BuildWorkCalendarHoliday(academicYearId, "First Break", new DateTime(2026, 9, 1), new DateTime(2026, 9, 2), source: "manual"),
			BuildWorkCalendarHoliday(academicYearId, "Second Break", new DateTime(2026, 9, 3), new DateTime(2026, 9, 4), source: "manual")
		]);

		var created = await _workCalendars.CreateAsync(input);

		created.Holidays.Count.ShouldBe(2);
		created.HolidaySummary.IncludedCount.ShouldBe(2);
	}

	[Fact]
	public async Task Holiday_Impact_Should_Use_Default_Policies_When_Ids_Are_Omitted()
	{
		var context = await CreatePolicyContextAsync();
		var holiday = await CreateHolidayAsync(context.AcademicYearId, "DefaultFallback", new DateTime(2026, 5, 10));

		var result = await _holidayImpact.GetPreviewAsync(
			context.AcademicYearId,
			workCalendarPolicyId: null,
			payrollPolicyId: null,
			attendancePolicyId: null,
			fromDate: new DateTime(2026, 5, 1),
			toDate: new DateTime(2026, 5, 31),
			month: null);

		result.PayrollPolicyId.ShouldBe(context.PayrollPolicy.Id);
		result.AttendancePolicyId.ShouldBe(context.AttendancePolicy.Id);
		result.WorkCalendarPolicyId.ShouldBe(context.WorkCalendar.Id);
		result.Items.ShouldContain(x => x.CalendarHolidayId == holiday.Id);
		var row = result.Items.Single(x => x.CalendarHolidayId == holiday.Id);
		row.PayrollImpact.ShouldBe("paid");
		row.AttendanceImpact.ShouldBe("non_working_paid");
		row.PayrollOverrideSource.ShouldBe("policy_default");
		row.PayrollBasisDayDelta.ShouldBe(0m);
		row.AttendanceBehaviorText.ShouldNotBeNullOrWhiteSpace();
		row.PayrollBasisImpactText.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Holiday_Impact_Should_Return_Work_Calendar_Context_And_Date_Filtered_Rows()
	{
		var context = await CreatePolicyContextAsync();
		await CreateHolidayAsync(context.AcademicYearId, "OutsideRange", new DateTime(2026, 4, 10));
		var inside = await CreateHolidayAsync(context.AcademicYearId, "InsideRange", new DateTime(2026, 5, 12));

		var result = await _holidayImpact.GetPreviewAsync(
			context.AcademicYearId,
			context.WorkCalendar.Id,
			context.PayrollPolicy.Id,
			context.AttendancePolicy.Id,
			fromDate: new DateTime(2026, 5, 1),
			toDate: new DateTime(2026, 5, 31),
			month: null);

		result.Items.ShouldAllBe(x => x.Date.Month == 5);
		result.Items.ShouldContain(x => x.CalendarHolidayId == inside.Id);
		result.Items.ShouldAllBe(x => x.WorkCalendarPolicyId == context.WorkCalendar.Id);
		result.Items.ShouldAllBe(x => !string.IsNullOrWhiteSpace(x.CalendarSource));
	}

	[Fact]
	public async Task Holiday_Impact_Should_Surface_Payroll_Override_And_Attendance_Difference()
	{
		var context = await CreatePolicyContextAsync(
			attendanceHolidayWorkBehavior: "ot_payout",
			payrollHolidayIncluded: false);
		var holiday = await CreateHolidayAsync(context.AcademicYearId, "OverrideHoliday", new DateTime(2026, 5, 18));

		var payroll = await _payrollPolicies.UpdateAsync(context.PayrollPolicy.Id, new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = context.PayrollPolicy.Name,
			Status = "active",
			IsDefault = true,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day",
			HolidayOverrides = new List<PayrollPolicyHolidayOverrideDto>
			{
				new()
				{
					Date = new DateTime(2026, 5, 18),
					HolidayName = "OverrideHoliday",
					IncludedInPayroll = false,
					CalendarHolidayId = holiday.Id
				}
			}
		});

		var result = await _holidayImpact.GetPreviewAsync(
			context.AcademicYearId,
			context.WorkCalendar.Id,
			payroll.Id,
			context.AttendancePolicy.Id,
			fromDate: null,
			toDate: null,
			month: "2026-05");

		var row = result.Items.Single(x => x.CalendarHolidayId == holiday.Id);
		row.IsPunchRequired.ShouldBeTrue();
		row.IsAttendanceWorkingDay.ShouldBeTrue();
		row.PayrollImpact.ShouldBe("unpaid");
		row.PayableUnit.ShouldBe(0m);
		row.PayrollBasisDayDelta.ShouldBe(-1m);
		row.PayrollOverrideSource.ShouldBe("policy_override");
		row.HasDifference.ShouldBeTrue();
	}

	[Fact]
	public async Task Clone_Should_Create_Draft_Non_Default_Copies()
	{
		var context = await CreatePolicyContextAsync();

		var calendarClone = await _workCalendars.CloneAsync(context.WorkCalendar.Id, new HrPolicyCloneRequestDto());
		var payrollClone = await _payrollPolicies.CloneAsync(context.PayrollPolicy.Id, new HrPolicyCloneRequestDto());
		var attendanceClone = await _attendancePolicies.CloneAsync(context.AttendancePolicy.Id, new HrPolicyCloneRequestDto());

		calendarClone.Id.ShouldNotBe(context.WorkCalendar.Id);
		calendarClone.Status.ShouldBe("draft");
		payrollClone.Id.ShouldNotBe(context.PayrollPolicy.Id);
		payrollClone.Status.ShouldBe("draft");
		payrollClone.IsDefault.ShouldBeFalse();
		attendanceClone.Id.ShouldNotBe(context.AttendancePolicy.Id);
		attendanceClone.Status.ShouldBe("draft");
		attendanceClone.IsDefault.ShouldBeFalse();
	}

	[Fact]
	public async Task Policy_Versions_Should_Capture_Compare_And_Restore_Draft()
	{
		var context = await CreatePolicyContextAsync();
		var updated = await _payrollPolicies.UpdateAsync(context.PayrollPolicy.Id, new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"{context.PayrollPolicy.Name} Updated",
			Status = "active",
			IsDefault = true,
			DayCalculationType = "manual-days",
			ManualDays = 26,
			SaturdayWorkPolicy = "half-day"
		});

		var versions = await _policyVersions.GetListAsync("payroll", context.PayrollPolicy.Id, new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto
		{
			MaxResultCount = 10
		});

		versions.TotalCount.ShouldBeGreaterThanOrEqualTo(2);
		var latest = versions.Items.First(x => x.VersionNumber == updated.Version);
		var first = versions.Items.First(x => x.VersionNumber == context.PayrollPolicy.Version);
		var compare = await _policyVersions.CompareAsync("payroll", context.PayrollPolicy.Id, first.Id, latest.Id);
		compare.Differences.ShouldContain(x => x.Field.Equals("name", StringComparison.OrdinalIgnoreCase));
		compare.Differences.ShouldContain(x => x.Field.Equals("dayCalculationType", StringComparison.OrdinalIgnoreCase));
		compare.Differences.ShouldContain(x =>
			x.FieldKey == "dayCalculationType" &&
			x.Category == "Payroll rules" &&
			x.ToDisplayValue == "Manual Days");
		compare.Differences.ShouldAllBe(x =>
			!x.FromDisplayValue.Contains(context.WorkCalendar.Id.ToString(), StringComparison.OrdinalIgnoreCase) &&
			!x.ToDisplayValue.Contains(context.WorkCalendar.Id.ToString(), StringComparison.OrdinalIgnoreCase));

		var restored = await _policyVersions.RestoreDraftAsync("payroll", context.PayrollPolicy.Id, latest.Id, new HrPolicyRestoreDraftRequestDto
		{
			ChangeReason = "Regression test restore"
		});
		var restoredPayroll = restored.ShouldBeOfType<PayrollPolicyDto>();
		restoredPayroll.Id.ShouldNotBe(context.PayrollPolicy.Id);
		restoredPayroll.Status.ShouldBe("draft");
		restoredPayroll.IsDefault.ShouldBeFalse();
		restoredPayroll.DayCalculationType.ShouldBe("manual-days");
	}

	[Fact]
	public async Task WorkCalendar_Version_Restore_Should_Restore_Holiday_Snapshots()
	{
		var academicYearId = await CreateAcademicYearAsync();
		var created = await _workCalendars.CreateAsync(BuildWorkCalendarInput(academicYearId, [
			BuildWorkCalendarHoliday(academicYearId, "Founders Day", new DateTime(2026, 5, 1), source: "manual")
		]));
		var versions = await _policyVersions.GetListAsync("work_calendar", created.Id, new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto
		{
			MaxResultCount = 10
		});

		var restored = await _policyVersions.RestoreDraftAsync("work_calendar", created.Id, versions.Items.Single().Id, new HrPolicyRestoreDraftRequestDto
		{
			ChangeReason = "Restore holiday snapshot"
		});

		var restoredCalendar = restored.ShouldBeOfType<WorkCalendarPolicyDto>();
		restoredCalendar.Id.ShouldNotBe(created.Id);
		restoredCalendar.Holidays.Count.ShouldBe(1);
		restoredCalendar.Holidays.Single().HolidayName.ShouldBe("Founders Day");
		restoredCalendar.HolidaySummary.IncludedCount.ShouldBe(1);
	}

	[Fact]
	public async Task Hr_Audit_Trail_Should_Record_Policy_And_Assignment_Changes()
	{
		var context = await CreatePolicyContextAsync();
		await _payrollPolicies.UpdateAsync(context.PayrollPolicy.Id, new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"{context.PayrollPolicy.Name} Audit",
			Status = "active",
			IsDefault = true,
			DayCalculationType = "manual-days",
			ManualDays = 25,
			SaturdayWorkPolicy = "half-day"
		});
		var group = await CreateGroupAsync(context.AcademicYearId, "Audit");
		var assignment = await _assignments.CreateAsync(BuildAssignment(context, "group", group.Id.ToString("D"), group.Name));

		var policyEvents = await _auditTrail.GetEntityEventsAsync("payroll_policy", context.PayrollPolicy.Id, new HrAuditEventListRequestDto
		{
			MaxResultCount = 20
		});
		var assignmentEvents = await _auditTrail.GetEntityEventsAsync("policy_assignment", assignment.Id, new HrAuditEventListRequestDto
		{
			MaxResultCount = 20
		});

		policyEvents.Items.ShouldContain(x => x.Action == "created");
		policyEvents.Items.ShouldContain(x => x.Action == "edited" && x.ChangedFields.Any(field => field.Field.Equals("name", StringComparison.OrdinalIgnoreCase)));
		assignmentEvents.Items.ShouldContain(x => x.Action == "assigned");
		var assignmentContext = assignmentEvents.Items.First().ContextJson;
		assignmentContext.ShouldNotBeNull();
		assignmentContext.ShouldContain("targetType");
	}

	[Fact]
	public async Task Hr_Audit_Trail_Should_Filter_By_Entity_Action_Date_And_Academic_Year()
	{
		var context = await CreatePolicyContextAsync();
		var group = await CreateGroupAsync(context.AcademicYearId, "FilteredAudit");
		var assignment = await _assignments.CreateAsync(BuildAssignment(context, "group", group.Id.ToString("D"), group.Name));

		var result = await _auditTrail.GetListAsync(new HrAuditEventListRequestDto
		{
			AcademicYearId = context.AcademicYearId,
			EntityType = "policy_assignment",
			EntityId = assignment.Id,
			Action = "assigned",
			FromDate = DateTime.MinValue,
			ToDate = DateTime.MaxValue,
			MaxResultCount = 20
		});

		result.TotalCount.ShouldBeGreaterThanOrEqualTo(1);
		result.Items.ShouldAllBe(x => x.AcademicYearId == context.AcademicYearId);
		result.Items.ShouldAllBe(x => x.EntityType == "policy_assignment");
		result.Items.ShouldAllBe(x => x.EntityId == assignment.Id);
		result.Items.ShouldAllBe(x => x.Action == "assigned");
		result.Items.ShouldContain(x => x.ContextJson != null && x.ContextJson.Contains("targetType", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public async Task Unarchive_Should_NoOp_When_Policy_Is_Not_Archived()
	{
		var context = await CreatePolicyContextAsync();
		var before = await _auditTrail.GetEntityEventsAsync("payroll_policy", context.PayrollPolicy.Id, new HrAuditEventListRequestDto
		{
			MaxResultCount = 20
		});

		var result = await _payrollPolicies.UnarchiveAsync(context.PayrollPolicy.Id);
		var after = await _auditTrail.GetEntityEventsAsync("payroll_policy", context.PayrollPolicy.Id, new HrAuditEventListRequestDto
		{
			MaxResultCount = 20
		});

		result.Status.ShouldBe("active");
		result.Version.ShouldBe(context.PayrollPolicy.Version);
		after.TotalCount.ShouldBe(before.TotalCount);
		after.Items.ShouldNotContain(x => x.Action == "unarchived");
	}

	[Fact]
	public async Task Unarchive_Should_Write_Audit_When_Policy_Was_Archived()
	{
		var context = await CreatePolicyContextAsync();
		var policy = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Archive Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});

		await _payrollPolicies.DeleteAsync(policy.Id);
		var archived = await _auditTrail.GetEntityEventsAsync("payroll_policy", policy.Id, new HrAuditEventListRequestDto
		{
			MaxResultCount = 20
		});

		var result = await _payrollPolicies.UnarchiveAsync(policy.Id);
		var after = await _auditTrail.GetEntityEventsAsync("payroll_policy", policy.Id, new HrAuditEventListRequestDto
		{
			MaxResultCount = 20
		});

		result.Status.ShouldBe("draft");
		result.Version.ShouldBeGreaterThan(policy.Version);
		after.TotalCount.ShouldBe(archived.TotalCount + 1);
		after.Items.Count(x => x.Action == "unarchived").ShouldBe(1);
	}

	[Fact]
	public async Task Policy_Impact_Should_Return_Audit_And_Affected_Assignment_Summary()
	{
		var context = await CreatePolicyContextAsync();
		var group = await CreateGroupAsync(context.AcademicYearId, "Science");
		await _assignments.CreateAsync(BuildAssignment(context, "group", group.Id.ToString("D"), group.Name));

		var impact = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "payroll",
			PolicyId = context.PayrollPolicy.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "update"
		});
		var audit = await _policySafety.GetAuditSummaryAsync("payroll", context.PayrollPolicy.Id);

		impact.PolicyType.ShouldBe("payroll");
		impact.AffectedAssignmentCount.ShouldBeGreaterThanOrEqualTo(1);
		impact.CurrentVersion.ShouldBe(context.PayrollPolicy.Version);
		audit.PolicyId.ShouldBe(context.PayrollPolicy.Id);
		audit.Version.ShouldBe(context.PayrollPolicy.Version);
	}

	[Fact]
	public async Task Payroll_Archive_Impact_Should_Not_Invent_Employees_For_Unassigned_Policy()
	{
		var context = await CreatePolicyContextAsync();
		var unassignedPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Unassigned Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});

		var impact = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "payroll",
			PolicyId = unassignedPayroll.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "archive"
		});

		impact.AffectedAssignmentCount.ShouldBe(0);
		impact.AffectedEmployeeCount.ShouldBe(0);
		impact.AffectedEmployees.ShouldBeEmpty();
		impact.Warnings.ShouldNotContain(x => x.Code == "ShikshaMitra:AffectedEmployees");
		impact.RequiresConfirmation.ShouldBeFalse();
	}

	[Fact]
	public async Task Payroll_SetDefault_Impact_Should_Not_Invent_Employees_For_Unassigned_Policy()
	{
		var context = await CreatePolicyContextAsync();
		var unassignedPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Default Candidate Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});

		var impact = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "payroll",
			PolicyId = unassignedPayroll.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "set-default"
		});

		impact.AffectedAssignmentCount.ShouldBe(0);
		impact.AffectedEmployeeCount.ShouldBe(0);
		impact.AffectedEmployees.ShouldBeEmpty();
		impact.Warnings.ShouldNotContain(x => x.Code == "ShikshaMitra:AffectedEmployees");
		impact.RequiresConfirmation.ShouldBeFalse();
	}

	[Fact]
	public async Task Payroll_Archive_Impact_Should_Report_Assigned_Employees()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		await _assignments.CreateAsync(BuildAssignment(
			context,
			"employee",
			employee.Id.ToString("D"),
			employee.FullName));

		var impact = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "payroll",
			PolicyId = context.PayrollPolicy.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "archive"
		});

		impact.AffectedAssignmentCount.ShouldBeGreaterThanOrEqualTo(1);
		impact.AffectedEmployeeCount.ShouldBeGreaterThanOrEqualTo(1);
		impact.AffectedEmployees.ShouldContain(x => x.EmployeeId == employee.Id);
		impact.Warnings.ShouldContain(x => x.Code == "ShikshaMitra:AffectedEmployees");
	}

	[Fact]
	public async Task WorkCalendar_Impact_Should_Report_Assignments_Through_Referenced_Policies()
	{
		var context = await CreatePolicyContextAsync();
		var group = await CreateGroupAsync(context.AcademicYearId, "Operations");
		await _assignments.CreateAsync(BuildAssignment(context, "group", group.Id.ToString("D"), group.Name));

		var impact = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "work_calendar",
			PolicyId = context.WorkCalendar.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "archive"
		});

		impact.AffectedAssignmentCount.ShouldBeGreaterThanOrEqualTo(1);
		impact.Warnings.ShouldContain(x => x.Code == "ShikshaMitra:WorkCalendarPolicyReferenced");
		impact.RequiresConfirmation.ShouldBeTrue();
	}

	[Fact]
	public async Task EffectivePolicyResolution_Should_Reject_Archived_WorkCalendar()
	{
		var context = await CreatePolicyContextAsync();
		var calendar = await _workCalendarPolicyRepository.GetAsync(context.WorkCalendar.Id);
		calendar.Archive();
		await _workCalendarPolicyRepository.UpdateAsync(calendar, autoSave: true);
		var group = await CreateGroupAsync(context.AcademicYearId, "Operations");

		var ex = await Should.ThrowAsync<BusinessException>(() =>
			_assignments.GetPreviewAsync(new PolicyAssignmentPreviewRequestDto
			{
				AcademicYearId = context.AcademicYearId,
				Date = new DateTime(2026, 5, 1),
				GroupId = group.Id
			}));

		ex.Code.ShouldBe("ShikshaMitra:WorkCalendarPolicyMustBeActive");
	}

	[Theory]
	[InlineData("employee", "6d2f1f76-7dfa-49ed-9baf-378913cd94e1")]
	[InlineData("group", "bf68f1bb-f0e6-44a4-ab83-b79ad85eeb87")]
	[InlineData("role", "bf68f1bb-f0e6-44a4-ab83-b79ad85eeb87")]
	[InlineData("shift", "d760cd03-8051-4942-a34c-6b770a2cf105")]
	[InlineData("employee_type", "Contractor")]
	[InlineData("department", " ")]
	public async Task Assignment_Should_Reject_Invalid_Targets(string targetType, string targetKey)
	{
		var context = await CreatePolicyContextAsync();

		var ex = await Should.ThrowAsync<BusinessException>(() =>
			_assignments.CreateAsync(BuildAssignment(context, targetType, targetKey)));

		ex.Code.ShouldBeOneOf(
			"ShikshaMitra:PolicyAssignmentTargetInvalid",
			"ShikshaMitra:PolicyAssignmentTargetRequired",
			"ShikshaMitra:PolicyAssignmentTargetTypeUnsupported");
	}

	[Fact]
	public async Task Assignment_Should_Reject_Overlapping_Active_Target_Ranges()
	{
		var context = await CreatePolicyContextAsync();
		var group = await CreateGroupAsync(context.AcademicYearId, "Science");
		await _assignments.CreateAsync(BuildAssignment(context, "group", group.Id.ToString("D"), group.Name, effectiveFrom: new DateTime(2026, 4, 1), effectiveTo: new DateTime(2026, 6, 30)));

		var ex = await Should.ThrowAsync<BusinessException>(() =>
			_assignments.CreateAsync(BuildAssignment(context, "group", group.Id.ToString("D"), group.Name, effectiveFrom: new DateTime(2026, 6, 1), effectiveTo: null)));

		ex.Code.ShouldBe("ShikshaMitra:HrPolicyAssignmentOverlap");
		ex.Data.Contains("conflict").ShouldBeTrue();
	}

	[Fact]
	public async Task Assignment_Should_Reject_Archived_Module_Policies()
	{
		var context = await CreatePolicyContextAsync();
		var archivedPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Archived Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		await _payrollPolicies.DeleteAsync(archivedPayroll.Id);

		var group = await CreateGroupAsync(context.AcademicYearId, "Admin");
		var input = BuildAssignment(context, "group", group.Id.ToString("D"), group.Name);
		input.PayrollPolicyId = archivedPayroll.Id;

		var ex = await Should.ThrowAsync<BusinessException>(() => _assignments.CreateAsync(input));

		ex.Code.ShouldBe("ShikshaMitra:PolicyMustBeActive");
	}

	private async Task<PolicyContext> CreatePolicyContextAsync(
		string attendanceHolidayWorkBehavior = "paid",
		bool payrollHolidayIncluded = true)
	{
		var academicYearId = await CreateAcademicYearAsync();
		var calendar = await _workCalendars.CreateAsync(new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = academicYearId,
			Name = $"Calendar {Guid.NewGuid():N}",
			Status = "active",
			EffectiveFrom = new DateTime(2026, 4, 1),
			EffectiveTo = new DateTime(2027, 3, 31),
			WeeklyOffPattern = "saturday_sunday",
			HolidaySyncMode = "linked_with_overrides"
		});
		var payroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = academicYearId,
			WorkCalendarPolicyId = calendar.Id,
			Name = $"Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = true,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day",
			HolidayOverrides = payrollHolidayIncluded
				? new List<PayrollPolicyHolidayOverrideDto>()
				: new List<PayrollPolicyHolidayOverrideDto>
				{
					new()
					{
						Date = new DateTime(2026, 5, 18),
						HolidayName = "OverrideHoliday",
						IncludedInPayroll = false
					}
				}
		});
		var attendance = await _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = academicYearId,
			WorkCalendarPolicyId = calendar.Id,
			Name = $"Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = true,
			FullDayThresholdPercentage = 75,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = attendanceHolidayWorkBehavior,
		});

		return new PolicyContext(academicYearId, calendar, payroll, attendance);
	}

	private static CreateUpdateWorkCalendarPolicyDto BuildWorkCalendarInput(
		Guid academicYearId,
		List<WorkCalendarHolidayDto> holidays) => new()
	{
		AcademicYearId = academicYearId,
		Name = $"Calendar {Guid.NewGuid():N}",
		Status = "active",
		EffectiveFrom = new DateTime(2026, 4, 1),
		EffectiveTo = new DateTime(2027, 3, 31),
		WeeklyOffPattern = "saturday_sunday",
		HolidaySyncMode = "linked_with_overrides",
		Holidays = holidays
	};

	private static WorkCalendarHolidayDto BuildWorkCalendarHoliday(
		Guid academicYearId,
		string name,
		DateTime fromDate,
		DateTime? toDate = null,
		string source = "manual",
		Guid? calendarHolidayId = null,
		bool isIncluded = true)
	{
		var resolvedToDate = toDate ?? fromDate;
		return new WorkCalendarHolidayDto
		{
			AcademicYearId = academicYearId,
			Date = fromDate,
			FromDate = fromDate,
			ToDate = resolvedToDate,
			DayCount = (resolvedToDate.Date - fromDate.Date).Days + 1,
			HolidayName = name,
			Source = source,
			CalendarHolidayId = calendarHolidayId,
			IsIncluded = isIncluded
		};
	}

	private CreateUpdateHrPolicyAssignmentDto BuildAssignment(
		PolicyContext context,
		string targetType,
		string targetKey,
		string? targetDisplayName = null,
		DateTime? effectiveFrom = null,
		DateTime? effectiveTo = null) => new()
	{
		AcademicYearId = context.AcademicYearId,
		TargetType = targetType,
		TargetKey = targetKey,
		TargetDisplayName = targetDisplayName ?? targetKey.Trim(),
		PayrollPolicyId = context.PayrollPolicy.Id,
		AttendancePolicyId = context.AttendancePolicy.Id,
		EffectiveFrom = effectiveFrom ?? new DateTime(2026, 4, 1),
		EffectiveTo = effectiveTo,
		IsActive = true,
		Priority = 10,
		AssignmentReason = "Test assignment"
	};

	private async Task<Group> CreateGroupAsync(Guid academicYearId, string name)
	{
		var group = Group.Create(
			Guid.NewGuid(),
			_currentTenant.Id,
			academicYearId,
			$"{name}-{Guid.NewGuid():N}",
			"Test HR policy group.",
			[],
			[],
			[],
			[]);
		await _groupRepository.InsertAsync(group, autoSave: true);
		return group;
	}

	private async Task<EmployeePersonalInfoDto> CreateEmployeeAsync()
	{
		var roleId = await EnsureRoleExistsAsync();
		var shiftId = await EnsureShiftExistsAsync();
		var organizationId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		return await _employeePersonalInfo.CreateAsync(new CreateEmployeePersonalInfoDto
		{
			FirstName = "Policy",
			LastName = "Impact",
			MobileNo = "9876543210",
			Email = $"policy.impact.{_guidGenerator.Create():N}@test.com",
			DateOfBirth = DateOnly.FromDateTime(new DateTime(1992, 3, 10)),
			GenderType = GenderType.Male,
			DateOfJoining = DateOnly.FromDateTime(new DateTime(2026, 4, 1)),
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
		await _roleRepository.InsertAsync(new IdentityRole(id, $"PolicyImpactRole-{id:N}", _currentTenant.Id), autoSave: true);
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
			ShiftName = $"PolicyImpactShift-{_guidGenerator.Create():N}",
			StartTime = new TimeOnly(9, 0),
			EndTime = new TimeOnly(17, 0)
		};
		await _shiftRepository.InsertAsync(shift, autoSave: true);
		return shift.Id;
	}

	private async Task<Guid> CreateAcademicYearAsync()
	{
		using var uow = _unitOfWorkManager.Begin(requiresNew: true);
		var marker = $"PolicyAY-{Guid.NewGuid():N}";
		var created = await _academicYearManager.CreateAsync(
			marker,
			new DateTime(2026, 4, 1),
			new DateTime(2027, 3, 31));
		await _academicYearRepository.InsertAsync(created, autoSave: true);
		await uow.CompleteAsync();
		return created.Id;
	}

	private async Task<Holiday> CreateHolidayAsync(Guid academicYearId, string name, DateTime date)
	{
		var holidayType = HolidayType.Create(Guid.NewGuid(), $"Type-{Guid.NewGuid():N}", "#336699", tenantId: _currentTenant.Id);
		await _holidayTypeRepository.InsertAsync(holidayType, autoSave: true);
		var holiday = Holiday.Create(
			_currentTenant.Id,
			academicYearId,
			$"{name}-{Guid.NewGuid():N}",
			holidayType.Id,
			new DateTimeOffset(date, TimeSpan.Zero),
			new DateTimeOffset(date, TimeSpan.Zero));
		await _holidayRepository.InsertAsync(holiday, autoSave: true);
		return holiday;
	}

	private sealed record PolicyContext(
		Guid AcademicYearId,
		WorkCalendarPolicyDto WorkCalendar,
		PayrollPolicyDto PayrollPolicy,
		AttendancePolicyDto AttendancePolicy);
}
