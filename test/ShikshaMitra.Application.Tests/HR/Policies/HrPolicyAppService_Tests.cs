using ShikshaMitra.Common;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Audit;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp.Validation;
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
	private readonly HrPolicyImpactConfirmationService _impactConfirmation;
	private readonly IEmployeePersonalInfoAppService _employeePersonalInfo;
	private readonly IEmployeeStatusAppService _employeeStatus;
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
	private readonly IHttpContextAccessor _httpContextAccessor;

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
		_impactConfirmation = GetRequiredService<HrPolicyImpactConfirmationService>();
		_employeePersonalInfo = GetRequiredService<IEmployeePersonalInfoAppService>();
		_employeeStatus = GetRequiredService<IEmployeeStatusAppService>();
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
		_httpContextAccessor = GetRequiredService<IHttpContextAccessor>();
	}

	[Fact]
	public async Task Impact_Confirmation_Should_Treat_Severity_Case_Insensitively()
	{
		var warningPreview = BuildImpactPreviewWithSeverity("Warning");
		await _impactConfirmation.AttachTokenAsync(warningPreview);
		warningPreview.RequiresConfirmation.ShouldBeTrue();
		warningPreview.ConfirmationToken.ShouldNotBeNullOrWhiteSpace();
		(await CaptureBusinessExceptionAsync(() => _impactConfirmation.EnsureConfirmedAsync(warningPreview)))
			.Code.ShouldBe("ShikshaMitra:HrPolicyImpactConfirmationRequired");
		await _impactConfirmation.EnsureConfirmedAsync(warningPreview, warningPreview.ConfirmationToken);

		var infoPreview = BuildImpactPreviewWithSeverity("INFO");
		await _impactConfirmation.AttachTokenAsync(infoPreview);
		infoPreview.RequiresConfirmation.ShouldBeTrue();
		(await CaptureBusinessExceptionAsync(() => _impactConfirmation.EnsureConfirmedAsync(infoPreview)))
			.Code.ShouldBe("ShikshaMitra:HrPolicyImpactConfirmationRequired");

		var blockingPreview = BuildImpactPreviewWithSeverity("BLOCKING");
		await _impactConfirmation.AttachTokenAsync(blockingPreview);
		blockingPreview.BlockingWarnings.Count.ShouldBe(1);
		blockingPreview.RequiresConfirmation.ShouldBeFalse();
		blockingPreview.ConfirmationToken.ShouldBeNull();
		blockingPreview.ExpiresAt.ShouldBeNull();
		(await CaptureBusinessExceptionAsync(() => _impactConfirmation.EnsureConfirmedAsync(blockingPreview)))
			.Code.ShouldBe("ShikshaMitra:HrPolicyImpactBlocking");

		var noRiskPreview = BuildImpactPreviewWithSeverity("warning");
		noRiskPreview.Warnings.Clear();
		noRiskPreview.ConfirmationToken = "stale-token";
		noRiskPreview.ExpiresAt = DateTime.UtcNow.AddMinutes(5);
		await _impactConfirmation.AttachTokenAsync(noRiskPreview);
		noRiskPreview.RequiresConfirmation.ShouldBeFalse();
		noRiskPreview.ConfirmationToken.ShouldBeNull();
		noRiskPreview.ExpiresAt.ShouldBeNull();
	}

	[Fact]
	public async Task Impact_Confirmation_Should_Block_Explicit_BlockingWarnings()
	{
		var preview = BuildImpactPreviewWithSeverity("info");
		preview.Warnings.Clear();
		preview.ConfirmationToken = "stale-token";
		preview.ExpiresAt = DateTime.UtcNow.AddMinutes(5);
		preview.BlockingWarnings.Add(new HrPolicyImpactWarningDto
		{
			Severity = "blocking",
			Code = "ShikshaMitra:PolicyArchiveBlocked",
			Message = "This policy is still referenced by active assignments."
		});

		await _impactConfirmation.AttachTokenAsync(preview);

		preview.BlockingWarnings.Count.ShouldBe(1);
		preview.RequiresConfirmation.ShouldBeFalse();
		preview.ConfirmationToken.ShouldBeNull();
		preview.ExpiresAt.ShouldBeNull();
		(await CaptureBusinessExceptionAsync(() => _impactConfirmation.EnsureConfirmedAsync(preview)))
			.Code.ShouldBe("ShikshaMitra:HrPolicyImpactBlocking");
	}

	[Fact]
	public void Impact_Summary_Should_Preserve_Explicit_BlockingWarnings()
	{
		var preview = BuildImpactPreviewWithSeverity("info");
		preview.Warnings.Clear();
		preview.BlockingWarnings.Add(new HrPolicyImpactWarningDto
		{
			Severity = "blocking",
			Code = "ShikshaMitra:PolicyArchiveBlocked",
			Message = "This policy is still referenced by active assignments."
		});

		HrPolicyImpactWarningBuilder.FinalizeSummary(preview);

		preview.BlockingWarnings.Count.ShouldBe(1);
		(preview.RiskSummary ?? string.Empty).ShouldContain("1 blocking issue");
		(preview.RecommendedAction ?? string.Empty).ShouldContain("Resolve the blocking warnings");
	}

	[Fact]
	public async Task Impact_Confirmation_Token_Should_Be_Bound_To_Reviewed_Risk()
	{
		var reviewedPreview = BuildImpactPreviewWithSeverity("warning");
		await _impactConfirmation.AttachTokenAsync(reviewedPreview);
		reviewedPreview.ConfirmationToken.ShouldNotBeNullOrWhiteSpace();
		await _impactConfirmation.EnsureConfirmedAsync(reviewedPreview, reviewedPreview.ConfirmationToken);

		var changedPreview = BuildImpactPreviewWithSeverity("info");
		var ex = await CaptureBusinessExceptionAsync(() =>
			_impactConfirmation.EnsureConfirmedAsync(changedPreview, reviewedPreview.ConfirmationToken));

		ex.Code.ShouldBe("ShikshaMitra:HrPolicyImpactConfirmationInvalid");
	}

	[Fact]
	public async Task Impact_Confirmation_Token_Should_Be_Bound_To_Reviewed_Affected_Employees()
	{
		var academicYearId = Guid.NewGuid();
		var reviewedPreview = BuildAssignmentImpactPreviewWithAffectedEmployee(academicYearId, Guid.NewGuid(), "E-001");
		await _impactConfirmation.AttachTokenAsync(reviewedPreview);
		reviewedPreview.ConfirmationToken.ShouldNotBeNullOrWhiteSpace();
		await _impactConfirmation.EnsureConfirmedAsync(reviewedPreview, reviewedPreview.ConfirmationToken);

		var changedPreview = BuildAssignmentImpactPreviewWithAffectedEmployee(academicYearId, Guid.NewGuid(), "E-002");
		var ex = await CaptureBusinessExceptionAsync(() =>
			_impactConfirmation.EnsureConfirmedAsync(changedPreview, reviewedPreview.ConfirmationToken));

		ex.Code.ShouldBe("ShikshaMitra:HrPolicyImpactConfirmationInvalid");
	}

	[Fact]
	public async Task WorkCalendar_Should_Reject_Custom_Weekly_Off_Without_Days()
	{
		var academicYearId = await CreateAcademicYearAsync();
		var input = BuildWorkCalendarInput(academicYearId, []);
		input.WeeklyOffPattern = "custom";
		input.CustomWeeklyOffs = [];

		await Should.ThrowAsync<AbpValidationException>(() => _workCalendars.CreateAsync(input));
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
	public async Task Holiday_Impact_Should_Reject_Ambiguous_Default_Policy_Fallback()
	{
		var context = await CreatePolicyContextAsync();
		var secondDefault = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Holiday Ambiguous Default Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		var payrollRepository = GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		var duplicateDefault = await payrollRepository.GetAsync(secondDefault.Id);
		duplicateDefault.SetDefault(true);
		await payrollRepository.UpdateAsync(duplicateDefault, autoSave: true);

		var ex = await Should.ThrowAsync<BusinessException>(() => _holidayImpact.GetPreviewAsync(
			context.AcademicYearId,
			workCalendarPolicyId: null,
			payrollPolicyId: null,
			attendancePolicyId: null,
			fromDate: new DateTime(2026, 5, 1),
			toDate: new DateTime(2026, 5, 31),
			month: null));

		ex.Code.ShouldBe("ShikshaMitra:DefaultPayrollPolicyRequired");
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
	public async Task Holiday_Impact_Should_Reject_Invalid_Date_Filters()
	{
		var context = await CreatePolicyContextAsync();

		var rangeEx = await Should.ThrowAsync<BusinessException>(() => _holidayImpact.GetPreviewAsync(
			context.AcademicYearId,
			context.WorkCalendar.Id,
			context.PayrollPolicy.Id,
			context.AttendancePolicy.Id,
			fromDate: new DateTime(2026, 5, 31),
			toDate: new DateTime(2026, 5, 1),
			month: null));
		rangeEx.Code.ShouldBe("ShikshaMitra:PolicyEffectiveRangeInvalid");

		var monthEx = await Should.ThrowAsync<BusinessException>(() => _holidayImpact.GetPreviewAsync(
			context.AcademicYearId,
			context.WorkCalendar.Id,
			context.PayrollPolicy.Id,
			context.AttendancePolicy.Id,
			fromDate: null,
			toDate: null,
			month: "2026-13"));
		monthEx.Code.ShouldBe("HR:Payroll:030005");
	}

	[Fact]
	public async Task Holiday_Impact_Should_Reject_NonActive_WorkCalendar_Context()
	{
		var context = await CreatePolicyContextAsync();
		var calendar = await _workCalendarPolicyRepository.GetAsync(context.WorkCalendar.Id);
		calendar.Update(
			calendar.Name,
			HrPolicyStatus.Draft,
			calendar.EffectiveFrom,
			calendar.EffectiveTo,
			calendar.WeeklyOffPattern,
			calendar.GetCustomWeeklyOffs(),
			calendar.HolidaySyncMode);
		await _workCalendarPolicyRepository.UpdateAsync(calendar, autoSave: true);

		var ex = await Should.ThrowAsync<BusinessException>(() => _holidayImpact.GetPreviewAsync(
			context.AcademicYearId,
			workCalendarPolicyId: null,
			payrollPolicyId: null,
			attendancePolicyId: null,
			fromDate: new DateTime(2026, 5, 1),
			toDate: new DateTime(2026, 5, 31),
			month: null));

		ex.Code.ShouldBe("ShikshaMitra:WorkCalendarPolicyMustBeActive");
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

	[Theory]
	[InlineData("no_benefit", "non_working_no_benefit", false, false)]
	[InlineData("both", "working_required_punch_with_comp_off", true, true)]
	public async Task Holiday_Impact_Should_Surface_New_Holiday_Compensation_Values(
		string holidayWorkBehavior,
		string expectedAttendanceImpact,
		bool expectedPunchRequired,
		bool expectedWorkingDay)
	{
		var context = await CreatePolicyContextAsync(attendanceHolidayWorkBehavior: holidayWorkBehavior);
		var holiday = await CreateHolidayAsync(context.AcademicYearId, "NewHolidayBehavior", new DateTime(2026, 5, 21));

		var result = await _holidayImpact.GetPreviewAsync(
			context.AcademicYearId,
			context.WorkCalendar.Id,
			context.PayrollPolicy.Id,
			context.AttendancePolicy.Id,
			fromDate: new DateTime(2026, 5, 1),
			toDate: new DateTime(2026, 5, 31),
			month: null);

		var row = result.Items.Single(x => x.CalendarHolidayId == holiday.Id);
		row.HolidayWorkBehavior.ShouldBe(holidayWorkBehavior);
		row.AttendanceImpact.ShouldBe(expectedAttendanceImpact);
		row.IsPunchRequired.ShouldBe(expectedPunchRequired);
		row.IsAttendanceWorkingDay.ShouldBe(expectedWorkingDay);
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
	public async Task Payroll_Update_Should_Persist_New_WorkCalendar_When_Old_Calendar_Is_Archived()
	{
		var context = await CreatePolicyContextAsync();
		await ArchiveCalendarDirectlyAsync(context.WorkCalendar.Id);
		var replacement = await CreateWorkCalendarAsync(context.AcademicYearId, "Replacement");

		var updated = await _payrollPolicies.UpdateAsync(context.PayrollPolicy.Id, new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = replacement.Id,
			Name = context.PayrollPolicy.Name,
			Status = "active",
			IsDefault = true,
			DayCalculationType = context.PayrollPolicy.DayCalculationType,
			SaturdayWorkPolicy = context.PayrollPolicy.SaturdayWorkPolicy
		});

		updated.WorkCalendarPolicyId.ShouldBe(replacement.Id);
		(await _payrollPolicies.GetAsync(context.PayrollPolicy.Id)).WorkCalendarPolicyId.ShouldBe(replacement.Id);
	}

	[Fact]
	public async Task Draft_Payroll_Update_Should_Persist_New_WorkCalendar()
	{
		var context = await CreatePolicyContextAsync();
		var draft = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Draft Payroll {Guid.NewGuid():N}",
			Status = "draft",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		var replacement = await CreateWorkCalendarAsync(context.AcademicYearId, "DraftReplacement");

		var updated = await _payrollPolicies.UpdateAsync(draft.Id, new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = replacement.Id,
			Name = draft.Name,
			Status = "draft",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});

		updated.WorkCalendarPolicyId.ShouldBe(replacement.Id);
	}

	[Fact]
	public async Task Attendance_Update_Should_Persist_New_WorkCalendar()
	{
		var context = await CreatePolicyContextAsync();
		var replacement = await CreateWorkCalendarAsync(context.AcademicYearId, "AttendanceReplacement");

		var updated = await _attendancePolicies.UpdateAsync(context.AttendancePolicy.Id, new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = replacement.Id,
			Name = context.AttendancePolicy.Name,
			Status = "active",
			IsDefault = true,
			FullDayThresholdPercentage = context.AttendancePolicy.FullDayThresholdPercentage,
			HalfDayThresholdPercentage = context.AttendancePolicy.HalfDayThresholdPercentage,
			AbsentThresholdPercentage = context.AttendancePolicy.AbsentThresholdPercentage,
			MissingPunchBehavior = context.AttendancePolicy.MissingPunchBehavior,
			ManualCorrectionAllowed = context.AttendancePolicy.ManualCorrectionAllowed,
			GraceTime = context.AttendancePolicy.GraceTime,
			MaxLateAllowedPerMonth = context.AttendancePolicy.MaxLateAllowedPerMonth,
			PenaltyType = context.AttendancePolicy.PenaltyType,
			PenaltyCountThreshold = context.AttendancePolicy.PenaltyCountThreshold,
			EarlyExitEnabled = context.AttendancePolicy.EarlyExitEnabled,
			EarlyExitGraceTime = context.AttendancePolicy.EarlyExitGraceTime,
			OvertimeEnabled = context.AttendancePolicy.OvertimeEnabled,
			OvertimeRate = context.AttendancePolicy.OvertimeRate,
			MinOvertimeMinutes = context.AttendancePolicy.MinOvertimeMinutes,
			HolidayWorkBehavior = context.AttendancePolicy.HolidayWorkBehavior,
			SandwichRuleEnabled = context.AttendancePolicy.SandwichRuleEnabled,
			PayrollPreviewEnabled = context.AttendancePolicy.PayrollPreviewEnabled
		});

		updated.WorkCalendarPolicyId.ShouldBe(replacement.Id);
		(await _attendancePolicies.GetAsync(context.AttendancePolicy.Id)).WorkCalendarPolicyId.ShouldBe(replacement.Id);
	}

	[Fact]
	public async Task Payroll_Update_Should_Reject_Removing_Only_Default()
	{
		var context = await CreatePolicyContextAsync();

		var ex = await Should.ThrowAsync<BusinessException>(() => _payrollPolicies.UpdateAsync(
			context.PayrollPolicy.Id,
			new CreateUpdatePayrollPolicyDto
			{
				AcademicYearId = context.AcademicYearId,
				WorkCalendarPolicyId = context.WorkCalendar.Id,
				Name = context.PayrollPolicy.Name,
				Status = "active",
				IsDefault = false,
				DayCalculationType = context.PayrollPolicy.DayCalculationType,
				SaturdayWorkPolicy = context.PayrollPolicy.SaturdayWorkPolicy
			}));

		ex.Code.ShouldBe("ShikshaMitra:DefaultPayrollPolicyRequired");
		(await _payrollPolicies.GetAsync(context.PayrollPolicy.Id)).IsDefault.ShouldBeTrue();
	}

	[Fact]
	public async Task Attendance_Update_Should_Reject_Removing_Only_Default()
	{
		var context = await CreatePolicyContextAsync();

		var ex = await Should.ThrowAsync<BusinessException>(() => _attendancePolicies.UpdateAsync(
			context.AttendancePolicy.Id,
			new CreateUpdateAttendancePolicyDto
			{
				AcademicYearId = context.AcademicYearId,
				WorkCalendarPolicyId = context.WorkCalendar.Id,
				Name = context.AttendancePolicy.Name,
				Status = "active",
				IsDefault = false,
				FullDayThresholdPercentage = context.AttendancePolicy.FullDayThresholdPercentage,
				HalfDayThresholdPercentage = context.AttendancePolicy.HalfDayThresholdPercentage,
				AbsentThresholdPercentage = context.AttendancePolicy.AbsentThresholdPercentage,
				MissingPunchBehavior = context.AttendancePolicy.MissingPunchBehavior,
				ManualCorrectionAllowed = context.AttendancePolicy.ManualCorrectionAllowed,
				GraceTime = context.AttendancePolicy.GraceTime,
				MaxLateAllowedPerMonth = context.AttendancePolicy.MaxLateAllowedPerMonth,
				PenaltyType = context.AttendancePolicy.PenaltyType,
				PenaltyCountThreshold = context.AttendancePolicy.PenaltyCountThreshold,
				EarlyExitEnabled = context.AttendancePolicy.EarlyExitEnabled,
				EarlyExitGraceTime = context.AttendancePolicy.EarlyExitGraceTime,
				OvertimeEnabled = context.AttendancePolicy.OvertimeEnabled,
				OvertimeRate = context.AttendancePolicy.OvertimeRate,
				MinOvertimeMinutes = context.AttendancePolicy.MinOvertimeMinutes,
				HolidayWorkBehavior = context.AttendancePolicy.HolidayWorkBehavior,
				SandwichRuleEnabled = context.AttendancePolicy.SandwichRuleEnabled,
				PayrollPreviewEnabled = context.AttendancePolicy.PayrollPreviewEnabled
			}));

		ex.Code.ShouldBe("ShikshaMitra:DefaultAttendancePolicyRequired");
		(await _attendancePolicies.GetAsync(context.AttendancePolicy.Id)).IsDefault.ShouldBeTrue();
	}

	[Fact]
	public async Task Payroll_Create_Or_Update_Should_Reject_NonActive_Default()
	{
		var context = await CreatePolicyContextAsync();

		await Should.ThrowAsync<AbpValidationException>(() => _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Draft Default Payroll {Guid.NewGuid():N}",
			Status = "draft",
			IsDefault = true,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		}));
		(await _payrollPolicies.GetAsync(context.PayrollPolicy.Id)).IsDefault.ShouldBeTrue();

		var draft = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Draft Payroll {Guid.NewGuid():N}",
			Status = "draft",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});

		await Should.ThrowAsync<AbpValidationException>(() => _payrollPolicies.UpdateAsync(draft.Id, new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = draft.Name,
			Status = "draft",
			IsDefault = true,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		}));
		(await _payrollPolicies.GetAsync(context.PayrollPolicy.Id)).IsDefault.ShouldBeTrue();
	}

	[Fact]
	public async Task Attendance_Create_Or_Update_Should_Reject_NonActive_Default()
	{
		var context = await CreatePolicyContextAsync();

		await Should.ThrowAsync<AbpValidationException>(() => _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Draft Default Attendance {Guid.NewGuid():N}",
			Status = "draft",
			IsDefault = true,
			FullDayThresholdPercentage = 75,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		}));
		(await _attendancePolicies.GetAsync(context.AttendancePolicy.Id)).IsDefault.ShouldBeTrue();

		var draft = await _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Draft Attendance {Guid.NewGuid():N}",
			Status = "draft",
			IsDefault = false,
			FullDayThresholdPercentage = 75,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		});

		await Should.ThrowAsync<AbpValidationException>(() => _attendancePolicies.UpdateAsync(draft.Id, new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = draft.Name,
			Status = "draft",
			IsDefault = true,
			FullDayThresholdPercentage = draft.FullDayThresholdPercentage,
			HalfDayThresholdPercentage = draft.HalfDayThresholdPercentage,
			AbsentThresholdPercentage = draft.AbsentThresholdPercentage,
			MissingPunchBehavior = draft.MissingPunchBehavior,
			ManualCorrectionAllowed = draft.ManualCorrectionAllowed,
			HolidayWorkBehavior = draft.HolidayWorkBehavior,
			PayrollPreviewEnabled = draft.PayrollPreviewEnabled
		}));
		(await _attendancePolicies.GetAsync(context.AttendancePolicy.Id)).IsDefault.ShouldBeTrue();
	}

	[Fact]
	public async Task Payroll_SetDefault_Should_Reject_NonActive_WorkCalendar()
	{
		var context = await CreatePolicyContextAsync();
		var alternate = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Set Default Stale Calendar Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		await MarkWorkCalendarDraftAsync(context.WorkCalendar.Id);

		var ex = await Should.ThrowAsync<BusinessException>(() => _payrollPolicies.SetDefaultAsync(alternate.Id));

		ex.Code.ShouldBe("ShikshaMitra:WorkCalendarPolicyMustBeActive");
		(await _payrollPolicies.GetAsync(context.PayrollPolicy.Id)).IsDefault.ShouldBeTrue();
	}

	[Fact]
	public async Task Attendance_SetDefault_Should_Reject_NonActive_WorkCalendar()
	{
		var context = await CreatePolicyContextAsync();
		var alternate = await _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Set Default Stale Calendar Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			FullDayThresholdPercentage = 75,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		});
		await MarkWorkCalendarDraftAsync(context.WorkCalendar.Id);

		var ex = await Should.ThrowAsync<BusinessException>(() => _attendancePolicies.SetDefaultAsync(alternate.Id));

		ex.Code.ShouldBe("ShikshaMitra:WorkCalendarPolicyMustBeActive");
		(await _attendancePolicies.GetAsync(context.AttendancePolicy.Id)).IsDefault.ShouldBeTrue();
	}

	[Fact]
	public async Task Active_Module_Policies_Should_Reject_Draft_WorkCalendar()
	{
		var context = await CreatePolicyContextAsync();
		var draftCalendar = await _workCalendars.CreateAsync(new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			Name = $"Draft Calendar {Guid.NewGuid():N}",
			Status = "draft",
			EffectiveFrom = new DateTime(2026, 4, 1),
			EffectiveTo = new DateTime(2027, 3, 31),
			WeeklyOffPattern = "saturday_sunday",
			HolidaySyncMode = "linked_with_overrides"
		});

		var payrollEx = await Should.ThrowAsync<BusinessException>(() => _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = draftCalendar.Id,
			Name = $"Active Payroll Draft Calendar {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		}));
		payrollEx.Code.ShouldBe("ShikshaMitra:WorkCalendarPolicyMustBeActive");

		var attendanceEx = await Should.ThrowAsync<BusinessException>(() => _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = draftCalendar.Id,
			Name = $"Active Attendance Draft Calendar {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			FullDayThresholdPercentage = 75,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		}));
		attendanceEx.Code.ShouldBe("ShikshaMitra:WorkCalendarPolicyMustBeActive");
	}

	[Fact]
	public async Task Policy_Update_Should_Reject_AcademicYear_Changes()
	{
		var context = await CreatePolicyContextAsync();
		var foreignAcademicYearId = await CreateAcademicYearAsync();
		var foreignCalendar = await CreateWorkCalendarAsync(foreignAcademicYearId, "ForeignUpdate");

		var payrollEx = await Should.ThrowAsync<BusinessException>(() => _payrollPolicies.UpdateAsync(context.PayrollPolicy.Id, new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = foreignAcademicYearId,
			WorkCalendarPolicyId = foreignCalendar.Id,
			Name = $"Foreign Year Payroll Update {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		}));
		payrollEx.Code.ShouldBe("ShikshaMitra:PolicyAssignmentAcademicYearMismatch");

		var attendanceEx = await Should.ThrowAsync<BusinessException>(() => _attendancePolicies.UpdateAsync(context.AttendancePolicy.Id, new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = foreignAcademicYearId,
			WorkCalendarPolicyId = foreignCalendar.Id,
			Name = $"Foreign Year Attendance Update {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			FullDayThresholdPercentage = 75,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		}));
		attendanceEx.Code.ShouldBe("ShikshaMitra:PolicyAssignmentAcademicYearMismatch");

		var calendarEx = await Should.ThrowAsync<BusinessException>(() => _workCalendars.UpdateAsync(context.WorkCalendar.Id, new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = foreignAcademicYearId,
			Name = $"Foreign Year Calendar Update {Guid.NewGuid():N}",
			Status = "active",
			EffectiveFrom = context.WorkCalendar.EffectiveFrom,
			EffectiveTo = context.WorkCalendar.EffectiveTo,
			WeeklyOffPattern = context.WorkCalendar.WeeklyOffPattern,
			CustomWeeklyOffs = context.WorkCalendar.CustomWeeklyOffs,
			HolidaySyncMode = context.WorkCalendar.HolidaySyncMode,
			Holidays = context.WorkCalendar.Holidays
		}));
		calendarEx.Code.ShouldBe("ShikshaMitra:PolicyAssignmentAcademicYearMismatch");
	}

	[Fact]
	public async Task WorkCalendar_Update_Should_Block_Deactivation_When_Referenced()
	{
		var context = await CreatePolicyContextAsync();

		var ex = await Should.ThrowAsync<BusinessException>(() => _workCalendars.UpdateAsync(context.WorkCalendar.Id, new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			Name = context.WorkCalendar.Name,
			Status = "draft",
			EffectiveFrom = context.WorkCalendar.EffectiveFrom,
			EffectiveTo = context.WorkCalendar.EffectiveTo,
			WeeklyOffPattern = context.WorkCalendar.WeeklyOffPattern,
			CustomWeeklyOffs = context.WorkCalendar.CustomWeeklyOffs,
			HolidaySyncMode = context.WorkCalendar.HolidaySyncMode,
			Holidays = context.WorkCalendar.Holidays
		}));

		ex.Code.ShouldBe("ShikshaMitra:HrPolicyImpactBlocking");
		(await _workCalendars.GetAsync(context.WorkCalendar.Id)).Status.ShouldBe("active");
	}

	[Fact]
	public async Task Payroll_Delete_Should_Reject_Default_Policy()
	{
		var context = await CreatePolicyContextAsync();

		var ex = await CaptureBusinessExceptionAsync(() => _payrollPolicies.DeleteAsync(context.PayrollPolicy.Id));

		ex.Code.ShouldBe("ShikshaMitra:DefaultPayrollPolicyRequired");
		(await _payrollPolicies.GetAsync(context.PayrollPolicy.Id)).Status.ShouldBe("active");
	}

	[Fact]
	public async Task Attendance_Delete_Should_Reject_Default_Policy()
	{
		var context = await CreatePolicyContextAsync();

		var ex = await CaptureBusinessExceptionAsync(() => _attendancePolicies.DeleteAsync(context.AttendancePolicy.Id));

		ex.Code.ShouldBe("ShikshaMitra:DefaultAttendancePolicyRequired");
		(await _attendancePolicies.GetAsync(context.AttendancePolicy.Id)).Status.ShouldBe("active");
	}

	[Fact]
	public async Task Payroll_SetDefault_Should_Require_Confirmation_When_Replacing_Default()
	{
		var context = await CreatePolicyContextAsync();
		var candidate = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Replacement Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});

		var ex = await Should.ThrowAsync<BusinessException>(() => _payrollPolicies.SetDefaultAsync(candidate.Id));
		ex.Code.ShouldBe("ShikshaMitra:HrPolicyImpactConfirmationRequired");

		var preview = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "payroll",
			PolicyId = candidate.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "set-default"
		});
		preview.RequiresConfirmation.ShouldBeTrue();
		preview.ConfirmationToken.ShouldNotBeNullOrWhiteSpace();
		preview.Warnings.ShouldContain(x => x.Code == HRErrorCodes.DefaultPolicyWillBeReplaced);

		PayrollPolicyDto updated = null!;
		await RunWithImpactTokenHeaderAsync(preview.ConfirmationToken!, async () =>
		{
			updated = await _payrollPolicies.SetDefaultAsync(candidate.Id);
		});

		updated.IsDefault.ShouldBeTrue();
		(await _payrollPolicies.GetAsync(context.PayrollPolicy.Id)).IsDefault.ShouldBeFalse();
	}

	[Fact]
	public async Task Attendance_SetDefault_Should_Require_Confirmation_When_Replacing_Default()
	{
		var context = await CreatePolicyContextAsync();
		var candidate = await _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Replacement Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			FullDayThresholdPercentage = 75,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		});

		var ex = await Should.ThrowAsync<BusinessException>(() => _attendancePolicies.SetDefaultAsync(candidate.Id));
		ex.Code.ShouldBe("ShikshaMitra:HrPolicyImpactConfirmationRequired");

		var preview = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "attendance",
			PolicyId = candidate.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "set-default"
		});
		preview.RequiresConfirmation.ShouldBeTrue();
		preview.ConfirmationToken.ShouldNotBeNullOrWhiteSpace();
		preview.Warnings.ShouldContain(x => x.Code == HRErrorCodes.DefaultPolicyWillBeReplaced);

		AttendancePolicyDto updated = null!;
		await RunWithImpactTokenHeaderAsync(preview.ConfirmationToken!, async () =>
		{
			updated = await _attendancePolicies.SetDefaultAsync(candidate.Id);
		});

		updated.IsDefault.ShouldBeTrue();
		(await _attendancePolicies.GetAsync(context.AttendancePolicy.Id)).IsDefault.ShouldBeFalse();
	}

	[Fact]
	public async Task Payroll_Create_Default_Should_Require_Confirmation_When_Replacing_Default()
	{
		var context = await CreatePolicyContextAsync();
		var input = new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Create Default Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = true,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		};

		var ex = await Should.ThrowAsync<BusinessException>(() => _payrollPolicies.CreateAsync(input));
		ex.Code.ShouldBe("ShikshaMitra:HrPolicyImpactConfirmationRequired");

		var preview = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "payroll",
			AcademicYearId = context.AcademicYearId,
			Action = "create-default",
			PayrollPolicy = input
		});
		preview.RequiresConfirmation.ShouldBeTrue();
		preview.ConfirmationToken.ShouldNotBeNullOrWhiteSpace();
		preview.Warnings.ShouldContain(x => x.Code == HRErrorCodes.DefaultPolicyWillBeReplaced);

		PayrollPolicyDto created = null!;
		await RunWithImpactTokenHeaderAsync(preview.ConfirmationToken!, async () =>
		{
			created = await _payrollPolicies.CreateAsync(input);
		});

		created.IsDefault.ShouldBeTrue();
		(await _payrollPolicies.GetAsync(context.PayrollPolicy.Id)).IsDefault.ShouldBeFalse();
	}

	[Fact]
	public async Task Attendance_Create_Default_Should_Require_Confirmation_When_Replacing_Default()
	{
		var context = await CreatePolicyContextAsync();
		var input = new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Create Default Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = true,
			FullDayThresholdPercentage = 75,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		};

		var ex = await Should.ThrowAsync<BusinessException>(() => _attendancePolicies.CreateAsync(input));
		ex.Code.ShouldBe("ShikshaMitra:HrPolicyImpactConfirmationRequired");

		var preview = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "attendance",
			AcademicYearId = context.AcademicYearId,
			Action = "create-default",
			AttendancePolicy = input
		});
		preview.RequiresConfirmation.ShouldBeTrue();
		preview.ConfirmationToken.ShouldNotBeNullOrWhiteSpace();
		preview.Warnings.ShouldContain(x => x.Code == HRErrorCodes.DefaultPolicyWillBeReplaced);

		AttendancePolicyDto created = null!;
		await RunWithImpactTokenHeaderAsync(preview.ConfirmationToken!, async () =>
		{
			created = await _attendancePolicies.CreateAsync(input);
		});

		created.IsDefault.ShouldBeTrue();
		(await _attendancePolicies.GetAsync(context.AttendancePolicy.Id)).IsDefault.ShouldBeFalse();
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
		var timestampDifference = compare.Differences.FirstOrDefault(x =>
			x.FieldKey is "creationTime" or "lastModificationTime");
		timestampDifference.ShouldNotBeNull();
		timestampDifference!.ToDisplayValue.ShouldNotContain("T");
		timestampDifference.ToDisplayValue.ShouldContain(" ");
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
	public async Task Policy_Clone_And_Restore_Should_Reject_Archived_WorkCalendar_References()
	{
		var context = await CreatePolicyContextAsync();
		var payrollVersions = await _policyVersions.GetListAsync("payroll", context.PayrollPolicy.Id, new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto
		{
			MaxResultCount = 10
		});
		var attendanceVersions = await _policyVersions.GetListAsync("attendance", context.AttendancePolicy.Id, new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto
		{
			MaxResultCount = 10
		});

		await ArchiveCalendarDirectlyAsync(context.WorkCalendar.Id);

		var payrollCloneEx = await Should.ThrowAsync<BusinessException>(() =>
			_payrollPolicies.CloneAsync(context.PayrollPolicy.Id, new HrPolicyCloneRequestDto()));
		payrollCloneEx.Code.ShouldBe("ShikshaMitra:InvalidWorkCalendarPolicy");

		var attendanceCloneEx = await Should.ThrowAsync<BusinessException>(() =>
			_attendancePolicies.CloneAsync(context.AttendancePolicy.Id, new HrPolicyCloneRequestDto()));
		attendanceCloneEx.Code.ShouldBe("ShikshaMitra:InvalidWorkCalendarPolicy");

		var payrollRestorePreview = await _policyVersions.GetRestorePreviewAsync("payroll", context.PayrollPolicy.Id, payrollVersions.Items.Single().Id);
		payrollRestorePreview.BlockingWarnings.ShouldContain(x => x.Code == "ShikshaMitra:InvalidWorkCalendarPolicy");
		payrollRestorePreview.RequiresConfirmation.ShouldBeFalse();

		var attendanceRestorePreview = await _policyVersions.GetRestorePreviewAsync("attendance", context.AttendancePolicy.Id, attendanceVersions.Items.Single().Id);
		attendanceRestorePreview.BlockingWarnings.ShouldContain(x => x.Code == "ShikshaMitra:InvalidWorkCalendarPolicy");
		attendanceRestorePreview.RequiresConfirmation.ShouldBeFalse();

		var payrollRestoreEx = await Should.ThrowAsync<BusinessException>(() =>
			_policyVersions.RestoreDraftAsync("payroll", context.PayrollPolicy.Id, payrollVersions.Items.Single().Id, new HrPolicyRestoreDraftRequestDto()));
		payrollRestoreEx.Code.ShouldBe("ShikshaMitra:InvalidWorkCalendarPolicy");

		var attendanceRestoreEx = await Should.ThrowAsync<BusinessException>(() =>
			_policyVersions.RestoreDraftAsync("attendance", context.AttendancePolicy.Id, attendanceVersions.Items.Single().Id, new HrPolicyRestoreDraftRequestDto()));
		attendanceRestoreEx.Code.ShouldBe("ShikshaMitra:InvalidWorkCalendarPolicy");
	}

	[Fact]
	public async Task Policy_Restore_Preview_Should_Not_Report_Current_Policy_Assignment_Impact()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		await CreateAssignmentWithImpactTokenAsync(BuildAssignment(
			context,
			"employee",
			employee.Id.ToString("D"),
			employee.FullName));
		var versions = await _policyVersions.GetListAsync(
			"payroll",
			context.PayrollPolicy.Id,
			new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto { MaxResultCount = 10 });

		var preview = await _policyVersions.GetRestorePreviewAsync(
			"payroll",
			context.PayrollPolicy.Id,
			versions.Items.Single().Id);

		preview.Action.ShouldBe("restore-draft");
		preview.AffectedEmployeeCount.ShouldBe(0);
		preview.AffectedAssignmentCount.ShouldBe(0);
		preview.AffectedEmployees.ShouldBeEmpty();
		preview.RequiresConfirmation.ShouldBeFalse();
		preview.ConfirmationToken.ShouldBeNull();
		preview.RiskSummary.ShouldBe("No policy risks detected for this action.");
	}

	[Fact]
	public async Task Policy_Restore_Preview_Should_Not_Report_Current_Policy_Payroll_Risk()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var snapshotRepository = GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		await snapshotRepository.InsertAsync(new PayrollRunItemSnapshot(
			_guidGenerator.Create(),
			_currentTenant.Id,
			"2026-05",
			employee.Id)
		{
			PayrollPolicyId = context.PayrollPolicy.Id,
			IsLocked = true
		}, autoSave: true);
		var versions = await _policyVersions.GetListAsync(
			"payroll",
			context.PayrollPolicy.Id,
			new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto { MaxResultCount = 10 });

		var preview = await _policyVersions.GetRestorePreviewAsync(
			"payroll",
			context.PayrollPolicy.Id,
			versions.Items.Single().Id);

		preview.Action.ShouldBe("restore-draft");
		preview.AffectedPayrollMonths.ShouldBeEmpty();
		preview.LockedPayrollMonths.ShouldBeEmpty();
		preview.RequiresConfirmation.ShouldBeFalse();
		preview.RiskSummary.ShouldBe("No policy risks detected for this action.");
	}

	[Fact]
	public async Task Policy_Restore_Preview_Should_Make_Draft_Reference_Warnings_Review_Only()
	{
		var academicYearId = await CreateAcademicYearAsync();
		var draftCalendar = await _workCalendars.CreateAsync(new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = academicYearId,
			Name = $"Restore Draft Calendar {Guid.NewGuid():N}",
			Status = "draft",
			EffectiveFrom = new DateTime(2026, 4, 1),
			EffectiveTo = new DateTime(2027, 3, 31),
			WeeklyOffPattern = "saturday_sunday",
			HolidaySyncMode = "linked_with_overrides"
		});
		var draftPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = academicYearId,
			WorkCalendarPolicyId = draftCalendar.Id,
			Name = $"Restore Draft Payroll {Guid.NewGuid():N}",
			Status = "draft",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		var versions = await _policyVersions.GetListAsync(
			"payroll",
			draftPayroll.Id,
			new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto { MaxResultCount = 10 });

		var preview = await _policyVersions.GetRestorePreviewAsync(
			"payroll",
			draftPayroll.Id,
			versions.Items.Single().Id);

		preview.Action.ShouldBe("restore-draft");
		preview.Warnings.ShouldContain(x => x.Code == "ShikshaMitra:WorkCalendarPolicyMustBeActive");
		preview.BlockingWarnings.ShouldBeEmpty();
		preview.RequiresConfirmation.ShouldBeFalse();
		preview.ConfirmationToken.ShouldBeNull();
		preview.RecommendedAction.ShouldBe("This action can be saved after reviewing the informational notices.");
	}

	[Fact]
	public async Task AttendancePolicy_Should_Reject_Out_Of_Range_Rule_Values()
	{
		var context = await CreatePolicyContextAsync();

		await Should.ThrowAsync<AbpValidationException>(() => _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Invalid Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			FullDayThresholdPercentage = 75,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			GraceTime = 241,
			MaxLateAllowedPerMonth = 0,
			PenaltyType = "none",
			PenaltyCountThreshold = 0,
			EarlyExitEnabled = false,
			EarlyExitGraceTime = 0,
			OvertimeEnabled = false,
			HolidayWorkBehavior = "paid",
			PayrollPreviewEnabled = true
		}));

		await Should.ThrowAsync<AbpValidationException>(() => _attendancePolicies.UpdateAsync(context.AttendancePolicy.Id, new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = context.AttendancePolicy.Name,
			Status = context.AttendancePolicy.Status,
			IsDefault = context.AttendancePolicy.IsDefault,
			FullDayThresholdPercentage = context.AttendancePolicy.FullDayThresholdPercentage,
			HalfDayThresholdPercentage = context.AttendancePolicy.HalfDayThresholdPercentage,
			AbsentThresholdPercentage = context.AttendancePolicy.AbsentThresholdPercentage,
			MissingPunchBehavior = context.AttendancePolicy.MissingPunchBehavior,
			ManualCorrectionAllowed = context.AttendancePolicy.ManualCorrectionAllowed,
			GraceTime = context.AttendancePolicy.GraceTime,
			MaxLateAllowedPerMonth = 32,
			PenaltyType = context.AttendancePolicy.PenaltyType,
			PenaltyCountThreshold = context.AttendancePolicy.PenaltyCountThreshold,
			EarlyExitEnabled = context.AttendancePolicy.EarlyExitEnabled,
			EarlyExitGraceTime = context.AttendancePolicy.EarlyExitGraceTime,
			OvertimeEnabled = context.AttendancePolicy.OvertimeEnabled,
			OvertimeRate = context.AttendancePolicy.OvertimeRate,
			MinOvertimeMinutes = context.AttendancePolicy.MinOvertimeMinutes,
			HolidayWorkBehavior = context.AttendancePolicy.HolidayWorkBehavior,
			SandwichRuleEnabled = context.AttendancePolicy.SandwichRuleEnabled,
			PayrollPreviewEnabled = context.AttendancePolicy.PayrollPreviewEnabled
		}));

		await Should.ThrowAsync<AbpValidationException>(() => _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Invalid Threshold Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			FullDayThresholdPercentage = 101,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			PenaltyType = "none",
			HolidayWorkBehavior = "paid",
			PayrollPreviewEnabled = true
		}));

		await Should.ThrowAsync<AbpValidationException>(() => _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Invalid Threshold Order Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			FullDayThresholdPercentage = 50,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			PenaltyType = "none",
			HolidayWorkBehavior = "paid",
			PayrollPreviewEnabled = true
		}));

		await Should.ThrowAsync<AbpValidationException>(() => _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Invalid Overtime Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			FullDayThresholdPercentage = 75,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			PenaltyType = "none",
			OvertimeEnabled = true,
			HolidayWorkBehavior = "paid",
			PayrollPreviewEnabled = true
		}));
	}

	[Fact]
	public async Task AttendancePolicy_Should_Persist_Backend_Advanced_Rule_Values()
	{
		var context = await CreatePolicyContextAsync();

		var updated = await _attendancePolicies.UpdateAsync(context.AttendancePolicy.Id, new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = context.AttendancePolicy.Name,
			Status = context.AttendancePolicy.Status,
			IsDefault = context.AttendancePolicy.IsDefault,
			FullDayThresholdPercentage = context.AttendancePolicy.FullDayThresholdPercentage,
			HalfDayThresholdPercentage = context.AttendancePolicy.HalfDayThresholdPercentage,
			AbsentThresholdPercentage = context.AttendancePolicy.AbsentThresholdPercentage,
			MissingPunchBehavior = "mark_half_day",
			ManualCorrectionAllowed = context.AttendancePolicy.ManualCorrectionAllowed,
			GraceTime = 10,
			MaxLateAllowedPerMonth = 3,
			PenaltyType = "deduct_half_day",
			PenaltyCountThreshold = 2,
			EarlyExitEnabled = true,
			EarlyExitGraceTime = 5,
			OvertimeEnabled = true,
			OvertimeRate = "1.5x",
			MinOvertimeMinutes = 30,
			HolidayWorkBehavior = "ot_payout",
			SandwichRuleEnabled = true,
			PayrollPreviewEnabled = true
		});

		updated.PenaltyType.ShouldBe("deduct_half_day");
		updated.MissingPunchBehavior.ShouldBe("mark_half_day");
		updated.OvertimeRate.ShouldBe("1.5x");
		updated.HolidayWorkBehavior.ShouldBe("ot_payout");
		updated.MinOvertimeMinutes.ShouldBe(30);
		updated.SandwichRuleEnabled.ShouldBeTrue();
	}

	[Theory]
	[InlineData("warning", "no_benefit")]
	[InlineData("salary_deduction", "both")]
	public async Task AttendancePolicy_Should_Persist_New_Advanced_Rule_Values(
		string penaltyType,
		string holidayWorkBehavior)
	{
		var context = await CreatePolicyContextAsync();

		var updated = await _attendancePolicies.UpdateAsync(context.AttendancePolicy.Id, new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = context.AttendancePolicy.Name,
			Status = context.AttendancePolicy.Status,
			IsDefault = context.AttendancePolicy.IsDefault,
			FullDayThresholdPercentage = context.AttendancePolicy.FullDayThresholdPercentage,
			HalfDayThresholdPercentage = context.AttendancePolicy.HalfDayThresholdPercentage,
			AbsentThresholdPercentage = context.AttendancePolicy.AbsentThresholdPercentage,
			MissingPunchBehavior = context.AttendancePolicy.MissingPunchBehavior,
			ManualCorrectionAllowed = context.AttendancePolicy.ManualCorrectionAllowed,
			GraceTime = context.AttendancePolicy.GraceTime,
			MaxLateAllowedPerMonth = context.AttendancePolicy.MaxLateAllowedPerMonth,
			PenaltyType = penaltyType,
			PenaltyCountThreshold = context.AttendancePolicy.PenaltyCountThreshold,
			EarlyExitEnabled = context.AttendancePolicy.EarlyExitEnabled,
			EarlyExitGraceTime = context.AttendancePolicy.EarlyExitGraceTime,
			OvertimeEnabled = context.AttendancePolicy.OvertimeEnabled,
			OvertimeRate = context.AttendancePolicy.OvertimeRate,
			MinOvertimeMinutes = context.AttendancePolicy.MinOvertimeMinutes,
			HolidayWorkBehavior = holidayWorkBehavior,
			SandwichRuleEnabled = context.AttendancePolicy.SandwichRuleEnabled,
			PayrollPreviewEnabled = context.AttendancePolicy.PayrollPreviewEnabled
		});

		updated.PenaltyType.ShouldBe(penaltyType);
		updated.HolidayWorkBehavior.ShouldBe(holidayWorkBehavior);
	}

	[Fact]
	public async Task AttendancePolicy_Should_Reject_Unsupported_Advanced_Rule_Values()
	{
		var context = await CreatePolicyContextAsync();

		async Task AssertUnsupportedAsync(
			string? missingPunchBehavior = null,
			string? penaltyType = null,
			string? overtimeRate = null,
			string? holidayWorkBehavior = null,
			string expectedMessageToken = "")
		{
			var ex = await Should.ThrowAsync<AbpValidationException>(() => _attendancePolicies.UpdateAsync(context.AttendancePolicy.Id, new CreateUpdateAttendancePolicyDto
			{
				AcademicYearId = context.AcademicYearId,
				WorkCalendarPolicyId = context.WorkCalendar.Id,
				Name = context.AttendancePolicy.Name,
				Status = context.AttendancePolicy.Status,
				IsDefault = context.AttendancePolicy.IsDefault,
				FullDayThresholdPercentage = context.AttendancePolicy.FullDayThresholdPercentage,
				HalfDayThresholdPercentage = context.AttendancePolicy.HalfDayThresholdPercentage,
				AbsentThresholdPercentage = context.AttendancePolicy.AbsentThresholdPercentage,
				MissingPunchBehavior = missingPunchBehavior ?? context.AttendancePolicy.MissingPunchBehavior,
				ManualCorrectionAllowed = context.AttendancePolicy.ManualCorrectionAllowed,
				GraceTime = context.AttendancePolicy.GraceTime,
				MaxLateAllowedPerMonth = context.AttendancePolicy.MaxLateAllowedPerMonth,
				PenaltyType = penaltyType ?? context.AttendancePolicy.PenaltyType,
				PenaltyCountThreshold = context.AttendancePolicy.PenaltyCountThreshold,
				EarlyExitEnabled = true,
				EarlyExitGraceTime = context.AttendancePolicy.EarlyExitGraceTime,
				OvertimeEnabled = true,
				OvertimeRate = overtimeRate ?? "1.5x",
				MinOvertimeMinutes = context.AttendancePolicy.MinOvertimeMinutes ?? 30,
				HolidayWorkBehavior = holidayWorkBehavior ?? context.AttendancePolicy.HolidayWorkBehavior,
				SandwichRuleEnabled = context.AttendancePolicy.SandwichRuleEnabled,
				PayrollPreviewEnabled = context.AttendancePolicy.PayrollPreviewEnabled
			}));
			ex.ValidationErrors.Any(error => (error.ErrorMessage ?? string.Empty).Contains(expectedMessageToken, StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
		}

		await AssertUnsupportedAsync(
			missingPunchBehavior: "ignore",
			expectedMessageToken: "Missing punch behavior");
		await AssertUnsupportedAsync(
			penaltyType: "deduct_quarter_day",
			expectedMessageToken: "Penalty type");
		await AssertUnsupportedAsync(
			overtimeRate: "3.0x",
			expectedMessageToken: "Overtime rate");
		await AssertUnsupportedAsync(
			holidayWorkBehavior: "ignore",
			expectedMessageToken: "Holiday work behavior");
	}

	[Fact]
	public async Task WorkCalendar_Update_Should_NoOp_When_Unchanged()
	{
		var context = await CreatePolicyContextAsync();
		var beforeEvents = await _auditTrail.GetEntityEventsAsync("work_calendar_policy", context.WorkCalendar.Id, new HrAuditEventListRequestDto
		{
			MaxResultCount = 20
		});
		var beforeVersions = await _policyVersions.GetListAsync("work_calendar", context.WorkCalendar.Id, new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto
		{
			MaxResultCount = 20
		});

		var result = await _workCalendars.UpdateAsync(context.WorkCalendar.Id, new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			Name = context.WorkCalendar.Name,
			Status = context.WorkCalendar.Status,
			EffectiveFrom = context.WorkCalendar.EffectiveFrom,
			EffectiveTo = context.WorkCalendar.EffectiveTo,
			WeeklyOffPattern = context.WorkCalendar.WeeklyOffPattern,
			CustomWeeklyOffs = context.WorkCalendar.CustomWeeklyOffs,
			HolidaySyncMode = context.WorkCalendar.HolidaySyncMode,
			Holidays = []
		});

		var afterEvents = await _auditTrail.GetEntityEventsAsync("work_calendar_policy", context.WorkCalendar.Id, new HrAuditEventListRequestDto
		{
			MaxResultCount = 20
		});
		var afterVersions = await _policyVersions.GetListAsync("work_calendar", context.WorkCalendar.Id, new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto
		{
			MaxResultCount = 20
		});

		result.Version.ShouldBe(context.WorkCalendar.Version);
		afterEvents.TotalCount.ShouldBe(beforeEvents.TotalCount);
		afterVersions.TotalCount.ShouldBe(beforeVersions.TotalCount);
	}

	[Fact]
	public async Task Attendance_Update_Should_NoOp_When_Unchanged()
	{
		var context = await CreatePolicyContextAsync();
		var beforeEvents = await _auditTrail.GetEntityEventsAsync("attendance_policy", context.AttendancePolicy.Id, new HrAuditEventListRequestDto
		{
			MaxResultCount = 20
		});
		var beforeVersions = await _policyVersions.GetListAsync("attendance", context.AttendancePolicy.Id, new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto
		{
			MaxResultCount = 20
		});

		var result = await _attendancePolicies.UpdateAsync(context.AttendancePolicy.Id, new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.AttendancePolicy.WorkCalendarPolicyId,
			Name = context.AttendancePolicy.Name,
			Status = context.AttendancePolicy.Status,
			IsDefault = context.AttendancePolicy.IsDefault,
			FullDayThresholdPercentage = context.AttendancePolicy.FullDayThresholdPercentage,
			HalfDayThresholdPercentage = context.AttendancePolicy.HalfDayThresholdPercentage,
			AbsentThresholdPercentage = context.AttendancePolicy.AbsentThresholdPercentage,
			MissingPunchBehavior = context.AttendancePolicy.MissingPunchBehavior,
			ManualCorrectionAllowed = context.AttendancePolicy.ManualCorrectionAllowed,
			GraceTime = context.AttendancePolicy.GraceTime,
			MaxLateAllowedPerMonth = context.AttendancePolicy.MaxLateAllowedPerMonth,
			PenaltyType = context.AttendancePolicy.PenaltyType,
			PenaltyCountThreshold = context.AttendancePolicy.PenaltyCountThreshold,
			EarlyExitEnabled = context.AttendancePolicy.EarlyExitEnabled,
			EarlyExitGraceTime = context.AttendancePolicy.EarlyExitGraceTime,
			OvertimeEnabled = context.AttendancePolicy.OvertimeEnabled,
			OvertimeRate = context.AttendancePolicy.OvertimeRate,
			MinOvertimeMinutes = context.AttendancePolicy.MinOvertimeMinutes,
			HolidayWorkBehavior = context.AttendancePolicy.HolidayWorkBehavior,
			SandwichRuleEnabled = context.AttendancePolicy.SandwichRuleEnabled,
			PayrollPreviewEnabled = context.AttendancePolicy.PayrollPreviewEnabled
		});

		var afterEvents = await _auditTrail.GetEntityEventsAsync("attendance_policy", context.AttendancePolicy.Id, new HrAuditEventListRequestDto
		{
			MaxResultCount = 20
		});
		var afterVersions = await _policyVersions.GetListAsync("attendance", context.AttendancePolicy.Id, new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto
		{
			MaxResultCount = 20
		});

		result.Version.ShouldBe(context.AttendancePolicy.Version);
		afterEvents.TotalCount.ShouldBe(beforeEvents.TotalCount);
		afterVersions.TotalCount.ShouldBe(beforeVersions.TotalCount);
	}

	[Fact]
	public async Task Archive_Should_NoOp_When_Policy_Is_Already_Archived()
	{
		var context = await CreatePolicyContextAsync();
		var payroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Repeat Archive Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		var attendance = await _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Repeat Archive Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			FullDayThresholdPercentage = 80,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		});
		var calendar = await CreateWorkCalendarAsync(context.AcademicYearId, "RepeatArchive");

		await _payrollPolicies.DeleteAsync(payroll.Id);
		await _attendancePolicies.DeleteAsync(attendance.Id);
		await _workCalendars.DeleteAsync(calendar.Id);

		var payrollEvents = await _auditTrail.GetEntityEventsAsync("payroll_policy", payroll.Id, new HrAuditEventListRequestDto { MaxResultCount = 20 });
		var payrollVersions = await _policyVersions.GetListAsync("payroll", payroll.Id, new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto { MaxResultCount = 20 });
		var attendanceEvents = await _auditTrail.GetEntityEventsAsync("attendance_policy", attendance.Id, new HrAuditEventListRequestDto { MaxResultCount = 20 });
		var attendanceVersions = await _policyVersions.GetListAsync("attendance", attendance.Id, new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto { MaxResultCount = 20 });
		var calendarEvents = await _auditTrail.GetEntityEventsAsync("work_calendar_policy", calendar.Id, new HrAuditEventListRequestDto { MaxResultCount = 20 });
		var calendarVersions = await _policyVersions.GetListAsync("work_calendar", calendar.Id, new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto { MaxResultCount = 20 });

		await _payrollPolicies.DeleteAsync(payroll.Id);
		await _attendancePolicies.DeleteAsync(attendance.Id);
		await _workCalendars.DeleteAsync(calendar.Id);

		var payrollEventsAfter = await _auditTrail.GetEntityEventsAsync("payroll_policy", payroll.Id, new HrAuditEventListRequestDto { MaxResultCount = 20 });
		var payrollVersionsAfter = await _policyVersions.GetListAsync("payroll", payroll.Id, new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto { MaxResultCount = 20 });
		var attendanceEventsAfter = await _auditTrail.GetEntityEventsAsync("attendance_policy", attendance.Id, new HrAuditEventListRequestDto { MaxResultCount = 20 });
		var attendanceVersionsAfter = await _policyVersions.GetListAsync("attendance", attendance.Id, new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto { MaxResultCount = 20 });
		var calendarEventsAfter = await _auditTrail.GetEntityEventsAsync("work_calendar_policy", calendar.Id, new HrAuditEventListRequestDto { MaxResultCount = 20 });
		var calendarVersionsAfter = await _policyVersions.GetListAsync("work_calendar", calendar.Id, new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto { MaxResultCount = 20 });

		(await _payrollPolicies.GetAsync(payroll.Id)).Status.ShouldBe("archived");
		(await _attendancePolicies.GetAsync(attendance.Id)).Status.ShouldBe("archived");
		(await _workCalendars.GetAsync(calendar.Id)).Status.ShouldBe("archived");
		payrollEventsAfter.TotalCount.ShouldBe(payrollEvents.TotalCount);
		payrollVersionsAfter.TotalCount.ShouldBe(payrollVersions.TotalCount);
		attendanceEventsAfter.TotalCount.ShouldBe(attendanceEvents.TotalCount);
		attendanceVersionsAfter.TotalCount.ShouldBe(attendanceVersions.TotalCount);
		calendarEventsAfter.TotalCount.ShouldBe(calendarEvents.TotalCount);
		calendarVersionsAfter.TotalCount.ShouldBe(calendarVersions.TotalCount);
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
	public async Task WorkCalendar_Restore_Preview_Should_Reject_AcademicYear_Mismatched_Snapshot()
	{
		var context = await CreatePolicyContextAsync();
		var foreignAcademicYearId = await CreateAcademicYearAsync();
		var snapshot = new WorkCalendarPolicyDto
		{
			Id = context.WorkCalendar.Id,
			AcademicYearId = foreignAcademicYearId,
			Name = context.WorkCalendar.Name,
			Status = context.WorkCalendar.Status,
			Version = context.WorkCalendar.Version,
			EffectiveFrom = context.WorkCalendar.EffectiveFrom,
			EffectiveTo = context.WorkCalendar.EffectiveTo,
			WeeklyOffPattern = context.WorkCalendar.WeeklyOffPattern,
			CustomWeeklyOffs = context.WorkCalendar.CustomWeeklyOffs,
			HolidaySyncMode = context.WorkCalendar.HolidaySyncMode,
			Holidays = context.WorkCalendar.Holidays,
			HolidaySummary = context.WorkCalendar.HolidaySummary
		};
		var version = new HrPolicyVersion(
			_guidGenerator.Create(),
			_currentTenant.Id,
			"work_calendar",
			context.WorkCalendar.Id,
			context.AcademicYearId,
			999,
			context.WorkCalendar.Name,
			context.WorkCalendar.Status,
			context.WorkCalendar.EffectiveFrom,
			context.WorkCalendar.EffectiveTo,
			JsonSerializer.Serialize(snapshot),
			"changed");

		var ex = await Should.ThrowAsync<BusinessException>(() =>
			Task.FromResult(HrPolicyVersionProjection.ToRestorePreviewRequest("work_calendar", context.WorkCalendar.Id, version)));

		ex.Code.ShouldBe("ShikshaMitra:PolicyAssignmentAcademicYearMismatch");
	}

	[Fact]
	public async Task Policy_Restore_Should_Reject_Unsupported_Snapshot_Enum_Values()
	{
		var context = await CreatePolicyContextAsync();
		var restoreService = GetRequiredService<HrPolicyVersionRestoreService>();

		var workCalendarSnapshot = new WorkCalendarPolicyDto
		{
			Id = context.WorkCalendar.Id,
			AcademicYearId = context.AcademicYearId,
			Name = context.WorkCalendar.Name,
			Status = context.WorkCalendar.Status,
			Version = context.WorkCalendar.Version,
			EffectiveFrom = context.WorkCalendar.EffectiveFrom,
			EffectiveTo = context.WorkCalendar.EffectiveTo,
			WeeklyOffPattern = "friday_only",
			CustomWeeklyOffs = context.WorkCalendar.CustomWeeklyOffs,
			HolidaySyncMode = context.WorkCalendar.HolidaySyncMode
		};
		var workCalendarVersion = BuildPolicyVersion(
			"work_calendar",
			context.WorkCalendar.Id,
			context.AcademicYearId,
			context.WorkCalendar.Name,
			context.WorkCalendar.Status,
			JsonSerializer.Serialize(workCalendarSnapshot));
		var workCalendarEx = await Should.ThrowAsync<BusinessException>(() =>
			restoreService.RestoreDraftAsync(workCalendarVersion, "work_calendar", context.WorkCalendar.Id, new HrPolicyRestoreDraftRequestDto()));
		workCalendarEx.Code.ShouldBe("ShikshaMitra:UnsupportedWorkCalendarWeeklyOffPattern");

		var payrollSnapshot = new PayrollPolicyDto
		{
			Id = context.PayrollPolicy.Id,
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = context.PayrollPolicy.Name,
			Status = context.PayrollPolicy.Status,
			IsDefault = context.PayrollPolicy.IsDefault,
			DayCalculationType = "rolling-average",
			SaturdayWorkPolicy = context.PayrollPolicy.SaturdayWorkPolicy,
			Version = context.PayrollPolicy.Version
		};
		var payrollVersion = BuildPolicyVersion(
			"payroll",
			context.PayrollPolicy.Id,
			context.AcademicYearId,
			context.PayrollPolicy.Name,
			context.PayrollPolicy.Status,
			JsonSerializer.Serialize(payrollSnapshot));
		var payrollEx = await Should.ThrowAsync<BusinessException>(() =>
			restoreService.RestoreDraftAsync(payrollVersion, "payroll", context.PayrollPolicy.Id, new HrPolicyRestoreDraftRequestDto()));
		payrollEx.Code.ShouldBe("ShikshaMitra:UnsupportedPayrollDayCalculationType");

		var attendanceSnapshot = new AttendancePolicyDto
		{
			Id = context.AttendancePolicy.Id,
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = context.AttendancePolicy.Name,
			Status = context.AttendancePolicy.Status,
			IsDefault = context.AttendancePolicy.IsDefault,
			CaptureMode = "badge_only",
			FullDayThresholdPercentage = context.AttendancePolicy.FullDayThresholdPercentage,
			HalfDayThresholdPercentage = context.AttendancePolicy.HalfDayThresholdPercentage,
			AbsentThresholdPercentage = context.AttendancePolicy.AbsentThresholdPercentage,
			MissingPunchBehavior = context.AttendancePolicy.MissingPunchBehavior,
			ManualCorrectionAllowed = context.AttendancePolicy.ManualCorrectionAllowed,
			GraceTime = context.AttendancePolicy.GraceTime,
			MaxLateAllowedPerMonth = context.AttendancePolicy.MaxLateAllowedPerMonth,
			PenaltyType = context.AttendancePolicy.PenaltyType,
			PenaltyCountThreshold = context.AttendancePolicy.PenaltyCountThreshold,
			EarlyExitEnabled = context.AttendancePolicy.EarlyExitEnabled,
			EarlyExitGraceTime = context.AttendancePolicy.EarlyExitGraceTime,
			OvertimeEnabled = context.AttendancePolicy.OvertimeEnabled,
			OvertimeRate = context.AttendancePolicy.OvertimeRate,
			MinOvertimeMinutes = context.AttendancePolicy.MinOvertimeMinutes,
			HolidayWorkBehavior = context.AttendancePolicy.HolidayWorkBehavior,
			SandwichRuleEnabled = context.AttendancePolicy.SandwichRuleEnabled,
			PayrollPreviewEnabled = context.AttendancePolicy.PayrollPreviewEnabled,
			Version = context.AttendancePolicy.Version
		};
		var attendanceVersion = BuildPolicyVersion(
			"attendance",
			context.AttendancePolicy.Id,
			context.AcademicYearId,
			context.AttendancePolicy.Name,
			context.AttendancePolicy.Status,
			JsonSerializer.Serialize(attendanceSnapshot));
		var attendanceEx = await Should.ThrowAsync<BusinessException>(() =>
			restoreService.RestoreDraftAsync(attendanceVersion, "attendance", context.AttendancePolicy.Id, new HrPolicyRestoreDraftRequestDto()));
		attendanceEx.Code.ShouldBe("ShikshaMitra:UnsupportedAttendanceCaptureMode");
	}

	[Fact]
	public async Task WorkCalendar_Restore_Should_Reject_Invalid_Holiday_Snapshot()
	{
		var context = await CreatePolicyContextAsync();
		var restoreService = GetRequiredService<HrPolicyVersionRestoreService>();
		var snapshot = new WorkCalendarPolicyDto
		{
			Id = context.WorkCalendar.Id,
			AcademicYearId = context.AcademicYearId,
			Name = context.WorkCalendar.Name,
			Status = context.WorkCalendar.Status,
			Version = context.WorkCalendar.Version,
			EffectiveFrom = context.WorkCalendar.EffectiveFrom,
			EffectiveTo = context.WorkCalendar.EffectiveTo,
			WeeklyOffPattern = context.WorkCalendar.WeeklyOffPattern,
			CustomWeeklyOffs = context.WorkCalendar.CustomWeeklyOffs,
			HolidaySyncMode = context.WorkCalendar.HolidaySyncMode,
			Holidays =
			[
				BuildWorkCalendarHoliday(context.AcademicYearId, "First Restore Break", new DateTime(2026, 7, 1), new DateTime(2026, 7, 2)),
				BuildWorkCalendarHoliday(context.AcademicYearId, "Second Restore Break", new DateTime(2026, 7, 2), new DateTime(2026, 7, 3))
			]
		};
		var version = BuildPolicyVersion(
			"work_calendar",
			context.WorkCalendar.Id,
			context.AcademicYearId,
			context.WorkCalendar.Name,
			context.WorkCalendar.Status,
			JsonSerializer.Serialize(snapshot));

		var ex = await Should.ThrowAsync<BusinessException>(() =>
			restoreService.RestoreDraftAsync(version, "work_calendar", context.WorkCalendar.Id, new HrPolicyRestoreDraftRequestDto()));

		ex.Code.ShouldBe("ShikshaMitra:WorkCalendarHolidayOverlap");
		await WithUnitOfWorkAsync(async () =>
		{
			var workCalendarRepository = GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>();
			(await workCalendarRepository.AnyAsync(x => x.Name == $"{context.WorkCalendar.Name} v{context.WorkCalendar.Version} Restore Draft"))
				.ShouldBeFalse();
		});
	}

	[Fact]
	public async Task Policy_Restore_Should_Map_Malformed_Snapshot_To_Business_Error()
	{
		var context = await CreatePolicyContextAsync();
		var restoreService = GetRequiredService<HrPolicyVersionRestoreService>();
		var version = BuildPolicyVersion(
			"payroll",
			context.PayrollPolicy.Id,
			context.AcademicYearId,
			context.PayrollPolicy.Name,
			context.PayrollPolicy.Status,
			"{");

		var previewEx = Should.Throw<BusinessException>(() =>
			HrPolicyVersionProjection.ToRestorePreviewRequest("payroll", context.PayrollPolicy.Id, version));
		previewEx.Code.ShouldBe("ShikshaMitra:InvalidPolicyVersionSnapshot");

		var restoreEx = await Should.ThrowAsync<BusinessException>(() =>
			restoreService.RestoreDraftAsync(version, "payroll", context.PayrollPolicy.Id, new HrPolicyRestoreDraftRequestDto()));
		restoreEx.Code.ShouldBe("ShikshaMitra:InvalidPolicyVersionSnapshot");
	}

	[Fact]
	public async Task Policy_Version_Compare_Should_Map_Malformed_Snapshot_To_Business_Error()
	{
		var context = await CreatePolicyContextAsync();
		var versions = await _policyVersions.GetListAsync(
			"payroll",
			context.PayrollPolicy.Id,
			new Volo.Abp.Application.Dtos.PagedAndSortedResultRequestDto { MaxResultCount = 10 });
		var malformed = BuildPolicyVersion(
			"payroll",
			context.PayrollPolicy.Id,
			context.AcademicYearId,
			context.PayrollPolicy.Name,
			context.PayrollPolicy.Status,
			"{");
		var versionRepository = GetRequiredService<IRepository<HrPolicyVersion, Guid>>();
		await versionRepository.InsertAsync(malformed, autoSave: true);

		var ex = await Should.ThrowAsync<BusinessException>(() =>
			_policyVersions.CompareAsync("payroll", context.PayrollPolicy.Id, versions.Items.Single().Id, malformed.Id));

		ex.Code.ShouldBe("ShikshaMitra:InvalidPolicyVersionSnapshot");
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
	public async Task Archived_Policies_Should_Reject_Direct_Update()
	{
		var context = await CreatePolicyContextAsync();
		var calendar = await CreateWorkCalendarAsync(context.AcademicYearId, "ArchivedUpdate");
		var payroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Archived Update Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		var attendance = await _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Archived Update Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			FullDayThresholdPercentage = 80,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		});

		await _workCalendars.DeleteAsync(calendar.Id);
		await _payrollPolicies.DeleteAsync(payroll.Id);
		await _attendancePolicies.DeleteAsync(attendance.Id);

		var calendarEx = await Should.ThrowAsync<BusinessException>(() => _workCalendars.UpdateAsync(calendar.Id, new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = calendar.AcademicYearId,
			Name = $"{calendar.Name} Edited",
			Status = "draft",
			EffectiveFrom = calendar.EffectiveFrom,
			EffectiveTo = calendar.EffectiveTo,
			WeeklyOffPattern = calendar.WeeklyOffPattern,
			CustomWeeklyOffs = calendar.CustomWeeklyOffs,
			HolidaySyncMode = calendar.HolidaySyncMode
		}));
		var payrollEx = await Should.ThrowAsync<BusinessException>(() => _payrollPolicies.UpdateAsync(payroll.Id, new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = payroll.AcademicYearId,
			WorkCalendarPolicyId = payroll.WorkCalendarPolicyId,
			Name = $"{payroll.Name} Edited",
			Status = "draft",
			IsDefault = false,
			DayCalculationType = payroll.DayCalculationType,
			ManualDays = payroll.ManualDays,
			ManualMonthDays = payroll.ManualMonthDays,
			SaturdayWorkPolicy = payroll.SaturdayWorkPolicy,
			HolidayOverrides = payroll.HolidayOverrides
		}));
		var attendanceEx = await Should.ThrowAsync<BusinessException>(() => _attendancePolicies.UpdateAsync(attendance.Id, new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = attendance.AcademicYearId,
			WorkCalendarPolicyId = attendance.WorkCalendarPolicyId,
			Name = $"{attendance.Name} Edited",
			Status = "draft",
			IsDefault = false,
			CaptureMode = attendance.CaptureMode,
			FullDayThresholdPercentage = attendance.FullDayThresholdPercentage,
			HalfDayThresholdPercentage = attendance.HalfDayThresholdPercentage,
			AbsentThresholdPercentage = attendance.AbsentThresholdPercentage,
			MissingPunchBehavior = attendance.MissingPunchBehavior,
			ManualCorrectionAllowed = attendance.ManualCorrectionAllowed,
			GraceTime = attendance.GraceTime,
			MaxLateAllowedPerMonth = attendance.MaxLateAllowedPerMonth,
			PenaltyType = attendance.PenaltyType,
			PenaltyCountThreshold = attendance.PenaltyCountThreshold,
			EarlyExitEnabled = attendance.EarlyExitEnabled,
			EarlyExitGraceTime = attendance.EarlyExitGraceTime,
			OvertimeEnabled = attendance.OvertimeEnabled,
			OvertimeRate = attendance.OvertimeRate,
			MinOvertimeMinutes = attendance.MinOvertimeMinutes,
			HolidayWorkBehavior = attendance.HolidayWorkBehavior,
			SandwichRuleEnabled = attendance.SandwichRuleEnabled,
			PayrollPreviewEnabled = attendance.PayrollPreviewEnabled
		}));

		calendarEx.Code.ShouldBe("ShikshaMitra:ArchivedPolicyCannotBeEdited");
		payrollEx.Code.ShouldBe("ShikshaMitra:ArchivedPolicyCannotBeEdited");
		attendanceEx.Code.ShouldBe("ShikshaMitra:ArchivedPolicyCannotBeEdited");
	}

	[Fact]
	public async Task Policy_Create_Should_Reject_Archived_Status()
	{
		var context = await CreatePolicyContextAsync();

		var calendarEx = await CaptureExceptionAsync(() => _workCalendars.CreateAsync(new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			Name = $"Archived Create Calendar {Guid.NewGuid():N}",
			Status = "archived",
			EffectiveFrom = new DateTime(2026, 4, 1),
			WeeklyOffPattern = "saturday_sunday",
			HolidaySyncMode = "linked_with_overrides"
		}));
		var payrollEx = await CaptureExceptionAsync(() => _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Archived Create Payroll {Guid.NewGuid():N}",
			Status = "archived",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		}));
		var attendanceEx = await CaptureExceptionAsync(() => _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Archived Create Attendance {Guid.NewGuid():N}",
			Status = "archived",
			IsDefault = false,
			FullDayThresholdPercentage = 80,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		}));

		AssertArchivedStatusRejected(calendarEx);
		AssertArchivedStatusRejected(payrollEx);
		AssertArchivedStatusRejected(attendanceEx);
	}

	[Fact]
	public async Task Policy_Update_Should_Reject_Archiving_By_Status()
	{
		var context = await CreatePolicyContextAsync();
		var calendar = await CreateWorkCalendarAsync(context.AcademicYearId, "StatusArchive");
		var payroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Status Archive Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		var attendance = await _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Status Archive Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			FullDayThresholdPercentage = 80,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		});

		var calendarEx = await Should.ThrowAsync<Exception>(() => _workCalendars.UpdateAsync(calendar.Id, new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = calendar.AcademicYearId,
			Name = calendar.Name,
			Status = "archived",
			EffectiveFrom = calendar.EffectiveFrom,
			EffectiveTo = calendar.EffectiveTo,
			WeeklyOffPattern = calendar.WeeklyOffPattern,
			CustomWeeklyOffs = calendar.CustomWeeklyOffs,
			HolidaySyncMode = calendar.HolidaySyncMode
		}));
		var payrollEx = await Should.ThrowAsync<Exception>(() => _payrollPolicies.UpdateAsync(payroll.Id, new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = payroll.AcademicYearId,
			WorkCalendarPolicyId = payroll.WorkCalendarPolicyId,
			Name = payroll.Name,
			Status = "archived",
			IsDefault = false,
			DayCalculationType = payroll.DayCalculationType,
			ManualDays = payroll.ManualDays,
			ManualMonthDays = payroll.ManualMonthDays,
			SaturdayWorkPolicy = payroll.SaturdayWorkPolicy,
			HolidayOverrides = payroll.HolidayOverrides
		}));
		var attendanceEx = await Should.ThrowAsync<Exception>(() => _attendancePolicies.UpdateAsync(attendance.Id, new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = attendance.AcademicYearId,
			WorkCalendarPolicyId = attendance.WorkCalendarPolicyId,
			Name = attendance.Name,
			Status = "archived",
			IsDefault = false,
			CaptureMode = attendance.CaptureMode,
			FullDayThresholdPercentage = attendance.FullDayThresholdPercentage,
			HalfDayThresholdPercentage = attendance.HalfDayThresholdPercentage,
			AbsentThresholdPercentage = attendance.AbsentThresholdPercentage,
			MissingPunchBehavior = attendance.MissingPunchBehavior,
			ManualCorrectionAllowed = attendance.ManualCorrectionAllowed,
			GraceTime = attendance.GraceTime,
			MaxLateAllowedPerMonth = attendance.MaxLateAllowedPerMonth,
			PenaltyType = attendance.PenaltyType,
			PenaltyCountThreshold = attendance.PenaltyCountThreshold,
			EarlyExitEnabled = attendance.EarlyExitEnabled,
			EarlyExitGraceTime = attendance.EarlyExitGraceTime,
			OvertimeEnabled = attendance.OvertimeEnabled,
			OvertimeRate = attendance.OvertimeRate,
			MinOvertimeMinutes = attendance.MinOvertimeMinutes,
			HolidayWorkBehavior = attendance.HolidayWorkBehavior,
			SandwichRuleEnabled = attendance.SandwichRuleEnabled,
			PayrollPreviewEnabled = attendance.PayrollPreviewEnabled
		}));

		AssertArchivedStatusRejected(calendarEx);
		AssertArchivedStatusRejected(payrollEx);
		AssertArchivedStatusRejected(attendanceEx);
		(await _workCalendars.GetAsync(calendar.Id)).Status.ShouldBe("active");
		(await _payrollPolicies.GetAsync(payroll.Id)).Status.ShouldBe("active");
		(await _attendancePolicies.GetAsync(attendance.Id)).Status.ShouldBe("active");
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
	public async Task Policy_Audit_Summary_Should_Accept_Policy_Entity_Alias()
	{
		var context = await CreatePolicyContextAsync();

		var audit = await _policySafety.GetAuditSummaryAsync("payroll_policy", context.PayrollPolicy.Id);

		audit.PolicyType.ShouldBe("payroll");
		audit.PolicyId.ShouldBe(context.PayrollPolicy.Id);
	}

	[Fact]
	public async Task Policy_Impact_Should_Accept_WorkCalendar_Alias()
	{
		var context = await CreatePolicyContextAsync();

		var impact = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "workcalendar",
			PolicyId = context.WorkCalendar.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "update"
		});

		impact.PolicyType.ShouldBe("work_calendar");
	}

	[Fact]
	public async Task Policy_Impact_Should_Reject_Unsupported_WorkCalendar_Hyphen_Alias()
	{
		var context = await CreatePolicyContextAsync();

		await Should.ThrowAsync<AbpValidationException>(() => _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "work-calendar",
			PolicyId = context.WorkCalendar.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "update"
		}));
	}

	[Fact]
	public async Task Policy_Impact_Should_Validate_Proposed_Payroll_Request()
	{
		var context = await CreatePolicyContextAsync();

		await Should.ThrowAsync<AbpValidationException>(() => _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "payroll",
			PolicyId = context.PayrollPolicy.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "update",
			PayrollPolicy = new CreateUpdatePayrollPolicyDto
			{
				AcademicYearId = context.AcademicYearId,
				WorkCalendarPolicyId = context.WorkCalendar.Id,
				Name = $"Invalid Preview Payroll {Guid.NewGuid():N}",
				Status = "active",
				IsDefault = false,
				DayCalculationType = "manual-days",
				ManualMonthDays =
				[
					new() { Year = 2026, Month = 5, ManualDays = 24 },
					new() { Year = 2026, Month = 5, ManualDays = 25 }
				],
				SaturdayWorkPolicy = "full-day"
			}
		}));
	}

	[Fact]
	public async Task Policy_Impact_Should_Return_Ordered_Payroll_Risk_Periods_With_Date_Parts()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var snapshotRepository = GetRequiredService<IRepository<PayrollRunItemSnapshot, Guid>>();
		await snapshotRepository.InsertAsync(new PayrollRunItemSnapshot(
			_guidGenerator.Create(),
			_currentTenant.Id,
			"2026-06",
			employee.Id)
		{
			PayrollPolicyId = context.PayrollPolicy.Id,
			IsLocked = true
		}, autoSave: false);
		await snapshotRepository.InsertAsync(new PayrollRunItemSnapshot(
			_guidGenerator.Create(),
			_currentTenant.Id,
			"2026-04",
			employee.Id)
		{
			PayrollPolicyId = context.PayrollPolicy.Id
		}, autoSave: false);
		await snapshotRepository.InsertAsync(new PayrollRunItemSnapshot(
			_guidGenerator.Create(),
			_currentTenant.Id,
			"2026-05",
			employee.Id)
		{
			PayrollPolicyId = context.PayrollPolicy.Id,
			IsLocked = true
		}, autoSave: true);

		var impact = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "payroll",
			PolicyId = context.PayrollPolicy.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "update"
		});

		impact.AffectedPayrollMonths.Select(x => x.Label).ShouldBe(["2026-04", "2026-05", "2026-06"]);
		impact.AffectedPayrollMonths.Select(x => (x.Year, x.Month)).ShouldBe([(2026, 4), (2026, 5), (2026, 6)]);
		impact.LockedPayrollMonths.Select(x => x.Label).ShouldBe(["2026-05", "2026-06"]);
		impact.LockedPayrollMonths.Select(x => (x.Year, x.Month)).ShouldBe([(2026, 5), (2026, 6)]);
	}

	[Fact]
	public async Task WorkCalendar_Update_Impact_Should_Not_Show_Archive_Guidance()
	{
		var context = await CreatePolicyContextAsync();

		var impact = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "work_calendar",
			PolicyId = context.WorkCalendar.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "update"
		});

		var warning = impact.Warnings.Single(x => x.Code == "ShikshaMitra:WorkCalendarPolicyReferenced");
		warning.Message.ShouldContain("before changing this calendar away from active");
		warning.Message.ShouldNotContain("before archiving this calendar");
	}

	[Fact]
	public async Task Policy_Impact_Should_Block_Missing_Proposed_WorkCalendar()
	{
		var context = await CreatePolicyContextAsync();
		var missingWorkCalendarId = Guid.NewGuid();

		var impact = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "payroll",
			AcademicYearId = context.AcademicYearId,
			Action = "update",
			PayrollPolicy = new CreateUpdatePayrollPolicyDto
			{
				AcademicYearId = context.AcademicYearId,
				WorkCalendarPolicyId = missingWorkCalendarId,
				Name = $"Missing Calendar Payroll {Guid.NewGuid():N}",
				Status = "active",
				IsDefault = false,
				DayCalculationType = "working-days",
				SaturdayWorkPolicy = "full-day"
			}
		});

		impact.BlockingWarnings.ShouldContain(x =>
			x.Code == "ShikshaMitra:InvalidWorkCalendarPolicy" &&
			x.Message.Contains(missingWorkCalendarId.ToString("D")));
		impact.RiskSummary.ShouldNotBeNullOrWhiteSpace();
		impact.RiskSummary.ShouldContain("blocking");
		impact.RecommendedAction.ShouldNotBeNullOrWhiteSpace();
		impact.RecommendedAction.ShouldContain("Resolve");
	}

	[Fact]
	public async Task Policy_Impact_Should_Block_Mismatched_Proposed_WorkCalendar_AcademicYear()
	{
		var context = await CreatePolicyContextAsync();
		var foreignCalendar = await CreateForeignActiveWorkCalendarDirectlyAsync();

		var impact = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "payroll",
			AcademicYearId = context.AcademicYearId,
			Action = "update",
			PayrollPolicy = new CreateUpdatePayrollPolicyDto
			{
				AcademicYearId = context.AcademicYearId,
				WorkCalendarPolicyId = foreignCalendar.Id,
				Name = $"Foreign Calendar Payroll {Guid.NewGuid():N}",
				Status = "active",
				IsDefault = false,
				DayCalculationType = "working-days",
				SaturdayWorkPolicy = "full-day"
			}
		});

		impact.BlockingWarnings.ShouldContain(x =>
			x.Code == "ShikshaMitra:InvalidWorkCalendarPolicy" &&
			x.Message.Contains("different academic year"));
		impact.RequiresConfirmation.ShouldBeFalse();
		impact.ConfirmationToken.ShouldBeNull();
		impact.RiskSummary.ShouldNotBeNullOrWhiteSpace();
		impact.RiskSummary.ShouldContain("blocking");
		impact.RecommendedAction.ShouldNotBeNullOrWhiteSpace();
		impact.RecommendedAction.ShouldContain("Resolve");
	}

	[Fact]
	public async Task Policy_Impact_Should_Block_Active_Proposed_Policy_With_Draft_WorkCalendar()
	{
		var context = await CreatePolicyContextAsync();
		var draftCalendar = await _workCalendars.CreateAsync(new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			Name = $"Draft Impact Calendar {Guid.NewGuid():N}",
			Status = "draft",
			EffectiveFrom = new DateTime(2026, 4, 1),
			EffectiveTo = new DateTime(2027, 3, 31),
			WeeklyOffPattern = "saturday_sunday",
			HolidaySyncMode = "linked_with_overrides"
		});

		var impact = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "payroll",
			AcademicYearId = context.AcademicYearId,
			Action = "update",
			PayrollPolicy = new CreateUpdatePayrollPolicyDto
			{
				AcademicYearId = context.AcademicYearId,
				WorkCalendarPolicyId = draftCalendar.Id,
				Name = $"Draft Calendar Payroll {Guid.NewGuid():N}",
				Status = "active",
				IsDefault = false,
				DayCalculationType = "working-days",
				SaturdayWorkPolicy = "full-day"
			}
		});

		impact.BlockingWarnings.ShouldContain(x =>
			x.Code == "ShikshaMitra:WorkCalendarPolicyMustBeActive" &&
			x.Message.Contains("draft"));
		impact.RequiresConfirmation.ShouldBeFalse();
		impact.ConfirmationToken.ShouldBeNull();
		impact.RiskSummary.ShouldNotBeNullOrWhiteSpace();
		impact.RiskSummary.ShouldContain("blocking");
		impact.RecommendedAction.ShouldNotBeNullOrWhiteSpace();
		impact.RecommendedAction.ShouldContain("Resolve");
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
		impact.Warnings.ShouldContain(x => x.Code == HRErrorCodes.DefaultPolicyWillBeReplaced);
		impact.RequiresConfirmation.ShouldBeTrue();
	}

	[Fact]
	public async Task Payroll_Archive_Impact_Should_Report_Assigned_Employees()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var assignedPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Assigned Archive Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		var assignment = BuildAssignment(
			context,
			"employee",
			employee.Id.ToString("D"),
			employee.FullName);
		assignment.PayrollPolicyId = assignedPayroll.Id;
		await CreateAssignmentWithImpactTokenAsync(assignment);

		var impact = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "payroll",
			PolicyId = assignedPayroll.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "archive"
		});

		impact.AffectedAssignmentCount.ShouldBeGreaterThanOrEqualTo(1);
		impact.AffectedEmployeeCount.ShouldBeGreaterThanOrEqualTo(1);
		impact.AffectedEmployees.ShouldContain(x => x.EmployeeId == employee.Id);
		impact.Warnings.ShouldContain(x => x.Code == "ShikshaMitra:AffectedEmployees");
		impact.RequiresConfirmation.ShouldBeTrue();
		impact.RiskSummary.ShouldNotBeNullOrWhiteSpace();
		impact.RiskSummary.ShouldContain("policy notice");
		impact.RecommendedAction.ShouldNotBeNullOrWhiteSpace();
		impact.RecommendedAction.ShouldContain("confirming");
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
		impact.Warnings.ShouldContain(x => x.Code == "ShikshaMitra:WorkCalendarPolicyInUse" && x.Severity == "blocking");
		impact.RequiresConfirmation.ShouldBeFalse();
		impact.ConfirmationToken.ShouldBeNull();
	}

	[Fact]
	public async Task WorkCalendar_Archive_Should_Block_When_Referenced_By_Active_Policies()
	{
		var context = await CreatePolicyContextAsync();

		var ex = await Should.ThrowAsync<BusinessException>(() => _workCalendars.DeleteAsync(context.WorkCalendar.Id));

		ex.Code.ShouldBe("ShikshaMitra:HrPolicyImpactBlocking");
		var warnings = ex.Data["warnings"]?.ToString() ?? string.Empty;
		warnings.ShouldContain(context.PayrollPolicy.Name);
		warnings.ShouldContain(context.AttendancePolicy.Name);
	}

	[Fact]
	public async Task WorkCalendar_Archive_Should_Block_When_Referenced_By_Draft_Payroll()
	{
		var academicYearId = await CreateAcademicYearAsync();
		var calendar = await CreateWorkCalendarAsync(academicYearId, "DraftPayrollRef");
		await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = academicYearId,
			WorkCalendarPolicyId = calendar.Id,
			Name = $"Draft Payroll {Guid.NewGuid():N}",
			Status = "draft",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});

		var ex = await Should.ThrowAsync<BusinessException>(() => _workCalendars.DeleteAsync(calendar.Id));

		ex.Code.ShouldBe("ShikshaMitra:HrPolicyImpactBlocking");
		(ex.Data["warnings"]?.ToString() ?? string.Empty).ShouldContain("Draft Payroll");
	}

	[Fact]
	public async Task WorkCalendar_Archive_Should_Block_When_Referenced_By_Draft_Attendance()
	{
		var academicYearId = await CreateAcademicYearAsync();
		var calendar = await CreateWorkCalendarAsync(academicYearId, "DraftAttendanceRef");
		await _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = academicYearId,
			WorkCalendarPolicyId = calendar.Id,
			Name = $"Draft Attendance {Guid.NewGuid():N}",
			Status = "draft",
			IsDefault = false,
			FullDayThresholdPercentage = 75,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		});

		var ex = await Should.ThrowAsync<BusinessException>(() => _workCalendars.DeleteAsync(calendar.Id));

		ex.Code.ShouldBe("ShikshaMitra:HrPolicyImpactBlocking");
		(ex.Data["warnings"]?.ToString() ?? string.Empty).ShouldContain("Draft Attendance");
	}

	[Fact]
	public async Task WorkCalendar_Archive_Should_Succeed_After_References_Are_Reassigned()
	{
		var context = await CreatePolicyContextAsync();
		var replacement = await CreateWorkCalendarAsync(context.AcademicYearId, "ArchiveReplacement");
		await _payrollPolicies.UpdateAsync(context.PayrollPolicy.Id, new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = replacement.Id,
			Name = context.PayrollPolicy.Name,
			Status = "active",
			IsDefault = true,
			DayCalculationType = context.PayrollPolicy.DayCalculationType,
			SaturdayWorkPolicy = context.PayrollPolicy.SaturdayWorkPolicy
		});
		await _attendancePolicies.UpdateAsync(context.AttendancePolicy.Id, new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = replacement.Id,
			Name = context.AttendancePolicy.Name,
			Status = "active",
			IsDefault = true,
			FullDayThresholdPercentage = context.AttendancePolicy.FullDayThresholdPercentage,
			HalfDayThresholdPercentage = context.AttendancePolicy.HalfDayThresholdPercentage,
			AbsentThresholdPercentage = context.AttendancePolicy.AbsentThresholdPercentage,
			MissingPunchBehavior = context.AttendancePolicy.MissingPunchBehavior,
			ManualCorrectionAllowed = context.AttendancePolicy.ManualCorrectionAllowed,
			HolidayWorkBehavior = context.AttendancePolicy.HolidayWorkBehavior,
			PayrollPreviewEnabled = context.AttendancePolicy.PayrollPreviewEnabled
		});

		await _workCalendars.DeleteAsync(context.WorkCalendar.Id);

		(await _workCalendars.GetAsync(context.WorkCalendar.Id)).Status.ShouldBe("archived");
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

	[Fact]
	public async Task EffectivePolicyPreview_Should_Reject_Missing_Request_Scope()
	{
		var context = await CreatePolicyContextAsync();

		await Should.ThrowAsync<AbpValidationException>(() =>
			_assignments.GetPreviewAsync(new PolicyAssignmentPreviewRequestDto
			{
				AcademicYearId = context.AcademicYearId,
				Date = new DateTime(2026, 5, 1)
			}));

		await Should.ThrowAsync<AbpValidationException>(() =>
			_assignments.GetPreviewAsync(new PolicyAssignmentPreviewRequestDto
			{
				Date = new DateTime(2026, 5, 1),
				GroupId = Guid.NewGuid()
			}));

		await Should.ThrowAsync<AbpValidationException>(() =>
			_assignments.GetPreviewAsync(new PolicyAssignmentPreviewRequestDto
			{
				AcademicYearId = context.AcademicYearId,
				GroupId = Guid.NewGuid()
			}));
	}

	[Fact]
	public async Task EffectivePolicyResolution_Should_Reject_Mismatched_WorkCalendar_AcademicYear()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var foreignCalendar = await CreateForeignActiveWorkCalendarDirectlyAsync();
		var payrollRepository = GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		var payroll = await payrollRepository.GetAsync(context.PayrollPolicy.Id);
		payroll.Update(
			foreignCalendar.Id,
			payroll.Name,
			payroll.Status,
			payroll.IsDefault,
			payroll.DayCalculationType,
			payroll.ManualDays,
			payroll.GetManualMonthDays(),
			payroll.SaturdayWorkPolicy,
			payroll.GetHolidayOverrides(),
			payroll.GetLockedMonthSettings());
		await payrollRepository.UpdateAsync(payroll, autoSave: true);

		var ex = await CaptureBusinessExceptionAsync(() =>
			_assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1)));

		ex.Code.ShouldBe("ShikshaMitra:InvalidWorkCalendarPolicy");
	}

	[Fact]
	public async Task EffectivePolicyResolution_Should_Reject_Assignment_Policy_AcademicYear_Mismatch()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var foreignCalendar = await CreateForeignActiveWorkCalendarDirectlyAsync();
		var payrollRepository = GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		var foreignPayroll = new PayrollPolicy(
			_guidGenerator.Create(),
			_currentTenant.Id,
			foreignCalendar.AcademicYearId,
			foreignCalendar.Id,
			$"Foreign Assignment Payroll {Guid.NewGuid():N}",
			HrPolicyStatus.Active,
			false,
			DayCalculationType.WorkingDays,
			null,
			[],
			SaturdayWorkPolicy.FullDay,
			[]);
		await payrollRepository.InsertAsync(foreignPayroll, autoSave: true);
		var assignmentRepository = GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		await assignmentRepository.InsertAsync(new HrPolicyAssignment(
			_guidGenerator.Create(),
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Employee,
			employee.Id.ToString("D").ToLowerInvariant(),
			employee.FullName,
			context.AttendancePolicy.Id,
			foreignPayroll.Id,
			null,
			new DateTime(2026, 5, 1),
			null,
			true,
			100,
			"Foreign policy regression"), autoSave: true);

		var ex = await CaptureBusinessExceptionAsync(() =>
			_assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1)));

		ex.Code.ShouldBe("ShikshaMitra:PolicyAssignmentAcademicYearMismatch");
	}

	[Fact]
	public async Task EffectivePolicyResolution_Should_Deterministically_Break_Exact_Assignment_Ties()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var assignmentRepository = GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var preferredPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Tie Preferred Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "total-month-days",
			SaturdayWorkPolicy = "full-day"
		});
		var laterIdPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Tie Later Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		var preferredAssignmentId = Guid.Parse("10000000-0000-0000-0000-000000000001");
		var laterAssignmentId = Guid.Parse("20000000-0000-0000-0000-000000000001");
		var targetKey = employee.Id.ToString("D").ToLowerInvariant();

		await assignmentRepository.InsertAsync(new HrPolicyAssignment(
			laterAssignmentId,
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Employee,
			targetKey,
			employee.FullName,
			context.AttendancePolicy.Id,
			laterIdPayroll.Id,
			null,
			new DateTime(2026, 4, 1),
			null,
			true,
			100,
			"Exact tie later id"), autoSave: true);
		await assignmentRepository.InsertAsync(new HrPolicyAssignment(
			preferredAssignmentId,
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Employee,
			targetKey,
			employee.FullName,
			context.AttendancePolicy.Id,
			preferredPayroll.Id,
			null,
			new DateTime(2026, 4, 1),
			null,
			true,
			100,
			"Exact tie preferred id"), autoSave: true);

		var effective = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1));

		effective.MatchedAssignmentId.ShouldBe(preferredAssignmentId);
		effective.PayrollPolicy.Id.ShouldBe(preferredPayroll.Id);
		effective.ResolutionSteps
			.Where(x => x.AssignmentId.HasValue)
			.Select(x => x.AssignmentId)
			.ShouldBe([preferredAssignmentId, laterAssignmentId]);
	}

	[Fact]
	public async Task EffectivePolicyResolution_Should_Explain_When_Assignment_Loses_By_Effective_Date()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var firstGroup = await CreateRoleGroupAsync(context.AcademicYearId, employee.RoleId, "Earlier Effective");
		var laterGroup = await CreateRoleGroupAsync(context.AcademicYearId, employee.RoleId, "Later Effective");
		var assignmentRepository = GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var earlierAssignmentId = Guid.NewGuid();
		var laterAssignmentId = Guid.NewGuid();

		await assignmentRepository.InsertAsync(new HrPolicyAssignment(
			earlierAssignmentId,
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Group,
			firstGroup.Id.ToString("D").ToLowerInvariant(),
			firstGroup.Name,
			context.AttendancePolicy.Id,
			context.PayrollPolicy.Id,
			null,
			new DateTime(2026, 4, 1),
			null,
			true,
			100,
			"Earlier effective group"), autoSave: true);
		await assignmentRepository.InsertAsync(new HrPolicyAssignment(
			laterAssignmentId,
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Group,
			laterGroup.Id.ToString("D").ToLowerInvariant(),
			laterGroup.Name,
			context.AttendancePolicy.Id,
			context.PayrollPolicy.Id,
			null,
			new DateTime(2026, 5, 1),
			null,
			true,
			100,
			"Later effective group"), autoSave: true);

		var effective = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 15));

		effective.MatchedAssignmentId.ShouldBe(laterAssignmentId);
		effective.ResolutionSteps.Single(x => x.AssignmentId == earlierAssignmentId).SkipReason.ShouldBe("lower_effective_date");
		effective.ResolutionSteps.Single(x => x.AssignmentId == laterAssignmentId).SkipReason.ShouldBe("winner");
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

		var ex = await Should.ThrowAsync<Exception>(() =>
			_assignments.CreateAsync(BuildAssignment(context, targetType, targetKey)));

		if (ex is BusinessException businessException)
		{
			businessException.Code.ShouldBeOneOf(
				"ShikshaMitra:PolicyAssignmentTargetInvalid",
				"ShikshaMitra:PolicyAssignmentTargetRequired",
				"ShikshaMitra:PolicyAssignmentTargetTypeUnsupported");
		}
		else
		{
			ex.ShouldBeOfType<AbpValidationException>();
		}
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
	public async Task EmployeeOverride_Upsert_Should_Be_Repeatable_For_Same_Employee_And_Range()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var totalMonthPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Total Month Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "total-month-days",
			SaturdayWorkPolicy = "full-day"
		});

		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, totalMonthPayroll.Id);
		(await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1)))
			.PayrollPolicy.Id.ShouldBe(totalMonthPayroll.Id);

		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.PayrollPolicy.Id);
		(await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1)))
			.PayrollPolicy.Id.ShouldBe(context.PayrollPolicy.Id);

		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, totalMonthPayroll.Id);
		var effective = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1));

		effective.PayrollPolicy.Id.ShouldBe(totalMonthPayroll.Id);
		var assignments = await _assignments.GetListAsync(new PolicyListRequestDto
		{
			AcademicYearId = context.AcademicYearId,
			TargetType = "employee",
			MaxResultCount = 100
		});
		var employeeAssignments = assignments.Items
			.Where(x => x.IsActive && x.TargetKey == employee.Id.ToString("D").ToLowerInvariant())
			.ToList();
		employeeAssignments.Count.ShouldBe(1);
		employeeAssignments.Single().PayrollPolicyId.ShouldBe(totalMonthPayroll.Id);
	}

	[Fact]
	public async Task EmployeeOverride_Preview_Should_Resolve_Existing_Assignment_When_Id_Is_Missing()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var assignmentRepository = GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var existing = await assignmentRepository.InsertAsync(new HrPolicyAssignment(
			_guidGenerator.Create(),
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Employee,
			employee.Id.ToString("D").ToLowerInvariant(),
			employee.FullName,
			context.AttendancePolicy.Id,
			context.PayrollPolicy.Id,
			null,
			new DateTime(2026, 4, 1),
			null,
			true,
			100,
			"Existing employee payroll step override"), autoSave: true);

		var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = HrPolicyAssignmentImpactService.EmployeeOverrideUpsertAction,
			Assignment = new CreateUpdateHrPolicyAssignmentDto
			{
				AcademicYearId = context.AcademicYearId,
				TargetType = "employee",
				TargetKey = employee.Id.ToString("D"),
				TargetDisplayName = employee.FullName,
				AttendancePolicyId = context.AttendancePolicy.Id,
				PayrollPolicyId = context.PayrollPolicy.Id,
				EffectiveFrom = new DateTime(2026, 5, 31),
				EffectiveTo = null,
				IsActive = true,
				Priority = 100,
				AssignmentReason = "Employee payroll edit without assignment id"
			}
		});

		preview.PolicyId.ShouldBe(existing.Id);
		preview.Action.ShouldBe(HrPolicyAssignmentImpactService.EmployeeOverrideUpsertAction);
		preview.BlockingWarnings.ShouldNotContain(x => x.Code == HRErrorCodes.HrPolicyAssignmentOverlap);
		preview.RequiresConfirmation.ShouldBeTrue();
		preview.ConfirmationToken.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task EmployeeOverride_Upsert_Should_Update_Existing_Override_When_Id_Is_Missing()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var assignmentRepository = GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var replacementPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Employee Edit Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "total-month-days",
			SaturdayWorkPolicy = "full-day"
		});
		var existing = await assignmentRepository.InsertAsync(new HrPolicyAssignment(
			_guidGenerator.Create(),
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Employee,
			employee.Id.ToString("D").ToLowerInvariant(),
			employee.FullName,
			context.AttendancePolicy.Id,
			context.PayrollPolicy.Id,
			null,
			new DateTime(2026, 4, 1),
			null,
			true,
			100,
			"Existing employee payroll step override"), autoSave: true);
		var input = new UpsertEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = context.AcademicYearId,
			AttendancePolicyId = context.AttendancePolicy.Id,
			PayrollPolicyId = replacementPayroll.Id,
			EffectiveFrom = new DateTime(2026, 5, 31),
			AssignmentReason = "Employee payroll edit without assignment id"
		};
		var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = HrPolicyAssignmentImpactService.EmployeeOverrideUpsertAction,
			Assignment = new CreateUpdateHrPolicyAssignmentDto
			{
				AcademicYearId = input.AcademicYearId,
				TargetType = "employee",
				TargetKey = employee.Id.ToString("D"),
				TargetDisplayName = employee.FullName,
				AttendancePolicyId = input.AttendancePolicyId,
				PayrollPolicyId = input.PayrollPolicyId,
				EffectiveFrom = input.EffectiveFrom,
				EffectiveTo = input.EffectiveTo,
				IsActive = true,
				Priority = 100,
				AssignmentReason = input.AssignmentReason
			}
		});
		input.ConfirmationToken = preview.ConfirmationToken;

		var result = await _assignments.UpsertEmployeeOverrideAsync(input);

		result.Id.ShouldBe(existing.Id);
		result.PayrollPolicyId.ShouldBe(replacementPayroll.Id);
		result.EffectiveFrom.ShouldBe(new DateTime(2026, 5, 31));
		(await assignmentRepository.GetAsync(existing.Id)).IsActive.ShouldBeTrue();
	}

	[Fact]
	public async Task EmployeeOverride_Upsert_Should_Honor_Selected_Assignment_Id_When_Duplicates_Exist()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var assignmentRepository = GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var replacementPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Selected Override Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "total-month-days",
			SaturdayWorkPolicy = "full-day"
		});
		var higherPriority = await assignmentRepository.InsertAsync(new HrPolicyAssignment(
			_guidGenerator.Create(),
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Employee,
			employee.Id.ToString("D").ToLowerInvariant(),
			employee.FullName,
			context.AttendancePolicy.Id,
			context.PayrollPolicy.Id,
			null,
			new DateTime(2026, 4, 1),
			null,
			true,
			200,
			"Higher priority duplicate"), autoSave: true);
		var selected = await assignmentRepository.InsertAsync(new HrPolicyAssignment(
			_guidGenerator.Create(),
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Employee,
			employee.Id.ToString("D").ToLowerInvariant(),
			employee.FullName,
			context.AttendancePolicy.Id,
			context.PayrollPolicy.Id,
			null,
			new DateTime(2026, 4, 1),
			null,
			true,
			100,
			"Selected duplicate"), autoSave: true);

		var result = await UpsertEmployeeOverrideWithImpactTokenAsync(new UpsertEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = context.AcademicYearId,
			AssignmentId = selected.Id,
			AttendancePolicyId = context.AttendancePolicy.Id,
			PayrollPolicyId = replacementPayroll.Id,
			EffectiveFrom = new DateTime(2026, 4, 1),
			AssignmentReason = "Selected duplicate upsert"
		});

		result.Id.ShouldBe(selected.Id);
		result.PayrollPolicyId.ShouldBe(replacementPayroll.Id);
		(await assignmentRepository.GetAsync(higherPriority.Id)).IsActive.ShouldBeFalse();
		(await assignmentRepository.GetAsync(selected.Id)).IsActive.ShouldBeTrue();
	}

	[Fact]
	public async Task EmployeeOverride_Upsert_Should_Move_Selected_Assignment_To_New_Range()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var assignmentRepository = GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var replacementPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Moved Override Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "total-month-days",
			SaturdayWorkPolicy = "full-day"
		});
		var selected = await assignmentRepository.InsertAsync(new HrPolicyAssignment(
			_guidGenerator.Create(),
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Employee,
			employee.Id.ToString("D").ToLowerInvariant(),
			employee.FullName,
			context.AttendancePolicy.Id,
			context.PayrollPolicy.Id,
			null,
			new DateTime(2026, 4, 1),
			new DateTime(2026, 4, 30),
			true,
			100,
			"Move selected override"), autoSave: true);

		var result = await UpsertEmployeeOverrideWithImpactTokenAsync(new UpsertEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = context.AcademicYearId,
			AssignmentId = selected.Id,
			AttendancePolicyId = context.AttendancePolicy.Id,
			PayrollPolicyId = replacementPayroll.Id,
			EffectiveFrom = new DateTime(2026, 6, 1),
			EffectiveTo = new DateTime(2026, 6, 30),
			AssignmentReason = "Move selected override"
		});

		result.Id.ShouldBe(selected.Id);
		result.PayrollPolicyId.ShouldBe(replacementPayroll.Id);
		result.EffectiveFrom.ShouldBe(new DateTime(2026, 6, 1));
		result.EffectiveTo.ShouldBe(new DateTime(2026, 6, 30));
	}

	[Fact]
	public async Task EmployeeOverride_Disable_Should_Fall_Back_To_Default_Policies()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var totalMonthPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Disable Override Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "total-month-days",
			SaturdayWorkPolicy = "full-day"
		});

		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, totalMonthPayroll.Id);
		var overridden = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1));
		overridden.PolicySource.ShouldBe("employee");
		overridden.MatchedTargetType.ShouldBe("employee");
		overridden.PayrollPolicy.Id.ShouldBe(totalMonthPayroll.Id);

		await DisableEmployeeOverrideWithImpactTokenAsync(new DisableEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = context.AcademicYearId,
			EffectiveDate = new DateTime(2026, 5, 1),
			AssignmentReason = "Toggle disabled from employee payroll step"
		});

		var fallback = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1));
		fallback.PolicySource.ShouldBe("default");
		fallback.MatchedAssignmentId.ShouldBeNull();
		fallback.PayrollPolicy.Id.ShouldBe(context.PayrollPolicy.Id);
		var assignments = await _assignments.GetListAsync(new PolicyListRequestDto
		{
			AcademicYearId = context.AcademicYearId,
			TargetType = "employee",
			MaxResultCount = 100
		});
		assignments.Items
			.Where(x => x.TargetKey == employee.Id.ToString("D").ToLowerInvariant())
			.ShouldAllBe(x => !x.IsActive || x.EffectiveTo < new DateTime(2026, 5, 1));
	}

	[Fact]
	public async Task EmployeeOverride_Disable_Should_Fall_Back_To_Group_Assignment_When_Present()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var group = await CreateRoleGroupAsync(context.AcademicYearId, employee.RoleId, "Employee Override Fallback Role");
		var groupPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Group Fallback Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "total-month-days",
			SaturdayWorkPolicy = "full-day"
		});
		var overridePayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Removed Override Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "manual-days",
			ManualDays = 24,
			SaturdayWorkPolicy = "full-day"
		});
		var groupAssignment = BuildAssignment(
			context,
			"group",
			group.Id.ToString("D"),
			group.Name,
			effectiveFrom: new DateTime(2026, 4, 1));
		groupAssignment.PayrollPolicyId = groupPayroll.Id;
		var createdGroupAssignment = await CreateAssignmentWithImpactTokenAsync(groupAssignment);
		await UpsertEmployeeOverrideWithImpactTokenAsync(new UpsertEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = context.AcademicYearId,
			AttendancePolicyId = context.AttendancePolicy.Id,
			PayrollPolicyId = overridePayroll.Id,
			EffectiveFrom = new DateTime(2026, 4, 1),
			AssignmentReason = "Employee override before group fallback"
		});

		await DisableEmployeeOverrideWithImpactTokenAsync(new DisableEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = context.AcademicYearId,
			EffectiveDate = new DateTime(2026, 5, 1),
			AssignmentReason = "Use organization policy chain"
		});

		var fallback = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1));
		fallback.PolicySource.ShouldBe("group");
		fallback.MatchedAssignmentId.ShouldBe(createdGroupAssignment.Id);
		fallback.PayrollPolicy.Id.ShouldBe(groupPayroll.Id);
	}

	[Fact]
	public async Task EmployeeOverride_Disable_Preview_Should_Resolve_Assignment_When_Id_Is_Missing()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var assignmentRepository = GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var assignment = await assignmentRepository.InsertAsync(new HrPolicyAssignment(
			_guidGenerator.Create(),
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Employee,
			employee.Id.ToString("D").ToLowerInvariant(),
			employee.FullName,
			context.AttendancePolicy.Id,
			context.PayrollPolicy.Id,
			null,
			new DateTime(2026, 4, 1),
			null,
			true,
			100,
			"Disable without id regression"), autoSave: true);

		var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = HrPolicyAssignmentImpactService.EmployeeOverrideDisableAction,
			Assignment = new CreateUpdateHrPolicyAssignmentDto
			{
				AcademicYearId = context.AcademicYearId,
				TargetType = "employee",
				TargetKey = employee.Id.ToString("D"),
				TargetDisplayName = employee.FullName,
				AttendancePolicyId = context.AttendancePolicy.Id,
				PayrollPolicyId = context.PayrollPolicy.Id,
				EffectiveFrom = new DateTime(2026, 5, 1),
				IsActive = false,
				Priority = 100,
				AssignmentReason = "Disable without id preview"
			}
		});

		preview.PolicyId.ShouldBe(assignment.Id);
		preview.RequiresConfirmation.ShouldBeTrue();
		preview.ConfirmationToken.ShouldNotBeNullOrWhiteSpace();

		await _assignments.DisableEmployeeOverrideAsync(new DisableEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = context.AcademicYearId,
			EffectiveDate = new DateTime(2026, 5, 1),
			AssignmentReason = "Disable without id preview",
			ConfirmationToken = preview.ConfirmationToken
		});

		var fallback = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1));
		fallback.PolicySource.ShouldBe("default");
	}

	[Fact]
	public async Task EmployeeOverride_Disable_Should_Require_Confirmation_When_Override_Is_Active()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var assignmentRepository = GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		await assignmentRepository.InsertAsync(new HrPolicyAssignment(
			_guidGenerator.Create(),
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Employee,
			employee.Id.ToString("D").ToLowerInvariant(),
			employee.FullName,
			context.AttendancePolicy.Id,
			context.PayrollPolicy.Id,
			null,
			new DateTime(2026, 4, 1),
			null,
			true,
			100,
			"Unconfirmed disable regression"), autoSave: true);

		BusinessException? ex = null;
		try
		{
			await _assignments.DisableEmployeeOverrideAsync(new DisableEmployeePolicyOverrideDto
			{
				EmployeeId = employee.Id,
				AcademicYearId = context.AcademicYearId,
				EffectiveDate = new DateTime(2026, 5, 1),
				AssignmentReason = "Unconfirmed disable"
			});
		}
		catch (BusinessException caught)
		{
			ex = caught;
		}

		ex.ShouldNotBeNull();
		ex.Code.ShouldBe("ShikshaMitra:HrPolicyImpactConfirmationRequired");
		var effective = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1));
		effective.PolicySource.ShouldBe("employee");
	}

	[Fact]
	public async Task EmployeeOverride_Disable_Should_Not_Require_Confirmation_When_No_Employees_Are_Affected()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var assignmentRepository = GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var assignment = await assignmentRepository.InsertAsync(new HrPolicyAssignment(
			_guidGenerator.Create(),
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Employee,
			employee.Id.ToString("D").ToLowerInvariant(),
			employee.FullName,
			context.AttendancePolicy.Id,
			context.PayrollPolicy.Id,
			null,
			new DateTime(2026, 4, 1),
			null,
			true,
			100,
			"No affected disable regression"), autoSave: true);
		await _employeeStatus.ChangeStatusAsync(employee.Id, new ChangeEmployeeStatusDto
		{
			EmployeeStatusType = EmployeeStatusType.Inactive,
			EffectiveDate = DateOnly.FromDateTime(new DateTime(2026, 4, 1))
		});

		var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = HrPolicyAssignmentImpactService.EmployeeOverrideDisableAction,
			AssignmentId = assignment.Id,
			Assignment = new CreateUpdateHrPolicyAssignmentDto
			{
				AcademicYearId = context.AcademicYearId,
				TargetType = "employee",
				TargetKey = employee.Id.ToString("D"),
				TargetDisplayName = employee.FullName,
				AttendancePolicyId = context.AttendancePolicy.Id,
				PayrollPolicyId = context.PayrollPolicy.Id,
				EffectiveFrom = new DateTime(2026, 5, 1),
				EffectiveTo = assignment.EffectiveTo,
				IsActive = false,
				Priority = assignment.Priority,
				AssignmentReason = "No affected disable"
			}
		});
		preview.AffectedAssignmentCount.ShouldBe(1);
		preview.RequiresConfirmation.ShouldBeFalse();
		preview.ConfirmationToken.ShouldBeNull();

		await _assignments.DisableEmployeeOverrideAsync(new DisableEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = context.AcademicYearId,
			AssignmentId = assignment.Id,
			EffectiveDate = new DateTime(2026, 5, 1),
			AssignmentReason = "No affected disable"
		});

		var updated = await assignmentRepository.GetAsync(assignment.Id);
		updated.EffectiveTo.ShouldBe(new DateTime(2026, 4, 30));
	}

	[Fact]
	public async Task EmployeeOverride_Disable_Should_Confirm_The_Previewed_Assignment_When_Duplicates_Exist()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var assignmentRepository = GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var preferred = await assignmentRepository.InsertAsync(new HrPolicyAssignment(
			_guidGenerator.Create(),
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Employee,
			employee.Id.ToString("D").ToLowerInvariant(),
			employee.FullName,
			context.AttendancePolicy.Id,
			context.PayrollPolicy.Id,
			null,
			new DateTime(2026, 4, 1),
			null,
			true,
			200,
			"Higher priority duplicate"), autoSave: true);
		var previewed = await assignmentRepository.InsertAsync(new HrPolicyAssignment(
			_guidGenerator.Create(),
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Employee,
			employee.Id.ToString("D").ToLowerInvariant(),
			employee.FullName,
			context.AttendancePolicy.Id,
			context.PayrollPolicy.Id,
			null,
			new DateTime(2026, 4, 1),
			null,
			true,
			100,
			"Previewed duplicate"), autoSave: true);

		var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = HrPolicyAssignmentImpactService.EmployeeOverrideDisableAction,
			AssignmentId = previewed.Id,
			Assignment = new CreateUpdateHrPolicyAssignmentDto
			{
				AcademicYearId = context.AcademicYearId,
				TargetType = "employee",
				TargetKey = employee.Id.ToString("D"),
				TargetDisplayName = employee.FullName,
				AttendancePolicyId = previewed.AttendancePolicyId,
				PayrollPolicyId = previewed.PayrollPolicyId,
				EffectiveFrom = new DateTime(2026, 5, 1),
				EffectiveTo = previewed.EffectiveTo,
				IsActive = false,
				Priority = previewed.Priority,
				AssignmentReason = "Confirmed duplicate disable"
			}
		});
		preview.AffectedAssignmentCount.ShouldBe(2);

		await DisableEmployeeOverrideWithImpactTokenAsync(new DisableEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = context.AcademicYearId,
			AssignmentId = previewed.Id,
			EffectiveDate = new DateTime(2026, 5, 1),
			AssignmentReason = "Confirmed duplicate disable"
		});

		var updatedPreferred = await assignmentRepository.GetAsync(preferred.Id);
		var updatedPreviewed = await assignmentRepository.GetAsync(previewed.Id);
		updatedPreferred.EffectiveTo.ShouldBe(new DateTime(2026, 4, 30));
		updatedPreviewed.EffectiveTo.ShouldBe(new DateTime(2026, 4, 30));
		var fallback = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1));
		fallback.PolicySource.ShouldBe("default");
	}

	[Fact]
	public async Task EmployeeOverride_Disable_Should_NoOp_When_No_Active_Override_Matches()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();

		await _assignments.DisableEmployeeOverrideAsync(new DisableEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = context.AcademicYearId,
			EffectiveDate = new DateTime(2026, 5, 1),
			AssignmentReason = "No active override"
		});

		var effective = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1));
		effective.PolicySource.ShouldBe("default");
	}

	[Fact]
	public async Task EmployeeOverride_Disable_Should_Reject_Missing_Request_Keys()
	{
		await Should.ThrowAsync<AbpValidationException>(() => _assignments.DisableEmployeeOverrideAsync(new DisableEmployeePolicyOverrideDto
		{
			EmployeeId = Guid.Empty,
			AcademicYearId = Guid.Empty,
			EffectiveDate = default
		}));
	}

	[Fact]
	public async Task PayrollPolicy_Archive_Should_Reassign_Overrides_To_Default_Payroll()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var overridePayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Archive Override Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "total-month-days",
			SaturdayWorkPolicy = "full-day"
		});

		var currentAssignment = BuildAssignment(
			context,
			"employee",
			employee.Id.ToString("D"),
			employee.FullName,
			effectiveFrom: new DateTime(2026, 4, 1),
			effectiveTo: new DateTime(2026, 6, 30));
		currentAssignment.PayrollPolicyId = overridePayroll.Id;
		currentAssignment.AssignmentReason = new string('A', 250);
		await CreateAssignmentWithImpactTokenAsync(currentAssignment);
		var futureAssignment = BuildAssignment(
			context,
			"employee",
			employee.Id.ToString("D"),
			employee.FullName,
			effectiveFrom: new DateTime(2026, 7, 1));
		futureAssignment.PayrollPolicyId = overridePayroll.Id;
		futureAssignment.Priority = 20;
		await CreateAssignmentWithImpactTokenAsync(futureAssignment);

		(await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1)))
			.PayrollPolicy.Id.ShouldBe(overridePayroll.Id);

		await RunWithPolicyImpactTokenAsync("payroll", overridePayroll.Id, context.AcademicYearId, "archive", () =>
			_payrollPolicies.DeleteAsync(overridePayroll.Id));

		var current = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1));
		var future = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 7, 1));
		current.PayrollPolicy.Id.ShouldBe(context.PayrollPolicy.Id);
		future.PayrollPolicy.Id.ShouldBe(context.PayrollPolicy.Id);
		current.AttendancePolicy.Id.ShouldBe(context.AttendancePolicy.Id);
		var assignments = await _assignments.GetListAsync(new PolicyListRequestDto
		{
			AcademicYearId = context.AcademicYearId,
			TargetType = "employee",
			MaxResultCount = 100
		});
		assignments.Items
			.Where(x => x.IsActive && x.TargetKey == employee.Id.ToString("D").ToLowerInvariant())
			.ShouldAllBe(x => x.PayrollPolicyId == context.PayrollPolicy.Id);
		assignments.Items
			.Where(x => x.IsActive && x.TargetKey == employee.Id.ToString("D").ToLowerInvariant())
			.ShouldAllBe(x => x.AssignmentReason == null || x.AssignmentReason.Length <= 256);
	}

	[Fact]
	public async Task PayrollPolicy_Archive_Should_Block_Reassignment_To_Default_With_NonActive_WorkCalendar()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var overrideCalendar = await CreateWorkCalendarAsync(context.AcademicYearId, "ArchiveOverrideCalendar");
		var overridePayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = overrideCalendar.Id,
			Name = $"Archive Bad Default Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		var assignment = BuildAssignment(
			context,
			"employee",
			employee.Id.ToString("D"),
			employee.FullName);
		assignment.PayrollPolicyId = overridePayroll.Id;
		await CreateAssignmentWithImpactTokenAsync(assignment);
		await MarkWorkCalendarDraftAsync(context.WorkCalendar.Id);

		var preview = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "payroll",
			PolicyId = overridePayroll.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "archive"
		});
		preview.BlockingWarnings.ShouldContain(x => x.Code == "ShikshaMitra:WorkCalendarPolicyMustBeActive");
		preview.RequiresConfirmation.ShouldBeFalse();
		preview.ConfirmationToken.ShouldBeNull();

		var ex = await Should.ThrowAsync<BusinessException>(() =>
			_payrollPolicies.DeleteAsync(overridePayroll.Id));

		ex.Code.ShouldBe("ShikshaMitra:HrPolicyImpactBlocking");
		(await _payrollPolicies.GetAsync(overridePayroll.Id)).Status.ShouldBe("active");
	}

	[Fact]
	public async Task PayrollPolicy_Archive_Should_Block_Reassignment_When_Default_Fallback_Is_Ambiguous()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var overridePayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Archive Ambiguous Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		var secondDefault = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Ambiguous Default Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		var payrollRepository = GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		var duplicateDefault = await payrollRepository.GetAsync(secondDefault.Id);
		duplicateDefault.SetDefault(true);
		await payrollRepository.UpdateAsync(duplicateDefault, autoSave: true);

		var assignment = BuildAssignment(
			context,
			"employee",
			employee.Id.ToString("D"),
			employee.FullName);
		assignment.PayrollPolicyId = overridePayroll.Id;
		await CreateAssignmentWithImpactTokenAsync(assignment);

		var preview = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "payroll",
			PolicyId = overridePayroll.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "archive"
		});
		preview.BlockingWarnings.ShouldContain(x => x.Code == "ShikshaMitra:DefaultPayrollPolicyRequired");
		preview.RequiresConfirmation.ShouldBeFalse();
		preview.ConfirmationToken.ShouldBeNull();

		var ex = await Should.ThrowAsync<BusinessException>(() =>
			_payrollPolicies.DeleteAsync(overridePayroll.Id));

		ex.Code.ShouldBe("ShikshaMitra:HrPolicyImpactBlocking");
		(await _payrollPolicies.GetAsync(overridePayroll.Id)).Status.ShouldBe("active");
	}

	[Fact]
	public async Task AttendancePolicy_Archive_Should_Reassign_Overrides_To_Default_Attendance()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var overrideAttendance = await _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Archive Override Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			FullDayThresholdPercentage = 80,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		});

		await UpsertEmployeeOverrideWithImpactTokenAsync(new UpsertEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = context.AcademicYearId,
			AttendancePolicyId = overrideAttendance.Id,
			PayrollPolicyId = context.PayrollPolicy.Id,
			EffectiveFrom = new DateTime(2026, 4, 1),
			AssignmentReason = "Attendance archive fallback regression"
		});

		(await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1)))
			.AttendancePolicy.Id.ShouldBe(overrideAttendance.Id);

		await RunWithPolicyImpactTokenAsync("attendance", overrideAttendance.Id, context.AcademicYearId, "archive", () =>
			_attendancePolicies.DeleteAsync(overrideAttendance.Id));

		var effective = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1));
		effective.AttendancePolicy.Id.ShouldBe(context.AttendancePolicy.Id);
		effective.PayrollPolicy.Id.ShouldBe(context.PayrollPolicy.Id);
		var assignments = await _assignments.GetListAsync(new PolicyListRequestDto
		{
			AcademicYearId = context.AcademicYearId,
			TargetType = "employee",
			MaxResultCount = 100
		});
		assignments.Items
			.Where(x => x.IsActive && x.TargetKey == employee.Id.ToString("D").ToLowerInvariant())
			.ShouldAllBe(x => x.AttendancePolicyId == context.AttendancePolicy.Id);
	}

	[Fact]
	public async Task AttendancePolicy_Archive_Should_Block_Reassignment_To_Default_With_NonActive_WorkCalendar()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var overrideCalendar = await CreateWorkCalendarAsync(context.AcademicYearId, "ArchiveAttendanceOverrideCalendar");
		var overrideAttendance = await _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = overrideCalendar.Id,
			Name = $"Archive Bad Default Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			FullDayThresholdPercentage = 80,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		});
		await UpsertEmployeeOverrideWithImpactTokenAsync(new UpsertEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = context.AcademicYearId,
			AttendancePolicyId = overrideAttendance.Id,
			PayrollPolicyId = context.PayrollPolicy.Id,
			EffectiveFrom = new DateTime(2026, 4, 1),
			AssignmentReason = "Archive attendance bad default regression"
		});
		await MarkWorkCalendarDraftAsync(context.WorkCalendar.Id);

		var preview = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "attendance",
			PolicyId = overrideAttendance.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "archive"
		});
		preview.BlockingWarnings.ShouldContain(x => x.Code == "ShikshaMitra:WorkCalendarPolicyMustBeActive");
		preview.RequiresConfirmation.ShouldBeFalse();
		preview.ConfirmationToken.ShouldBeNull();

		var ex = await Should.ThrowAsync<BusinessException>(() =>
			_attendancePolicies.DeleteAsync(overrideAttendance.Id));

		ex.Code.ShouldBe("ShikshaMitra:HrPolicyImpactBlocking");
		(await _attendancePolicies.GetAsync(overrideAttendance.Id)).Status.ShouldBe("active");
	}

	[Fact]
	public async Task AttendancePolicy_Archive_Should_Block_Reassignment_When_Default_Fallback_Is_Ambiguous()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var overrideAttendance = await _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Archive Ambiguous Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			FullDayThresholdPercentage = 80,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		});
		var secondDefault = await _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Ambiguous Default Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			FullDayThresholdPercentage = 80,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		});
		var attendanceRepository = GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		var duplicateDefault = await attendanceRepository.GetAsync(secondDefault.Id);
		duplicateDefault.SetDefault(true);
		await attendanceRepository.UpdateAsync(duplicateDefault, autoSave: true);

		await UpsertEmployeeOverrideWithImpactTokenAsync(new UpsertEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = context.AcademicYearId,
			AttendancePolicyId = overrideAttendance.Id,
			PayrollPolicyId = context.PayrollPolicy.Id,
			EffectiveFrom = new DateTime(2026, 4, 1),
			AssignmentReason = "Archive attendance ambiguous default regression"
		});

		var preview = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "attendance",
			PolicyId = overrideAttendance.Id,
			AcademicYearId = context.AcademicYearId,
			Action = "archive"
		});
		preview.BlockingWarnings.ShouldContain(x => x.Code == "ShikshaMitra:DefaultAttendancePolicyRequired");
		preview.RequiresConfirmation.ShouldBeFalse();
		preview.ConfirmationToken.ShouldBeNull();

		var ex = await Should.ThrowAsync<BusinessException>(() =>
			_attendancePolicies.DeleteAsync(overrideAttendance.Id));

		ex.Code.ShouldBe("ShikshaMitra:HrPolicyImpactBlocking");
		(await _attendancePolicies.GetAsync(overrideAttendance.Id)).Status.ShouldBe("active");
	}

	[Fact]
	public async Task EmployeeOverride_Disable_Should_Not_Deactivate_Future_Override()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var currentPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Current Override Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "total-month-days",
			SaturdayWorkPolicy = "full-day"
		});
		var futurePayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Future Override Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "manual-days",
			ManualDays = 24,
			SaturdayWorkPolicy = "full-day"
		});

		await UpsertEmployeeOverrideWithImpactTokenAsync(new UpsertEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = context.AcademicYearId,
			AttendancePolicyId = context.AttendancePolicy.Id,
			PayrollPolicyId = currentPayroll.Id,
			EffectiveFrom = new DateTime(2026, 4, 1),
			EffectiveTo = new DateTime(2026, 6, 30),
			AssignmentReason = "Current payroll step override"
		});
		await CreateAssignmentWithImpactTokenAsync(new CreateUpdateHrPolicyAssignmentDto
		{
			AcademicYearId = context.AcademicYearId,
			TargetType = "employee",
			TargetKey = employee.Id.ToString("D"),
			TargetDisplayName = employee.FullName,
			PayrollPolicyId = futurePayroll.Id,
			AttendancePolicyId = context.AttendancePolicy.Id,
			EffectiveFrom = new DateTime(2026, 7, 1),
			IsActive = true,
			Priority = 100,
			AssignmentReason = "Future scheduled override"
		});

		await DisableEmployeeOverrideWithImpactTokenAsync(new DisableEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = context.AcademicYearId,
			EffectiveDate = new DateTime(2026, 5, 1),
			AssignmentReason = "Disable current override only"
		});

		var fallback = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1));
		fallback.PolicySource.ShouldBe("default");
		fallback.PayrollPolicy.Id.ShouldBe(context.PayrollPolicy.Id);

		var future = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 7, 1));
		future.PolicySource.ShouldBe("employee");
		future.PayrollPolicy.Id.ShouldBe(futurePayroll.Id);
	}

	[Fact]
	public async Task EmployeeOverride_Upsert_Should_Deactivate_Competing_Active_Overrides()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var targetPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Overwrite Target Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "total-month-days",
			SaturdayWorkPolicy = "full-day"
		});
		var stalePayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Stale Duplicate Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "manual-days",
			ManualDays = 24,
			SaturdayWorkPolicy = "full-day"
		});

		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, context.PayrollPolicy.Id);
		var assignmentRepository = GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		await assignmentRepository.InsertAsync(new HrPolicyAssignment(
			_guidGenerator.Create(),
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Employee,
			employee.Id.ToString("D"),
			employee.FullName,
			context.AttendancePolicy.Id,
			stalePayroll.Id,
			null,
			new DateTime(2026, 5, 1),
			null,
			true,
			100,
			"Historical duplicate active override"), autoSave: true);

		var unconfirmedUpdate = new UpsertEmployeePolicyOverrideDto
		{
			AcademicYearId = context.AcademicYearId,
			EmployeeId = employee.Id,
			AttendancePolicyId = context.AttendancePolicy.Id,
			PayrollPolicyId = targetPayroll.Id,
			EffectiveFrom = new DateTime(2026, 5, 1),
			AssignmentReason = "Unconfirmed update"
		};
		var unconfirmedEx = await Should.ThrowAsync<BusinessException>(() =>
			_assignments.UpsertEmployeeOverrideAsync(unconfirmedUpdate));
		unconfirmedEx.Code.ShouldBe("ShikshaMitra:HrPolicyImpactConfirmationRequired");

		var beforeConfirmedAssignments = await _assignments.GetListAsync(new PolicyListRequestDto
		{
			AcademicYearId = context.AcademicYearId,
			TargetType = "employee",
			MaxResultCount = 100
		});
		beforeConfirmedAssignments.Items
			.Count(x => x.IsActive && x.TargetKey == employee.Id.ToString("D").ToLowerInvariant())
			.ShouldBe(2);

		await UpsertEmployeePayrollPolicyAsync(context, employee.Id, targetPayroll.Id);

		var effective = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1));
		effective.PayrollPolicy.Id.ShouldBe(targetPayroll.Id);
		var assignments = await _assignments.GetListAsync(new PolicyListRequestDto
		{
			AcademicYearId = context.AcademicYearId,
			TargetType = "employee",
			MaxResultCount = 100
		});
		var activeEmployeeAssignments = assignments.Items
			.Where(x => x.IsActive && x.TargetKey == employee.Id.ToString("D").ToLowerInvariant())
			.ToList();
		activeEmployeeAssignments.Count.ShouldBe(1);
		activeEmployeeAssignments.Single().PayrollPolicyId.ShouldBe(targetPayroll.Id);
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

	[Fact]
	public async Task Assignment_Should_Reject_Missing_Module_Policy_References_With_Policy_Errors()
	{
		var context = await CreatePolicyContextAsync();
		var group = await CreateGroupAsync(context.AcademicYearId, "MissingPolicyReferences");
		var missingPayrollInput = BuildAssignment(context, "group", group.Id.ToString("D"), group.Name);
		missingPayrollInput.PayrollPolicyId = Guid.NewGuid();
		var missingAttendanceInput = BuildAssignment(context, "group", group.Id.ToString("D"), group.Name);
		missingAttendanceInput.AttendancePolicyId = Guid.NewGuid();

		var payrollEx = await Should.ThrowAsync<BusinessException>(() => _assignments.CreateAsync(missingPayrollInput));
		var attendanceEx = await Should.ThrowAsync<BusinessException>(() => _assignments.CreateAsync(missingAttendanceInput));

		payrollEx.Code.ShouldBe("ShikshaMitra:PayrollPolicyRequired");
		attendanceEx.Code.ShouldBe("ShikshaMitra:AttendancePolicyRequired");
	}

	[Fact]
	public async Task Assignment_Delete_Should_Allow_Cleanup_When_Referenced_Policy_Is_Archived()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var stalePayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Stale Assigned Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		var input = BuildAssignment(
			context,
			"employee",
			employee.Id.ToString("D"),
			employee.FullName,
			effectiveFrom: new DateTime(2026, 5, 1));
		input.PayrollPolicyId = stalePayroll.Id;
		var assignment = await CreateAssignmentWithImpactTokenAsync(input);
		var payrollRepository = GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		var staleEntity = await payrollRepository.GetAsync(stalePayroll.Id);
		staleEntity.Archive();
		await payrollRepository.UpdateAsync(staleEntity, autoSave: true);

		await DeleteAssignmentWithImpactTokenAsync(assignment);

		var deleted = await _assignments.GetAsync(assignment.Id);
		deleted.IsActive.ShouldBeFalse();

		await _assignments.DeleteAsync(assignment.Id);
		var deletedAgain = await _assignments.GetAsync(assignment.Id);
		deletedAgain.IsActive.ShouldBeFalse();
	}

	[Fact]
	public async Task Assignment_Delete_Should_Allow_Cleanup_When_Group_Target_Is_Missing()
	{
		var context = await CreatePolicyContextAsync();
		var assignmentRepository = GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var assignment = new HrPolicyAssignment(
			_guidGenerator.Create(),
			_currentTenant.Id,
			context.AcademicYearId,
			HrPolicyAssignmentTargetType.Group,
			Guid.NewGuid().ToString("D").ToLowerInvariant(),
			"Removed group",
			context.AttendancePolicy.Id,
			context.PayrollPolicy.Id,
			null,
			new DateTime(2026, 5, 1),
			null,
			true,
			10,
			"Missing target cleanup regression");
		await assignmentRepository.InsertAsync(assignment, autoSave: true);

		await _assignments.DeleteAsync(assignment.Id);

		var deleted = await _assignments.GetAsync(assignment.Id);
		deleted.IsActive.ShouldBeFalse();
	}

	[Fact]
	public async Task Assignment_Update_Should_Reject_Academic_Year_Changes()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var assignment = await CreateAssignmentWithImpactTokenAsync(BuildAssignment(
			context,
			"employee",
			employee.Id.ToString("D"),
			employee.FullName));
		var foreignAcademicYearId = await CreateAcademicYearAsync();
		var foreignCalendar = await CreateWorkCalendarAsync(foreignAcademicYearId, "AssignmentForeignYear");
		var foreignPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = foreignAcademicYearId,
			WorkCalendarPolicyId = foreignCalendar.Id,
			Name = $"Foreign Assignment Payroll {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		var foreignAttendance = await _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = foreignAcademicYearId,
			WorkCalendarPolicyId = foreignCalendar.Id,
			Name = $"Foreign Assignment Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false
		});

		var input = BuildAssignment(
			context,
			"employee",
			employee.Id.ToString("D"),
			employee.FullName);
		input.AcademicYearId = foreignAcademicYearId;
		input.PayrollPolicyId = foreignPayroll.Id;
		input.AttendancePolicyId = foreignAttendance.Id;

		var ex = await Should.ThrowAsync<BusinessException>(() => _assignments.UpdateAsync(assignment.Id, input));

		ex.Code.ShouldBe("ShikshaMitra:PolicyAssignmentAcademicYearMismatch");
		var unchanged = await _assignments.GetAsync(assignment.Id);
		unchanged.AcademicYearId.ShouldBe(context.AcademicYearId);
		unchanged.PayrollPolicyId.ShouldBe(context.PayrollPolicy.Id);
		unchanged.AttendancePolicyId.ShouldBe(context.AttendancePolicy.Id);
	}

	[Fact]
	public async Task Assignment_Should_Reject_Group_From_Different_Academic_Year()
	{
		var context = await CreatePolicyContextAsync();
		var foreignAcademicYearId = await CreateAcademicYearAsync();
		var foreignGroup = await CreateGroupAsync(foreignAcademicYearId, "ForeignYearGroup");
		var input = BuildAssignment(context, "group", foreignGroup.Id.ToString("D"), foreignGroup.Name);

		var ex = await Should.ThrowAsync<BusinessException>(() => _assignments.CreateAsync(input));

		ex.Code.ShouldBe("ShikshaMitra:PolicyAssignmentAcademicYearMismatch");
	}

	[Fact]
	public async Task Assignment_Impact_Preview_Should_Reject_Group_From_Different_Academic_Year()
	{
		var context = await CreatePolicyContextAsync();
		var foreignAcademicYearId = await CreateAcademicYearAsync();
		var foreignGroup = await CreateGroupAsync(foreignAcademicYearId, "ForeignYearPreviewGroup");
		var input = BuildAssignment(context, "group", foreignGroup.Id.ToString("D"), foreignGroup.Name);

		var ex = await Should.ThrowAsync<BusinessException>(() => _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = "create",
			Assignment = input
		}));

		ex.Code.ShouldBe("ShikshaMitra:PolicyAssignmentAcademicYearMismatch");
	}

	[Fact]
	public async Task PayrollPolicy_Should_Reject_Duplicate_Manual_Month_Days()
	{
		var context = await CreatePolicyContextAsync();

		await Should.ThrowAsync<AbpValidationException>(() => _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Duplicate Manual Month {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			DayCalculationType = "manual-days",
			ManualMonthDays =
			[
				new() { Year = 2026, Month = 5, ManualDays = 24 },
				new() { Year = 2026, Month = 5, ManualDays = 25 }
			],
			SaturdayWorkPolicy = "full-day"
		}));
	}

	[Fact]
	public async Task Assignment_Should_Reject_Invalid_Effective_Range_At_Request_Level()
	{
		var context = await CreatePolicyContextAsync();
		var group = await CreateGroupAsync(context.AcademicYearId, "Validation Range");
		var input = BuildAssignment(
			context,
			"group",
			group.Id.ToString("D"),
			group.Name,
			effectiveFrom: new DateTime(2026, 6, 1),
			effectiveTo: new DateTime(2026, 5, 31));

		await Should.ThrowAsync<AbpValidationException>(() => _assignments.CreateAsync(input));
	}

	[Fact]
	public async Task Assignment_Impact_Preview_Should_Block_Inactive_Referenced_Policies()
	{
		var context = await CreatePolicyContextAsync();
		var draftPayroll = await _payrollPolicies.CreateAsync(new CreateUpdatePayrollPolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Draft Payroll {Guid.NewGuid():N}",
			Status = "draft",
			IsDefault = false,
			DayCalculationType = "working-days",
			SaturdayWorkPolicy = "full-day"
		});
		var group = await CreateGroupAsync(context.AcademicYearId, "Preview Blocking");
		var input = BuildAssignment(context, "group", group.Id.ToString("D"), group.Name);
		input.PayrollPolicyId = draftPayroll.Id;

		var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = "create",
			Assignment = input
		});

		preview.BlockingWarnings.ShouldContain(x => x.Code == "ShikshaMitra:PolicyMustBeActive");
		preview.RiskSummary.ShouldNotBeNullOrWhiteSpace();
		preview.RiskSummary.ShouldContain("blocking");
		preview.RecommendedAction.ShouldNotBeNullOrWhiteSpace();
		preview.RecommendedAction.ShouldContain("Resolve");
	}

	[Fact]
	public async Task Assignment_Impact_Preview_Should_Count_Active_Group_Employees_And_Require_Confirmation()
	{
		var context = await CreatePolicyContextAsync();
		var roleId = await CreateUniqueRoleAsync();
		var activeEmployees = new List<EmployeePersonalInfoDto>();
		for (var i = 0; i < 2; i++)
		{
			activeEmployees.Add(await CreateEmployeeAsync(roleId));
		}
		var group = await CreateRoleGroupAsync(context.AcademicYearId, roleId, "Large Assignment Preview");
		var inactiveEmployee = await CreateEmployeeAsync(roleId);
		await _employeeStatus.ChangeStatusAsync(inactiveEmployee.Id, new ChangeEmployeeStatusDto
		{
			EmployeeStatusType = EmployeeStatusType.Inactive,
			EffectiveDate = DateOnly.FromDateTime(new DateTime(2026, 4, 1))
		});

		var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = "create",
			Assignment = BuildAssignment(context, "group", group.Id.ToString("D"), group.Name)
		});

		Assert.True(preview.AffectedEmployeeCount >= 2);
		Assert.Equal(2, preview.AffectedEmployees.Count);
		Assert.DoesNotContain(preview.AffectedEmployees, x => x.EmployeeId == inactiveEmployee.Id);
		Assert.Contains(preview.Warnings, x => x.Code == "ShikshaMitra:AffectedEmployees");
		Assert.True(preview.RequiresConfirmation);
		Assert.False(string.IsNullOrWhiteSpace(preview.ConfirmationToken));
		preview.RiskSummary.ShouldNotBeNullOrWhiteSpace();
		preview.RiskSummary.ShouldContain("policy notice");
		preview.RecommendedAction.ShouldNotBeNullOrWhiteSpace();
		preview.RecommendedAction.ShouldContain("confirming");
	}

	[Fact]
	public async Task Assignment_Impact_Preview_Should_Not_Request_Confirmation_For_No_Affected_Employees()
	{
		var context = await CreatePolicyContextAsync();
		var group = await CreateGroupAsync(context.AcademicYearId, "Empty Assignment Preview");

		var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = "create",
			Assignment = BuildAssignment(context, "group", group.Id.ToString("D"), group.Name)
		});

		preview.AffectedEmployeeCount.ShouldBe(0);
		preview.Warnings.ShouldContain(x => x.Code == "ShikshaMitra:NoAffectedEmployees");
		preview.RequiresConfirmation.ShouldBeFalse();
		preview.ConfirmationToken.ShouldBeNull();
		preview.RiskSummary.ShouldNotBeNullOrWhiteSpace();
		preview.RiskSummary.ShouldContain("no confirmation is required");
		preview.RecommendedAction.ShouldNotBeNullOrWhiteSpace();
		preview.RecommendedAction.ShouldContain("informational notices");
	}

	[Fact]
	public async Task Assignment_Create_Should_Require_Confirmation_When_Employees_Affected()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var input = BuildAssignment(context, "employee", employee.Id.ToString("D"), employee.FullName);

		var ex = await Should.ThrowAsync<BusinessException>(() => _assignments.CreateAsync(input));

		ex.Code.ShouldBe("ShikshaMitra:HrPolicyImpactConfirmationRequired");
	}

	[Fact]
	public async Task Assignment_Should_Reject_Too_Long_Display_Name_And_Reason_At_Request_Level()
	{
		var context = await CreatePolicyContextAsync();
		var group = await CreateGroupAsync(context.AcademicYearId, "Assignment Length");
		var input = BuildAssignment(context, "group", group.Id.ToString("D"), group.Name);
		input.TargetDisplayName = new string('D', 257);
		input.AssignmentReason = new string('R', 257);

		await Should.ThrowAsync<AbpValidationException>(() => _assignments.CreateAsync(input));
	}

	[Fact]
	public async Task Assignment_Should_Block_When_Active_Policy_References_NonActive_WorkCalendar()
	{
		var context = await CreatePolicyContextAsync();
		var calendar = await _workCalendarPolicyRepository.GetAsync(context.WorkCalendar.Id);
		calendar.Update(
			calendar.Name,
			HrPolicyStatus.Draft,
			calendar.EffectiveFrom,
			calendar.EffectiveTo,
			calendar.WeeklyOffPattern,
			calendar.GetCustomWeeklyOffs(),
			calendar.HolidaySyncMode);
		await _workCalendarPolicyRepository.UpdateAsync(calendar, autoSave: true);
		var group = await CreateGroupAsync(context.AcademicYearId, "Stale Calendar Assignment");
		var input = BuildAssignment(context, "group", group.Id.ToString("D"), group.Name);

		var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = "create",
			Assignment = input
		});
		preview.BlockingWarnings.ShouldContain(x => x.Code == "ShikshaMitra:WorkCalendarPolicyMustBeActive");

		var ex = await Should.ThrowAsync<BusinessException>(() => _assignments.CreateAsync(input));
		ex.Code.ShouldBe("ShikshaMitra:WorkCalendarPolicyMustBeActive");
	}

	[Fact]
	public async Task Assignment_Impact_Preview_Should_Show_Employee_Override_Precedence_In_Proposed_Range()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var group = await CreateRoleGroupAsync(context.AcademicYearId, employee.RoleId, "Preview Role Range");

		await UpsertEmployeeOverrideWithImpactTokenAsync(new UpsertEmployeePolicyOverrideDto
		{
			EmployeeId = employee.Id,
			AcademicYearId = context.AcademicYearId,
			AttendancePolicyId = context.AttendancePolicy.Id,
			PayrollPolicyId = context.PayrollPolicy.Id,
			EffectiveFrom = new DateTime(2026, 5, 15),
			EffectiveTo = new DateTime(2026, 5, 31),
			AssignmentReason = "Employee override should win over group preview"
		});

		var input = BuildAssignment(
			context,
			"group",
			group.Id.ToString("D"),
			group.Name,
			effectiveFrom: new DateTime(2026, 4, 1),
			effectiveTo: new DateTime(2026, 6, 30));
		input.Priority = 1000;

		var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = "create",
			Assignment = input
		});

		var conflict = preview.EffectiveAssignmentConflicts.Single(x => x.EmployeeId == employee.Id);
		conflict.Date.ShouldBe(new DateTime(2026, 5, 15));
		conflict.CandidateAssignments.ShouldContain(x => x.TargetType == "employee");
		conflict.CandidateAssignments.ShouldContain(x => x.TargetType == "group");
		conflict.WinningAssignment.ShouldNotBeNull();
		conflict.WinningAssignment!.TargetType.ShouldBe("employee");
	}

	[Fact]
	public async Task Assignment_Impact_Preview_Should_Explain_When_Candidate_Loses_By_Effective_Date()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var existingGroup = await CreateRoleGroupAsync(context.AcademicYearId, employee.RoleId, "Existing Earlier Role");
		var proposedGroup = await CreateRoleGroupAsync(context.AcademicYearId, employee.RoleId, "Proposed Later Role");
		var existingAssignment = await CreateAssignmentWithImpactTokenAsync(BuildAssignment(
			context,
			"group",
			existingGroup.Id.ToString("D"),
			existingGroup.Name,
			effectiveFrom: new DateTime(2026, 4, 1)));
		var input = BuildAssignment(
			context,
			"group",
			proposedGroup.Id.ToString("D"),
			proposedGroup.Name,
			effectiveFrom: new DateTime(2026, 5, 1));
		input.Priority = existingAssignment.Priority;

		var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = "create",
			Assignment = input
		});

		var conflict = preview.EffectiveAssignmentConflicts.Single(x => x.EmployeeId == employee.Id);
		conflict.WinningAssignment.ShouldNotBeNull();
		conflict.WinningAssignment!.AssignmentId.ShouldBeNull();
		conflict.WinningAssignment.SkipReason.ShouldBe("winner");
		conflict.CandidateAssignments.Single(x => x.AssignmentId == existingAssignment.Id).SkipReason.ShouldBe("lower_effective_date");
	}

	[Fact]
	public async Task EffectivePolicyResolution_Should_Return_Admin_Readable_Summary()
	{
		await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var effective = await _assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1));

		effective.ResolutionSummary.ShouldNotBeNullOrWhiteSpace();
		effective.ResolutionSummary.ShouldContain("default");
		effective.RecommendedAction.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task EffectivePolicyResolution_Should_Reject_Ambiguous_Default_Policy_Fallback()
	{
		var context = await CreatePolicyContextAsync();
		var employee = await CreateEmployeeAsync();
		var secondDefault = await _attendancePolicies.CreateAsync(new CreateUpdateAttendancePolicyDto
		{
			AcademicYearId = context.AcademicYearId,
			WorkCalendarPolicyId = context.WorkCalendar.Id,
			Name = $"Resolution Ambiguous Default Attendance {Guid.NewGuid():N}",
			Status = "active",
			IsDefault = false,
			FullDayThresholdPercentage = 80,
			HalfDayThresholdPercentage = 50,
			AbsentThresholdPercentage = 0,
			MissingPunchBehavior = "requires_review",
			ManualCorrectionAllowed = true,
			HolidayWorkBehavior = "paid"
		});
		var attendanceRepository = GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		var duplicateDefault = await attendanceRepository.GetAsync(secondDefault.Id);
		duplicateDefault.SetDefault(true);
		await attendanceRepository.UpdateAsync(duplicateDefault, autoSave: true);

		var ex = await Should.ThrowAsync<BusinessException>(() =>
			_assignments.GetEffectivePoliciesAsync(employee.Id, new DateTime(2026, 5, 1)));

		ex.Code.ShouldBe("ShikshaMitra:DefaultAttendancePolicyRequired");
	}

	private static void AssertArchivedStatusRejected(Exception ex)
	{
		if (ex is BusinessException businessException)
		{
			businessException.Code.ShouldBe("ShikshaMitra:PolicyArchiveRequiresArchiveWorkflow");
			return;
		}

		if (ex is AbpValidationException validationException)
		{
			validationException.ValidationErrors.Any(error =>
				!string.IsNullOrEmpty(error.ErrorMessage) &&
				error.ErrorMessage.Contains("Use the archive action to archive a policy.", StringComparison.Ordinal)).ShouldBeTrue();
			return;
		}

		throw new ShouldAssertException($"Expected archived status rejection, but received {ex.GetType().FullName}.");
	}

	private static async Task<Exception> CaptureExceptionAsync(Func<Task> action)
	{
		try
		{
			await action();
		}
		catch (Exception ex)
		{
			return ex;
		}

		throw new ShouldAssertException("Expected an exception, but no exception was thrown.");
	}

	private static async Task<BusinessException> CaptureBusinessExceptionAsync(Func<Task> action)
	{
		var ex = await CaptureExceptionAsync(action);
		if (ex is BusinessException businessException)
		{
			return businessException;
		}

		throw new ShouldAssertException($"Expected {nameof(BusinessException)}, but received {ex.GetType().FullName}.");
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

	private async Task<Group> CreateRoleGroupAsync(Guid academicYearId, Guid roleId, string name)
	{
		var group = Group.Create(
			Guid.NewGuid(),
			_currentTenant.Id,
			academicYearId,
			$"{name}-{Guid.NewGuid():N}",
			"Test HR policy role group.",
			[],
			[roleId],
			[],
			[]);
		await _groupRepository.InsertAsync(group, autoSave: true);
		return group;
	}

	private async Task MarkWorkCalendarDraftAsync(Guid workCalendarPolicyId)
	{
		var calendar = await _workCalendarPolicyRepository.GetAsync(workCalendarPolicyId);
		calendar.Update(
			calendar.Name,
			HrPolicyStatus.Draft,
			calendar.EffectiveFrom,
			calendar.EffectiveTo,
			calendar.WeeklyOffPattern,
			calendar.GetCustomWeeklyOffs(),
			calendar.HolidaySyncMode);
		await _workCalendarPolicyRepository.UpdateAsync(calendar, autoSave: true);
	}

	private async Task<WorkCalendarPolicy> CreateForeignActiveWorkCalendarDirectlyAsync()
	{
		var calendar = new WorkCalendarPolicy(
			_guidGenerator.Create(),
			_currentTenant.Id,
			Guid.NewGuid(),
			$"Foreign Year Calendar {Guid.NewGuid():N}",
			HrPolicyStatus.Active,
			new DateTime(2026, 4, 1),
			new DateTime(2027, 3, 31),
			AttendanceWeeklyOffPattern.SaturdaySunday,
			[],
			WorkCalendarHolidaySyncMode.LinkedWithOverrides);
		await _workCalendarPolicyRepository.InsertAsync(calendar, autoSave: true);
		return calendar;
	}

	private async Task<EmployeePersonalInfoDto> CreateEmployeeAsync(Guid? roleId = null)
	{
		var resolvedRoleId = roleId ?? await EnsureRoleExistsAsync();
		var shiftId = await EnsureShiftExistsAsync();
		var organizationId = await EmployeeOrganizationTestHelper.EnsureOrganizationExistsAsync(ServiceProvider);
		return await _employeePersonalInfo.CreateAsync(new CreateEmployeePersonalInfoDto
		{
			FirstName = "Policy",
			LastName = "Impact",
			MobileNo = UniqueMobileNo(),
			Email = $"policy.impact.{_guidGenerator.Create():N}@test.com",
			PanNo = UniquePan(),
			AdharNo = UniqueAdhar(),
			DateOfBirth = DateOnly.FromDateTime(new DateTime(1992, 3, 10)),
			GenderType = GenderType.Male,
			DateOfJoining = DateOnly.FromDateTime(new DateTime(2026, 4, 1)),
			EmployeeStatusType = EmployeeStatusType.Active,
			RoleId = resolvedRoleId,
			ShiftId = shiftId,
			OrganizationIds = [organizationId]
		});
	}

	private string UniqueMobileNo()
	{
		var suffix = Math.Abs(_guidGenerator.Create().GetHashCode()) % 1_000_000_000;
		return $"9{suffix:D9}";
	}

	private Task<HrPolicyAssignmentDto> UpsertEmployeePayrollPolicyAsync(
		PolicyContext context,
		Guid employeeId,
		Guid payrollPolicyId) =>
		UpsertEmployeeOverrideWithImpactTokenAsync(new UpsertEmployeePolicyOverrideDto
		{
			EmployeeId = employeeId,
			AcademicYearId = context.AcademicYearId,
			AttendancePolicyId = context.AttendancePolicy.Id,
			PayrollPolicyId = payrollPolicyId,
			EffectiveFrom = new DateTime(2026, 4, 1),
			AssignmentReason = "Repeatable policy override regression"
		});

	private async Task<HrPolicyAssignmentDto> CreateAssignmentWithImpactTokenAsync(CreateUpdateHrPolicyAssignmentDto input)
	{
		var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = "create",
			Assignment = input
		});
		if (preview.RequiresConfirmation)
		{
			preview.ConfirmationToken.ShouldNotBeNullOrWhiteSpace();
			input.ConfirmationToken = preview.ConfirmationToken;
		}
		return await _assignments.CreateAsync(input);
	}

	private async Task DeleteAssignmentWithImpactTokenAsync(HrPolicyAssignmentDto assignment)
	{
		var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = "delete",
			AssignmentId = assignment.Id,
			Assignment = new CreateUpdateHrPolicyAssignmentDto
			{
				AcademicYearId = assignment.AcademicYearId,
				TargetType = assignment.TargetType,
				TargetKey = assignment.TargetKey,
				TargetDisplayName = assignment.TargetDisplayName,
				AttendancePolicyId = assignment.AttendancePolicyId,
				PayrollPolicyId = assignment.PayrollPolicyId,
				EffectiveFrom = assignment.EffectiveFrom,
				EffectiveTo = assignment.EffectiveTo,
				IsActive = false,
				Priority = assignment.Priority,
				AssignmentReason = assignment.AssignmentReason
			}
		});

		var previousContext = _httpContextAccessor.HttpContext;
		var httpContext = new DefaultHttpContext();
		if (preview.RequiresConfirmation)
		{
			preview.ConfirmationToken.ShouldNotBeNullOrWhiteSpace();
			httpContext.Request.Headers[HrPolicyImpactConfirmationService.HeaderName] = preview.ConfirmationToken;
		}
		_httpContextAccessor.HttpContext = httpContext;
		try
		{
			await _assignments.DeleteAsync(assignment.Id);
		}
		finally
		{
			_httpContextAccessor.HttpContext = previousContext;
		}
	}

	private async Task DisableEmployeeOverrideWithImpactTokenAsync(DisableEmployeePolicyOverrideDto input)
	{
		var targetKey = input.EmployeeId.ToString("D").ToLowerInvariant();
		var assignment = input.AssignmentId.HasValue
			? (await _assignments.GetAsync(input.AssignmentId.Value))
			: await FindActiveEmployeeAssignmentAsync(
				input.AcademicYearId,
				targetKey,
				input.EffectiveDate);
		if (assignment != null)
		{
			var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
			{
				Action = HrPolicyAssignmentImpactService.EmployeeOverrideDisableAction,
				AssignmentId = assignment.Id,
				Assignment = new CreateUpdateHrPolicyAssignmentDto
				{
					AcademicYearId = assignment.AcademicYearId,
					TargetType = assignment.TargetType,
					TargetKey = assignment.TargetKey,
					TargetDisplayName = assignment.TargetDisplayName,
					AttendancePolicyId = assignment.AttendancePolicyId,
					PayrollPolicyId = assignment.PayrollPolicyId,
					EffectiveFrom = input.EffectiveDate.Date,
					EffectiveTo = assignment.EffectiveTo,
					IsActive = false,
					Priority = assignment.Priority,
					AssignmentReason = input.AssignmentReason
				}
			});
			if (preview.RequiresConfirmation)
			{
				preview.ConfirmationToken.ShouldNotBeNullOrWhiteSpace();
				input.ConfirmationToken = preview.ConfirmationToken;
			}
		}

		await _assignments.DisableEmployeeOverrideAsync(input);
	}

	private async Task<HrPolicyAssignmentDto> UpsertEmployeeOverrideWithImpactTokenAsync(UpsertEmployeePolicyOverrideDto input)
	{
		var targetKey = input.EmployeeId.ToString("D").ToLowerInvariant();
		var assignmentId = input.AssignmentId ?? await FindExistingEmployeeAssignmentIdAsync(input, targetKey);
		var preview = await _assignments.PreviewImpactAsync(new HrPolicyAssignmentImpactPreviewRequestDto
		{
			Action = assignmentId.HasValue ? HrPolicyAssignmentImpactService.EmployeeOverrideUpsertAction : "create",
			AssignmentId = assignmentId,
			Assignment = new CreateUpdateHrPolicyAssignmentDto
			{
				AcademicYearId = input.AcademicYearId,
				TargetType = "employee",
				TargetKey = targetKey,
				AttendancePolicyId = input.AttendancePolicyId,
				PayrollPolicyId = input.PayrollPolicyId,
				EffectiveFrom = input.EffectiveFrom,
				EffectiveTo = input.EffectiveTo,
				IsActive = true,
				Priority = 100,
				AssignmentReason = input.AssignmentReason
			}
		});
		if (preview.RequiresConfirmation)
		{
			preview.ConfirmationToken.ShouldNotBeNullOrWhiteSpace();
			input.ConfirmationToken = preview.ConfirmationToken;
		}
		return await _assignments.UpsertEmployeeOverrideAsync(input);
	}

	private async Task<HrPolicyAssignmentDto?> FindActiveEmployeeAssignmentAsync(
		Guid academicYearId,
		string targetKey,
		DateTime effectiveDate)
	{
		var assignments = await _assignments.GetListAsync(new PolicyListRequestDto
		{
			AcademicYearId = academicYearId,
			TargetType = "employee",
			MaxResultCount = 1000
		});
		return assignments.Items
			.Where(x =>
				x.TargetKey == targetKey &&
				x.IsActive &&
				x.EffectiveFrom <= effectiveDate &&
				(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= effectiveDate))
			.OrderByDescending(x => x.Priority)
			.ThenByDescending(x => x.EffectiveFrom)
			.FirstOrDefault();
	}

	private async Task<Guid?> FindExistingEmployeeAssignmentIdAsync(UpsertEmployeePolicyOverrideDto input, string targetKey)
	{
		var effectiveEnd = input.EffectiveTo ?? DateTime.MaxValue.Date;
		var assignments = await _assignments.GetListAsync(new PolicyListRequestDto
		{
			AcademicYearId = input.AcademicYearId,
			TargetType = "employee",
			MaxResultCount = 1000
		});
		return assignments.Items
			.Where(x =>
				x.TargetKey == targetKey &&
				x.IsActive &&
				x.EffectiveFrom <= effectiveEnd &&
				(!x.EffectiveTo.HasValue || x.EffectiveTo.Value >= input.EffectiveFrom))
			.OrderByDescending(x => x.Priority)
			.ThenByDescending(x => x.EffectiveFrom)
			.FirstOrDefault()?.Id;
	}

	private async Task RunWithPolicyImpactTokenAsync(
		string policyType,
		Guid policyId,
		Guid academicYearId,
		string action,
		Func<Task> run)
	{
		var preview = await _policySafety.PreviewPolicyImpactAsync(new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = policyType,
			PolicyId = policyId,
			AcademicYearId = academicYearId,
			Action = action
		});
		preview.RequiresConfirmation.ShouldBeTrue();
		preview.ConfirmationToken.ShouldNotBeNullOrWhiteSpace();

		var previousContext = _httpContextAccessor.HttpContext;
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers[HrPolicyImpactConfirmationService.HeaderName] = preview.ConfirmationToken;
		_httpContextAccessor.HttpContext = httpContext;
		try
		{
			await run();
		}
		finally
		{
			_httpContextAccessor.HttpContext = previousContext;
		}
	}

	private async Task RunWithImpactTokenHeaderAsync(string token, Func<Task> run)
	{
		var previousContext = _httpContextAccessor.HttpContext;
		var httpContext = new DefaultHttpContext();
		httpContext.Request.Headers[HrPolicyImpactConfirmationService.HeaderName] = token;
		_httpContextAccessor.HttpContext = httpContext;
		try
		{
			await run();
		}
		finally
		{
			_httpContextAccessor.HttpContext = previousContext;
		}
	}

	private static HrPolicyImpactPreviewDto BuildImpactPreviewWithSeverity(string severity) =>
		new()
		{
			PolicyType = "payroll",
			AcademicYearId = Guid.NewGuid(),
			Action = "update",
			Warnings =
			[
				new HrPolicyImpactWarningDto
				{
					Code = $"Test:{severity}",
					Severity = severity,
					Message = $"{severity} impact"
				}
			]
		};

	private static HrPolicyImpactPreviewDto BuildAssignmentImpactPreviewWithAffectedEmployee(
		Guid academicYearId,
		Guid employeeId,
		string employeeCode) =>
		new()
		{
			PolicyType = "assignment",
			AcademicYearId = academicYearId,
			Action = "create",
			AffectedEmployeeCount = 1,
			AffectedAssignmentCount = 1,
			AffectedEmployees =
			[
				new HrPolicyAffectedEmployeeDto
				{
					EmployeeId = employeeId,
					EmployeeCode = employeeCode,
					EmployeeName = $"Employee {employeeCode}"
				}
			],
			Warnings =
			[
				new HrPolicyImpactWarningDto
				{
					Code = HRErrorCodes.AffectedEmployees,
					Severity = "info",
					Message = "1 employees may be affected."
				}
			]
		};

	private HrPolicyVersion BuildPolicyVersion(
		string policyType,
		Guid policyId,
		Guid academicYearId,
		string name,
		string status,
		string snapshotJson) =>
		new(
			_guidGenerator.Create(),
			_currentTenant.Id,
			policyType,
			policyId,
			academicYearId,
			999,
			name,
			status,
			null,
			null,
			snapshotJson,
			"corrupted snapshot regression");

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

	private async Task<Guid> CreateUniqueRoleAsync()
	{
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

	private Task<WorkCalendarPolicyDto> CreateWorkCalendarAsync(Guid academicYearId, string namePrefix) =>
		_workCalendars.CreateAsync(new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = academicYearId,
			Name = $"{namePrefix} Calendar {Guid.NewGuid():N}",
			Status = "active",
			EffectiveFrom = new DateTime(2026, 4, 1),
			EffectiveTo = new DateTime(2027, 3, 31),
			WeeklyOffPattern = "saturday_sunday",
			HolidaySyncMode = "linked_with_overrides"
		});

	private async Task ArchiveCalendarDirectlyAsync(Guid calendarId)
	{
		var calendar = await _workCalendarPolicyRepository.GetAsync(calendarId);
		calendar.Archive();
		await _workCalendarPolicyRepository.UpdateAsync(calendar, autoSave: true);
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
