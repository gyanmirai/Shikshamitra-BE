using System.Collections.Generic;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ShikshaMitra.Infrastructure.Traits;

public class TestTierDiscoverer : ITraitDiscoverer
{
	public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
	{
		var tier = traitAttribute.GetNamedArgument<string>(nameof(TestTierAttribute.Tier));
		if (!string.IsNullOrWhiteSpace(tier))
		{
			yield return new KeyValuePair<string, string>(TestTraits.Tier, tier);
		}
	}
}
