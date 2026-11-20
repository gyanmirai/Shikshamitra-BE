using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Setting.BorrowingRules;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Library.Policies;

public class LibraryBorrowingPolicyResolver(IRepository<LibraryBorrowingPolicyVersion, Guid> policies,
	IRepository<BorrowingRule, Guid> legacyRules) : DomainService
{
	/// <summary>Unpublished schools keep their existing quotas and manual due dates.</summary>
	public async Task<LibraryBorrowingPolicyDecision> ResolveAsync(LibraryBorrowingPolicyContext context)
	{
		if (context.Role is < 0 or > 1 || context.AcademicYearId == Guid.Empty || context.AsOfDate == default)
			throw new BusinessException(LibraryErrorCodes.PolicyInvalid);
		var candidates = (await policies.GetListAsync(x => x.Role == context.Role && x.Status != LibraryBorrowingPolicyState.Draft))
			.Where(x => x.IsEffectiveOn(context.AsOfDate) && x.Matches(context))
			.OrderByDescending(x => x.Specificity).ThenByDescending(x => x.Priority).ToList();
		if (candidates.Count > 1 && candidates[0].Specificity == candidates[1].Specificity && candidates[0].Priority == candidates[1].Priority)
			throw new BusinessException(LibraryErrorCodes.PolicyOverlap, "More than one policy has the same precedence for this borrower and item.");
		if (candidates.Count > 0)
		{
			var policy = candidates[0];
			return new(policy, context.Role, policy.Terms.MaxBooksAtTime, policy.Terms.ReservationLimit);
		}
		var legacy = await legacyRules.FindAsync(x => x.AcademicYearId == context.AcademicYearId && x.Role == context.Role);
		return new(null, context.Role, legacy?.MaxBooksAtTime ?? (context.Role == 0 ? 3 : 5),
			legacy?.ReservationLimit ?? (context.Role == 0 ? 2 : 3));
	}
}
