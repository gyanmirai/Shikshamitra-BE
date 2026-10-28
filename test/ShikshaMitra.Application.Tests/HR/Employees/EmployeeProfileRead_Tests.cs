using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Common;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.HR.Employees.PersonalInfo;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Shouldly;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.HR.Employees;

public class EmployeeProfileRead_Tests(ITestOutputHelper output)
	: ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Profile_preserves_the_previous_wire_contract_including_nullable_fields_and_organizations(bool populated)
	{
		var tenantId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(Principal(userId, tenantId));
		var id = await FixtureAsync(tenantId, userId, populated);
		await WithUnitOfWorkAsync(async () =>
		{
			var previous = await GetRequiredService<IEmployeePersonalInfoAppService>().GetWithDetailsAsync(id);
			var actual = await GetRequiredService<IEmployeeProfileAppService>().GetAsync(id);
			JsonSerializer.Serialize(actual).ShouldBe(JsonSerializer.Serialize(PreviousContract(previous)));
			actual.FullName.ShouldBe(populated ? "  Profile  Employee" : string.Empty);
			actual.MiddleName.ShouldBe(populated ? "Middle" : null);
			actual.Shift?.StartTime.ShouldBe(populated ? "07:08:09" : null);
			actual.Shift?.EndTime.ShouldBe(populated ? "16:17:18" : null);
			if (populated)
			{
				actual.OrganizationNames.ShouldBe(["Profile stage"]);
				actual.RoleName.ShouldStartWith("ProfileRole");
				actual.Address.ShouldBe("  Address one  , Address three");
			}
			else
			{
				actual.OrganizationNames.ShouldBeEmpty();
				actual.Address.ShouldBe(string.Empty);
				actual.Status.ShouldBe((int)EmployeeStatusType.Active);
			}
		});
	}

	[Fact]
	public async Task Post_authorization_profile_read_removes_family_query_and_second_graph_materialization()
	{
		var tenantId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		// Isolate the post-authorization cost: the unchanged authorization service itself
		// still loads an employee graph and is deliberately excluded from this budget.
		var id = await FixtureAsync(tenantId, null, true);
		await WithUnitOfWorkAsync(async () =>
		{
			var repository = GetRequiredService<IEmployeeRepository>();
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			await repository.GetWithDetailsAsync(id); // Warm the existing employee cache equally.
			db.ChangeTracker.Clear();
			string[] beforeSql;
			using (var before = new AcademicQueryCounter(db.Database.GetDbConnection()))
			{
				await GetRequiredService<IEmployeePersonalInfoAppService>().GetWithDetailsAsync(id);
				beforeSql = before.ReadCommands.ToArray();
			}
			db.ChangeTracker.Entries<Employee>().Count().ShouldBe(1);
			foreach (var sql in beforeSql) output.WriteLine("BEFORE SQL: " + sql);
			beforeSql.Count(sql => sql.Contains("\"AppEmployees\"", StringComparison.Ordinal) ||
				sql.Contains("EmployeeFamilyMembers", StringComparison.Ordinal)).ShouldBe(2);
			db.ChangeTracker.Clear();
			var authorization = Substitute.For<IEmployeeAuthorizationService>();
			var reader = new EmployeeProfileAppService(authorization, repository,
				GetRequiredService<EmployeeOrganizationScopeResolver>())
			{
				LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>()
			};
			using var after = new AcademicQueryCounter(db.Database.GetDbConnection());
			var result = await reader.GetAsync(id);
			result.Id.ShouldBe(id);
			await authorization.Received(1).CheckEmployeeAccessAsync(id);
			beforeSql.ShouldContain(sql => sql.Contains("EmployeeFamilyMembers", StringComparison.Ordinal));
			var afterSql = after.ReadCommands.Single(sql => sql.Contains("\"AppEmployees\"", StringComparison.Ordinal));
			after.ReadCommands.ShouldNotContain(sql => sql.Contains("EmployeeFamilyMembers", StringComparison.Ordinal));
			AssertNarrow(afterSql);
			db.ChangeTracker.Entries<Employee>().ShouldBeEmpty();
			db.ChangeTracker.Entries<EmployeeFamilyMember>().ShouldBeEmpty();
			output.WriteLine($"Synthetic SQLite, warm employee cache, post-authorization only: employee/family reads 2->1; service total reads {beforeSql.Length}->{after.ReadCount}, including framework reads; employee tracked graphs 1->0. Authorization cost is unchanged and excluded.");
			output.WriteLine("AFTER SQL: " + afterSql);
			foreach (var sql in after.ReadCommands.Where(sql => sql != afterSql)) output.WriteLine("OTHER SQL: " + sql);
		});
	}

	[Fact]
	public async Task Direct_profile_read_denial_precedes_all_profile_queries()
	{
		var id = Guid.NewGuid();
		var authorization = Substitute.For<IEmployeeAuthorizationService>();
		authorization.CheckEmployeeAccessAsync(id).Returns(Task.FromException(new AbpAuthorizationException("Denied")));
		var repository = Substitute.For<IEmployeeRepository>();
		var reader = new EmployeeProfileAppService(authorization, repository, null!);
		await Should.ThrowAsync<AbpAuthorizationException>(() => reader.GetAsync(id));
		repository.ReceivedCalls().ShouldBeEmpty();
	}

	[Fact]
	public async Task Profile_preserves_missing_cross_tenant_and_out_of_scope_denials()
	{
		var tenantId = Guid.NewGuid();
		var userId = Guid.NewGuid();
		using var tenant = GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var principal = GetRequiredService<ICurrentPrincipalAccessor>().Change(Principal(userId, tenantId));
		var id = await FixtureAsync(tenantId, userId, true);
		var service = GetRequiredService<IEmployeeProfileAppService>();
		await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(Guid.NewGuid()));
		using (GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid()))
			await Should.ThrowAsync<EntityNotFoundException>(() => service.GetAsync(id));
		using (GetRequiredService<ICurrentPrincipalAccessor>().Change(Principal(Guid.NewGuid(), tenantId)))
			await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetAsync(id));
	}

	[Fact]
	public void PostgreSql_profile_translation_excludes_unrelated_employee_graphs()
	{
		using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
			.UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only;Username=translation_only;Timeout=1").Options)
		{
			LazyServiceProvider = GetRequiredService<IAbpLazyServiceProvider>()
		};
		var source = db.Set<Employee>().Where(x => x.Id == Guid.Empty);
		var before = source.ToQueryString();
		var after = source.Select(EmployeeProfileProjection.Details).ToQueryString();
		before.ShouldContain("EmployeeDocuments");
		AssertNarrow(after);
		after.ShouldContain("Roles");
		after.ShouldContain("ShiftName");
		after.ShouldContain("StartTime");
		after.ShouldContain("EndTime");
		after.ShouldContain("TenantId");
		after.ShouldContain("IsDeleted");
		output.WriteLine($"Npgsql SQL compilation only; profile projection SQL chars {before.Length}->{after.Length}. No PostgreSQL connection opened.");
		output.WriteLine(after);
	}

	private async Task<Guid> FixtureAsync(Guid tenantId, Guid? userId, bool populated)
	{
		return await WithUnitOfWorkAsync(async () =>
		{
			if (userId.HasValue)
			{
				var login = "profile-" + userId.Value.ToString("N");
				await GetRequiredService<IIdentityUserRepository>().InsertAsync(
					new IdentityUser(userId.Value, login, login + "@example.test", tenantId), true);
			}
			var role = new IdentityRole(Guid.NewGuid(), "ProfileRole" + Guid.NewGuid().ToString("N"), tenantId);
			await GetRequiredService<IIdentityRoleRepository>().InsertAsync(role, true);
			Guid? shiftId = null;
			if (populated)
			{
				var shift = new ShiftManagement(Guid.NewGuid(), tenantId, "Profile shift", new(7, 8, 9), new(16, 17, 18));
				await GetRequiredService<IRepository<ShiftManagement, Guid>>().InsertAsync(shift, true);
				shiftId = shift.Id;
			}
			var employee = new Employee(Guid.NewGuid(), tenantId, userId, "PROFILE-" + Guid.NewGuid().ToString("N"),
				new EmployeeBasicInfo(populated ? "profile-picture" : null, populated ? "Employee" : "\u2003", populated ? "  Profile " : null,
					populated ? "Middle" : null, null, populated ? EmployeeStatusType.Active : null,
					populated ? new DateOnly(1990, 2, 3) : null, null, null, populated ? GenderType.Female : null,
					null, null, populated ? "O+" : null, populated ? "1234567890" : null, null, populated ? "profile@example.test" : null,
					UniquePan(), UniqueAdhar(), populated ? "  Address one  " : null, "\u2003", populated ? "Address three" : null,
					populated ? "Department" : null, populated ? "Designation" : null, populated ? new DateOnly(2020, 4, 5) : null,
					populated ? EmployeeGroupType.Teaching : null, null, role.Id, null, null, null, null, null, shiftId),
				new BankDetails("Unrelated bank", null, null, "12345", null),
				[new WorkExperience("Unrelated organization", null, null, null, null, null, new string('x', 4096), null)]);
			employee.AddDocument(new EmployeeDocument(Guid.NewGuid(), tenantId, employee.Id, "Unrelated document", "application/pdf", 1, "unused-blob", DateTime.UtcNow));
			await GetRequiredService<IEmployeeRepository>().InsertAsync(employee, true);
			await GetRequiredService<IEmployeeFamilyMemberRepository>().InsertAsync(new EmployeeFamilyMember(Guid.NewGuid(), tenantId,
				employee.Id, FamilyRelationType.Father, "Unrelated family", null, null, null, null, null, null, null, null, null, null, null), true);
			if (populated && userId.HasValue) await AddOrganizationAsync(tenantId, userId.Value);
			return employee.Id;
		});
	}

	private async Task AddOrganizationAsync(Guid tenantId, Guid userId)
	{
		var year = new AcademicYear(Guid.NewGuid(), tenantId, "2026-27", new(2026, 4, 1), new(2027, 3, 31));
		await GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
		var board = new EducationBoard(Guid.NewGuid(), tenantId, Enum.GetValues<EducationBoardType>()[0], year.Id);
		await GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
		var medium = new EducationMedium(Guid.NewGuid(), tenantId, "English", board.Id);
		await GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
		var stage = new EducationMediumStage(Guid.NewGuid(), tenantId, "Profile stage", null, medium.Id);
		await GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
		await GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId,
			userId: userId, academicYearId: year.Id, educationBoardId: board.Id, educationMediumId: medium.Id, educationMediumStageId: stage.Id), true);
	}

	private static ClaimsPrincipal Principal(Guid userId, Guid tenantId) => new(new ClaimsIdentity(
		[new Claim(AbpClaimTypes.UserId, userId.ToString()), new Claim(AbpClaimTypes.TenantId, tenantId.ToString())], "Test"));

	private static void AssertNarrow(string sql)
	{
		foreach (var column in new[] { "EmployeeDocuments", "EmployeeFamilyMembers", "WorkExperiences", "BankDetails", "PanNo", "AdharNo", "ExtraProperties", "ConcurrencyStamp" })
			sql.ShouldNotContain(column);
	}

	// Frozen controller mapping used before this change; compare the entire serialized
	// contract so scalar/null/display/role/shift/organization parity is not inferred.
	private static EmployeeDetails PreviousContract(EmployeeDto employee) => new()
	{
		Id = employee.Id, UserId = employee.UserId, RoleName = employee.Role?.Name,
		FullName = employee.EmployeeName, FirstName = employee.FirstName, MiddleName = employee.MiddleName, LastName = employee.LastName,
		ThumbnailPicture = employee.ProfilePictureBlobName, EmployeeIdentifier = employee.EmployeeCode,
		Dob = employee.DateOfBirth?.ToDateTime(TimeOnly.MinValue), Gender = employee.GenderType?.ToString(), Doj = employee.DateOfJoining?.ToDateTime(TimeOnly.MinValue),
		Email = employee.Email, MobileNo = employee.MobileNo, Department = employee.Department, Designation = employee.Designation, BloodGroup = employee.BloodGroup,
		Address = string.Join(", ", new[] { employee.Address1, employee.Address2, employee.Address3 }.Where(x => !string.IsNullOrWhiteSpace(x))),
		IsTeaching = employee.EmployeeGroup == EmployeeGroupType.Teaching,
		Shift = employee.Shift == null ? null : new ShiftInfo { Id = employee.Shift.Id, Name = employee.Shift.ShiftName ?? string.Empty, StartTime = employee.Shift.StartTime.ToString("HH:mm:ss"), EndTime = employee.Shift.EndTime.ToString("HH:mm:ss") },
		Organizations = employee.Organizations, OrganizationNames = employee.OrganizationNames,
		Status = (int)(employee.EmployeeStatusType ?? EmployeeStatusType.Active)
	};
}
