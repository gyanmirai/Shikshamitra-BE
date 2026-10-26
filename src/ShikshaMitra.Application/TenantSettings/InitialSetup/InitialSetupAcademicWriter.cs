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
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using System.Linq;
using System.Collections.Generic;
using ShikshaMitra.TenantSettings.AcademicSetups.Formatting;

namespace ShikshaMitra.TenantSettings.InitialSetup;

[RemoteService(false)]
public class InitialSetupAcademicWriter(
	InitialSetupStatusReader reader,
	IRepository<AcademicYear, Guid> years, AcademicYearManager yearManager,
	IRepository<EducationBoard, Guid> boards, EducationBoardManager boardManager,
	IRepository<EducationMedium, Guid> mediums, EducationMediumManager mediumManager,
	IRepository<EducationMediumStage, Guid> stages, EducationMediumStageManager stageManager,
	IRepository<Standard, Guid> standards, StandardManager standardManager,
	IRepository<Division, Guid> divisions, DivisionManager divisionManager,
	AcademicFieldFormattingStore formatting) : ShikshaMitraAppService
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
		var customName = EducationBoardNames.NormalizeCustomName(draft.BoardType.Value, draft.CustomBoardName);
		var boardCandidates = await boards.GetListAsync(x => x.TenantId == tenantId && x.AcademicYearId == year.Id && x.Type == draft.BoardType.Value);
		var board = boardCandidates.FirstOrDefault(x => customName == null || string.Equals(x.CustomName, customName, StringComparison.OrdinalIgnoreCase))
			?? await boards.InsertAsync(await boardManager.CreateAsync(year.Id, draft.BoardType.Value, year.StartDate, year.EndDate, customName));
		var mediumName = draft.Medium.Trim(); var stageName = draft.Stage.Trim();
		var className = draft.ClassName.Trim(); var divisionName = draft.DivisionName.Trim();
		var rules = await formatting.GetAsync();
		var mediumNames = LookupNames(mediumName, rules.Medium);
		var mediumCandidates = await mediums.GetListAsync(x => x.TenantId == tenantId && x.EducationBoardId == board.Id && Enumerable.Contains(mediumNames, x.Name));
		var medium = Match(mediumCandidates, mediumNames, x => x.Name)
			?? await mediums.InsertAsync(await mediumManager.CreateAsync(board.Id, mediumName));
		var stageNames = LookupNames(stageName, rules.Stage);
		var stageCandidates = await stages.GetListAsync(x => x.TenantId == tenantId && x.EducationMediumId == medium.Id && Enumerable.Contains(stageNames, x.Name));
		var stage = Match(stageCandidates, stageNames, x => x.Name)
			?? await stages.InsertAsync(await stageManager.CreateAsync(medium.Id, stageName));
		var classNames = LookupNames(className, rules.Standard);
		var classCandidates = await standards.GetListAsync(x => x.TenantId == tenantId && x.EducationMediumStageId == stage.Id && Enumerable.Contains(classNames, x.Name));
		var standard = Match(classCandidates, classNames, x => x.Name);
		if (standard == null) standard = await standards.InsertAsync(await standardManager.CreateAsync(stage.Id, className, draft.AgeLowerLimit.Value, null));
		else if (standard.AgeLowerLimit != draft.AgeLowerLimit.Value)
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit);
			await standardManager.UpdateAsync(standard, standard.Name, draft.AgeLowerLimit.Value, standard.ShiftId);
			await standards.UpdateAsync(standard);
		}
		var divisionNames = LookupNames(divisionName, rules.Division);
		var divisionCandidates = await divisions.GetListAsync(x => x.TenantId == tenantId && x.StandardId == standard.Id && Enumerable.Contains(divisionNames, x.Name));
		var division = Match(divisionCandidates, divisionNames, x => x.Name);
		if (division == null) division = await divisions.InsertAsync(await divisionManager.CreateAsync(standard.Id, divisionName, draft.Strength.Value, ""));
		else if (division.Strength != draft.Strength.Value)
		{
			await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Edit);
			await divisionManager.UpdateAsync(division, division.Name, draft.Strength.Value, division.RoomNumber);
			await divisions.UpdateAsync(division);
		}
		return division.Id;
	}
	private static string[] LookupNames(string raw, AcademicTextCase current) =>
		new[] { raw, AcademicNameFormatter.Format(raw, current) }
			.Concat(Enum.GetValues<AcademicTextCase>().Select(mode => AcademicNameFormatter.Format(raw, mode)))
			.Distinct(StringComparer.Ordinal).ToArray();

	private static T? Match<T>(IReadOnlyList<T> candidates, IReadOnlyList<string> names, Func<T, string> getName) where T : class =>
		names.Select(name => candidates.FirstOrDefault(candidate => getName(candidate) == name)).FirstOrDefault(candidate => candidate != null);

	private async Task<Guid> AuthorizeAsync()
	{
		var tenantId = reader.RequireTenant();
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.InstituteSetup.AcademicSetup.Add);
		return tenantId;
	}
}
