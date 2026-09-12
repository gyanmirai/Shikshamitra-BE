using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using Volo.Abp;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.View)]
	public async Task<FeeStructureContextDto> GetContextAsync(Guid feeStructureMetadataId, Guid standardId)
	{
		var scope = await EnsureStandardScopeAsync(standardId);
		var metadata = await GetMetadataReferenceAsync(feeStructureMetadataId);
		if (metadata.AcademicYearId != scope.AcademicYearId || metadata.AcademicBoardId != scope.EducationBoardId || metadata.AcademicMediumId != scope.EducationMediumId)
			throw new BusinessException(FeeStructureErrorCodes.ScopeMismatch);
		var year = await academicYearRepository.GetAsync(scope.AcademicYearId!.Value);
		var board = await GetBoardCalendarAsync(scope.EducationBoardId!.Value);
		var className = await AsyncExecuter.SingleAsync((await standardRepository.GetQueryableAsync()).Where(x => x.Id == standardId).Select(x => x.Name));
		var result = new FeeStructureContextDto { AcademicYearId = year.Id, AcademicYearName = year.Name, BoardId = board.Id,
			ClassName = className, StartDate = board.StartDate, EndDate = board.EndDate };
		var permissions = new Dictionary<string, string>
		{
			["add"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add,
			["edit"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add,
			["delete"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Delete,
			["publish"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeStructure.Add,
			["headsRead"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeHead.View,
			["termsRead"] = ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_PaymentTerms.View,
			["collection"] = ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.Add,
			["concession"] = ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.Add,
			["proration"] = ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.Add,
			["refund"] = ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.Add,
			["collectionRead"] = ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.View,
			["concessionRead"] = ShikshaMitraSubmodulePermissions.Finance.Policies_ConcessionPolicy.View,
			["prorationRead"] = ShikshaMitraSubmodulePermissions.Finance.Policies_ProrationPolicy.View,
			["refundRead"] = ShikshaMitraSubmodulePermissions.Finance.Policies_RefundPolicy.View
		};
		foreach (var permission in permissions) result.Permissions[permission.Key] = await AuthorizationService.IsGrantedAsync(permission.Value);
		return result;
	}
}
