using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmOperationalStandardOptionsWorkflow(
	IRepository<AdmissionCrmAdmissionYearSetup, Guid> admissionYearRepository,
	IUserScopeAccessService userScopeAccess,
	StudentScopeAccessService scopeAccess,
	IRepository<Standard, Guid> standardRepository,
	IRepository<EducationMediumStage, Guid> mediumStageRepository,
	IRepository<EducationMedium, Guid> mediumRepository,
	IRepository<EducationBoard, Guid> boardRepository,
	AdmissionCrmLeadAccess access) : ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add)]
	public async Task<IReadOnlyList<AdmissionCrmAdmissionYearStandardOptionDto>>
		GetOperationalStandardOptionsAsync(Guid academicYearId)
	{
		await access.EnsureAcademicYearExistsAsync(academicYearId);
		await scopeAccess.EnsureCanAccessAcademicYearAsync(
			academicYearId,
			nameof(GetOperationalStandardOptionsAsync));

		var admissionYearSetups = await admissionYearRepository.GetListAsync(setup =>
			setup.TenantId == CurrentTenant.Id &&
			setup.AcademicYearId == academicYearId);
		var publishedSetup = admissionYearSetups.SingleOrDefault(setup =>
			setup.Status == AdmissionCrmAdmissionYearSetupStatus.Published);
		HashSet<Guid>? captureStandardIds = admissionYearSetups.Count == 0
			? null
			: publishedSetup is null
				? []
				: JsonSerializer.Deserialize<HashSet<Guid>>(
					publishedSetup.TargetStandardIdsJson,
					AdmissionCrmLeadFormat.JsonOptions) ?? [];

		var standards = await standardRepository.GetQueryableAsync();
		var stages = await mediumStageRepository.GetQueryableAsync();
		var mediums = await mediumRepository.GetQueryableAsync();
		var boards = await boardRepository.GetQueryableAsync();
		var optionQuery =
			from standard in standards
			join stage in stages on standard.EducationMediumStageId equals stage.Id
			join medium in mediums on stage.EducationMediumId equals medium.Id
			join board in boards on medium.EducationBoardId equals board.Id
			where board.TenantId == CurrentTenant.Id &&
				  board.AcademicYearId == academicYearId
			select new OperationalStandardOptionRow
			{
				AcademicYearId = board.AcademicYearId,
				EducationBoardId = board.Id,
				EducationMediumId = medium.Id,
				EducationMediumStageId = stage.Id,
				StandardId = standard.Id,
				StandardName = standard.Name,
				MediumStageName = stage.Name,
				MediumName = medium.Name,
				BoardType = board.Type
			};

		if (captureStandardIds is not null)
		{
			optionQuery = optionQuery.Where(option =>
				option.StandardId.HasValue && captureStandardIds.Contains(option.StandardId.Value));
		}

		var userScope = await userScopeAccess.GetCurrentUserScopeAsync();
		var standardOptions = await AsyncExecuter.ToListAsync(optionQuery);
		var scopedOptions = standardOptions.FilterByUserScope(userScope);
		return scopedOptions
			.OrderBy(option => option.BoardType)
			.ThenBy(option => option.MediumName)
			.ThenBy(option => option.MediumStageName)
			.ThenBy(option => option.StandardName)
			.Select(option => new AdmissionCrmAdmissionYearStandardOptionDto
			{
				StandardId = option.StandardId!.Value,
				StandardName = option.StandardName,
				MediumStageId = option.EducationMediumStageId!.Value,
				MediumStageName = option.MediumStageName,
				MediumId = option.EducationMediumId!.Value,
				MediumName = option.MediumName,
				BoardId = option.EducationBoardId!.Value,
				BoardName = option.BoardType.ToString()
			})
			.ToList();
	}

	private sealed class OperationalStandardOptionRow : IUserScopedEntity
	{
		public Guid? AcademicYearId { get; init; }
		public Guid? EducationBoardId { get; init; }
		public Guid? EducationMediumId { get; init; }
		public Guid? EducationMediumStageId { get; init; }
		public Guid? StandardId { get; init; }
		public Guid? DivisionId => null;
		public string StandardName { get; init; } = string.Empty;
		public string MediumStageName { get; init; } = string.Empty;
		public string MediumName { get; init; } = string.Empty;
		public EducationBoardType BoardType { get; init; }
	}
}