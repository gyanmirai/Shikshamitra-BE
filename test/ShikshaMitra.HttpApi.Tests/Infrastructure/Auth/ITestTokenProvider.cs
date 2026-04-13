using System.Threading;
using System.Threading.Tasks;

namespace ShikshaMitra.Infrastructure.Auth;

/// <summary>
/// Abstraction for obtaining bearer tokens in test scenarios.
/// Implementations exist for password grant and client credentials grant.
/// </summary>
public interface ITestTokenProvider
{
	/// <summary>
	/// Returns a bearer token string (without the "Bearer " prefix).
	/// Implementations should cache tokens until expiry.
	/// </summary>
	Task<string> GetTokenAsync(CancellationToken cancellationToken = default);
}
