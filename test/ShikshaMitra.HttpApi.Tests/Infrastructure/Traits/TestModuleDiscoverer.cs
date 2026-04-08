using System.Collections.Generic;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ShikshaMitra.Infrastructure.Traits;

public class TestModuleDiscoverer : ITraitDiscoverer
{
	public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
	{
		var module = traitAttribute.GetNamedArgument<string>(nameof(TestModuleAttribute.Module));
		if (!string.IsNullOrWhiteSpace(module))
		{
			yield return new KeyValuePair<string, string>(TestTraits.Module, module);
		}
	}
}
