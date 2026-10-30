using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Reports;
using ShikshaMitra.Common;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Xunit;
using ClassTeacherEntity = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;

namespace ShikshaMitra.Academic.Mobile;

public partial class MobileAcademicAppService_Tests
{
	[Theory]
	[InlineData(false, false)]
	[InlineData(false, true)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	public Task Dashboard_class_teacher_uses_active_owned_placement_and_web_names(bool parent, bool yearless) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, parent); using var identity = SignIn(f.User.Id);
		var employee = await TeacherEmployeeAsync();
		await Insert(TeacherAssignment(f, employee.Id, yearless ? null : f.Year.Id));
		// Identical mappings must not create an apparent conflict.
		await Insert(TeacherAssignment(f, employee.Id, yearless ? null : f.Year.Id));
		var result = await GetRequiredService<IMobileDashboardAppService>().GetAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id });
		result.ClassTeacher.ShouldNotBeNull();
		result.ClassTeacher.TeacherId.ShouldBe(employee.Id);
		result.ClassTeacher.TeacherName.ShouldBe(employee.EmployeeName);
		using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
		var teacher = json.RootElement.GetProperty("classTeacher");
		teacher.EnumerateObject().Select(x => x.Name).OrderBy(x => x).ShouldBe(new[] { "teacherId", "teacherName" });
		json.RootElement.GetProperty("studentId").GetGuid().ShouldBe(f.Student.Id);
		json.RootElement.GetProperty("pendingHomework").GetInt32().ShouldBe(
			(await GetRequiredService<IMobileHomeworkAppService>().GetCountsAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id })).PendingHomework);
	});

	[Theory]
	[InlineData("missing")]
	[InlineData("draft")]
	[InlineData("deleted")]
	[InlineData("employee-deleted")]
	[InlineData("inactive")]
	[InlineData("non-teaching")]
	[InlineData("unknown-employee")]
	[InlineData("assignment-tenant")]
	[InlineData("employee-tenant")]
	[InlineData("year")]
	[InlineData("standard")]
	[InlineData("division")]
	[InlineData("medium")]
	[InlineData("conflict")]
	public Task Dashboard_class_teacher_omits_ineligible_or_ambiguous_staff(string scenario) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		Employee employee;
		using (GetRequiredService<ICurrentTenant>().Change(scenario == "employee-tenant" ? Guid.NewGuid() : tenantId))
			employee = await TeacherEmployeeAsync(scenario == "inactive" ? EmployeeStatusType.Inactive : EmployeeStatusType.Active,
				scenario == "non-teaching" ? EmployeeGroupType.NonTeaching : EmployeeGroupType.Teaching);
		var assignment = new ClassTeacherEntity(Guid.NewGuid(), scenario == "assignment-tenant" ? Guid.NewGuid() : tenantId,
			scenario == "standard" ? Guid.NewGuid() : f.Enrollment.StandardId!.Value,
			scenario == "division" ? Guid.NewGuid() : f.Enrollment.DivisionId!.Value,
			scenario == "unknown-employee" ? Guid.NewGuid() : employee.Id,
			scenario == "medium" ? Guid.NewGuid() : f.Student.AcademicMediumId,
			scenario == "year" ? Guid.NewGuid() : f.Year.Id,
			status: scenario == "draft" ? ClassTeacherAssignmentStatus.Draft : ClassTeacherAssignmentStatus.Active);
		if (scenario != "missing") await Insert(assignment);
		if (scenario == "deleted") await GetRequiredService<IRepository<ClassTeacherEntity, Guid>>().DeleteAsync(assignment, true);
		if (scenario == "employee-deleted") await GetRequiredService<IRepository<Employee, Guid>>().DeleteAsync(employee, true);
		if (scenario == "conflict") await Insert(TeacherAssignment(f, (await TeacherEmployeeAsync()).Id, null));
		(await GetRequiredService<IMobileDashboardAppService>().GetAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id }))
			.ClassTeacher.ShouldBeNull();
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Dashboard_class_teacher_reuses_family_access_and_year_checks(bool parent) => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, parent); using var identity = SignIn(f.User.Id);
		await Insert(TeacherAssignment(f, (await TeacherEmployeeAsync()).Id, null));
		var service = GetRequiredService<IMobileDashboardAppService>();
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(new() { StudentId = Guid.NewGuid(), AcademicYearId = f.Year.Id }));
		await Should.ThrowAsync<BusinessException>(() => service.GetAsync(new() { StudentId = f.Student.Id, AcademicYearId = Guid.NewGuid() }));
		using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id }));
		await GetRequiredService<IRepository<ShikshaMitra.Students.Portal.StudentPortalAccountLink, Guid>>().DeleteAsync(f.Link, true);
		await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id }));
	});

	[Fact]
	public Task Dashboard_class_teacher_reader_projects_names_without_loading_hr_profile() => WithUnitOfWorkAsync(async () =>
	{
		var tenantId = Guid.NewGuid(); using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		var f = await Seed(tenantId, false); using var identity = SignIn(f.User.Id);
		await Insert(TeacherAssignment(f, (await TeacherEmployeeAsync()).Id, null));
		var placement = await GetRequiredService<MobileAcademicContextAppService>().ResolveAsync(new() { StudentId = f.Student.Id, AcademicYearId = f.Year.Id });
		var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		db.ChangeTracker.Clear();
		using var capture = new AcademicReportPerformance_Tests.ReportQueryCapture(db.Database.GetDbConnection());
		(await GetRequiredService<MobileDashboardClassTeacherReader>().ReadAsync(placement)).ShouldNotBeNull();
		db.ChangeTracker.Entries<Employee>().ShouldBeEmpty();
		// Repository scope resolution can issue its own authorization reads; the
		// actual teacher projection remains one narrow SQL statement.
		capture.Reads.Count(x => x.Sql.Contains("ClassTeachers", StringComparison.Ordinal) && x.Sql.Contains("\"FirstName\"", StringComparison.Ordinal)).ShouldBe(1);
		capture.Reads.ShouldNotContain(x => x.Sql.Contains("\"BankDetails", StringComparison.Ordinal) || x.Sql.Contains("\"MobileNo\"", StringComparison.Ordinal));
	});

	private ClassTeacherEntity TeacherAssignment(Fixture f, Guid teacherId, Guid? yearId) => new(Guid.NewGuid(), f.Student.TenantId,
		f.Enrollment.StandardId!.Value, f.Enrollment.DivisionId!.Value, teacherId, f.Student.AcademicMediumId, yearId);

	private async Task<Employee> TeacherEmployeeAsync(EmployeeStatusType status = EmployeeStatusType.Active, EmployeeGroupType group = EmployeeGroupType.Teaching)
	{
		var suffix = Guid.NewGuid().ToString("N");
		var role = await Insert(new IdentityRole(Guid.NewGuid(), "teacher-" + suffix, GetRequiredService<ICurrentTenant>().Id));
		return await Insert(new Employee(Guid.NewGuid(), GetRequiredService<ICurrentTenant>().Id, null, "TE" + suffix,
			new EmployeeBasicInfo(ProfilePictureBlobName: null, LastName: "Teacher", FirstName: "Meera", MiddleName: "Private",
				ShortName: null, EmployeeStatusType: status, DateOfBirth: null, FamilyCast: null, CastType: null, GenderType: null,
				Religion: null, MaritalStatus: null, BloodGroup: null, MobileNo: "private-phone", SecondaryMobileNo: null,
				Email: suffix + "@example.test", PanNo: suffix[..10].ToUpperInvariant(), AdharNo: suffix[10..22], Address1: null, Address2: null,
				Address3: null, Department: null, Designation: null, DateOfJoining: null, EmployeeGroup: group, Qualification: null,
				RoleId: role.Id, IdentificationMark1: null, City: null, State: null, Country: null, PinCode: null, ShiftId: null),
			null, null));
	}
}
