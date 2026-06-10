using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;

namespace ShikshaMitra.HR.Reports;

public class HrReportScheduleRecipientValidator(
	IIdentityUserRepository userRepository,
	IIdentityRoleRepository roleRepository,
	IRepository<PermissionGrant, Guid> permissionGrantRepository)
	: ITransientDependency
{
	public async Task<List<Guid>> NormalizeRecipientsAsync(IEnumerable<Guid>? userIds, Guid ownerUserId)
	{
		var recipients = (userIds ?? []).Where(x => x != Guid.Empty).Distinct().ToList();
		if (!recipients.Contains(ownerUserId))
		{
			recipients.Insert(0, ownerUserId);
		}

		foreach (var recipientId in recipients.Where(x => x != ownerUserId))
		{
			if (!await HasReportViewPermissionAsync(recipientId))
			{
				throw new BusinessException(HRErrorCodes.ReportAccessDenied)
					.WithData("recipientUserId", recipientId);
			}
		}

		return recipients;
	}

	private async Task<bool> HasReportViewPermissionAsync(Guid userId)
	{
		var user = await userRepository.FindAsync(userId, includeDetails: true);
		if (user == null)
		{
			return false;
		}

		var permissionGrants = await permissionGrantRepository.GetListAsync(x =>
			x.Name == Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.Reports.View &&
			(x.ProviderName == "U" && x.ProviderKey == userId.ToString() ||
			 x.ProviderName == "R"));
		if (permissionGrants.Any(x => x.ProviderName == "U" && x.ProviderKey == userId.ToString()))
		{
			return true;
		}

		var grantedRoleKeys = permissionGrants
			.Where(x => x.ProviderName == "R")
			.Select(x => x.ProviderKey)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		foreach (var roleRef in user.Roles)
		{
			var role = await roleRepository.FindAsync(roleRef.RoleId);
			if (role != null && grantedRoleKeys.Contains(role.Name))
			{
				return true;
			}
		}

		return false;
	}
}
