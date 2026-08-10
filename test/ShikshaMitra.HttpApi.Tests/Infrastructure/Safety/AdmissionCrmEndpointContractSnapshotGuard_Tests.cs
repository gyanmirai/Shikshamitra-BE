using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Infrastructure.Safety;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class AdmissionCrmEndpointContractSnapshotGuard_Tests
{
	private const int ExpectedEndpointCount = 340;
	private const string ExpectedSnapshotHash = "685f3dbe5face5dbbf0c3cc589eb58af55b9d66c356d20b25921663dd7083ee5";

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Admission_Crm_Endpoint_Contracts_Should_Not_Drift_Accidentally()
	{
		var repoRoot = FindRepositoryRoot();
		var controllersRoot = Path.Combine(
			repoRoot,
			"src",
			"ShikshaMitra.HttpApi",
			"Controllers",
			"Student",
			"Enrollment",
			"AdmissionsCrm");
		var inventory = BuildEndpointInventory(controllersRoot);
		var snapshot = string.Join('\n', inventory);
		var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot))).ToLowerInvariant();

		inventory.Count.ShouldBe(
			ExpectedEndpointCount,
			$"Enrollment endpoint count changed. Current hash: {hash}.{Environment.NewLine}{snapshot}");

		hash.ShouldBe(
			ExpectedSnapshotHash,
			$"Enrollment endpoint contract snapshot changed. Review route, verb, binding, return type, action name, and authorization compatibility before approving a new snapshot.{Environment.NewLine}{snapshot}");
	}

	private static List<string> BuildEndpointInventory(string controllersRoot)
	{
		var endpoints = new List<string>();
		foreach (var file in Directory.EnumerateFiles(controllersRoot, "*.cs", SearchOption.AllDirectories))
		{
			var text = File.ReadAllText(file);
			var classMatch = ClassPattern().Match(text);
			if (!classMatch.Success)
			{
				continue;
			}

			var baseRoute = classMatch.Groups["route"].Value;
			var classAuthorization = NormalizeAuthorization(classMatch.Groups["attrs"].Value);
			foreach (Match action in ActionStartPattern().Matches(text))
			{
				var parameters = ReadBalancedParameters(text, action.Index + action.Length);
				var actionAuthorization = NormalizeAuthorization(action.Groups["attrs"].Value);
				var authorization = string.Join(
					" ",
					new[] { classAuthorization, actionAuthorization }.Where(value => value.Length > 0));
				foreach (Match httpAttribute in HttpAttributePattern().Matches(action.Groups["attrs"].Value))
				{
					var route = CombineRoute(baseRoute, httpAttribute.Groups["route"].Value);
					var returnType = Normalize(action.Groups["return"].Value);
					endpoints.Add(
						$"{httpAttribute.Groups["verb"].Value.ToUpperInvariant()} /{route} => {action.Groups["method"].Value} : {returnType} ({parameters}) | {authorization}");
				}
			}
		}

		return endpoints.OrderBy(value => value, StringComparer.Ordinal).ToList();
	}

	private static string NormalizeAuthorization(string attributes)
	{
		return string.Join(
			" ",
			AuthorizationPattern()
				.Matches(attributes)
				.Cast<Match>()
				.Select(match => Normalize(match.Value))
				.OrderBy(value => value, StringComparer.Ordinal));
	}

	private static string CombineRoute(string baseRoute, string actionRoute) =>
		$"{baseRoute.TrimEnd('/')}/{actionRoute.TrimStart('/')}".TrimEnd('/');

	private static string Normalize(string value) =>
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
				return Normalize(text[parameterStart..index]);
			}
		}

		throw new InvalidOperationException("Could not read a balanced enrollment controller action parameter list.");
	}

	private static Regex ClassPattern() =>
		new(
			"(?s)(?<attrs>(?:\\s*\\[[^\\]]+\\]\\s*)*\\s*\\[Route\\(\"(?<route>[^\"]+)\"\\)\\](?:\\s*\\[[^\\]]+\\]\\s*)*)\\s*public\\s+class\\s+[A-Za-z_][A-Za-z0-9_]*",
			RegexOptions.CultureInvariant);

	private static Regex ActionStartPattern() =>
		new(
			@"(?s)(?<attrs>(?:\s*\[[^\]]+\]\s*)+)\s*public\s+(?:virtual\s+)?(?:async\s+)?(?<return>[^{;=]+?)\s+(?<method>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
			RegexOptions.CultureInvariant);

	private static Regex HttpAttributePattern() =>
		new(
			"\\[Http(?<verb>Get|Post|Put|Delete|Patch)(?:\\(\"(?<route>[^\"]*)\"\\))?",
			RegexOptions.CultureInvariant);

	private static Regex AuthorizationPattern() =>
		new(@"\[(?:Authorize(?:\([^\]]*\))?|AllowAnonymous)\]", RegexOptions.CultureInvariant);

	private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
	{
		var current = new FileInfo(sourceFilePath).Directory;
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
