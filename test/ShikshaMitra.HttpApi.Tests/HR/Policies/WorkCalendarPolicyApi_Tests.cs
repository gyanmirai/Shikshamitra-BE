using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Payroll.Settings;
using System.Threading.Tasks;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.TenantSettings.AcademicYears;
using Shouldly;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.HR.Policies;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class WorkCalendarPolicyApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/hr/work-calendar-policies";

	public WorkCalendarPolicyApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory) { }

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Holiday_Import_Preview_Should_Not_Return_404()
	{
		var response = await Client.GetAsync($"{BaseUrl}/holiday-import-preview?academicYearId={Factory.SeededAcademicYearId}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<List<WorkCalendarHolidayDto>>(JsonOptions);
		body.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Impact_Preview_Should_Accept_WorkCalendar_Alias()
	{
		var response = await Client.PostAsJsonAsync("/api/hr/policies/impact-preview", new HrPolicyImpactPreviewRequestDto
		{
			PolicyType = "workcalendar",
			AcademicYearId = Factory.SeededAcademicYearId,
			Action = "update"
		}, JsonOptions);

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<HrPolicyImpactPreviewDto>(JsonOptions);
		body.ShouldNotBeNull();
		body!.PolicyType.ShouldBe("work_calendar");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Restore_Preview_Should_Not_Report_Current_Policy_Assignment_Impact()
	{
		var (payrollPolicyId, versionId) = await SeedPayrollVersionWithReferencingAssignmentAsync();

		var response = await Client.GetAsync($"/api/hr/payroll/policies/{payrollPolicyId}/versions/{versionId}/restore-preview");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<HrPolicyImpactPreviewDto>(JsonOptions);
		body.ShouldNotBeNull();
		body!.Action.ShouldBe("restore-draft");
		body.AffectedEmployeeCount.ShouldBe(0);
		body.AffectedAssignmentCount.ShouldBe(0);
		body.RequiresConfirmation.ShouldBeFalse();
		body.ConfirmationToken.ShouldBeNull();
		body.RiskSummary.ShouldBe("No policy risks detected for this action.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Restore_Preview_Should_Return_Review_Only_Warnings_Without_Confirmation_Token()
	{
		var (payrollPolicyId, versionId) = await SeedDraftPayrollVersionWithDraftCalendarAsync();

		var response = await Client.GetAsync($"/api/hr/payroll/policies/{payrollPolicyId}/versions/{versionId}/restore-preview");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var body = await response.Content.ReadFromJsonAsync<HrPolicyImpactPreviewDto>(JsonOptions);
		body.ShouldNotBeNull();
		body!.Action.ShouldBe("restore-draft");
		body.Warnings.ShouldContain(x => x.Code == HRErrorCodes.WorkCalendarPolicyMustBeActive);
		body.BlockingWarnings.ShouldBeEmpty();
		body.RequiresConfirmation.ShouldBeFalse();
		body.ConfirmationToken.ShouldBeNull();
		body.RiskSummary.ShouldBe("1 non-blocking notice(s) found; no confirmation is required.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Work_Calendar_Should_Persist_Manual_Holiday_Range()
	{
		var academicYear = await GetAcademicYearDateRangeAsync(Factory.SeededAcademicYearId);
		var fromDate = academicYear.StartDate.AddMonths(1).Date;
		var toDate = fromDate.AddDays(4);
		var request = new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = Factory.SeededAcademicYearId,
			Name = $"Range Calendar {Guid.NewGuid():N}",
			Status = "draft",
			EffectiveFrom = academicYear.StartDate,
			EffectiveTo = academicYear.EndDate,
			WeeklyOffPattern = "sunday",
			CustomWeeklyOffs = [0],
			HolidaySyncMode = "snapshot",
			Holidays =
			[
				new WorkCalendarHolidayDto
				{
					AcademicYearId = Factory.SeededAcademicYearId,
					Date = fromDate,
					FromDate = fromDate,
					ToDate = toDate,
					HolidayName = "Summer Vacation",
					Source = "manual",
					IsIncluded = true,
					Notes = "Range holiday API test"
				}
			]
		};

		var createResponse = await Client.PostAsJsonAsync(BaseUrl, request, JsonOptions);
		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var created = await createResponse.Content.ReadFromJsonAsync<WorkCalendarPolicyDto>(JsonOptions);
		created.ShouldNotBeNull();

		var holidaysResponse = await Client.GetAsync($"{BaseUrl}/{created!.Id}/holidays");
		holidaysResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var holidays = await holidaysResponse.Content.ReadFromJsonAsync<List<WorkCalendarHolidayDto>>(JsonOptions);
		var range = holidays.ShouldNotBeNull().ShouldHaveSingleItem();
		range.FromDate?.Date.ShouldBe(fromDate);
		range.ToDate?.Date.ShouldBe(toDate);
		range.DayCount.ShouldBe(5);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Work_Calendar_Should_Accept_Holiday_Through_Academic_Year_End()
	{
		var academicYear = await GetAcademicYearDateRangeAsync(Factory.SeededAcademicYearId);
		var fromDate = academicYear.EndDate.AddDays(-1).Date;
		var toDate = academicYear.EndDate.Date;
		var request = new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = Factory.SeededAcademicYearId,
			Name = $"Year End Calendar {Guid.NewGuid():N}",
			Status = "draft",
			EffectiveFrom = academicYear.StartDate,
			EffectiveTo = academicYear.EndDate,
			WeeklyOffPattern = "sunday",
			CustomWeeklyOffs = [0],
			HolidaySyncMode = "snapshot",
			Holidays =
			[
				new WorkCalendarHolidayDto
				{
					AcademicYearId = Factory.SeededAcademicYearId,
					Date = fromDate,
					FromDate = fromDate,
					ToDate = toDate,
					HolidayName = "Year End Break",
					Source = "manual",
					IsIncluded = true
				}
			]
		};

		var createResponse = await Client.PostAsJsonAsync(BaseUrl, request, JsonOptions);

		createResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
		var created = await createResponse.Content.ReadFromJsonAsync<WorkCalendarPolicyDto>(JsonOptions);
		created.ShouldNotBeNull();
		created!.Holidays.Single().ToDate?.Date.ShouldBe(toDate);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Work_Calendar_Should_Return_Specific_Detail_For_Holiday_Outside_Academic_Year()
	{
		var academicYear = await GetAcademicYearDateRangeAsync(Factory.SeededAcademicYearId);
		var fromDate = academicYear.EndDate.Date;
		var toDate = academicYear.EndDate.AddDays(1).Date;
		var request = new CreateUpdateWorkCalendarPolicyDto
		{
			AcademicYearId = Factory.SeededAcademicYearId,
			Name = $"Outside Year Calendar {Guid.NewGuid():N}",
			Status = "draft",
			EffectiveFrom = academicYear.StartDate,
			EffectiveTo = academicYear.EndDate,
			WeeklyOffPattern = "sunday",
			CustomWeeklyOffs = [0],
			HolidaySyncMode = "snapshot",
			Holidays =
			[
				new WorkCalendarHolidayDto
				{
					AcademicYearId = Factory.SeededAcademicYearId,
					Date = fromDate,
					FromDate = fromDate,
					ToDate = toDate,
					HolidayName = "Overflow Break",
					Source = "manual",
					IsIncluded = false
				}
			]
		};

		var createResponse = await Client.PostAsJsonAsync(BaseUrl, request, JsonOptions);

		createResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var raw = await createResponse.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(raw);
		document.RootElement.GetProperty("errorCode").GetString()
			.ShouldBe(HRErrorCodes.WorkCalendarHolidayOutsideAcademicYear);
		var detail = document.RootElement.GetProperty("detail").GetString();
		detail.ShouldNotBeNullOrWhiteSpace();
		detail.ShouldContain("Overflow Break");
		detail.ShouldContain(fromDate.ToString("yyyy-MM-dd"));
		detail.ShouldContain(toDate.ToString("yyyy-MM-dd"));
		detail.ShouldNotBe("One or more business rules were violated.");
	}

	private async Task<(Guid PayrollPolicyId, Guid VersionId)> SeedPayrollVersionWithReferencingAssignmentAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var workCalendarRepository = scope.ServiceProvider.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>();
		var payrollRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		var attendanceRepository = scope.ServiceProvider.GetRequiredService<IRepository<AttendancePolicy, Guid>>();
		var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrPolicyAssignment, Guid>>();
		var versionRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrPolicyVersion, Guid>>();

		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		{
			var academicYear = await GetAcademicYearDateRangeAsync(Factory.SeededAcademicYearId);
			var calendar = await workCalendarRepository.InsertAsync(new WorkCalendarPolicy(
				Guid.NewGuid(),
				Guid.Parse(TestAuthHandler.TestTenantId),
				Factory.SeededAcademicYearId,
				$"Restore Preview Calendar {Guid.NewGuid():N}",
				HrPolicyStatus.Active,
				academicYear.StartDate,
				academicYear.EndDate,
				AttendanceWeeklyOffPattern.SaturdaySunday,
				[],
				WorkCalendarHolidaySyncMode.LinkedWithOverrides), autoSave: true);

			var payroll = await payrollRepository.InsertAsync(new PayrollPolicy(
				Guid.NewGuid(),
				Guid.Parse(TestAuthHandler.TestTenantId),
				Factory.SeededAcademicYearId,
				calendar.Id,
				$"Restore Preview Payroll {Guid.NewGuid():N}",
				HrPolicyStatus.Active,
				false,
				DayCalculationType.WorkingDays,
				null,
				[],
				SaturdayWorkPolicy.FullDay,
				[]), autoSave: true);

			var attendance = await attendanceRepository.InsertAsync(new AttendancePolicy(
				Guid.NewGuid(),
				Guid.Parse(TestAuthHandler.TestTenantId),
				Factory.SeededAcademicYearId,
				calendar.Id,
				$"Restore Preview Attendance {Guid.NewGuid():N}",
				HrPolicyStatus.Active,
				false,
				AttendanceCaptureMode.ManualStatus,
				80,
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
				false,
				true), autoSave: true);

			await assignmentRepository.InsertAsync(new HrPolicyAssignment(
				Guid.NewGuid(),
				Guid.Parse(TestAuthHandler.TestTenantId),
				Factory.SeededAcademicYearId,
				HrPolicyAssignmentTargetType.Employee,
				Guid.NewGuid().ToString("D").ToLowerInvariant(),
				"Restore Preview Employee",
				attendance.Id,
				payroll.Id,
				null,
				academicYear.StartDate,
				null,
				true,
				100,
				"Restore preview API regression"), autoSave: true);

			var snapshot = new PayrollPolicyDto
			{
				Id = payroll.Id,
				AcademicYearId = payroll.AcademicYearId,
				WorkCalendarPolicyId = payroll.WorkCalendarPolicyId,
				Name = payroll.Name,
				Status = "active",
				IsDefault = false,
				DayCalculationType = "working-days",
				SaturdayWorkPolicy = "full-day",
				Version = payroll.Version
			};
			var version = await versionRepository.InsertAsync(new HrPolicyVersion(
				Guid.NewGuid(),
				Guid.Parse(TestAuthHandler.TestTenantId),
				"payroll",
				payroll.Id,
				payroll.AcademicYearId,
				payroll.Version,
				payroll.Name,
				"active",
				null,
				null,
				JsonSerializer.Serialize(snapshot, JsonOptions),
				"created"), autoSave: true);

			return (payroll.Id, version.Id);
		}
	}

	private async Task<(Guid PayrollPolicyId, Guid VersionId)> SeedDraftPayrollVersionWithDraftCalendarAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var workCalendarRepository = scope.ServiceProvider.GetRequiredService<IRepository<WorkCalendarPolicy, Guid>>();
		var payrollRepository = scope.ServiceProvider.GetRequiredService<IRepository<PayrollPolicy, Guid>>();
		var versionRepository = scope.ServiceProvider.GetRequiredService<IRepository<HrPolicyVersion, Guid>>();

		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		{
			var academicYear = await GetAcademicYearDateRangeAsync(Factory.SeededAcademicYearId);
			var calendar = await workCalendarRepository.InsertAsync(new WorkCalendarPolicy(
				Guid.NewGuid(),
				Guid.Parse(TestAuthHandler.TestTenantId),
				Factory.SeededAcademicYearId,
				$"Restore Draft Calendar {Guid.NewGuid():N}",
				HrPolicyStatus.Draft,
				academicYear.StartDate,
				academicYear.EndDate,
				AttendanceWeeklyOffPattern.SaturdaySunday,
				[],
				WorkCalendarHolidaySyncMode.LinkedWithOverrides), autoSave: true);

			var payroll = await payrollRepository.InsertAsync(new PayrollPolicy(
				Guid.NewGuid(),
				Guid.Parse(TestAuthHandler.TestTenantId),
				Factory.SeededAcademicYearId,
				calendar.Id,
				$"Restore Draft Payroll {Guid.NewGuid():N}",
				HrPolicyStatus.Draft,
				false,
				DayCalculationType.WorkingDays,
				null,
				[],
				SaturdayWorkPolicy.FullDay,
				[]), autoSave: true);

			var snapshot = new PayrollPolicyDto
			{
				Id = payroll.Id,
				AcademicYearId = payroll.AcademicYearId,
				WorkCalendarPolicyId = payroll.WorkCalendarPolicyId,
				Name = payroll.Name,
				Status = "draft",
				IsDefault = false,
				DayCalculationType = "working-days",
				SaturdayWorkPolicy = "full-day",
				Version = payroll.Version
			};
			var version = await versionRepository.InsertAsync(new HrPolicyVersion(
				Guid.NewGuid(),
				Guid.Parse(TestAuthHandler.TestTenantId),
				"payroll",
				payroll.Id,
				payroll.AcademicYearId,
				payroll.Version,
				payroll.Name,
				"draft",
				null,
				null,
				JsonSerializer.Serialize(snapshot, JsonOptions),
				"created"), autoSave: true);

			return (payroll.Id, version.Id);
		}
	}

	private async Task<(DateTime StartDate, DateTime EndDate)> GetAcademicYearDateRangeAsync(Guid academicYearId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var academicYearRepository = scope.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>();

		using (currentTenant.Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		{
			var academicYear = await academicYearRepository.GetAsync(academicYearId);
			return (academicYear.StartDate.Date, academicYear.EndDate.Date);
		}
	}
}
