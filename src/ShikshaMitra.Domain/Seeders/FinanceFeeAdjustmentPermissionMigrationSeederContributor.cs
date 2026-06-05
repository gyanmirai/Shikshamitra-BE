using System.Collections.Generic;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Copies legacy <c>Finance.Adjustment.*</c> role grants to <c>Finance.Fees.Adjustment.*</c>
/// after the permission namespace fix.
/// </summary>
public class FinanceFeeAdjustmentPermissionMigrationSeederContributor(
	ICurrentTenant currentTenant,
	IGuidGenerator guidGenerator,
	IUnitOfWork unitOfWork,
	IPermissionGrantSeedRepository permissionGrantSeedRepository)
	: BaseSeederContributor(currentTenant, guidGenerator, unitOfWork)
{
	private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> LegacyToTargets =
		new Dictionary<string, IReadOnlyCollection<string>>
		{
			{
				ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.LegacyView,
				[ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.View]
			},
			{
				ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.LegacyAdd,
				[
					ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add,
					ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Approve,
					ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Reject,
				]
			},
			{
				ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.LegacyDelete,
				[ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Delete]
			},
		};

	public override async Task SeedAsync(DataSeedContext context)
	{
		await permissionGrantSeedRepository.MirrorRoleGrantsIgnoringDuplicatesAsync(
			LegacyToTargets,
			CurrentTenant.Id);
	}
}
