using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.TenantSettings.AcademicSetups.Services;

internal static class AcademicSiblingOrder
{
	internal static void Validate(IReadOnlyList<Guid>? requestedIds, IEnumerable<Guid> siblingIds)
	{
		var expected = siblingIds.ToHashSet();
		if (requestedIds == null || requestedIds.Count == 0 || requestedIds.Count != expected.Count
			|| requestedIds.Distinct().Count() != requestedIds.Count || !expected.SetEquals(requestedIds))
		{
			throw new BusinessException(TenantSettingsDomainErrorCodes.InvalidAcademicOrder);
		}
	}
}
