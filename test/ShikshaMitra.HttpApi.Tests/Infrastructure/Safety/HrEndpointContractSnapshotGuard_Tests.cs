using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
	private const int ExpectedEndpointCount = 264;
	private const string ExpectedSnapshotHash = "5a54a60b6bf57ec681412d0e4445503c73a3867dcc2f3dfa8ae15d2b482d398f";

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
}
