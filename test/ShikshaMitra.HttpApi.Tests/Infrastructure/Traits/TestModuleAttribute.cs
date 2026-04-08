using System;
using Xunit.Sdk;

namespace ShikshaMitra.Infrastructure.Traits;

/// <summary>
/// Marks which module this test belongs to.
/// Usage: [TestModule(TestTraits.Modules.HR)]
/// Filter: dotnet test --filter "Module=HR"
/// </summary>
[TraitDiscoverer("ShikshaMitra.Infrastructure.Traits.TestModuleDiscoverer", "ShikshaMitra.HttpApi.Tests")]
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class TestModuleAttribute(string module) : Attribute, ITraitAttribute
{
	public string Module { get; } = module;
}
