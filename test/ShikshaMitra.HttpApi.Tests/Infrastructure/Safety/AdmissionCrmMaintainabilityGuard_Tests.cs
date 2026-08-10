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
[TestModule(TestTraits.Modules.Students)]
public class AdmissionCrmMaintainabilityGuard_Tests
{
	private const int NewServiceLineLimit = 300;
	private const int NewServiceDependencyLimit = 8;

	private static readonly IReadOnlyDictionary<string, int> ApprovedOversizedFiles =
		new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
		{
			["AdmissionCrmPublicAppService.cs"] = 3924,
			["AdmissionCrmLeadAppService.cs"] = 3117,
			["AdmissionCrmOperationsAppService.cs"] = 1661,
			["AdmissionCrmAnalyticsAppService.cs"] = 1398,
			["AdmissionCrmConfigurationSafetyAppService.cs"] = 1397,
			["AdmissionCrmSetupAppService.cs"] = 1390,
			["AdmissionCrmConfigurationAppService.cs"] = 1109,
			["AdmissionCrmApplicantProgressProjector.cs"] = 596,
			["AdmissionCrmConfigurationCloneWriter.cs"] = 455,
			["AdmissionCrmFinancialReadinessAppService.cs"] = 447,
			["AdmissionCrmLifecycleAutomationAppService.cs"] = 364,
			["AdmissionCrmCommunicationDispatcher.cs"] = 364,
			["AdmissionCrmCatalogAppService.cs"] = 358,
			["AdmissionCrmBulkOperationsAppService.cs"] = 335,
			["AdmissionCrmReportAppService.cs"] = 314,
			["AdmissionCrmDataQualityAppService.cs"] = 312
		};

	private static readonly IReadOnlyDictionary<string, int> ApprovedDependencyCounts =
		new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
		{
			["AdmissionCrmPublicAppService.cs"] = 36,
			["AdmissionCrmAnalyticsAppService.cs"] = 30,
			["AdmissionCrmLeadAppService.cs"] = 26,
			["AdmissionCrmOperationsAppService.cs"] = 24,
			["AdmissionCrmSetupAppService.cs"] = 23,
			["AdmissionCrmDataQualityAppService.cs"] = 14,
			["AdmissionCrmLifecycleAutomationAppService.cs"] = 12,
			["AdmissionCrmBulkOperationsAppService.cs"] = 9,
			["AdmissionCrmConfigurationAppService.cs"] = 9,
			["AdmissionCrmConfigurationCloneWriter.cs"] = 18,
			["AdmissionCrmConfigurationSnapshotReader.cs"] = 20
		};

	private static readonly IReadOnlyDictionary<string, int> ApprovedFacadeRepositoryReferences =
		new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
		{
			["AdmissionCrmAnalyticsAppService.cs"] = 58,
			["AdmissionCrmBulkOperationsAppService.cs"] = 6,
			["AdmissionCrmCatalogAppService.cs"] = 10,
			["AdmissionCrmConfigurationAppService.cs"] = 10,
			["AdmissionCrmDataQualityAppService.cs"] = 27,
			["AdmissionCrmEventAppService.cs"] = 6,
			["AdmissionCrmFinancialReadinessAppService.cs"] = 8,
			["AdmissionCrmLeadAppService.cs"] = 36,
			["AdmissionCrmLifecycleAutomationAppService.cs"] = 16,
			["AdmissionCrmOperationsAppService.cs"] = 44,
			["AdmissionCrmPublicAppService.cs"] = 60,
			["AdmissionCrmReportAppService.cs"] = 4,
			["AdmissionCrmSetupAppService.cs"] = 36,
			["AdmissionCrmStructuredReviewAppService.cs"] = 8,
			["AdmissionCrmWorkspaceAppService.cs"] = 6
		};

	private static readonly IReadOnlyDictionary<string, int> ApprovedAppServiceReferences =
		new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
		{
			["AdmissionCrmBulkOperationsAppService.cs"] = 10,
			["AdmissionCrmLeadAppService.cs"] = 4,
			["AdmissionCrmLifecycleAutomationAppService.cs"] = 4,
			["AdmissionCrmPublicAppService.cs"] = 6,
			["AdmissionCrmSetupAppService.cs"] = 2
		};

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Enrollment_Services_Should_Stay_Within_The_Size_Ratchet()
	{
		var violations = GetSourceFiles()
			.Select(file => new { File = file, Lines = File.ReadLines(file).Count() })
			.Where(item => item.Lines > NewServiceLineLimit)
			.Where(item =>
				!ApprovedOversizedFiles.TryGetValue(Path.GetFileName(item.File), out var approved) ||
				item.Lines > approved)
			.Select(item => $"{Path.GetFileName(item.File)} has {item.Lines} lines")
			.ToList();

		violations.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Enrollment_Services_Should_Stay_Within_The_Dependency_Ratchet()
	{
		var violations = GetSourceFiles()
			.Select(file => new { File = file, Dependencies = CountConstructorDependencies(File.ReadAllText(file)) })
			.Where(item => item.Dependencies > NewServiceDependencyLimit)
			.Where(item =>
				!ApprovedDependencyCounts.TryGetValue(Path.GetFileName(item.File), out var approved) ||
				item.Dependencies > approved)
			.Select(item => $"{Path.GetFileName(item.File)} has {item.Dependencies} constructor dependencies")
			.ToList();

		violations.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Enrollment_Endpoint_Facades_Should_Not_Add_Repository_Plumbing()
	{
		var violations = GetSourceFiles()
			.Select(file => new { File = file, Text = File.ReadAllText(file) })
			.Where(item => EndpointFacadePattern().IsMatch(item.Text))
			.Select(item => new
			{
				item.File,
				References = RepositoryInjectionPattern().Matches(item.Text).Count
			})
			.Where(item => item.References > 0)
			.Where(item =>
				!ApprovedFacadeRepositoryReferences.TryGetValue(Path.GetFileName(item.File), out var approved) ||
				item.References > approved)
			.Select(item => $"{Path.GetFileName(item.File)} has {item.References} repository references in an endpoint facade")
			.ToList();

		violations.ShouldBeEmpty();
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Enrollment_Services_Should_Not_Add_App_Service_Chains_Or_Partial_Types()
	{
		var violations = new List<string>();
		foreach (var file in GetSourceFiles())
		{
			var text = File.ReadAllText(file);
			var appServiceReferences = AppServiceInjectionPattern().Matches(text).Count;
			if (appServiceReferences > 0 &&
			    (!ApprovedAppServiceReferences.TryGetValue(Path.GetFileName(file), out var approved) ||
			     appServiceReferences > approved))
			{
				violations.Add($"{Path.GetFileName(file)} has {appServiceReferences} app-service dependencies");
			}

			if (PartialTypePattern().IsMatch(text))
			{
				violations.Add($"{Path.GetFileName(file)} declares a partial type");
			}
		}

		violations.ShouldBeEmpty();
	}

	private static int CountConstructorDependencies(string text)
	{
		var maximum = 0;
		foreach (Match classMatch in ClassNamePattern().Matches(text))
		{
			var className = classMatch.Groups["name"].Value;
			var constructorPatterns = new[]
			{
				$@"\bclass\s+{Regex.Escape(className)}\s*\(",
				$@"\b(?:public|internal)\s+{Regex.Escape(className)}\s*\("
			};

			foreach (var pattern in constructorPatterns)
			{
				foreach (Match constructor in Regex.Matches(text, pattern, RegexOptions.CultureInvariant))
				{
					var parameters = ReadBalancedContent(text, constructor.Index + constructor.Length);
					maximum = Math.Max(maximum, CountTopLevelParameters(parameters));
				}
			}
		}

		return maximum;
	}

	private static string ReadBalancedContent(string text, int start)
	{
		var depth = 1;
		for (var index = start; index < text.Length; index++)
		{
			depth += text[index] switch
			{
				'(' => 1,
				')' => -1,
				_ => 0
			};

			if (depth == 0)
			{
				return text[start..index];
			}
		}

		return string.Empty;
	}

	private static int CountTopLevelParameters(string parameters)
	{
		if (string.IsNullOrWhiteSpace(parameters))
		{
			return 0;
		}

		var angleDepth = 0;
		var parenthesisDepth = 0;
		var count = 1;
		foreach (var character in parameters)
		{
			switch (character)
			{
				case '<': angleDepth++; break;
				case '>': angleDepth--; break;
				case '(': parenthesisDepth++; break;
				case ')': parenthesisDepth--; break;
				case ',' when angleDepth == 0 && parenthesisDepth == 0: count++; break;
			}
		}

		return count;
	}

	private static IEnumerable<string> GetSourceFiles()
	{
		var root = Path.Combine(
			FindRepositoryRoot(),
			"src",
			"ShikshaMitra.Application",
			"Students",
			"StudentEnrollments",
			"AdmissionsCrm");
		return Directory.EnumerateFiles(root, "*.cs", SearchOption.TopDirectoryOnly);
	}

	private static Regex ClassNamePattern() =>
		new(@"\bclass\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant);

	private static Regex EndpointFacadePattern() =>
		new(
			@":\s*ApplicationService\s*,\s*IAdmissionCrm[A-Za-z0-9_]*(?:AppService|Service)\b",
			RegexOptions.CultureInvariant);

	private static Regex RepositoryInjectionPattern() =>
		new(@"\b(?:IRepository<[^>]+>|I[A-Za-z0-9_]*Repository)\s+[A-Za-z_][A-Za-z0-9_]*", RegexOptions.CultureInvariant);

	private static Regex AppServiceInjectionPattern() =>
		new(@"\bIAdmissionCrm[A-Za-z0-9_]*AppService\s+[A-Za-z_][A-Za-z0-9_]*", RegexOptions.CultureInvariant);

	private static Regex PartialTypePattern() =>
		new(@"\bpartial\s+(?:class|record|struct)\b", RegexOptions.CultureInvariant);

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

		throw new InvalidOperationException("Could not find the backend repository root.");
	}
}
