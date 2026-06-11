using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Security.UserScope;

public class UserScopeApplicationGuardrail_Tests
{
	private static readonly Regex RawScopeMemberAccess = new(
		@"(scope|userScope)\.(AcademicYearIds|BoardIds|MediumIds|MediumStageIds|StandardIds|DivisionIds|WildcardBoardAcademicYearIds|WildcardMediumBoardIds|WildcardMediumStageMediumIds|WildcardStandardMediumStageIds|WildcardDivisionStandardIds|HasWildcard\w+|CanAccessMediumStage|HasTenantWideScope|HasAnyAccess|IsAdmin)",
		RegexOptions.Compiled);

	private static readonly HashSet<string> AllowedFiles = new(StringComparer.OrdinalIgnoreCase)
	{
		@"Security\UserScope\UserScopeAccessService.cs",
		@"TenantSettings\AcademicSetups\AcademicOrganizationScopeAccessService.cs",
		@"TenantSettings\AcademicSetups\EducationBoards\EducationBoardScopeFilter.cs",
		@"Finance\FinanceScopeAccessService.cs",
		@"HR\HrUserScopeAccess.cs",
		@"HR\HrUserScopeAccessService.cs",
		@"HR\Employees\HrEmployeeScopeQueryService.cs",
		@"HR\Dashboard\HRDashboardScopeService.cs",
		@"HR\AttendanceAndLeaves\TakeAttendance\TakeAttendanceAccessService.cs",
		@"HR\AttendanceAndLeaves\TakeAttendance\TakeAttendanceDayReader.cs",
		@"Students\StudentScopeAccessService.cs"
	};

	[Fact]
	public void Application_Code_Should_Not_Inspect_UserScopeContext_Collections_Outside_Gateways()
	{
		var applicationRoot = Path.GetFullPath(Path.Combine(
			AppContext.BaseDirectory,
			"..",
			"..",
			"..",
			"..",
			"..",
			"src",
			"ShikshaMitra.Application"));

		var violations = Directory
			.GetFiles(applicationRoot, "*.cs", SearchOption.AllDirectories)
			.Select(file => new
			{
				File = file,
				RelativePath = Path.GetRelativePath(applicationRoot, file)
			})
			.Where(x => !AllowedFiles.Contains(x.RelativePath))
			.SelectMany(x => File.ReadLines(x.File)
				.Select((line, index) => new { x.RelativePath, LineNumber = index + 1, Line = line })
				.Where(row => RawScopeMemberAccess.IsMatch(row.Line)))
			.Select(row => $"{row.RelativePath}:{row.LineNumber}: {row.Line.Trim()}")
			.ToList();

		violations.ShouldBeEmpty();
	}

	[Fact]
	public void Demo_Scope_Seeders_Should_Not_Reintroduce_Role_Derived_Or_Destructive_User_Scope_Seeding()
	{
		var seedersRoot = Path.GetFullPath(Path.Combine(
			AppContext.BaseDirectory,
			"..",
			"..",
			"..",
			"..",
			"..",
			"src",
			"ShikshaMitra.Domain",
			"Seeders"));

		var staffScopeSeeder = File.ReadAllText(Path.Combine(seedersRoot, "StaffUserScopeSeederContributor.cs"));
		staffScopeSeeder.ShouldNotContain("new UserScopeAssignment");
		staffScopeSeeder.ShouldNotContain("DeleteAsync");
		staffScopeSeeder.ShouldNotContain("DeleteManyAsync");

		var academicScopeSyncer = File.ReadAllText(Path.Combine(seedersRoot, "AcademicPermissionUserScopeSyncer.cs"));
		academicScopeSyncer.ShouldNotContain("DeleteAsync");
		academicScopeSyncer.ShouldNotContain("DeleteManyAsync");
		academicScopeSyncer.ShouldContain("Derived from Academic Permissions");

		var testLoginSeeder = File.ReadAllText(Path.Combine(seedersRoot, "TestLoginSeederContributor.cs"));
		testLoginSeeder.ShouldNotContain("student-demo");
		testLoginSeeder.ShouldNotContain("\"Student\"");
	}
}
