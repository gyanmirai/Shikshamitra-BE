using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Books;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Library.Policies;

public class LibraryBorrowingPolicyPublisher(IRepository<LibraryBorrowingPolicyVersion, Guid> policies,
	ILibraryInventoryLock inventoryLock) : DomainService
{
	/// <summary>Publishing shares the circulation lock so matching effective policies cannot race.</summary>
	public async Task PublishAsync(LibraryBorrowingPolicyVersion policy, string expectedStamp, DateTimeOffset now)
	{
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var existing = await policies.GetListAsync(x => x.Id != policy.Id && x.Role == policy.Role && x.Status != LibraryBorrowingPolicyState.Draft);
		if (existing.Any(policy.ConflictsWith))
			throw new BusinessException(LibraryErrorCodes.PolicyOverlap, "An effective policy with overlapping dates and the same precedence already exists. End or archive it before publishing this version.");
		policy.Publish(expectedStamp, now);
	}
}
