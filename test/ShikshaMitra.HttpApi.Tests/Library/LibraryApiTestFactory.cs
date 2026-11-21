using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Seeders;
using Xunit;

namespace ShikshaMitra.Library;

[CollectionDefinition(Name)]
public sealed class LibraryApiTestCollection : ICollectionFixture<LibraryApiTestFactory>
{
	public const string Name = "Library API";
}

public sealed class LibraryApiTestFactory : ShikshaMitraApiTestFactory
{
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(
			new Dictionary<string, string?> { [ShikshaMitraSeederProfile.ContextPropertyName] = ShikshaMitraSeederProfile.Minimal }));
	}
}
