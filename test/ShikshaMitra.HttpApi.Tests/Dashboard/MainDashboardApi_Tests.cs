using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Application.Contracts.HR.Calendar.Events;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Calendar;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Data;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Compliance;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Dashboard;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Dashboard)]
public class MainDashboardApi_Tests : ShikshaMitraApiTestBase
{
	private const string BaseUrl = "/api/dashboard/summary";
	private const string WidgetMetadataUrl = "/api/dashboard/widgets";
	private const string CalendarEventsUrl = "/api/calendar/events";

	public static IEnumerable<object[]> MustHaveDashboardWidgetsByTemplate =>
	[
		[
			"leadership",
			new[]
			{
				"leadership-total-students",
				"leadership-fee-collection",
				"leadership-attendance",
				"leadership-compliance",
				"leadership-academic-health"
			}
		],
		[
			"principalAdmin",
			new[]
			{
				"principal-student-attendance",
				"principal-teacher-attendance",
				"principal-curriculum",
				"principal-pending-admissions",
				"principal-academic-dashboard"
			}
		],
		[
			"teacher",
			new[]
			{
				"teacher-classes",
				"teacher-homework",
				"teacher-exam-evaluation",
				"teacher-attendance",
				"teacher-class-performance"
			}
		],
		[
			"financeHr",
			new[]
			{
				"finance-collection",
				"finance-dues",
				"hr-payroll-readiness",
				"hr-staff-attendance",
				"finance-reconciliation",
				"hr-leave-approvals",
				"finance-dashboard",
				"hr-dashboard"
			}
		],
		[
			"studentParent",
			new[]
			{
				"student-attendance",
				"student-assignments",
				"student-fee-status",
				"student-document-update",
				"student-academic-summary"
			}
		]
	];

	public MainDashboardApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Get_Dashboard_Summary_With_Contract_Shape()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=principalAdmin&from=2026-05-01&to=2026-05-23");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();
		using var document = JsonDocument.Parse(json);

		document.RootElement.GetProperty("dashboardTemplate").GetString().ShouldBe("principalAdmin");
		document.RootElement.TryGetProperty("academicYearId", out _).ShouldBeTrue();
		document.RootElement.GetProperty("dateRange").GetProperty("from").GetString().ShouldBe("2026-05-01");
		document.RootElement.GetProperty("dateRange").GetProperty("to").GetString().ShouldBe("2026-05-23");
		document.RootElement.TryGetProperty("kpiCards", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("actionItems", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("riskAlerts", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("calendarItems", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("moduleSnapshots", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("announcements", out _).ShouldBeTrue();
		document.RootElement.TryGetProperty("zoneErrors", out _).ShouldBeTrue();
	}

	[Theory]
	[TestTier(TestTraits.Tiers.Critical)]
	[MemberData(nameof(MustHaveDashboardWidgetsByTemplate))]
	public async Task Should_Return_Must_Have_Module_Anchors_For_Each_Template(
		string dashboardTemplate,
		string[] expectedWidgetIds)
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var response = await Client.GetAsync(
			$"{BaseUrl}?dashboardTemplate={dashboardTemplate}&academicYearId={Factory.SeededAcademicYearId:D}&from=2026-05-01&to=2026-05-23");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
		              ?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var widgets = payload.KpiCards
			.Concat(payload.ActionItems)
			.Concat(payload.RiskAlerts)
			.Concat(payload.CalendarItems)
			.Concat(payload.ModuleSnapshots)
			.Concat(payload.Announcements)
			.ToArray();
		var widgetIds = widgets.Select(widget => widget.Id).ToArray();

		foreach (var expectedWidgetId in expectedWidgetIds)
		{
			widgetIds.ShouldContain(expectedWidgetId, $"{dashboardTemplate} should include {expectedWidgetId}.");
			var widget = widgets.Single(x => x.Id == expectedWidgetId);

			(!string.IsNullOrWhiteSpace(widget.DrilldownUrl) || !string.IsNullOrWhiteSpace(widget.Owner))
				.ShouldBeTrue($"{expectedWidgetId} should be actionable through a drilldown or accountable through an owner.");

			if (!string.IsNullOrWhiteSpace(widget.DrilldownUrl))
			{
				widget.DrilldownUrl.ShouldStartWith("/");
				widget.DrilldownUrl.ShouldNotStartWith("//");
				widget.DrilldownUrl.ShouldNotContain("://");
			}
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Get_Dashboard_Widget_Metadata_Catalog()
	{
		var response = await Client.GetAsync(WidgetMetadataUrl);
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var metadata = await response.Content.ReadFromJsonAsync<List<DashboardWidgetMetadataDto>>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard widget metadata.");

		metadata.Count.ShouldBeGreaterThan(0);
		metadata.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(metadata.Count);
		metadata.ShouldAllBe(widget => !string.IsNullOrWhiteSpace(widget.Id));
		metadata.ShouldAllBe(widget => !string.IsNullOrWhiteSpace(widget.Zone));
		metadata.ShouldAllBe(widget => !string.IsNullOrWhiteSpace(widget.DrilldownUrl) || !string.IsNullOrWhiteSpace(widget.Owner));
		metadata.ShouldAllBe(widget => widget.RequiredPermissions.Count > 0);

		var financeCollection = metadata.Single(x => x.Id == "finance-collection");
		financeCollection.Zone.ShouldBe("kpis");
		financeCollection.DrilldownUrl.ShouldBe("/finance/fee-collection");
		financeCollection.RequiredPermissions.ShouldContain(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Forbidden_When_Widget_Metadata_Dashboard_Permission_Is_Missing()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			"__none__");

		var response = await client.GetAsync(WidgetMetadataUrl);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Theory]
	[TestTier(TestTraits.Tiers.Important)]
	[InlineData("unknown", "2026-05-01", "2026-05-23")]
	[InlineData("principalAdmin", "2026-05-24", "2026-05-23")]
	[InlineData("principalAdmin", "05-01-2026", "2026-05-23")]
	public async Task Should_Reject_Invalid_Template_Or_Date_Range(
		string dashboardTemplate,
		string from,
		string to)
	{
		var response = await Client.GetAsync(
			$"{BaseUrl}?dashboardTemplate={dashboardTemplate}&from={from}&to={to}");

		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Return_Forbidden_When_Dashboard_Permission_Is_Missing()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			"__none__");

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=principalAdmin");

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Return_Zone_Error_And_Base_Widgets_When_Dashboard_Slice_Throws()
	{
		var reader = CreateReaderForSliceFailureTests(
			new DashboardSummarySliceRegistration(
				"failing-kpis",
				10,
				"kpis",
				"KPI slice could not be loaded.",
				new FailingAfterMutationSliceProvider()));

		var summary = await reader.GetSummaryAsync(
			new DashboardSummaryRequestDto { DashboardTemplate = "principalAdmin" },
			new DateTime(2026, 5, 23));

		summary.ZoneErrors["kpis"].ShouldBe("KPI slice could not be loaded.");
		summary.KpiCards.Single(x => x.Id == "principal-teacher-attendance")
			.Title.ShouldBe("Teacher Attendance");
		summary.KpiCards.ShouldNotContain(widget => widget.Title == FailingAfterMutationSliceProvider.LeakedTitle);
		summary.KpiCards.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Filter_Unauthorized_Widgets_After_Dashboard_Slice_Failure()
	{
		var reader = CreateReaderForSliceFailureTests(
			new DashboardSummarySliceRegistration(
				"failing-kpis",
				10,
				"kpis",
				"KPI slice could not be loaded.",
				new FailingAfterMutationSliceProvider()),
			new FixedPermissionChecker(
				ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View));

		var summary = await reader.GetSummaryAsync(
			new DashboardSummaryRequestDto { DashboardTemplate = "principalAdmin" },
			new DateTime(2026, 5, 23));

		summary.ZoneErrors["kpis"].ShouldBe("KPI slice could not be loaded.");
		summary.KpiCards.Select(widget => widget.Id).ShouldContain("principal-teacher-attendance");
		summary.KpiCards.Select(widget => widget.Id).ShouldNotContain("principal-curriculum");
		summary.KpiCards.Select(widget => widget.Id).ShouldNotContain("principal-pending-admissions");
		summary.ActionItems.ShouldBeEmpty();
		summary.ModuleSnapshots.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Filter_Unregistered_Widgets_From_Successful_Dashboard_Slices()
	{
		var reader = CreateReaderForSliceFailureTests(
			new DashboardSummarySliceRegistration(
				"unknown-kpis",
				10,
				"kpis",
				"KPI slice could not be loaded.",
				new UnknownWidgetSliceProvider()));

		var summary = await reader.GetSummaryAsync(
			new DashboardSummaryRequestDto { DashboardTemplate = "principalAdmin" },
			new DateTime(2026, 5, 23));

		summary.ZoneErrors.ShouldBeEmpty();
		summary.KpiCards.Select(widget => widget.Id).ShouldContain("principal-teacher-attendance");
		summary.KpiCards.Select(widget => widget.Id).ShouldNotContain(UnknownWidgetSliceProvider.UnknownWidgetId);
		summary.KpiCards.ShouldNotContain(widget => widget.Title == UnknownWidgetSliceProvider.UnknownTitle);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Hide_Attendance_Widget_When_Student_Attendance_Permission_Is_Missing()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraModulePermissions.Dashboard);

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=principalAdmin&from=2026-05-01&to=2026-05-23");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();

		json.ShouldNotContain("principal-student-attendance");
		json.ShouldNotContain("Student Attendance");
		json.ShouldNotContain("/student/attendance/take-attendance");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Hide_Attendance_Drilldown_When_User_Has_Only_Student_Records_Permission()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=principalAdmin&from=2026-05-01&to=2026-05-23");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();

		json.ShouldNotContain("principal-student-attendance");
		json.ShouldNotContain("Student Attendance");
		json.ShouldNotContain("/student/attendance/take-attendance");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Hide_Action_Widget_Metadata_When_Module_Permissions_Are_Missing()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraModulePermissions.Dashboard);

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=leadership&from=2026-05-01&to=2026-05-23");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();

		json.ShouldNotContain("leadership-admission-approvals");
		json.ShouldNotContain("Admission approvals");
		json.ShouldNotContain("/student/enrollment/admission");
		json.ShouldNotContain("leadership-payroll-review");
		json.ShouldNotContain("Payroll review");
		json.ShouldNotContain("/hr/payroll/run-payroll");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Surface_Live_Scoped_Total_Students_When_Record_Permission_Is_Granted()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await SeedEnrolledStudentAsync();

		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=leadership&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var totalStudentsWidget = payload.KpiCards.SingleOrDefault(x => x.Id == "leadership-total-students");

		totalStudentsWidget.ShouldNotBeNull();
		totalStudentsWidget.Title.ShouldBe("Total Students");
		totalStudentsWidget.DrilldownUrl.ShouldBe("/student/profile");
		totalStudentsWidget.Subtitle.ShouldNotBeNull();
		totalStudentsWidget.Subtitle.ShouldContain("enrolled student");
		var totalStudents = int.Parse(totalStudentsWidget.Value!.ToString()!, CultureInfo.InvariantCulture);
		totalStudents.ShouldBeGreaterThanOrEqualTo(1);
		totalStudentsWidget.Count.ShouldBe(totalStudents);
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Hide_Total_Students_When_Record_Permission_Is_Missing()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraModulePermissions.Dashboard);

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=leadership&from=2026-05-01&to=2026-05-23");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();

		json.ShouldNotContain("leadership-total-students");
		json.ShouldNotContain("Total Students");
		json.ShouldNotContain("/student/profile");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Apply_UserScope_To_Total_Students()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await SeedEnrolledStudentAsync();

		var client = CreateClientAs(Guid.NewGuid());
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=leadership&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var totalStudentsWidget = payload.KpiCards.SingleOrDefault(x => x.Id == "leadership-total-students");

		totalStudentsWidget.ShouldNotBeNull();
		totalStudentsWidget.Value!.ToString().ShouldBe("0");
		totalStudentsWidget.Count.ShouldBe(0);
		totalStudentsWidget.Subtitle.ShouldBe("No enrolled students in scope");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Include_Scoped_Student_Attendance_Widget_When_Permission_Is_Granted()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View,
				ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=principalAdmin&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var attendanceWidget = payload.KpiCards.SingleOrDefault(x => x.Id == "principal-student-attendance");

		attendanceWidget.ShouldNotBeNull();
		attendanceWidget.Title.ShouldBe("Student Attendance");
		attendanceWidget.DrilldownUrl.ShouldBe("/student/attendance/take-attendance");
		attendanceWidget.Value.ShouldNotBeNull();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Surface_Live_Principal_Teacher_Attendance_When_Hr_Attendance_Permission_Is_Granted()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=principalAdmin&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var attendanceWidget = payload.KpiCards.SingleOrDefault(x => x.Id == "principal-teacher-attendance");

		attendanceWidget.ShouldNotBeNull();
		attendanceWidget.Title.ShouldBe("Teacher Attendance");
		attendanceWidget.DrilldownUrl.ShouldBe("/hr/attendance-tracking");
		attendanceWidget.Value.ShouldNotBeNull();
		attendanceWidget.Value!.ToString()!.ShouldContain("%");
		attendanceWidget.Value!.ToString()!.ShouldNotBe("Open");
		attendanceWidget.Subtitle.ShouldNotBeNull();
		attendanceWidget.Subtitle!.ShouldContain("teaching staff");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Surface_Live_Scoped_Curriculum_Completion_When_Permission_Is_Granted()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await SeedCurriculumProgressAsync();

		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_ChapterManagement.View));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=principalAdmin&academicYearId={Factory.SeededAcademicYearId:D}&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var curriculumWidget = payload.KpiCards.SingleOrDefault(x => x.Id == "principal-curriculum");

		curriculumWidget.ShouldNotBeNull();
		curriculumWidget.Title.ShouldBe("Curriculum Completion");
		curriculumWidget.DrilldownUrl.ShouldBe("/academic/courseSubjects");
		curriculumWidget.Value.ShouldNotBeNull();
		curriculumWidget.Value!.ToString().ShouldEndWith("%");
		curriculumWidget.Value!.ToString().ShouldNotBe("Review");
		curriculumWidget.Subtitle.ShouldNotBeNull();
		curriculumWidget.Subtitle.ShouldContain("scoped division-chapters completed");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Surface_Live_Academic_Health_When_Dashboard_Permission_Is_Granted()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await SeedCurriculumProgressAsync();

		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Academics.Dashboard.View));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=leadership&academicYearId={Factory.SeededAcademicYearId:D}&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var academicHealth = payload.ModuleSnapshots.SingleOrDefault(x => x.Id == "leadership-academic-health");

		academicHealth.ShouldNotBeNull();
		academicHealth.Title.ShouldBe("Academic Health");
		academicHealth.DrilldownUrl.ShouldBe("/academic/dashboard");
		academicHealth.Value.ShouldNotBeNull();
		academicHealth.Value!.ToString().ShouldEndWith("%");
		academicHealth.Value!.ToString().ShouldNotBe("Open");
		academicHealth.Subtitle.ShouldNotBeNull();
		academicHealth.Subtitle.ShouldContain("scoped division-chapters completed");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Surface_Curriculum_Pacing_Risk_When_Planned_Chapters_Are_Incomplete()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await SeedCurriculumProgressAsync();

		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Academics.Dashboard.View,
				ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_ChapterManagement.View));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=principalAdmin&academicYearId={Factory.SeededAcademicYearId:D}&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var pacingRisk = payload.RiskAlerts.SingleOrDefault(x => x.Id == "principal-weak-classes");
		var academicDashboard = payload.ModuleSnapshots.SingleOrDefault(x => x.Id == "principal-academic-dashboard");

		pacingRisk.ShouldNotBeNull();
		pacingRisk.Title.ShouldBe("Curriculum pacing risk");
		pacingRisk.Count.ShouldNotBeNull();
		pacingRisk.Count!.Value.ShouldBeGreaterThan(0);
		pacingRisk.Subtitle.ShouldNotBeNull();
		pacingRisk.Subtitle.ShouldContain("past plan");
		pacingRisk.DrilldownUrl.ShouldBe("/academic/dashboard");

		academicDashboard.ShouldNotBeNull();
		academicDashboard.Value.ShouldNotBeNull();
		academicDashboard.Value!.ToString().ShouldEndWith("%");
		academicDashboard.Subtitle.ShouldNotBeNull();
		academicDashboard.Subtitle.ShouldContain("need pacing review");
		academicDashboard.DrilldownUrl.ShouldBe("/academic/dashboard");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Apply_UserScope_To_Curriculum_Completion()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await SeedCurriculumProgressAsync();

		var client = CreateClientAs(Guid.NewGuid());
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Academics.CurriculumManagement_ChapterManagement.View));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=principalAdmin&academicYearId={Factory.SeededAcademicYearId:D}&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var curriculumWidget = payload.KpiCards.SingleOrDefault(x => x.Id == "principal-curriculum");

		curriculumWidget.ShouldNotBeNull();
		curriculumWidget.Value!.ToString().ShouldBe("0%");
		curriculumWidget.Count.ShouldBe(0);
		curriculumWidget.Subtitle.ShouldBe("No scoped curriculum chapters found");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Surface_Scoped_Academic_Exam_Workflow_Backlog()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await SeedExamWorkflowBacklogAsync();

		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Academics.Dashboard.View,
				ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=principalAdmin&academicYearId={Factory.SeededAcademicYearId:D}&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var academicDashboard = payload.ModuleSnapshots.SingleOrDefault(x => x.Id == "principal-academic-dashboard");

		academicDashboard.ShouldNotBeNull();
		academicDashboard.Count.ShouldBe(1);
		academicDashboard.Subtitle.ShouldNotBeNull();
		academicDashboard.Subtitle.ShouldContain("scoped exam marks workflow");
		academicDashboard.Subtitle.ShouldContain("submit, verify, or publish");
		academicDashboard.DrilldownUrl.ShouldBe("/academic/dashboard");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Apply_UserScope_To_Academic_Exam_Workflow_Backlog()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await SeedExamWorkflowBacklogAsync();

		var client = CreateClientAs(Guid.NewGuid());
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Academics.Dashboard.View,
				ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=principalAdmin&academicYearId={Factory.SeededAcademicYearId:D}&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var academicDashboard = payload.ModuleSnapshots.SingleOrDefault(x => x.Id == "principal-academic-dashboard");

		academicDashboard.ShouldNotBeNull();
		academicDashboard.Count.ShouldBe(0);
		academicDashboard.Subtitle.ShouldNotBeNull();
		academicDashboard.Subtitle.ShouldContain("No scoped exam marks workflow pending");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Surface_Scoped_Academic_Result_Outcome_When_Result_Permission_Is_Granted()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await SeedPublishedResultOutcomeAsync();

		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Academics.Dashboard.View,
				ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.View));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=principalAdmin&academicYearId={Factory.SeededAcademicYearId:D}&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var academicDashboard = payload.ModuleSnapshots.SingleOrDefault(x => x.Id == "principal-academic-dashboard");

		academicDashboard.ShouldNotBeNull();
		academicDashboard.Count.ShouldBe(0);
		academicDashboard.Status.ShouldBe("warning");
		academicDashboard.Health.ShouldBe("attention");
		academicDashboard.Subtitle.ShouldNotBeNull();
		academicDashboard.Subtitle.ShouldContain("Published result pass rate 67%");
		academicDashboard.Subtitle.ShouldContain("students need result follow-up");
		academicDashboard.Subtitle.ShouldNotContain("exam marks workflow");
		academicDashboard.DrilldownUrl.ShouldBe("/academic/dashboard");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Apply_UserScope_To_Academic_Result_Outcome()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await SeedPublishedResultOutcomeAsync();

		var client = CreateClientAs(Guid.NewGuid());
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Academics.Dashboard.View,
				ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.View));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=principalAdmin&academicYearId={Factory.SeededAcademicYearId:D}&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var academicDashboard = payload.ModuleSnapshots.SingleOrDefault(x => x.Id == "principal-academic-dashboard");

		academicDashboard.ShouldNotBeNull();
		academicDashboard.Count.ShouldBe(0);
		academicDashboard.Subtitle.ShouldNotBeNull();
		academicDashboard.Subtitle.ShouldContain("No scoped published result outcome found");
		academicDashboard.Subtitle.ShouldNotContain("Published result pass rate");
		academicDashboard.Subtitle.ShouldNotContain("exam marks workflow");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Not_Surface_Academic_Result_Outcome_Without_Result_Permission()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await SeedPublishedResultOutcomeAsync();

		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Academics.Dashboard.View));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=principalAdmin&academicYearId={Factory.SeededAcademicYearId:D}&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var academicDashboard = payload.ModuleSnapshots.SingleOrDefault(x => x.Id == "principal-academic-dashboard");

		academicDashboard.ShouldNotBeNull();
		academicDashboard.Subtitle.ShouldNotBeNull();
		academicDashboard.Subtitle.ShouldNotContain("Published result pass rate");
		academicDashboard.Subtitle.ShouldNotContain("No scoped published result outcome found");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Surface_Live_Scoped_Admission_Action_Count()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		await SeedPendingAdmissionAsync();

		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=leadership&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var admissionAction = payload.ActionItems.SingleOrDefault(x => x.Id == "leadership-admission-approvals");

		admissionAction.ShouldNotBeNull();
		admissionAction.Count.ShouldNotBeNull();
		admissionAction.Count.Value.ShouldBeGreaterThanOrEqualTo(1);
		admissionAction.Subtitle.ShouldNotBeNull();
		admissionAction.Subtitle.ShouldContain("admission");
		admissionAction.DrilldownUrl.ShouldBe("/student/enrollment/admission");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Surface_Finance_Hr_Live_Snapshot_When_Permissions_Are_Granted()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();

		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View,
				ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.View,
				ShikshaMitraSubmodulePermissions.HR.PayrollManagement_Dashboard.View,
				ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_AttendanceTracking.View,
				ShikshaMitraModulePermissions.Finance,
				ShikshaMitraSubModulePermissions.HRDashboard));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=financeHr&academicYearId={Factory.SeededAcademicYearId:D}&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var widgets = payload.KpiCards.ToDictionary(x => x.Id);
		var moduleSnapshots = payload.ModuleSnapshots.ToDictionary(x => x.Id);

		widgets["finance-collection"].Value.ShouldNotBeNull();
		widgets["finance-collection"].Value!.ToString()!.ShouldStartWith("INR");
		widgets["finance-dues"].Value.ShouldNotBeNull();
		widgets["finance-dues"].Value!.ToString()!.ShouldStartWith("INR");
		widgets["hr-payroll-readiness"].Value.ShouldNotBeNull();
		widgets["hr-payroll-readiness"].Value!.ToString()!.ShouldContain("%");
		widgets["hr-staff-attendance"].Value.ShouldNotBeNull();
		widgets["hr-staff-attendance"].Value!.ToString()!.ShouldContain("%");
		var concessionRisk = payload.RiskAlerts.Single(x => x.Id == "finance-concession-impact");
		concessionRisk.Title.ShouldBe("Concession impact review");
		concessionRisk.DrilldownUrl.ShouldBe("/finance/concession");

		moduleSnapshots["finance-dashboard"].Value.ShouldNotBeNull();
		moduleSnapshots["finance-dashboard"].Value!.ToString()!.ShouldStartWith("INR");
		moduleSnapshots["finance-dashboard"].Subtitle.ShouldNotBeNull();
		moduleSnapshots["finance-dashboard"].Subtitle!.ShouldContain("due students");
		moduleSnapshots["finance-dashboard"].DrilldownUrl.ShouldBe("/finance/dashboard");

		moduleSnapshots["hr-dashboard"].Value.ShouldNotBeNull();
		moduleSnapshots["hr-dashboard"].Value!.ToString()!.ShouldContain("%");
		moduleSnapshots["hr-dashboard"].Subtitle.ShouldNotBeNull();
		moduleSnapshots["hr-dashboard"].Subtitle!.ShouldContain("staff attendance");
		moduleSnapshots["hr-dashboard"].DrilldownUrl.ShouldBe("/hr/dashboard");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Hide_Finance_Hr_Metadata_When_Finance_Hr_Permissions_Are_Missing()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraModulePermissions.Dashboard);

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=financeHr&academicYearId={Factory.SeededAcademicYearId:D}");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();
		json.ShouldNotContain("finance-collection");
		json.ShouldNotContain("Today Collection");
		json.ShouldNotContain("/finance/fee-collection");
		json.ShouldNotContain("hr-payroll-readiness");
		json.ShouldNotContain("Payroll Readiness");
		json.ShouldNotContain("/hr/payroll/dashboard");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Surface_Visible_Calendar_Item_When_Calendar_Permission_Is_Granted()
	{
		var created = await SeedCalendarEventAsync("DashboardCalendar");

		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(",", ShikshaMitraModulePermissions.Dashboard, ShikshaMitraSubModulePermissions.CalendarView));

		var response = await client.GetAsync(
			$"{BaseUrl}?dashboardTemplate=principalAdmin&academicYearId={Factory.SeededAcademicYearId:D}&from={created.StartDate:yyyy-MM-dd}&to={created.StartDate:yyyy-MM-dd}");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var calendarWidget = payload.CalendarItems.Single(x => x.Id == "calendar-safety-drill");

		calendarWidget.Title.ShouldBe(created.Title);
		calendarWidget.Subtitle!.ShouldContain("Event:");
		calendarWidget.DrilldownUrl.ShouldBe("/calendar/view");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Hide_Calendar_View_Drilldown_When_User_Has_Only_Calendar_Module_Permission()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(",", ShikshaMitraModulePermissions.Dashboard, ShikshaMitraModulePermissions.Calendar));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=principalAdmin");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();
		json.ShouldNotContain("calendar-safety-drill");
		json.ShouldNotContain("Safety drill follow-up");
		json.ShouldNotContain("/calendar/view");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Hide_Communication_Dashboard_Drilldown_When_User_Has_Only_Communication_Module_Permission()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(",", ShikshaMitraModulePermissions.Dashboard, ShikshaMitraModulePermissions.Communication));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=principalAdmin");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);

		var json = await response.Content.ReadAsStringAsync();
		json.ShouldNotContain("announcement-ptm");
		json.ShouldNotContain("PTM circular pending acknowledgement");
		json.ShouldNotContain("/communication/dashboard");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Surface_Transport_And_Security_Readiness_When_Permitted()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubModulePermissions.ApplicationManagement,
				ShikshaMitraSubModulePermissions.SecurityGuardDashboard));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=principalAdmin");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");

		payload.ActionItems.Single(x => x.Id == "principal-transport-applications")
			.DrilldownUrl.ShouldBe("/transport/application-management");
		payload.ModuleSnapshots.Single(x => x.Id == "principal-security-dashboard")
			.DrilldownUrl.ShouldBe("/security-guard/dashboard");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Surface_Local_Compliance_Readiness_When_Student_Compliance_Permission_Is_Granted()
	{
		await EnsureDefaultTestUserHasTenantWideScopeAsync();
		var studentId = await SeedPendingAdmissionAsync();
		await SeedStudentComplianceAsync(studentId);

		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraModulePermissions.InstituteSetup,
				ShikshaMitraSubmodulePermissions.Students.IdentityCompliance.View));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=leadership");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var compliance = payload.KpiCards.Single(x => x.Id == "leadership-compliance");

		compliance.Value.ShouldNotBeNull();
		compliance.Value!.ToString()!.ShouldContain("%");
		compliance.Subtitle!.ShouldContain("APAAR");
		compliance.Subtitle!.ShouldContain("UDISE");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Surface_Teacher_Assigned_Class_Context_For_Mapped_Teacher()
	{
		var teacher = await FindSeededEmployeeByEmailAsync("teacher-demo-1@ddvs.com");
		teacher.UserId.ShouldNotBeNull();
		await EnsureUserHasTenantWideScopeAsync(teacher.UserId.Value);
		await SeedClassTeacherAssignmentAsync(teacher.Id);

		var client = CreateClientAs(teacher.UserId.Value);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View,
				ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=teacher");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var classWidget = payload.KpiCards.Single(x => x.Id == "teacher-classes");
		var classCount = int.Parse(classWidget.Value!.ToString()!, CultureInfo.InvariantCulture);

		classWidget.Title.ShouldBe("Assigned Classes");
		classCount.ShouldBeGreaterThanOrEqualTo(1);
		classWidget.Subtitle!.ShouldContain("assigned");
		payload.RiskAlerts.Single(x => x.Id == "teacher-student-risk")
			.Subtitle!.ShouldContain("scoped student records");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Surface_Teacher_Class_Performance_For_Assigned_Class_When_Result_Permission_Is_Granted()
	{
		var teacher = await EmployeeTestHelper.CreateEmployeeAsync(Client, "DashboardClassPerformance");
		teacher.UserId.ShouldNotBeNull();
		await EnsureUserHasTenantWideScopeAsync(teacher.UserId.Value);
		await SeedTeacherClassPerformanceAsync(teacher.Id, teacher.UserId.Value);

		var client = CreateClientAs(teacher.UserId.Value);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Academics.Dashboard.View,
				ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.View));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=teacher");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var performance = payload.ModuleSnapshots.Single(x => x.Id == "teacher-class-performance");

		performance.Value!.ToString().ShouldBe("67%");
		performance.Count.ShouldBe(1);
		performance.Subtitle!.ShouldContain("assigned class");
		performance.Subtitle!.ShouldContain("1 student needs follow-up");
		performance.DrilldownUrl.ShouldBe("/academic/dashboard");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Surface_Teacher_Class_Attendance_Action_For_Assigned_Classes()
	{
		var teacher = await EmployeeTestHelper.CreateEmployeeAsync(Client, "DashboardClassAttendance");
		teacher.UserId.ShouldNotBeNull();
		await EnsureUserHasTenantWideScopeAsync(teacher.UserId.Value);
		await SeedTeacherClassAttendanceAsync(teacher.Id, teacher.UserId.Value, new DateTime(2026, 5, 23));

		var client = CreateClientAs(teacher.UserId.Value);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=teacher&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var attendance = payload.ActionItems.Single(x => x.Id == "teacher-attendance");

		attendance.Count.ShouldBe(1);
		attendance.Subtitle!.ShouldContain("assigned class attendance not marked");
		attendance.DrilldownUrl.ShouldBe("/student/attendance/take-attendance");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Surface_Teacher_Exam_Evaluation_For_Assigned_Subject_Workflow()
	{
		var teacher = await EmployeeTestHelper.CreateEmployeeAsync(Client, "DashboardExamEvaluation");
		teacher.UserId.ShouldNotBeNull();
		await EnsureUserHasTenantWideScopeAsync(teacher.UserId.Value);
		await SeedTeacherExamEvaluationAsync(teacher.Id);

		var client = CreateClientAs(teacher.UserId.Value);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=teacher");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var evaluation = payload.KpiCards.Single(x => x.Id == "teacher-exam-evaluation");

		evaluation.Value!.ToString().ShouldBe("1");
		evaluation.Count.ShouldBe(1);
		evaluation.Subtitle!.ShouldContain("assigned-subject exam marks workflow");
		evaluation.DrilldownUrl.ShouldBe("/academic/gradeEntry");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Hide_Teacher_Exam_Evaluation_When_Record_Marks_Permission_Is_Missing()
	{
		var teacher = await EmployeeTestHelper.CreateEmployeeAsync(Client, "DashboardDeniedExamEvaluation");
		teacher.UserId.ShouldNotBeNull();
		await EnsureUserHasTenantWideScopeAsync(teacher.UserId.Value);
		await SeedTeacherExamEvaluationAsync(teacher.Id);

		var client = CreateClientAs(teacher.UserId.Value);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraModulePermissions.Dashboard);

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=teacher");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var json = await response.Content.ReadAsStringAsync();
		json.ShouldNotContain("teacher-exam-evaluation");
		json.ShouldNotContain("Pending Exam Evaluation");
		json.ShouldNotContain("/academic/gradeEntry");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Keep_Teacher_Readiness_When_User_Is_Not_Mapped_To_Employee()
	{
		var userId = Guid.NewGuid();
		await EnsureUserHasTenantWideScopeAsync(userId);

		var client = CreateClientAs(userId);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Academics.Timetable_TimetableManagement.View));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=teacher");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var classWidget = payload.KpiCards.Single(x => x.Id == "teacher-classes");

		classWidget.Title.ShouldBe("Today's Classes");
		classWidget.Value!.ToString().ShouldBe("Open");
		classWidget.Subtitle!.ShouldContain("assigned classes");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Surface_Teacher_Learning_Task_Workload_For_Creator_Owned_Scoped_Submissions()
	{
		var teacher = await EmployeeTestHelper.CreateEmployeeAsync(Client, "DashboardWorkload");
		teacher.UserId.ShouldNotBeNull();
		await SeedTeacherLearningTaskWorkloadAsync(teacher.UserId.Value);

		var client = CreateClientAs(teacher.UserId.Value);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.View));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=teacher");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var homework = payload.KpiCards.Single(x => x.Id == "teacher-homework");

		homework.Title.ShouldBe("Pending Learning Reviews");
		homework.Value!.ToString().ShouldBe("2");
		homework.Count.ShouldBe(2);
		homework.Subtitle!.ShouldContain("creator-owned");
		homework.DrilldownUrl.ShouldBe("/academic/gradingAndFeedback");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Keep_Teacher_Homework_Readiness_When_User_Is_Not_Mapped_To_Employee()
	{
		var userId = Guid.NewGuid();
		await EnsureUserHasTenantWideScopeAsync(userId);

		var client = CreateClientAs(userId);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.View));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=teacher");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var homework = payload.KpiCards.Single(x => x.Id == "teacher-homework");

		homework.Title.ShouldBe("Pending Homework Review");
		homework.Value!.ToString().ShouldBe("Review");
		homework.Subtitle!.ShouldContain("feedback queue");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Hide_Teacher_Homework_When_Assessment_Feedback_Permission_Is_Missing()
	{
		var teacher = await EmployeeTestHelper.CreateEmployeeAsync(Client, "DashboardDeniedWorkload");
		teacher.UserId.ShouldNotBeNull();
		await SeedTeacherLearningTaskWorkloadAsync(teacher.UserId.Value);

		var client = CreateClientAs(teacher.UserId.Value);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraModulePermissions.Dashboard);

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=teacher");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var json = await response.Content.ReadAsStringAsync();
		json.ShouldNotContain("teacher-homework");
		json.ShouldNotContain("Pending Learning Reviews");
		json.ShouldNotContain("/academic/gradingAndFeedback");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Keep_Unsafe_Teacher_Areas_Readiness_Only_When_Workload_Is_Live()
	{
		var teacher = await EmployeeTestHelper.CreateEmployeeAsync(Client, "DashboardReadinessWorkload");
		teacher.UserId.ShouldNotBeNull();
		await SeedTeacherLearningTaskWorkloadAsync(teacher.UserId.Value);

		var client = CreateClientAs(teacher.UserId.Value);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_AssessmentAndFeedback.View,
				ShikshaMitraSubmodulePermissions.Academics.Examination_RecordMarks.View,
				ShikshaMitraSubmodulePermissions.Academics.Dashboard.View,
				ShikshaMitraSubmodulePermissions.Students.Attendance_MarkAttendance.View,
				ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=teacher");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");

		payload.KpiCards.Single(x => x.Id == "teacher-homework").Value!.ToString().ShouldBe("2");
		payload.KpiCards.Single(x => x.Id == "teacher-exam-evaluation").Value!.ToString().ShouldBe("Review");
		payload.ActionItems.Single(x => x.Id == "teacher-attendance").Count.ShouldBe(0);
		payload.ModuleSnapshots.Single(x => x.Id == "teacher-class-performance").Value!.ToString().ShouldBe("Open");
		rawPayload.ShouldNotContain("teacher-parent");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Surface_Student_Parent_Personal_Snapshot_When_Email_Maps_To_One_Enrolled_Student()
	{
		var email = $"dashboard-student-{Guid.NewGuid():N}@example.test";
		var studentId = await SeedEnrolledStudentWithContactEmailAsync(email);
		await SeedStudentPersonalAttendanceAsync(studentId);
		await SeedStudentOutstandingDueAsync(studentId, 1250d);
		await SeedStudentRequiredDocumentStatusAsync(studentId);

		var client = CreateClientAs(Guid.NewGuid(), email);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraModulePermissions.Students,
				ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View,
				ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=studentParent&from=2026-05-01&to=2026-05-23");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var attendance = payload.KpiCards.Single(x => x.Id == "student-attendance");
		var fee = payload.KpiCards.Single(x => x.Id == "student-fee-status");
		var documents = payload.ActionItems.Single(x => x.Id == "student-document-update");

		attendance.Value!.ToString().ShouldBe("50%");
		attendance.Subtitle!.ShouldContain("marked attendance days");
		fee.Value!.ToString().ShouldBe("INR 1250");
		fee.Count.ShouldBe(1);
		documents.Count.ShouldNotBeNull();
		documents.Count.Value.ShouldBeGreaterThan(0);
		documents.Subtitle!.ShouldContain("required document");
		rawPayload.ShouldNotContain("school-wide");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Surface_Student_Parent_Academic_Work_When_Email_Maps_To_One_Enrolled_Student()
	{
		var email = $"dashboard-student-academic-{Guid.NewGuid():N}@example.test";
		var studentId = await SeedEnrolledStudentWithContactEmailAsync(email);
		await SeedStudentAcademicWorkAsync(studentId);

		var client = CreateClientAs(Guid.NewGuid(), email);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraModulePermissions.Students,
				ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=studentParent");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var assignments = payload.KpiCards.Single(x => x.Id == "student-assignments");
		var academicSummary = payload.ModuleSnapshots.Single(x => x.Id == "student-academic-summary");

		assignments.Title.ShouldBe("Assignments Due");
		assignments.Value!.ToString().ShouldBe("2");
		assignments.Count.ShouldBe(2);
		assignments.Subtitle!.ShouldContain("overdue");
		assignments.DrilldownUrl.ShouldBe("/academic/homeworkCreation");

		academicSummary.Value!.ToString().ShouldBe("88%");
		academicSummary.Subtitle!.ShouldContain("Latest published result");
		academicSummary.Subtitle!.ShouldContain("Pass");
		academicSummary.Subtitle!.ShouldContain("grade A");
		academicSummary.DrilldownUrl.ShouldBe("/student/dashboard");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Keep_Student_Parent_Readiness_When_Email_Matches_Multiple_Students()
	{
		var email = $"dashboard-ambiguous-{Guid.NewGuid():N}@example.test";
		await SeedEnrolledStudentWithContactEmailAsync(email);
		await SeedEnrolledStudentWithContactEmailAsync(email);

		var client = CreateClientAs(Guid.NewGuid(), email);
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraModulePermissions.Students,
				ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View,
				ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=studentParent");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var attendance = payload.KpiCards.Single(x => x.Id == "student-attendance");
		var fee = payload.KpiCards.Single(x => x.Id == "student-fee-status");

		attendance.Value!.ToString().ShouldBe("View");
		attendance.Subtitle!.ShouldContain("Open student dashboard");
		fee.Value!.ToString().ShouldBe("Review");
		rawPayload.ShouldNotContain("marked attendance days");
		rawPayload.ShouldNotContain("Latest published result");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Keep_Student_Parent_Readiness_When_No_Personal_Mapping_Exists()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraModulePermissions.Students,
				ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_LearningTasks.View,
				ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View,
				ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=studentParent");
		var rawPayload = await response.Content.ReadAsStringAsync();

		response.StatusCode.ShouldBe(HttpStatusCode.OK, rawPayload);

		var payload = await response.Content.ReadFromJsonAsync<DashboardSummaryDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to deserialize dashboard summary.");
		var attendance = payload.KpiCards.Single(x => x.Id == "student-attendance");
		var fee = payload.KpiCards.Single(x => x.Id == "student-fee-status");

		attendance.Value!.ToString().ShouldBe("View");
		attendance.Subtitle!.ShouldContain("Open student dashboard");
		fee.Value!.ToString().ShouldBe("Review");
		rawPayload.ShouldNotContain("94%");
		rawPayload.ShouldNotContain("school-wide");
		rawPayload.ShouldNotContain("Latest published result");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task Should_Not_Surface_Student_Transport_Alert_Without_Personal_Mapping()
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			string.Join(
				",",
				ShikshaMitraModulePermissions.Dashboard,
				ShikshaMitraSubModulePermissions.TransportDashboard));

		var response = await client.GetAsync($"{BaseUrl}?dashboardTemplate=studentParent");

		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		var json = await response.Content.ReadAsStringAsync();
		json.ShouldNotContain("student-transport-alert");
		json.ShouldNotContain("Route timing changed");
	}

	private static DashboardSummaryReader CreateReaderForSliceFailureTests(
		DashboardSummarySliceRegistration registration,
		IPermissionChecker? permissionChecker = null) =>
		new(
			DashboardSummarySlicePipeline.CreateForTesting([registration]),
			permissionChecker ?? FixedPermissionChecker.GrantAll(),
			NullLogger<DashboardSummaryReader>.Instance);

	private HttpClient CreateClientAs(Guid userId, string email)
	{
		var client = CreateClientAs(userId);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestEmailHeader, email);
		return client;
	}

	private sealed class FailingAfterMutationSliceProvider : IDashboardSummarySliceProvider
	{
		public const string LeakedTitle = "Partial failure leaked into dashboard";

		public Task ApplyAsync(DashboardSummarySliceContext context)
		{
			if (context.Summary.KpiCards.Count > 0)
			{
				context.Summary.KpiCards[0].Title = LeakedTitle;
			}

			throw new InvalidOperationException("Dashboard test slice failure.");
		}
	}

	private sealed class UnknownWidgetSliceProvider : IDashboardSummarySliceProvider
	{
		public const string UnknownWidgetId = "unknown-dashboard-widget";
		public const string UnknownTitle = "Unregistered Dashboard Widget";

		public Task ApplyAsync(DashboardSummarySliceContext context)
		{
			context.Summary.KpiCards.Add(
				new DashboardWidgetDto
				{
					Id = UnknownWidgetId,
					Title = UnknownTitle,
					Value = "Should not render",
					Subtitle = "This widget is not in the dashboard catalog.",
					Status = "warning",
					DrilldownUrl = "/dashboard/unknown"
				});

			return Task.CompletedTask;
		}
	}

	private sealed class FixedPermissionChecker : IPermissionChecker
	{
		private readonly HashSet<string>? _grantedPermissions;

		public FixedPermissionChecker(params string[] grantedPermissions)
		{
			_grantedPermissions = new HashSet<string>(grantedPermissions, StringComparer.Ordinal);
		}

		private FixedPermissionChecker()
		{
		}

		public static FixedPermissionChecker GrantAll() => new();

		public Task<bool> IsGrantedAsync(string name) => Task.FromResult(IsGranted(name));

		public Task<bool> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string name) =>
			Task.FromResult(IsGranted(name));

		public Task<MultiplePermissionGrantResult> IsGrantedAsync(string[] names)
		{
			var result = new MultiplePermissionGrantResult();
			foreach (var name in names)
			{
				result.Result[name] = IsGranted(name)
					? PermissionGrantResult.Granted
					: PermissionGrantResult.Prohibited;
			}

			return Task.FromResult(result);
		}

		public Task<MultiplePermissionGrantResult> IsGrantedAsync(ClaimsPrincipal? claimsPrincipal, string[] names) =>
			IsGrantedAsync(names);

		private bool IsGranted(string name) =>
			_grantedPermissions == null || _grantedPermissions.Contains(name);
	}

	private async Task SeedTeacherLearningTaskWorkloadAsync(Guid teacherUserId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var homeworkRepository = scope.ServiceProvider.GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>();
		var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<AssignmentEntity, Guid>>();
		var submissionRepository = scope.ServiceProvider.GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>();
		var homeworkTypeRepository = scope.ServiceProvider.GetRequiredService<IHomeworkTypeRepository>();
		var assignmentTypeRepository = scope.ServiceProvider.GetRequiredService<IAssignmentTypeRepository>();
		var homeworkTypeManager = scope.ServiceProvider.GetRequiredService<HomeworkTypeManager>();
		var assignmentTypeManager = scope.ServiceProvider.GetRequiredService<AssignmentTypeManager>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var otherUserId = Guid.NewGuid();

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var homeworkType = (await homeworkTypeRepository.GetListAsync()).FirstOrDefault()
			                   ?? await homeworkTypeManager.CreateAsync(
				                   Factory.SeededMediumId,
				                   TestDataHelper.UniqueName("DashboardHomeworkType"));
			var assignmentType = (await assignmentTypeRepository.GetListAsync()).FirstOrDefault()
			                     ?? await assignmentTypeManager.CreateAsync(
				                     Factory.SeededMediumId,
				                     TestDataHelper.UniqueName("DashboardAssignmentType"));

			var homework = BuildHomework(
				homeworkType.Id,
				tenantId,
				teacherUserId,
				Factory.SeededStandardId,
				Factory.SeededDivisionId,
				Factory.SeededMediumStageId);
			await homeworkRepository.InsertAsync(homework, autoSave: false);
			await submissionRepository.InsertAsync(
				BuildHomeworkSubmission(homework.Id, tenantId, Factory.SeededDivisionId, SubmissionStatus.NeedsReview),
				autoSave: false);

			var assignment = BuildAssignment(
				assignmentType.Id,
				tenantId,
				teacherUserId,
				Factory.SeededStandardId,
				Factory.SeededDivisionId,
				Factory.SeededMediumStageId);
			await assignmentRepository.InsertAsync(assignment, autoSave: false);
			await submissionRepository.InsertAsync(
				BuildAssignmentSubmission(assignment.Id, tenantId, Factory.SeededDivisionId, SubmissionStatus.Late),
				autoSave: false);

			var otherUserAssignment = BuildAssignment(
				assignmentType.Id,
				tenantId,
				otherUserId,
				Factory.SeededStandardId,
				Factory.SeededDivisionId,
				Factory.SeededMediumStageId);
			await assignmentRepository.InsertAsync(otherUserAssignment, autoSave: false);
			await submissionRepository.InsertAsync(
				BuildAssignmentSubmission(otherUserAssignment.Id, tenantId, Factory.SeededDivisionId, SubmissionStatus.NeedsReview),
				autoSave: false);

			var reviewedHomework = BuildHomework(
				homeworkType.Id,
				tenantId,
				teacherUserId,
				Factory.SeededStandardId,
				Factory.SeededDivisionId,
				Factory.SeededMediumStageId);
			await homeworkRepository.InsertAsync(reviewedHomework, autoSave: false);
			var reviewedSubmission = BuildHomeworkSubmission(
				reviewedHomework.Id,
				tenantId,
				Factory.SeededDivisionId,
				SubmissionStatus.Submitted);
			reviewedSubmission.UpdateFeedback(
				8,
				10,
				"Reviewed",
				null,
				SubmissionStatus.Reviewed,
				Guid.NewGuid());
			await submissionRepository.InsertAsync(reviewedSubmission, autoSave: false);

			var outOfScopeHomework = BuildHomework(
				homeworkType.Id,
				tenantId,
				teacherUserId,
				Guid.NewGuid(),
				Guid.NewGuid(),
				Guid.NewGuid(),
				educationMediumId: Guid.NewGuid());
			await homeworkRepository.InsertAsync(outOfScopeHomework, autoSave: false);
			await submissionRepository.InsertAsync(
				BuildHomeworkSubmission(outOfScopeHomework.Id, tenantId, Guid.NewGuid(), SubmissionStatus.NeedsReview),
				autoSave: false);

			await uow.CompleteAsync();
		}
	}

	private HomeworkAssignmentEntity BuildHomework(
		Guid homeworkTypeId,
		Guid tenantId,
		Guid creatorUserId,
		Guid standardId,
		Guid divisionId,
		Guid educationMediumStageId,
		Guid? educationMediumId = null)
	{
		var homework = new HomeworkAssignmentEntity(
			Guid.NewGuid(),
			standardId,
			Guid.NewGuid(),
			TestDataHelper.UniqueName("DashboardHomework"),
			"Dashboard workload homework",
			homeworkTypeId,
			SubmissionType.OnlineText,
			tenantId,
			Factory.SeededAcademicYearId,
			educationMediumId: educationMediumId ?? Factory.SeededMediumId,
			educationMediumStageId: educationMediumStageId);
		homework.DivisionId = divisionId;
		homework.ChangeStatus(HomeworkStatus.Published);
		SetCreatorId(homework, creatorUserId);
		return homework;
	}

	private AssignmentEntity BuildAssignment(
		Guid assignmentTypeId,
		Guid tenantId,
		Guid creatorUserId,
		Guid standardId,
		Guid divisionId,
		Guid educationMediumStageId,
		Guid? educationMediumId = null)
	{
		var assignment = new AssignmentEntity(
			Guid.NewGuid(),
			standardId,
			Guid.NewGuid(),
			TestDataHelper.UniqueName("DashboardAssignment"),
			"Dashboard workload assignment",
			assignmentTypeId,
			SubmissionType.OnlineText,
			10,
			tenantId,
			Factory.SeededAcademicYearId,
			educationMediumId: educationMediumId ?? Factory.SeededMediumId,
			educationMediumStageId: educationMediumStageId);
		assignment.DivisionId = divisionId;
		assignment.ChangeStatus(AssignmentStatus.Published);
		SetCreatorId(assignment, creatorUserId);
		return assignment;
	}

	private static StudentSubmissionEntity BuildHomeworkSubmission(
		Guid homeworkId,
		Guid tenantId,
		Guid divisionId,
		SubmissionStatus status,
		Guid? studentId = null)
	{
		var submission = new StudentSubmissionEntity(
			Guid.NewGuid(),
			homeworkId,
			studentId ?? Guid.NewGuid(),
			divisionId,
			DateTime.UtcNow.AddDays(-1),
			tenantId);
		submission.UpdateStatus(status);
		return submission;
	}

	private static StudentSubmissionEntity BuildAssignmentSubmission(
		Guid assignmentId,
		Guid tenantId,
		Guid divisionId,
		SubmissionStatus status,
		Guid? studentId = null)
	{
		var submission = new StudentSubmissionEntity(
			Guid.NewGuid(),
			assignmentId,
			studentId ?? Guid.NewGuid(),
			divisionId,
			DateTime.UtcNow.AddDays(-1),
			tenantId,
			isAssignment: true);
		submission.UpdateStatus(status);
		return submission;
	}

	private static void SetCreatorId(object entity, Guid creatorUserId)
	{
		var property = entity.GetType().GetProperty("CreatorId")
		               ?? throw new InvalidOperationException($"CreatorId property not found on {entity.GetType().Name}.");
		var setter = property.GetSetMethod(nonPublic: true)
		             ?? throw new InvalidOperationException($"CreatorId setter not found on {entity.GetType().Name}.");
		setter.Invoke(entity, [creatorUserId]);
	}

	private async Task<Employee> FindSeededEmployeeByEmailAsync(string email)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var employeeRepository = scope.ServiceProvider.GetRequiredService<IRepository<Employee, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var employees = await employeeRepository.GetListAsync(employee => employee.Email == email);
			await uow.CompleteAsync();
			return employees.Single();
		}
	}

	private async Task SeedClassTeacherAssignmentAsync(Guid teacherId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var classTeacherRepository = scope.ServiceProvider.GetRequiredService<IRepository<ClassTeacher, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var existing = await classTeacherRepository.GetListAsync(assignment =>
				assignment.TeacherId == teacherId &&
				assignment.StandardId == Factory.SeededStandardId &&
				assignment.DivisionId == Factory.SeededDivisionId);

			if (existing.Count == 0)
			{
				await classTeacherRepository.InsertAsync(
					new ClassTeacher(
						Guid.NewGuid(),
						tenantId,
						Factory.SeededStandardId,
						Factory.SeededDivisionId,
						teacherId,
						Factory.SeededMediumId,
						Factory.SeededAcademicYearId,
						educationMediumId: Factory.SeededMediumId,
						educationMediumStageId: Factory.SeededMediumStageId),
					autoSave: false);
			}

			await uow.CompleteAsync();
		}
	}

	private async Task SeedTeacherClassPerformanceAsync(Guid teacherId, Guid userId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var classTeacherRepository = scope.ServiceProvider.GetRequiredService<IRepository<ClassTeacher, Guid>>();
		var examTypeRepository = scope.ServiceProvider.GetRequiredService<IRepository<ExamType, Guid>>();
		var examRepository = scope.ServiceProvider.GetRequiredService<IRepository<Exam, Guid>>();
		var resultHeaderRepository = scope.ServiceProvider.GetRequiredService<IRepository<ExamResultHeader, Guid>>();
		var resultDetailRepository = scope.ServiceProvider.GetRequiredService<IRepository<ExamResultDetail, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var standardId = Guid.NewGuid();
		var divisionId = Guid.NewGuid();

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			await classTeacherRepository.InsertAsync(
				new ClassTeacher(
					Guid.NewGuid(),
					tenantId,
					standardId,
					divisionId,
					teacherId,
					Factory.SeededMediumId,
					Factory.SeededAcademicYearId,
					educationMediumId: Factory.SeededMediumId,
					educationMediumStageId: Factory.SeededMediumStageId),
				autoSave: false);

			var examType = new ExamType(
				Guid.NewGuid(),
				tenantId,
				TestDataHelper.UniqueName("DashboardTeacherResultType"),
				"Dashboard teacher class performance test",
				Factory.SeededMediumId);
			await examTypeRepository.InsertAsync(examType, autoSave: false);

			var exam = new Exam(
				Guid.NewGuid(),
				examType.Id,
				TestDataHelper.UniqueName("DashboardTeacherResultExam"),
				standardId,
				tenantId);
			await examRepository.InsertAsync(exam, autoSave: false);

			var header = new ExamResultHeader(
				Guid.NewGuid(),
				exam.Id,
				divisionId,
				userId,
				tenantId);
			header.Publish(userId);
			await resultHeaderRepository.InsertAsync(header, autoSave: false);

			var rows = new[]
			{
				BuildResultDetail(header, 91, "A", ExamStudentResultStatus.Pass, 1, divisionId: divisionId),
				BuildResultDetail(header, 52, "C", ExamStudentResultStatus.Compartment, 2, divisionId: divisionId),
				BuildResultDetail(header, 28, "F", ExamStudentResultStatus.Fail, 3, divisionId: divisionId),
				BuildResultDetail(header, 0, "AB", ExamStudentResultStatus.Absent, null, divisionId: divisionId)
			};

			foreach (var row in rows)
			{
				await resultDetailRepository.InsertAsync(row, autoSave: false);
			}

			await uow.CompleteAsync();
		}
	}

	private async Task SeedTeacherClassAttendanceAsync(
		Guid teacherId,
		Guid userId,
		DateTime attendanceDate)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var classTeacherRepository = scope.ServiceProvider.GetRequiredService<IRepository<ClassTeacher, Guid>>();
		var attendanceDayRepository = scope.ServiceProvider.GetRequiredService<IAttendanceDayRepository>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var standardId = Guid.NewGuid();
		var markedDivisionId = Guid.NewGuid();
		var missingDivisionId = Guid.NewGuid();

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			foreach (var divisionId in new[] { markedDivisionId, missingDivisionId })
			{
				await classTeacherRepository.InsertAsync(
					new ClassTeacher(
						Guid.NewGuid(),
						tenantId,
						standardId,
						divisionId,
						teacherId,
						Factory.SeededMediumId,
						Factory.SeededAcademicYearId,
						educationMediumId: Factory.SeededMediumId,
						educationMediumStageId: Factory.SeededMediumStageId),
					autoSave: false);
			}

			await attendanceDayRepository.InsertAsync(
				BuildAttendanceDay(
					tenantId,
					attendanceDate,
					userId,
					standardId,
					markedDivisionId),
				autoSave: false);

			await uow.CompleteAsync();
		}
	}

	private async Task SeedTeacherExamEvaluationAsync(Guid teacherId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var allocationRepository = scope.ServiceProvider.GetRequiredService<IRepository<TeacherSubjectAllocation, Guid>>();
		var examTypeRepository = scope.ServiceProvider.GetRequiredService<IRepository<ExamType, Guid>>();
		var examRepository = scope.ServiceProvider.GetRequiredService<IRepository<Exam, Guid>>();
		var workflowRepository = scope.ServiceProvider.GetRequiredService<IRepository<ExamMarksWorkflowState, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var assignedSubjectId = Guid.NewGuid();
		var otherTeacherSubjectId = Guid.NewGuid();

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var assignedAllocation = new TeacherSubjectAllocation(
				Guid.NewGuid(),
				Factory.SeededStandardId,
				Factory.SeededDivisionId,
				assignedSubjectId);
			assignedAllocation.Teachers.Add(new AllocatedTeacher(assignedAllocation.Id, teacherId));
			await allocationRepository.InsertAsync(assignedAllocation, autoSave: false);

			var otherTeacherAllocation = new TeacherSubjectAllocation(
				Guid.NewGuid(),
				Factory.SeededStandardId,
				Factory.SeededDivisionId,
				otherTeacherSubjectId);
			otherTeacherAllocation.Teachers.Add(new AllocatedTeacher(otherTeacherAllocation.Id, Guid.NewGuid()));
			await allocationRepository.InsertAsync(otherTeacherAllocation, autoSave: false);

			var examType = new ExamType(
				Guid.NewGuid(),
				tenantId,
				TestDataHelper.UniqueName("DashboardTeacherExamType"),
				"Dashboard teacher exam evaluation test",
				Factory.SeededMediumId);
			await examTypeRepository.InsertAsync(examType, autoSave: false);

			var pendingExam = new Exam(
				Guid.NewGuid(),
				examType.Id,
				TestDataHelper.UniqueName("DashboardTeacherPendingExam"),
				Factory.SeededStandardId,
				tenantId);
			var publishedExam = new Exam(
				Guid.NewGuid(),
				examType.Id,
				TestDataHelper.UniqueName("DashboardTeacherPublishedExam"),
				Factory.SeededStandardId,
				tenantId);
			var otherTeacherExam = new Exam(
				Guid.NewGuid(),
				examType.Id,
				TestDataHelper.UniqueName("DashboardOtherTeacherExam"),
				Factory.SeededStandardId,
				tenantId);
			await examRepository.InsertAsync(pendingExam, autoSave: false);
			await examRepository.InsertAsync(publishedExam, autoSave: false);
			await examRepository.InsertAsync(otherTeacherExam, autoSave: false);

			var pendingWorkflow = new ExamMarksWorkflowState(
				Guid.NewGuid(),
				pendingExam.Id,
				assignedSubjectId,
				Factory.SeededDivisionId,
				tenantId);
			pendingWorkflow.TransitionTo(ExamMarksWorkflowStatus.Submitted);
			await workflowRepository.InsertAsync(pendingWorkflow, autoSave: false);

			var publishedWorkflow = new ExamMarksWorkflowState(
				Guid.NewGuid(),
				publishedExam.Id,
				assignedSubjectId,
				Factory.SeededDivisionId,
				tenantId);
			publishedWorkflow.TransitionTo(ExamMarksWorkflowStatus.Published);
			await workflowRepository.InsertAsync(publishedWorkflow, autoSave: false);

			var otherTeacherWorkflow = new ExamMarksWorkflowState(
				Guid.NewGuid(),
				otherTeacherExam.Id,
				otherTeacherSubjectId,
				Factory.SeededDivisionId,
				tenantId);
			otherTeacherWorkflow.TransitionTo(ExamMarksWorkflowStatus.Submitted);
			await workflowRepository.InsertAsync(otherTeacherWorkflow, autoSave: false);

			await uow.CompleteAsync();
		}
	}

	private async Task EnsureUserHasTenantWideScopeAsync(Guid userId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var scopeRepository = scope.ServiceProvider.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		var userScopeService = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var query = await scopeRepository.GetQueryableAsync();
			if (!query.Any(x =>
				    x.UserId == userId &&
				    x.AcademicYearId == null &&
				    x.EducationBoardId == null &&
				    x.EducationMediumId == null &&
				    x.EducationMediumStageId == null &&
				    x.StandardId == null &&
				    x.DivisionId == null &&
				    x.IsActive))
			{
				await scopeRepository.InsertAsync(
					new UserScopeAssignment(
						Guid.NewGuid(),
						tenantId,
						userId: userId,
						description: "Dashboard personalization test tenant-wide scope"),
					autoSave: false);
			}

			await userScopeService.InvalidateUserScopeCacheAsync(userId);
			await uow.CompleteAsync();
		}
	}

	private async Task SeedCurriculumProgressAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var subjectRepository = scope.ServiceProvider.GetRequiredService<IRepository<Subject, Guid>>();
		var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<AssignedSubject, Guid>>();
		var chapterRepository = scope.ServiceProvider.GetRequiredService<IRepository<AssignedSubjectChapter, Guid>>();
		var progressRepository = scope.ServiceProvider.GetRequiredService<IRepository<ChapterProgress, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var subject = new Subject(
				Guid.NewGuid(),
				tenantId,
				TestDataHelper.UniqueName("DashboardSubject"),
				"#2563eb",
				Factory.SeededMediumId);
			await subjectRepository.InsertAsync(subject, autoSave: false);

			var assignment = new AssignedSubject(
				Guid.NewGuid(),
				tenantId,
				Factory.SeededStandardId,
				subject.Id,
				TestDataHelper.UniqueName("DASH-SUB"),
				SubjectStatus.Published);
			await assignmentRepository.InsertAsync(assignment, autoSave: false);

			var firstChapter = BuildChapter(
				assignment.Id,
				1,
				"Dashboard curriculum started",
				new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero));
			var secondChapter = BuildChapter(
				assignment.Id,
				2,
				"Dashboard curriculum pending",
				new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero));
			await chapterRepository.InsertAsync(firstChapter, autoSave: false);
			await chapterRepository.InsertAsync(secondChapter, autoSave: false);

			var progress = new ChapterProgress(Guid.NewGuid(), firstChapter.Id, Factory.SeededDivisionId);
			progress.UpdateProgress(100, "Dashboard test completed chapter");
			await progressRepository.InsertAsync(progress, autoSave: false);

			await uow.CompleteAsync();
		}
	}

	private async Task SeedExamWorkflowBacklogAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var examTypeRepository = scope.ServiceProvider.GetRequiredService<IRepository<ExamType, Guid>>();
		var examRepository = scope.ServiceProvider.GetRequiredService<IRepository<Exam, Guid>>();
		var workflowRepository = scope.ServiceProvider.GetRequiredService<IRepository<ExamMarksWorkflowState, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var examType = new ExamType(
				Guid.NewGuid(),
				tenantId,
				TestDataHelper.UniqueName("DashboardExamType"),
				"Dashboard exam workflow test",
				Factory.SeededMediumId);
			await examTypeRepository.InsertAsync(examType, autoSave: false);

			var exam = new Exam(
				Guid.NewGuid(),
				examType.Id,
				TestDataHelper.UniqueName("DashboardExam"),
				Factory.SeededStandardId,
				tenantId);
			await examRepository.InsertAsync(exam, autoSave: false);

			var pendingWorkflow = new ExamMarksWorkflowState(
				Guid.NewGuid(),
				exam.Id,
				Guid.NewGuid(),
				Factory.SeededDivisionId,
				tenantId);
			pendingWorkflow.TransitionTo(ExamMarksWorkflowStatus.Submitted);
			await workflowRepository.InsertAsync(pendingWorkflow, autoSave: false);

			var publishedWorkflow = new ExamMarksWorkflowState(
				Guid.NewGuid(),
				exam.Id,
				Guid.NewGuid(),
				Factory.SeededDivisionId,
				tenantId);
			publishedWorkflow.TransitionTo(ExamMarksWorkflowStatus.Published);
			await workflowRepository.InsertAsync(publishedWorkflow, autoSave: false);

			await uow.CompleteAsync();
		}
	}

	private async Task SeedPublishedResultOutcomeAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var examTypeRepository = scope.ServiceProvider.GetRequiredService<IRepository<ExamType, Guid>>();
		var examRepository = scope.ServiceProvider.GetRequiredService<IRepository<Exam, Guid>>();
		var resultHeaderRepository = scope.ServiceProvider.GetRequiredService<IRepository<ExamResultHeader, Guid>>();
		var resultDetailRepository = scope.ServiceProvider.GetRequiredService<IRepository<ExamResultDetail, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var userId = Guid.Parse(TestAuthHandler.TestUserId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var examType = new ExamType(
				Guid.NewGuid(),
				tenantId,
				TestDataHelper.UniqueName("DashboardResultExamType"),
				"Dashboard result outcome test",
				Factory.SeededMediumId);
			await examTypeRepository.InsertAsync(examType, autoSave: false);

			var exam = new Exam(
				Guid.NewGuid(),
				examType.Id,
				TestDataHelper.UniqueName("DashboardResultExam"),
				Factory.SeededStandardId,
				tenantId);
			await examRepository.InsertAsync(exam, autoSave: false);

			var header = new ExamResultHeader(
				Guid.NewGuid(),
				exam.Id,
				Factory.SeededDivisionId,
				userId,
				tenantId);
			header.Publish(userId);
			await resultHeaderRepository.InsertAsync(header, autoSave: false);

			var rows = new[]
			{
				BuildResultDetail(header, 80, "A", ExamStudentResultStatus.Pass, 1),
				BuildResultDetail(header, 55, "C", ExamStudentResultStatus.Compartment, 2),
				BuildResultDetail(header, 25, "F", ExamStudentResultStatus.Fail, 3),
				BuildResultDetail(header, 0, "AB", ExamStudentResultStatus.Absent, null)
			};

			foreach (var row in rows)
			{
				await resultDetailRepository.InsertAsync(row, autoSave: false);
			}

			await uow.CompleteAsync();
		}
	}

	private ExamResultDetail BuildResultDetail(
		ExamResultHeader header,
		double percentage,
		string grade,
		ExamStudentResultStatus status,
		int? rank,
		Guid? studentId = null,
		Guid? divisionId = null) =>
		new(
			Guid.NewGuid(),
			header.Id,
			header.ExamId,
			studentId ?? Guid.NewGuid(),
			divisionId ?? Factory.SeededDivisionId,
			100,
			percentage,
			percentage,
			grade,
			rank,
			rank,
			status,
			Guid.Parse(TestAuthHandler.TestTenantId));

	private static AssignedSubjectChapter BuildChapter(
		Guid assignedSubjectId,
		int chapterNumber,
		string name,
		DateTimeOffset plannedEndDate)
	{
		var chapter = new AssignedSubjectChapter(
			Guid.NewGuid(),
			assignedSubjectId,
			chapterNumber,
			name,
			plannedEndDate.AddDays(-7),
			plannedEndDate,
			"Dashboard curriculum test chapter");
		chapter.AssignedSubjectId = assignedSubjectId;
		return chapter;
	}

	private async Task<Guid> SeedEnrolledStudentAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var studentRepository = scope.ServiceProvider.GetRequiredService<IRepository<Student, Guid>>();
		var mediumRepository = scope.ServiceProvider.GetRequiredService<IRepository<EducationMedium, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var medium = await mediumRepository.GetAsync(Factory.SeededMediumId);
			var student = new Student(
				Guid.NewGuid(),
				tenantId,
				"Dashboard",
				null,
				"Enrolled",
				Factory.SeededAcademicYearId,
				medium.EducationBoardId,
				Factory.SeededMediumId,
				Factory.SeededMediumStageId,
				Factory.SeededStandardId,
				Factory.SeededDivisionId,
				status: StudentLifeCycleStatusEnum.Enrolled);

			await studentRepository.InsertAsync(student, autoSave: false);
			await uow.CompleteAsync();
			return student.Id;
		}
	}

	private async Task<Guid> SeedEnrolledStudentWithContactEmailAsync(string email)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var studentRepository = scope.ServiceProvider.GetRequiredService<IRepository<Student, Guid>>();
		var mediumRepository = scope.ServiceProvider.GetRequiredService<IRepository<EducationMedium, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var medium = await mediumRepository.GetAsync(Factory.SeededMediumId);
			var student = Student.CreateDirectRegistration(
				Guid.NewGuid(),
				tenantId,
				"Dashboard",
				null,
				"StudentParent",
				Factory.SeededAcademicYearId,
				medium.EducationBoardId,
				Factory.SeededMediumId,
				Factory.SeededMediumStageId,
				Factory.SeededStandardId,
				Factory.SeededDivisionId,
				new DateTimeOffset(new DateTime(2014, 6, 1), TimeSpan.Zero),
				GenderEnum.Male,
				"9999999999",
				email: email);

			student.MoveToAdmission();
			student.AssignDivision(Factory.SeededDivisionId);
			student.ApproveAdmission();

			await studentRepository.InsertAsync(student, autoSave: false);
			await uow.CompleteAsync();
			return student.Id;
		}
	}

	private async Task SeedStudentPersonalAttendanceAsync(Guid studentId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var attendanceDayRepository = scope.ServiceProvider.GetRequiredService<IAttendanceDayRepository>();
		var attendanceEntryRepository = scope.ServiceProvider.GetRequiredService<IAttendanceEntryRepository>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var userId = Guid.Parse(TestAuthHandler.TestUserId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var presentDay = BuildAttendanceDay(tenantId, new DateTime(2026, 5, 1), userId);
			var absentDay = BuildAttendanceDay(tenantId, new DateTime(2026, 5, 2), userId);
			await attendanceDayRepository.InsertAsync(presentDay, autoSave: false);
			await attendanceDayRepository.InsertAsync(absentDay, autoSave: false);
			await attendanceEntryRepository.InsertAsync(
				BuildAttendanceEntry(tenantId, presentDay.Id, studentId, AttendanceStatus.Present, userId),
				autoSave: false);
			await attendanceEntryRepository.InsertAsync(
				BuildAttendanceEntry(tenantId, absentDay.Id, studentId, AttendanceStatus.Absent, userId),
				autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task SeedStudentOutstandingDueAsync(Guid studentId, double amount)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var dueFeeRepository = scope.ServiceProvider.GetRequiredService<IDueFeeRepository>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var dueFee = new DueFee(
				Guid.NewGuid(),
				tenantId,
				studentId,
				Guid.NewGuid(),
				MonthEnum.May,
				2026,
				false,
				amount,
				new DateTime(2026, 5, 10));
			await dueFeeRepository.InsertAsync(dueFee, autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task SeedStudentRequiredDocumentStatusAsync(Guid studentId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var documentTypeManager = scope.ServiceProvider.GetRequiredService<DocumentTypeManager>();
		var documentTypeRepository = scope.ServiceProvider.GetRequiredService<IRepository<DocumentType, Guid>>();
		var studentDocumentRepository = scope.ServiceProvider.GetRequiredService<IRepository<StudentDocument, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var uploadedType = await documentTypeManager.CreateAsync(
				tenantId,
				TestDataHelper.UniqueName("DashboardStudentDocUploaded"),
				2,
				[AcceptedFileFormat.Pdf],
				isRequired: true,
				displayOrder: 1);
			var missingType = await documentTypeManager.CreateAsync(
				tenantId,
				TestDataHelper.UniqueName("DashboardStudentDocMissing"),
				2,
				[AcceptedFileFormat.Pdf],
				isRequired: true,
				displayOrder: 2);
			await documentTypeRepository.InsertAsync(uploadedType, autoSave: false);
			await documentTypeRepository.InsertAsync(missingType, autoSave: false);
			await studentDocumentRepository.InsertAsync(
				new StudentDocument(
					Guid.NewGuid(),
					tenantId,
					studentId,
					uploadedType.Id,
					$"dashboard/{studentId:N}/{uploadedType.Id:N}.pdf",
					"uploaded.pdf",
					"application/pdf",
					256,
					uploadedByUserId: Guid.Parse(TestAuthHandler.TestUserId)),
				autoSave: false);
			await uow.CompleteAsync();
		}
	}

	private async Task SeedStudentAcademicWorkAsync(Guid studentId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var homeworkRepository = scope.ServiceProvider.GetRequiredService<IRepository<HomeworkAssignmentEntity, Guid>>();
		var assignmentRepository = scope.ServiceProvider.GetRequiredService<IRepository<AssignmentEntity, Guid>>();
		var submissionRepository = scope.ServiceProvider.GetRequiredService<IRepository<StudentSubmissionEntity, Guid>>();
		var homeworkTypeRepository = scope.ServiceProvider.GetRequiredService<IHomeworkTypeRepository>();
		var assignmentTypeRepository = scope.ServiceProvider.GetRequiredService<IAssignmentTypeRepository>();
		var homeworkTypeManager = scope.ServiceProvider.GetRequiredService<HomeworkTypeManager>();
		var assignmentTypeManager = scope.ServiceProvider.GetRequiredService<AssignmentTypeManager>();
		var examTypeRepository = scope.ServiceProvider.GetRequiredService<IRepository<ExamType, Guid>>();
		var examRepository = scope.ServiceProvider.GetRequiredService<IRepository<Exam, Guid>>();
		var resultHeaderRepository = scope.ServiceProvider.GetRequiredService<IRepository<ExamResultHeader, Guid>>();
		var resultDetailRepository = scope.ServiceProvider.GetRequiredService<IRepository<ExamResultDetail, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var userId = Guid.Parse(TestAuthHandler.TestUserId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var homeworkType = (await homeworkTypeRepository.GetListAsync()).FirstOrDefault()
			                   ?? await homeworkTypeManager.CreateAsync(
				                   Factory.SeededMediumId,
				                   TestDataHelper.UniqueName("DashboardStudentHomeworkType"));
			var assignmentType = (await assignmentTypeRepository.GetListAsync()).FirstOrDefault()
			                     ?? await assignmentTypeManager.CreateAsync(
				                     Factory.SeededMediumId,
				                     TestDataHelper.UniqueName("DashboardStudentAssignmentType"));

			var homework = BuildHomework(
				homeworkType.Id,
				tenantId,
				userId,
				Factory.SeededStandardId,
				Factory.SeededDivisionId,
				Factory.SeededMediumStageId);
			await homeworkRepository.InsertAsync(homework, autoSave: false);
			await submissionRepository.InsertAsync(
				BuildHomeworkSubmission(homework.Id, tenantId, Factory.SeededDivisionId, SubmissionStatus.Late, studentId),
				autoSave: false);

			var assignment = BuildAssignment(
				assignmentType.Id,
				tenantId,
				userId,
				Factory.SeededStandardId,
				Factory.SeededDivisionId,
				Factory.SeededMediumStageId);
			await assignmentRepository.InsertAsync(assignment, autoSave: false);
			await submissionRepository.InsertAsync(
				BuildAssignmentSubmission(assignment.Id, tenantId, Factory.SeededDivisionId, SubmissionStatus.NeedsReview, studentId),
				autoSave: false);

			var reviewedHomework = BuildHomework(
				homeworkType.Id,
				tenantId,
				userId,
				Factory.SeededStandardId,
				Factory.SeededDivisionId,
				Factory.SeededMediumStageId);
			await homeworkRepository.InsertAsync(reviewedHomework, autoSave: false);
			var reviewedSubmission = BuildHomeworkSubmission(
				reviewedHomework.Id,
				tenantId,
				Factory.SeededDivisionId,
				SubmissionStatus.Submitted,
				studentId);
			reviewedSubmission.UpdateFeedback(
				9,
				10,
				"Reviewed",
				null,
				SubmissionStatus.Reviewed,
				userId);
			await submissionRepository.InsertAsync(reviewedSubmission, autoSave: false);

			var examType = new ExamType(
				Guid.NewGuid(),
				tenantId,
				TestDataHelper.UniqueName("DashboardStudentResultType"),
				"Dashboard student personal result test",
				Factory.SeededMediumId);
			await examTypeRepository.InsertAsync(examType, autoSave: false);

			var exam = new Exam(
				Guid.NewGuid(),
				examType.Id,
				TestDataHelper.UniqueName("DashboardStudentResultExam"),
				Factory.SeededStandardId,
				tenantId);
			await examRepository.InsertAsync(exam, autoSave: false);

			var personalResultDivisionId = Guid.NewGuid();
			var header = new ExamResultHeader(
				Guid.NewGuid(),
				exam.Id,
				personalResultDivisionId,
				userId,
				tenantId);
			header.Publish(userId);
			await resultHeaderRepository.InsertAsync(header, autoSave: false);
			await resultDetailRepository.InsertAsync(
				BuildResultDetail(header, 88, "A", ExamStudentResultStatus.Pass, 3, studentId, personalResultDivisionId),
				autoSave: false);
			await resultDetailRepository.InsertAsync(
				BuildResultDetail(header, 28, "F", ExamStudentResultStatus.Fail, 12, divisionId: personalResultDivisionId),
				autoSave: false);

			await uow.CompleteAsync();
		}
	}

	private AttendanceDay BuildAttendanceDay(
		Guid tenantId,
		DateTime date,
		Guid markedByUserId,
		Guid? standardId = null,
		Guid? divisionId = null) =>
		(AttendanceDay)(Activator.CreateInstance(
			typeof(AttendanceDay),
			BindingFlags.Instance | BindingFlags.NonPublic,
			binder: null,
			args:
			[
				Guid.NewGuid(),
				(Guid?)tenantId,
				Factory.SeededAcademicYearId,
				standardId ?? Factory.SeededStandardId,
				divisionId ?? Factory.SeededDivisionId,
				date,
				markedByUserId
			],
			culture: CultureInfo.InvariantCulture)
			?? throw new InvalidOperationException("Failed to create attendance day."));

	private static AttendanceEntry BuildAttendanceEntry(
		Guid tenantId,
		Guid attendanceDayId,
		Guid studentId,
		AttendanceStatus status,
		Guid markedByUserId) =>
		(AttendanceEntry)(Activator.CreateInstance(
			typeof(AttendanceEntry),
			BindingFlags.Instance | BindingFlags.NonPublic,
			binder: null,
			args:
			[
				Guid.NewGuid(),
				(Guid?)tenantId,
				attendanceDayId,
				studentId,
				status,
				markedByUserId,
				null
			],
			culture: CultureInfo.InvariantCulture)
			?? throw new InvalidOperationException("Failed to create attendance entry."));

	private async Task<Guid> SeedPendingAdmissionAsync()
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var studentRepository = scope.ServiceProvider.GetRequiredService<IRepository<Student, Guid>>();
		var mediumRepository = scope.ServiceProvider.GetRequiredService<IRepository<EducationMedium, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var medium = await mediumRepository.GetAsync(Factory.SeededMediumId);
			var student = new Student(
				Guid.NewGuid(),
				tenantId,
				"Dashboard",
				null,
				"Admission",
				Factory.SeededAcademicYearId,
				medium.EducationBoardId,
				Factory.SeededMediumId,
				Factory.SeededMediumStageId,
				Factory.SeededStandardId,
				Factory.SeededDivisionId,
				status: StudentLifeCycleStatusEnum.Registration);

			student.MoveToAdmission();
			await studentRepository.InsertAsync(student, autoSave: false);
			await uow.CompleteAsync();
			return student.Id;
		}
	}

	private async Task<EventDto> SeedCalendarEventAsync(string suffix)
	{
		var eventType = await CalendarTestHelper.CreateEventTypeAsync(Client, suffix);
		var date = DateTimeOffset.UtcNow.AddDays(2);
		var title = TestDataHelper.UniqueName(suffix);

		var input = new CreateUpdateEventDto
		{
			AcademicYearId = Factory.SeededAcademicYearId,
			Title = title,
			EventTypeId = eventType.Id,
			Mode = EventMode.Offline,
			Venue = "Dashboard Test Venue",
			StartDate = date,
			EndDate = date,
			StartTime = "09:00",
			EndTime = "10:00",
			IsRecurring = false,
			AudienceIds = new List<Guid>()
		};

		var response = await Client.PostAsJsonAsync(
			$"{CalendarEventsUrl}?academicYearId={Factory.SeededAcademicYearId:D}", input, JsonOptions);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<EventDto>(JsonOptions)
			?? throw new InvalidOperationException("Failed to seed dashboard calendar event.");
	}

	private async Task SeedStudentComplianceAsync(Guid studentId)
	{
		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var complianceRepository = scope.ServiceProvider.GetRequiredService<IRepository<StudentIdentityCompliance, Guid>>();
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);

		using (currentTenant.Change(tenantId))
		using (var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var compliance = new StudentIdentityCompliance(Guid.NewGuid(), tenantId, studentId);
			compliance.SetApaar("APAAR-DASH-1", ConsentStatus.Granted, DateTimeOffset.UtcNow, IdentityVerificationStatus.Verified);
			compliance.SetAdministrativeIdentifiers("PEN-DASH-1", "UDISE-DASH-1", UdiseSyncStatus.Ready);
			await complianceRepository.InsertAsync(compliance, autoSave: false);
			await uow.CompleteAsync();
		}
	}
}
