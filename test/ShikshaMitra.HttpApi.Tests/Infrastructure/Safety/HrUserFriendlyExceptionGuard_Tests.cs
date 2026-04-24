using System;
using System.IO;
using System.Linq;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Infrastructure.Safety;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.HR)]
public class HrUserFriendlyExceptionGuard_Tests
{
	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public void Hr_Source_Should_Not_Use_UserFriendlyException()
	{
		var repoRoot = FindRepositoryRoot();
		var targetDirectories = new[]
		{
			Path.Combine(repoRoot, "src", "ShikshaMitra.Application", "HR"),
			Path.Combine(repoRoot, "src", "ShikshaMitra.HttpApi", "Controllers", "HR"),
			Path.Combine(repoRoot, "src", "ShikshaMitra.Domain", "HR")
		};

		var offenders = targetDirectories
			.Where(Directory.Exists)
			.SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
			.Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
			               !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
			.Where(file => File.ReadAllText(file).Contains("UserFriendlyException", StringComparison.Ordinal))
			.ToList();

		offenders.ShouldBeEmpty();
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
