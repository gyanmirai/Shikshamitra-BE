using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Settings.Dtos;

namespace ShikshaMitra.Finance.Fees.Settings;
public partial class FeeCategoryAppService
{
 [Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_Settings_FeeCategory.View)]
 public async Task<List<FeeCategoryOptionDto>> GetOptionsAsync(Guid academicYearId, Guid? selectedId = null) =>
  await AsyncExecuter.ToListAsync((await feeCategoryRepository.GetQueryableAsync())
   .Where(c => c.AcademicYearId == academicYearId && (c.IsActive || c.Id == selectedId))
   .OrderBy(c => c.Name).ThenBy(c => c.Id)
   .Select(c => new FeeCategoryOptionDto { Id = c.Id, Name = c.Name, IsActive = c.IsActive }));
}
