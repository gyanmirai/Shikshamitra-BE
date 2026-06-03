using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Infrastructure.Safety;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class HrMaintainabilityGuard_Tests
{
	private const int HrSourceLineLimit = 200;

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Hr_Source_Files_Should_Remain_Small_And_Readable()
	{
		var repoRoot = FindRepositoryRoot();

		var oversized = GetMainHrSourceFiles(repoRoot)
			.Select(file => new
			{
				Path = Path.GetRelativePath(repoRoot, file),
				Lines = File.ReadLines(file).Count()
			})
			.Where(x => x.Lines > HrSourceLineLimit)
			.Select(x => $"{x.Path} has {x.Lines} lines")
			.ToList();

		oversized.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Hr_Domain_Should_Not_Depend_On_Application_Or_EfCore()
	{
		var repoRoot = FindRepositoryRoot();
		var forbiddenPatterns = new Dictionary<string, Regex>
		{
			["Application reference"] = new(@"\bShikshaMitra\.Application\b", RegexOptions.CultureInvariant),
			["EntityFrameworkCore reference"] = new(@"\bShikshaMitra\.EntityFrameworkCore\b", RegexOptions.CultureInvariant)
		};
		var files = Directory
			.EnumerateFiles(Path.Combine(repoRoot, "src", "ShikshaMitra.Domain", "HR"), "*.cs", SearchOption.AllDirectories)
			.Append(Path.Combine(repoRoot, "src", "ShikshaMitra.Domain", "ShikshaMitra.Domain.csproj"));
		var violations = new List<string>();

		foreach (var file in files)
		{
			var text = File.ReadAllText(file);
			foreach (var (name, pattern) in forbiddenPatterns)
			{
				if (pattern.IsMatch(text))
				{
					violations.Add($"{Path.GetRelativePath(repoRoot, file)} contains {name}");
				}
			}
		}

		violations.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Named_Hr_App_Service_Facades_Should_Not_Own_Repository_Plumbing()
	{
		var repoRoot = FindRepositoryRoot();
		var facadeFiles = new[]
		{
			"src/ShikshaMitra.Application/HR/Payroll/RunPayroll/RunPayrollAppService.cs",
			"src/ShikshaMitra.Application/HR/AttendanceAndLeaves/LeaveManagementAppService.cs",
			"src/ShikshaMitra.Application/HR/Dashboard/HRDashboardAppService.cs",
			"src/ShikshaMitra.Application/HR/ProfessionalManagement/ProfessionalManagementAppService.cs"
		};
		var violations = new List<string>();

		foreach (var relativePath in facadeFiles)
		{
			var file = Path.Combine(repoRoot, relativePath);
			var text = File.ReadAllText(file);
			foreach (Match match in RepositoryInjectionPattern().Matches(text))
			{
				violations.Add($"{relativePath} injects repository dependency '{match.Value}'");
			}
		}

		violations.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Hr_Source_Should_Not_Reintroduce_Known_Maintainability_Smells()
	{
		var repoRoot = FindRepositoryRoot();
		var allowedTenantFilterFile = Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.Application",
			"HR",
			"Dashboard",
			"HRDashboardPreviewService.cs");
		var smellPatterns = new Dictionary<string, Regex>
		{
			["DateTime.Now"] = new(@"\bDateTime\.Now\b", RegexOptions.CultureInvariant),
			["DateTime.UtcNow"] = new(@"\bDateTime\.UtcNow\b", RegexOptions.CultureInvariant),
			["DateTime.Today"] = new(@"\bDateTime\.Today\b", RegexOptions.CultureInvariant),
			["DateTimeOffset.Now"] = new(@"\bDateTimeOffset\.Now\b", RegexOptions.CultureInvariant),
			["DateTimeOffset.UtcNow"] = new(@"\bDateTimeOffset\.UtcNow\b", RegexOptions.CultureInvariant),
			["IValidatableObject"] = new(@"\bIValidatableObject\b", RegexOptions.CultureInvariant),
			["MapToDto"] = new(@"\bMapToDto\b", RegexOptions.CultureInvariant),
			["manual CurrentTenant filter"] = new(@"TenantId\s*==\s*CurrentTenant\.Id", RegexOptions.CultureInvariant)
		};

		var violations = new List<string>();
		foreach (var file in GetMainHrSourceFiles(repoRoot))
		{
			var text = File.ReadAllText(file);
			foreach (var (name, pattern) in smellPatterns)
			{
				if (name == "manual CurrentTenant filter" &&
				    string.Equals(file, allowedTenantFilterFile, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if (pattern.IsMatch(text))
				{
					violations.Add($"{Path.GetRelativePath(repoRoot, file)} contains {name}");
				}
			}
		}

		violations.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Hr_Source_Should_Not_Reintroduce_Removed_Surface()
	{
		var repoRoot = FindRepositoryRoot();
		var forbiddenPatterns = new Dictionary<string, Regex>
		{
			["Retired attribute"] = new(@$"\[{Join("Ob", "solete")}\b", RegexOptions.CultureInvariant),
			["Friendly exception"] = new(@$"\b{Join("User", "Friendly", "Exception")}\b", RegexOptions.CultureInvariant),
			["Employee facade"] = new(
				@$"\b{Join("I", "Employee", "App", "Service")}\b|\b{Join("Employee", "App", "Service")}\b",
				RegexOptions.CultureInvariant),
			["Removed naming"] = new(
				@$"\b{Join("Leg", "acy")}\b|\b{Join("leg", "acy")}\b|\b{Join("Compa", "tibility")}\b",
				RegexOptions.CultureInvariant)
		};
		var violations = new List<string>();

		foreach (var file in GetMainHrSourceFiles(repoRoot))
		{
			var text = File.ReadAllText(file);
			foreach (var (name, pattern) in forbiddenPatterns)
			{
				if (pattern.IsMatch(text))
				{
					violations.Add($"{Path.GetRelativePath(repoRoot, file)} contains {name}");
				}
			}
		}

		violations.ShouldBeEmpty();
	}

	private static string Join(params string[] parts) => string.Concat(parts);

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Professional_Management_Domain_Should_Use_Canonical_Folders()
	{
		var repoRoot = FindRepositoryRoot();
		var professionalManagementRoot = Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.Domain",
			"HR",
			"ProfessionalManagement");
		var expectedDirectories = new HashSet<string>(StringComparer.Ordinal)
		{
			"Aggregates",
			"Entities",
			"ValueObjects",
			"Managers",
			"Services",
			"Repositories",
			"Exceptions"
		};
		var directoryViolations = Directory
			.EnumerateDirectories(professionalManagementRoot)
			.Select(Path.GetFileName)
			.Where(name => name is not null && !expectedDirectories.Contains(name))
			.Select(name => $"Unexpected ProfessionalManagement folder: {name}");
		var rootFiles = Directory
			.EnumerateFiles(professionalManagementRoot, "*.cs", SearchOption.TopDirectoryOnly)
			.Select(file => $"ProfessionalManagement root file should move into a canonical folder: {Path.GetFileName(file)}");

		directoryViolations
			.Concat(rootFiles)
			.ToList()
			.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Professional_Management_Domain_Files_Should_Have_One_Public_Type()
	{
		var repoRoot = FindRepositoryRoot();
		var professionalManagementRoot = Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.Domain",
			"HR",
			"ProfessionalManagement");

		var violations = Directory
			.EnumerateFiles(professionalManagementRoot, "*.cs", SearchOption.AllDirectories)
			.Select(file => new
			{
				File = Path.GetRelativePath(repoRoot, file),
				PublicTypeCount = PublicTypePattern().Matches(File.ReadAllText(file)).Count
			})
			.Where(x => x.PublicTypeCount > 1)
			.Select(x => $"{x.File} has {x.PublicTypeCount} public/internal types")
			.ToList();

		violations.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Professional_Management_Ef_Configurations_Should_Have_One_Public_Type()
	{
		var repoRoot = FindRepositoryRoot();
		var professionalManagementEfRoot = Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.EntityFrameworkCore",
			"HR",
			"ProfessionalManagement");

		var violations = Directory
			.EnumerateFiles(professionalManagementEfRoot, "*.cs", SearchOption.AllDirectories)
			.Select(file => new
			{
				File = Path.GetRelativePath(repoRoot, file),
				PublicTypeCount = PublicTypePattern().Matches(File.ReadAllText(file)).Count
			})
			.Where(x => x.PublicTypeCount > 1)
			.Select(x => $"{x.File} has {x.PublicTypeCount} public/internal types")
			.ToList();

		violations.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Professional_Management_Risky_Record_Methods_Should_Call_Authorization_Service()
	{
		var repoRoot = FindRepositoryRoot();
		var expectations = new[]
		{
			new ProfessionalManagementAuthorizationExpectation(
				"ProfessionalManagementAppService.Reviews.cs",
				"GetReviewAsync",
				"EnsureCanViewReviewAsync"),
			new ProfessionalManagementAuthorizationExpectation(
				"ProfessionalManagementAppService.Reviews.cs",
				"SaveDraftAsync",
				"EnsureCanSelfReviewAsync"),
			new ProfessionalManagementAuthorizationExpectation(
				"ProfessionalManagementAppService.Reviews.cs",
				"GetEmployeeReviewHistoryAsync",
				"EnsureCanViewEmployeeHistoryAsync"),
			new ProfessionalManagementAuthorizationExpectation(
				"ProfessionalManagementAppService.Reviews.cs",
				"SaveFinalOutcomeAsync",
				"EnsureCanManageOutcomeAsync"),
			new ProfessionalManagementAuthorizationExpectation(
				"ProfessionalManagementAppService.Contributions.cs",
				"AddEvidenceAsync",
				"EnsureCanUploadEvidenceAsync"),
			new ProfessionalManagementAuthorizationExpectation(
				"ProfessionalManagementAppService.Contributions.cs",
				"DeleteEvidenceAsync",
				"EnsureCanDeleteEvidenceAsync"),
			new ProfessionalManagementAuthorizationExpectation(
				"ProfessionalManagementAppService.Contributions.cs",
				"AddObservationAsync",
				"EnsureCanAddObservationAsync"),
			new ProfessionalManagementAuthorizationExpectation(
				"ProfessionalManagementAppService.Contributions.cs",
				"GetObservationsAsync",
				"EnsureCanViewReviewAsync"),
			new ProfessionalManagementAuthorizationExpectation(
				"ProfessionalManagementAppService.Dashboard.cs",
				"GetReviewPdfAsync",
				"EnsureCanViewReviewAsync")
		};
		var root = Path.Combine(repoRoot, "src", "ShikshaMitra.Application", "HR", "ProfessionalManagement");
		var violations = expectations
			.Where(expectation =>
			{
				var text = File.ReadAllText(Path.Combine(root, expectation.FileName));
				var body = ExtractMethodBody(text, expectation.MethodName);
				return body == null || !body.Contains(expectation.RequiredAuthorizationCall, StringComparison.Ordinal);
			})
			.Select(expectation => $"{expectation.FileName}.{expectation.MethodName} must call {expectation.RequiredAuthorizationCall}")
			.ToList();

		violations.ShouldBeEmpty();
	}

	private static Regex PublicTypePattern() =>
		new(
			@"(^|\n)\s*(public|internal)\s+(sealed\s+|abstract\s+|partial\s+)*\b(class|record|struct|enum|interface)\s+[A-Za-z_][A-Za-z0-9_]*",
			RegexOptions.Multiline | RegexOptions.CultureInvariant);

	private static Regex RepositoryInjectionPattern() =>
		new(
			@"\b(?:IRepository<[^>]+>|I[A-Za-z0-9_]*Repository)\s+[A-Za-z_][A-Za-z0-9_]*",
			RegexOptions.CultureInvariant);

	private static string? ExtractMethodBody(string text, string methodName)
	{
		var methodIndex = text.IndexOf(methodName, StringComparison.Ordinal);
		if (methodIndex < 0)
		{
			return null;
		}

		var nextMethodIndex = Regex
			.Matches(text[(methodIndex + methodName.Length)..], @"\n\s*public\s+", RegexOptions.CultureInvariant)
			.Cast<Match>()
			.Select(match => methodIndex + methodName.Length + match.Index)
			.FirstOrDefault();

		return nextMethodIndex > methodIndex
			? text[methodIndex..nextMethodIndex]
			: text[methodIndex..];
	}

	private static IEnumerable<string> GetMainHrSourceFiles(string repoRoot)
	{
		var directories = new[]
		{
			Path.Combine(repoRoot, "src", "ShikshaMitra.Application", "HR"),
			Path.Combine(repoRoot, "src", "ShikshaMitra.Domain", "HR"),
			Path.Combine(repoRoot, "src", "ShikshaMitra.Domain.Shared", "HR"),
			Path.Combine(repoRoot, "src", "ShikshaMitra.Application.Contracts", "HR"),
			Path.Combine(repoRoot, "src", "ShikshaMitra.EntityFrameworkCore", "HR"),
			Path.Combine(repoRoot, "src", "ShikshaMitra.HttpApi", "Controllers", "HR"),
			Path.Combine(repoRoot, "src", "ShikshaMitra.Domain", "Seeders")
		};

		return directories
			.Where(Directory.Exists)
			.SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
			.Where(file =>
				!file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
				!file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
			.Where(file =>
				!file.Contains($"{Path.DirectorySeparatorChar}Seeders{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
				Path.GetFileName(file).StartsWith("HRSeederContributor", StringComparison.Ordinal));
	}

	private static string FindRepositoryRoot()
	{
		var current = new DirectoryInfo(AppContext.BaseDirectory);
		while (current is not null)
		{
			if (File.Exists(Path.Combine(current.FullName, "ShikshaMitra.sln")))
			{
				return current.FullName;
			}

			current = current.Parent;
		}

		throw new InvalidOperationException("Could not find repository root from test execution directory.");
	}

	private sealed record ProfessionalManagementAuthorizationExpectation(
		string FileName,
		string MethodName,
		string RequiredAuthorizationCall);
}
