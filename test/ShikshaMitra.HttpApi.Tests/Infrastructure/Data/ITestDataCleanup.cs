using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Infrastructure.Data;

/// <summary>
/// Contract for cleaning up test data after destructive/extended tests.
/// Implement per-module to handle entity-specific cascading deletes.
/// </summary>
public interface ITestDataCleanup
{
	/// <summary>
	/// Attempts to delete a test entity by ID. Swallows not-found exceptions.
	/// </summary>
	Task TryDeleteAsync(Guid id);
}
