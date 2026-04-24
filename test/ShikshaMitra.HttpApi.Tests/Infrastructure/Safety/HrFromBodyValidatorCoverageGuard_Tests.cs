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
public class HrFromBodyValidatorCoverageGuard_Tests
{
	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Hr_FromBody_Request_Types_Should_Have_Validation_Coverage()
	{
		var repoRoot = FindRepositoryRoot();
		var controllersRoot = Path.Combine(repoRoot, "src", "ShikshaMitra.HttpApi", "Controllers", "HR");
		var contractsRoot = Path.Combine(repoRoot, "src", "ShikshaMitra.Application.Contracts", "HR");

		var fromBodyTypes = GetFromBodyTypes(controllersRoot);
		var validatorTargets = GetValidatorTargets(contractsRoot);

		var allowlist = new HashSet<string>(StringComparer.Ordinal)
		{
			// Validation is performed by application service business flow.
			"DocumentRequest"
		};

		var missing = fromBodyTypes
			.Where(t =>
				!validatorTargets.Contains(t) &&
				!allowlist.Contains(t) &&
				!HasDataAnnotationCoverage(t, contractsRoot))
			.OrderBy(x => x, StringComparer.Ordinal)
			.ToList();

		missing.ShouldBeEmpty();
	}

	private static HashSet<string> GetFromBodyTypes(string controllersRoot)
	{
		var result = new HashSet<string>(StringComparer.Ordinal);
		if (!Directory.Exists(controllersRoot))
		{
			return result;
		}

		var pattern = new Regex(@"\[FromBody[^\]]*\]\s*(?<type>[A-Za-z_][A-Za-z0-9_<>?,\.\[\]]*)\s+[A-Za-z_][A-Za-z0-9_]*",
			RegexOptions.Multiline | RegexOptions.CultureInvariant);

		foreach (var file in Directory.EnumerateFiles(controllersRoot, "*.cs", SearchOption.AllDirectories))
		{
			var text = File.ReadAllText(file);
			foreach (Match match in pattern.Matches(text))
			{
				var rawType = match.Groups["type"].Value.Trim();
				var normalized = NormalizeTypeName(rawType);
				if (!string.IsNullOrWhiteSpace(normalized))
				{
					result.Add(normalized);
				}
			}
		}

		return result;
	}

	private static HashSet<string> GetValidatorTargets(string contractsRoot)
	{
		var result = new HashSet<string>(StringComparer.Ordinal);
		if (!Directory.Exists(contractsRoot))
		{
			return result;
		}

		var pattern = new Regex(@"AbstractValidator<\s*(?<type>[A-Za-z_][A-Za-z0-9_<>?,\.\[\]]*)\s*>",
			RegexOptions.Multiline | RegexOptions.CultureInvariant);

		foreach (var file in Directory.EnumerateFiles(contractsRoot, "*.cs", SearchOption.AllDirectories))
		{
			var text = File.ReadAllText(file);
			foreach (Match match in pattern.Matches(text))
			{
				var rawType = match.Groups["type"].Value.Trim();
				var normalized = NormalizeTypeName(rawType);
				if (!string.IsNullOrWhiteSpace(normalized))
				{
					result.Add(normalized);
				}
			}
		}

		return result;
	}

	private static string NormalizeTypeName(string typeName)
	{
		var withoutNullable = typeName.Trim().TrimEnd('?');
		var lastDot = withoutNullable.LastIndexOf('.');
		var simple = lastDot >= 0 ? withoutNullable[(lastDot + 1)..] : withoutNullable;
		var genericStart = simple.IndexOf('<');
		var genericEnd = simple.LastIndexOf('>');
		if (genericStart > 0 && genericEnd > genericStart)
		{
			var inner = simple[(genericStart + 1)..genericEnd].Trim();
			return NormalizeTypeName(inner);
		}

		return simple;
	}

	private static bool HasDataAnnotationCoverage(string typeName, string contractsRoot)
	{
		var attributeTokens = new[]
		{
			"[Required",
			"[StringLength",
			"[MaxLength",
			"[MinLength",
			"[Range",
			"[RegularExpression",
			"[EmailAddress",
			"[Phone",
			"[Url"
		};

		foreach (var file in Directory.EnumerateFiles(contractsRoot, "*.cs", SearchOption.AllDirectories))
		{
			var text = File.ReadAllText(file);
			if (!Regex.IsMatch(text, $@"class\s+{Regex.Escape(typeName)}\b"))
			{
				continue;
			}

			if (text.Contains("IValidatableObject", StringComparison.Ordinal))
			{
				return true;
			}

			if (attributeTokens.Any(token => text.Contains(token, StringComparison.Ordinal)))
			{
				return true;
			}
		}

		return false;
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
}
