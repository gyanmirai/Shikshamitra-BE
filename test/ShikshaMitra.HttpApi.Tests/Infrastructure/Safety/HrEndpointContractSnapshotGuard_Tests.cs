using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Infrastructure.Safety;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class HrEndpointContractSnapshotGuard_Tests
{
	private const int ExpectedEndpointCount = 293;
	private const string ExpectedSnapshotHash = "849053b22fd413bac55b14d562f0e7bad51658c361dab77f5cb5d2c312fb87fa";

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Hr_Controller_Endpoint_Contracts_Should_Not_Drift_Accidentally()
	{
		var repoRoot = FindRepositoryRoot();
		var controllersRoot = Path.Combine(repoRoot, "src", "ShikshaMitra.HttpApi", "Controllers", "HR");
		var inventory = BuildEndpointInventory(controllersRoot);
		var snapshot = string.Join('\n', inventory);
		var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))).ToLowerInvariant();

		inventory.Count.ShouldBe(
			ExpectedEndpointCount,
			$"HR endpoint count changed. Current inventory:{Environment.NewLine}{snapshot}");

		hash.ShouldBe(
			ExpectedSnapshotHash,
			$"HR endpoint contract snapshot changed. Review route/DTO compatibility, then update the approved hash if intentional.{Environment.NewLine}{snapshot}");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Policy_Holiday_Impact_Preview_Should_Require_Impact_Permission()
	{
		var repoRoot = FindRepositoryRoot();
		var servicePath = Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.Application",
			"HR",
			"Policies",
			"PolicyHolidayImpactAppService.cs");
		var text = File.ReadAllText(servicePath);

		Regex.IsMatch(
				text,
				@"\[Authorize\(ShikshaMitraSubmodulePermissions\.HR\.Policies\.PreviewImpact\)\]\s*public\s+async\s+Task<PolicyHolidayImpactPreviewDto>\s+GetPreviewAsync",
				RegexOptions.CultureInvariant)
			.ShouldBeTrue("Holiday impact preview exposes policy/payroll impact details and must not fall back to broad authenticated access.");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Hr_Policy_App_Service_Methods_Should_Keep_Specific_Permissions()
	{
		var repoRoot = FindRepositoryRoot();
		var expectations = new[]
		{
			new PolicyPermissionExpectation("WorkCalendarPolicyAppService.cs", "GetListAsync", "View"),
			new PolicyPermissionExpectation("WorkCalendarPolicyAppService.cs", "GetAsync", "View"),
			new PolicyPermissionExpectation("WorkCalendarPolicyAppService.cs", "GetHolidaysAsync", "View"),
			new PolicyPermissionExpectation("WorkCalendarPolicyAppService.cs", "GetHolidayImportPreviewAsync", "View"),
			new PolicyPermissionExpectation("WorkCalendarPolicyAppService.cs", "CreateAsync", "Create"),
			new PolicyPermissionExpectation("WorkCalendarPolicyAppService.cs", "UpdateAsync", "Edit"),
			new PolicyPermissionExpectation("WorkCalendarPolicyAppService.cs", "CloneAsync", "Clone"),
			new PolicyPermissionExpectation("WorkCalendarPolicyAppService.cs", "DeleteAsync", "Archive"),
			new PolicyPermissionExpectation("WorkCalendarPolicyAppService.cs", "UnarchiveAsync", "Archive"),
			new PolicyPermissionExpectation("PayrollPolicyAppService.cs", "GetListAsync", "View"),
			new PolicyPermissionExpectation("PayrollPolicyAppService.cs", "GetAsync", "View"),
			new PolicyPermissionExpectation("PayrollPolicyAppService.cs", "CreateAsync", "Create"),
			new PolicyPermissionExpectation("PayrollPolicyAppService.cs", "UpdateAsync", "Edit"),
			new PolicyPermissionExpectation("PayrollPolicyAppService.cs", "CloneAsync", "Clone"),
			new PolicyPermissionExpectation("PayrollPolicyAppService.cs", "DeleteAsync", "Archive"),
			new PolicyPermissionExpectation("PayrollPolicyAppService.cs", "UnarchiveAsync", "Archive"),
			new PolicyPermissionExpectation("PayrollPolicyAppService.cs", "SetDefaultAsync", "SetDefault"),
			new PolicyPermissionExpectation("AttendancePolicyAppService.cs", "GetListAsync", "View"),
			new PolicyPermissionExpectation("AttendancePolicyAppService.cs", "GetAsync", "View"),
			new PolicyPermissionExpectation("AttendancePolicyAppService.cs", "CreateAsync", "Create"),
			new PolicyPermissionExpectation("AttendancePolicyAppService.cs", "UpdateAsync", "Edit"),
			new PolicyPermissionExpectation("AttendancePolicyAppService.cs", "CloneAsync", "Clone"),
			new PolicyPermissionExpectation("AttendancePolicyAppService.cs", "DeleteAsync", "Archive"),
			new PolicyPermissionExpectation("AttendancePolicyAppService.cs", "UnarchiveAsync", "Archive"),
			new PolicyPermissionExpectation("AttendancePolicyAppService.cs", "SetDefaultAsync", "SetDefault"),
			new PolicyPermissionExpectation("HrPolicyAssignmentAppService.cs", "GetListAsync", "View"),
			new PolicyPermissionExpectation("HrPolicyAssignmentAppService.cs", "GetAsync", "View"),
			new PolicyPermissionExpectation("HrPolicyAssignmentAppService.cs", "CreateAsync", "Assign"),
			new PolicyPermissionExpectation("HrPolicyAssignmentAppService.cs", "UpdateAsync", "Assign"),
			new PolicyPermissionExpectation("HrPolicyAssignmentAppService.cs", "UpsertEmployeeOverrideAsync", "Assign"),
			new PolicyPermissionExpectation("HrPolicyAssignmentAppService.cs", "DisableEmployeeOverrideAsync", "Assign"),
			new PolicyPermissionExpectation("HrPolicyAssignmentAppService.cs", "PreviewImpactAsync", "PreviewImpact"),
			new PolicyPermissionExpectation("HrPolicyAssignmentAppService.cs", "DeleteAsync", "Assign"),
			new PolicyPermissionExpectation("HrPolicyAssignmentAppService.cs", "GetEffectivePoliciesAsync", "View"),
			new PolicyPermissionExpectation("HrPolicyAssignmentAppService.cs", "GetPreviewAsync", "View"),
			new PolicyPermissionExpectation("HrPolicySafetyAppService.cs", "PreviewPolicyImpactAsync", "PreviewImpact"),
			new PolicyPermissionExpectation("HrPolicySafetyAppService.cs", "GetAuditSummaryAsync", "ViewAudit"),
			new PolicyPermissionExpectation("HrPolicyVersionAppService.cs", "GetListAsync", "View"),
			new PolicyPermissionExpectation("HrPolicyVersionAppService.cs", "GetAsync", "View"),
			new PolicyPermissionExpectation("HrPolicyVersionAppService.cs", "CompareAsync", "View"),
			new PolicyPermissionExpectation("HrPolicyVersionAppService.cs", "GetRestorePreviewAsync", "PreviewImpact"),
			new PolicyPermissionExpectation("HrPolicyVersionAppService.cs", "RestoreDraftAsync", "Clone"),
			new PolicyPermissionExpectation("PolicyHolidayImpactAppService.cs", "GetPreviewAsync", "PreviewImpact"),
			new PolicyPermissionExpectation("AttendanceAndLeaves/Settings/LeavePolicyAppService.cs", "GetListAsync", "View", "LeaveManagement_SettingLeavePolicy"),
			new PolicyPermissionExpectation("AttendanceAndLeaves/Settings/LeavePolicyAppService.cs", "GetAsync", "View", "LeaveManagement_SettingLeavePolicy"),
			new PolicyPermissionExpectation("AttendanceAndLeaves/Settings/LeavePolicyAppService.cs", "CreateLeavePolicyAsync", "Create", "LeaveManagement_SettingLeavePolicy"),
			new PolicyPermissionExpectation("AttendanceAndLeaves/Settings/LeavePolicyAppService.cs", "UpdateLeavePolicyAsync", "Edit", "LeaveManagement_SettingLeavePolicy"),
			new PolicyPermissionExpectation("AttendanceAndLeaves/Settings/LeavePolicyAppService.cs", "DeleteLeavePolicyAsync", "Archive", "LeaveManagement_SettingLeavePolicy"),
			new PolicyPermissionExpectation("AttendanceAndLeaves/Settings/LeavePolicyAppService.cs", "SetDefaultAsync", "SetDefault", "LeaveManagement_SettingLeavePolicy")
		};
		var applicationHrRoot = Path.Combine(repoRoot, "src", "ShikshaMitra.Application", "HR");
		var policyRoot = Path.Combine(repoRoot, "src", "ShikshaMitra.Application", "HR", "Policies");
		var violations = expectations
			.Where(expectation =>
			{
				var servicePath = expectation.FileName.Contains('/')
					? Path.Combine(applicationHrRoot, expectation.FileName.Replace('/', Path.DirectorySeparatorChar))
					: Path.Combine(policyRoot, expectation.FileName);
				var text = File.ReadAllText(servicePath);
				return !HasPermissionAttributeForMethod(
					text,
					expectation.MethodName,
					expectation.PermissionRoot,
					expectation.Permission);
			})
			.Select(expectation => $"{expectation.FileName}.{expectation.MethodName} must require HR.{expectation.PermissionRoot}.{expectation.Permission}")
			.ToList();

		violations.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Default_Leave_Policy_Migration_Should_Use_Null_Safe_Tenant_Index()
	{
		var repoRoot = FindRepositoryRoot();
		var migrationPath = Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.EntityFrameworkCore",
			"Migrations",
			"20260611173000_AddDefaultLeavePolicyAssignment.cs");
		var text = File.ReadAllText(migrationPath);

		text.ShouldContain("ADD COLUMN IF NOT EXISTS \"IsDefault\"");
		text.ShouldContain("CREATE UNIQUE INDEX IF NOT EXISTS \"IX_EmployeeLeavePolicies_TenantId_AcademicYearId_Default\"");
		text.ShouldContain("COALESCE(\"TenantId\", '00000000-0000-0000-0000-000000000000'::uuid)");
		text.ShouldContain("WHERE \"IsDefault\" = true");
		text.ShouldContain("[Migration(\"20260611173000_AddDefaultLeavePolicyAssignment\")]");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Attendance_Import_Metadata_Migration_Should_Be_Idempotent()
	{
		var repoRoot = FindRepositoryRoot();
		var migrationPath = Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.EntityFrameworkCore",
			"Migrations",
			"20260611120000_AddAttendanceImportOriginalFileMetadata.cs");
		var text = File.ReadAllText(migrationPath);

		text.ShouldContain("ADD COLUMN IF NOT EXISTS \"OriginalFileBlobName\"");
		text.ShouldContain("ADD COLUMN IF NOT EXISTS \"OriginalFileContentType\"");
		text.ShouldContain("DROP COLUMN IF EXISTS \"OriginalFileBlobName\"");
		text.ShouldContain("DROP COLUMN IF EXISTS \"OriginalFileContentType\"");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Leave_Policy_Backfill_Migration_Should_Be_Discoverable_And_Idempotent()
	{
		var repoRoot = FindRepositoryRoot();
		var migrationPath = Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.EntityFrameworkCore",
			"Migrations",
			"20260612120500_BackfillAcademicYearLeavePolicies.cs");
		var text = File.ReadAllText(migrationPath);

		text.ShouldContain("[Migration(\"20260612120500_BackfillAcademicYearLeavePolicies\")]");
		text.ShouldContain("NOT EXISTS");
		text.ShouldContain("\"AppAcademicYears\"");
		text.ShouldContain("Teaching Staff Leave Policy");
		text.ShouldContain("\"IsDefault\"");
		text.ShouldContain("md5('leave-policy:");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Leave_Policy_Seeder_Should_Not_Seed_HR_Leave_Types()
	{
		var repoRoot = FindRepositoryRoot();
		var seederPath = Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.Domain",
			"Seeders",
			"HRSeederContributor.PolicyCatalog.cs");
		var text = File.ReadAllText(seederPath);

		text.ShouldContain("SeedLeavePoliciesAsync()");
		text.ShouldContain("_leavePolicyRepository.AnyAsync(x => x.AcademicYearId == academicYearId)");
		text.ShouldContain("LeavePaidType.Paid");
		text.ShouldContain("LeavePaidType.Unpaid");
		text.ShouldNotContain("LeaveType");
		text.ShouldNotContain("_leaveTypeRepository");
		text.ShouldNotContain("if (await _leavePolicyRepository.AnyAsync())");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Hand_Written_Ef_Migrations_Should_Be_Discoverable()
	{
		var repoRoot = FindRepositoryRoot();
		var migrationsRoot = Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.EntityFrameworkCore",
			"Migrations");
		var migrationFiles = new[]
		{
			"20260611120000_AddAttendanceImportOriginalFileMetadata.cs",
			"20260611173000_AddDefaultLeavePolicyAssignment.cs",
			"20260612120500_BackfillAcademicYearLeavePolicies.cs"
		};

		foreach (var migrationFile in migrationFiles)
		{
			var migrationId = Path.GetFileNameWithoutExtension(migrationFile);
			var text = File.ReadAllText(Path.Combine(migrationsRoot, migrationFile));
			text.ShouldContain("[DbContext(typeof(ShikshaMitraDbContext))]");
			text.ShouldContain($"[Migration(\"{migrationId}\")]");
		}
	}

	private static List<string> BuildEndpointInventory(string controllersRoot)
	{
		var files = Directory
			.EnumerateFiles(controllersRoot, "*.cs", SearchOption.AllDirectories)
			.OrderBy(x => x, StringComparer.Ordinal)
			.ToList();
		var classRoutes = GetClassRoutes(files);
		var endpoints = new List<string>();

		foreach (var file in files)
		{
			var text = File.ReadAllText(file);
			var classMatch = Regex.Match(
				text,
				@"class\s+(?<class>[A-Za-z_][A-Za-z0-9_]*)",
				RegexOptions.CultureInvariant);

			if (!classMatch.Success)
			{
				continue;
			}

			var className = classMatch.Groups["class"].Value;
			if (!classRoutes.TryGetValue(className, out var baseRoute))
			{
				continue;
			}

			foreach (Match action in ActionStartPattern().Matches(text))
			{
				var parameters = ReadBalancedParameters(text, action.Index + action.Length);
				foreach (Match httpAttribute in HttpAttributePattern().Matches(action.Groups["attrs"].Value))
				{
					var route = CombineRoute(baseRoute, httpAttribute.Groups["route"].Value);
					var returnType = NormalizeSignaturePart(action.Groups["return"].Value);
					endpoints.Add(
						$"{httpAttribute.Groups["verb"].Value.ToUpperInvariant()} /{route} => {className}.{action.Groups["method"].Value} : {returnType} ({parameters})");
				}
			}
		}

		return endpoints
			.OrderBy(x => x, StringComparer.Ordinal)
			.ToList();
	}

	private static Dictionary<string, string> GetClassRoutes(IEnumerable<string> files)
	{
		var routes = new Dictionary<string, string>(StringComparer.Ordinal);

		foreach (var file in files)
		{
			var text = File.ReadAllText(file);
			var match = ClassRoutePattern().Match(text);
			if (match.Success)
			{
				routes[match.Groups["class"].Value] = match.Groups["route"].Value;
			}
		}

		return routes;
	}

	private static string CombineRoute(string baseRoute, string actionRoute) =>
		$"{baseRoute.TrimEnd('/')}/{actionRoute.TrimStart('/')}".TrimEnd('/');

	private static string NormalizeSignaturePart(string value) =>
		Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant).Trim();

	private static string ReadBalancedParameters(string text, int parameterStart)
	{
		var depth = 1;
		for (var index = parameterStart; index < text.Length; index++)
		{
			depth += text[index] switch
			{
				'(' => 1,
				')' => -1,
				_ => 0
			};

			if (depth == 0)
			{
				return NormalizeSignaturePart(text[parameterStart..index]);
			}
		}

		throw new InvalidOperationException("Could not read a balanced controller action parameter list.");
	}

	private static bool HasPermissionAttributeForMethod(
		string text,
		string methodName,
		string permissionRoot,
		string permission)
	{
		var methodMatch = Regex.Match(
			text,
			$@"^\s*public\s+(?:(?:override|virtual|async)\s+)*Task[^\r\n]*\s+{methodName}\s*\(",
			RegexOptions.CultureInvariant | RegexOptions.Multiline);
		if (!methodMatch.Success)
		{
			return false;
		}

		var precedingText = text[..methodMatch.Index];
		var previousPublicMethodIndex = precedingText.LastIndexOf("public ", StringComparison.Ordinal);
		var previousClassAuthorizeIndex = precedingText.LastIndexOf("[Authorize]", StringComparison.Ordinal);
		var searchStart = Math.Max(previousPublicMethodIndex, previousClassAuthorizeIndex);
		var methodPreamble = precedingText[searchStart..];
		return methodPreamble.Contains(
			$"[Authorize(ShikshaMitraSubmodulePermissions.HR.{permissionRoot}.{permission})]",
			StringComparison.Ordinal);
	}

	private static Regex ClassRoutePattern() =>
		new(
			@"\[Route\(""(?<route>[^""]+)""\)\][\s\S]*?class\s+(?<class>[A-Za-z_][A-Za-z0-9_]*)",
			RegexOptions.CultureInvariant);

	private static Regex ActionStartPattern() =>
		new(
			@"(?s)(?<attrs>(?:\s*\[[^\]]+\]\s*)+)\s*public\s+(?:virtual\s+)?(?:async\s+)?(?<return>[^{;=]+?)\s+(?<method>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
			RegexOptions.CultureInvariant);

	private static Regex HttpAttributePattern() =>
		new(
			@"\[Http(?<verb>Get|Post|Put|Delete|Patch)(?:\(""(?<route>[^""]*)""\))?",
			RegexOptions.CultureInvariant);

	private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
	{
		var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
		if (File.Exists(Path.Combine(currentDirectory.FullName, "ShikshaMitra.sln")))
		{
			return currentDirectory.FullName;
		}

		var backendRootFromCurrentDirectory = Path.Combine(currentDirectory.FullName, "Shikshamitra-BE");
		if (File.Exists(Path.Combine(backendRootFromCurrentDirectory, "ShikshaMitra.sln")))
		{
			return backendRootFromCurrentDirectory;
		}

		var sourceDirectory = new FileInfo(sourceFilePath).Directory;
		while (sourceDirectory is not null)
		{
			if (File.Exists(Path.Combine(sourceDirectory.FullName, "ShikshaMitra.sln")))
			{
				return sourceDirectory.FullName;
			}

			var backendRootFromSourceDirectory = Path.Combine(sourceDirectory.FullName, "Shikshamitra-BE");
			if (File.Exists(Path.Combine(backendRootFromSourceDirectory, "ShikshaMitra.sln")))
			{
				return backendRootFromSourceDirectory;
			}

			sourceDirectory = sourceDirectory.Parent;
		}

		var current = new DirectoryInfo(AppContext.BaseDirectory);
		while (current is not null)
		{
			if (File.Exists(Path.Combine(current.FullName, "ShikshaMitra.sln")))
			{
				return current.FullName;
			}

			var backendRoot = Path.Combine(current.FullName, "Shikshamitra-BE");
			if (File.Exists(Path.Combine(backendRoot, "ShikshaMitra.sln")))
			{
				return backendRoot;
			}

			current = current.Parent;
		}

		throw new InvalidOperationException("Could not find repository root from test execution directory.");
	}

	private sealed record PolicyPermissionExpectation(
		string FileName,
		string MethodName,
		string Permission,
		string PermissionRoot = "Policies");
}
