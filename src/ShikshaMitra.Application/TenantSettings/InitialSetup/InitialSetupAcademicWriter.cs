using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.TenantSettings.InitialSetup;

[RemoteService(false)]
public class InitialSetupAcademicWriter(
	InitialSetupStatusReader reader,
	IRepository<AcademicYear, Guid> years, AcademicYearManager yearManager,
	IRepository<EducationBoard, Guid> boards, EducationBoardManager boardManager,
	IRepository<EducationMedium, Guid> mediums, EducationMediumManager mediumManager,
	IRepository<EducationMediumStage, Guid> stages, EducationMediumStageManager stageManager,
	IRepository<Standard, Guid> standards, StandardManager standardManager,
	IRepository<Division, Guid> divisions, DivisionManager divisionManager) : ShikshaMitraAppService
{
	public async Task<AcademicYear> WriteYearAsync(SaveSetupYearInput input, Guid? savedYearId)
	{
		var tenantId = await AuthorizeAsync();
		var yearId = input.CreateNew ? null : input.ExistingYearId ?? savedYearId;
		if (yearId.HasValue)
		{
			var year = await years.GetAsync(x => x.Id == yearId && x.TenantId == tenantId);
			if (year.Name == input.Name.Trim() && year.StartDate == input.StartDate && year.EndDate == input.EndDate) return year;
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit);
			await yearManager.UpdateAsync(year, input.Name.Trim(), input.StartDate, input.EndDate);
			return await years.UpdateAsync(year);
		}
		return await years.InsertAsync(await yearManager.CreateAsync(input.Name.Trim(), input.StartDate, input.EndDate));
	}
	public async Task<Guid> WriteStructureAsync(Guid? yearId, InitialSetupDraftDto draft)
	{
		var tenantId = await AuthorizeAsync();
		if (!yearId.HasValue) throw new BusinessException(InitialSetupErrorCodes.YearRequired);
		var year = await years.GetAsync(x => x.Id == yearId && x.TenantId == tenantId);
		if (!draft.BoardType.HasValue || !Enum.IsDefined(draft.BoardType.Value) ||
			string.IsNullOrWhiteSpace(draft.Medium) || string.IsNullOrWhiteSpace(draft.Stage) ||
			string.IsNullOrWhiteSpace(draft.ClassName) || string.IsNullOrWhiteSpace(draft.DivisionName) ||
			draft.AgeLowerLimit is null or <= 0 || draft.Strength is null or <= 0)
			throw new BusinessException(InitialSetupErrorCodes.InvalidStructure);
		var board = await boards.FindAsync(x => x.TenantId == tenantId && x.AcademicYearId == year.Id && x.Type == draft.BoardType.Value)
			?? await boards.InsertAsync(await boardManager.CreateAsync(year.Id, draft.BoardType.Value, year.StartDate, year.EndDate));
		var mediumName = draft.Medium.Trim(); var stageName = draft.Stage.Trim();
		var className = draft.ClassName.Trim(); var divisionName = draft.DivisionName.Trim();
		var medium = await mediums.FindAsync(x => x.TenantId == tenantId && x.EducationBoardId == board.Id && x.Name == mediumName)
			?? await mediums.InsertAsync(await mediumManager.CreateAsync(board.Id, mediumName));
		var stage = await stages.FindAsync(x => x.TenantId == tenantId && x.EducationMediumId == medium.Id && x.Name == stageName)
			?? await stages.InsertAsync(await stageManager.CreateAsync(medium.Id, stageName));
		var standard = await standards.FindAsync(x => x.TenantId == tenantId && x.EducationMediumStageId == stage.Id && x.Name == className);
		if (standard == null) standard = await standards.InsertAsync(await standardManager.CreateAsync(stage.Id, className, draft.AgeLowerLimit.Value, null));
		else if (standard.AgeLowerLimit != draft.AgeLowerLimit.Value)
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit);
			await standardManager.UpdateAsync(standard, className, draft.AgeLowerLimit.Value, standard.ShiftId);
			await standards.UpdateAsync(standard);
		}
		var division = await divisions.FindAsync(x => x.TenantId == tenantId && x.StandardId == standard.Id && x.Name == divisionName);
		if (division == null) division = await divisions.InsertAsync(await divisionManager.CreateAsync(standard.Id, divisionName, draft.Strength.Value, draft.RoomNumber?.Trim() ?? ""));
		else if (division.Strength != draft.Strength.Value || division.RoomNumber != (draft.RoomNumber?.Trim() ?? ""))
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit);
			await divisionManager.UpdateAsync(division, divisionName, draft.Strength.Value, draft.RoomNumber?.Trim() ?? "");
			await divisions.UpdateAsync(division);
		}
		return division.Id;
	}
	private async Task<Guid> AuthorizeAsync()
	{
		var tenantId = reader.RequireTenant();
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Add);
		return tenantId;
	}
}
