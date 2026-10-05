using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Seeders;
using Xunit;

namespace ShikshaMitra.Academic.HomeworkAssignment;

[CollectionDefinition(Name)]
public sealed class RubricScoringApiTestCollection : ICollectionFixture<RubricScoringApiTestFactory>
{
    public const string Name = "Rubric Scoring API";
}

/// <summary>Real API and SQLite persistence with foundational school seeds.</summary>
public sealed class RubricScoringApiTestFactory : ShikshaMitraApiTestFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(configuration =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ShikshaMitraSeederProfile.ContextPropertyName] = ShikshaMitraSeederProfile.Minimal
            }));
    }
}
