using System;
using Xunit.Sdk;

namespace ShikshaMitra.Infrastructure.Traits;

/// <summary>
/// Marks the test tier for selective execution.
/// Usage: [TestTier(TestTraits.Tiers.Critical)]
/// Filter: dotnet test --filter "Tier=Critical"
/// </summary>
[TraitDiscoverer("ShikshaMitra.Infrastructure.Traits.TestTierDiscoverer", "ShikshaMitra.HttpApi.Tests")]
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class TestTierAttribute(string tier) : Attribute, ITraitAttribute
{
	public string Tier { get; } = tier;
}
