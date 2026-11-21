using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.Sections;
using ShikshaMitra.Library.Shared;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Policies;

[RemoteService(false)]
public class LibraryPolicyTargetAccess(ILibraryAccessService access,
	IRepository<BookCategory, Guid> categories, IRepository<LibrarySection, Guid> sections,
	IRepository<Standard, Guid> standards, IRepository<EducationMediumStage, Guid> stages,
	IRepository<EducationMedium, Guid> mediums, IRepository<EducationBoard, Guid> boards) : ApplicationService, ITransientDependency
{
	public async Task<LibraryBorrowingPolicyDefinition> ValidateAsync(Guid yearId, LibraryBorrowingPolicyDefinition definition)
	{
		definition.Validated();
		var scope = (await access.GetContextAsync(yearId)).UserScope;
		if (definition.BookCategoryId.HasValue && !await categories.AnyAsync(x => x.Id == definition.BookCategoryId.Value))
			throw new BusinessException(LibraryErrorCodes.BookCategoryNotFound);
		if (definition.SectionId.HasValue && !await sections.AnyAsync(x => x.Id == definition.SectionId.Value))
			throw new BusinessException(LibraryErrorCodes.SectionNotFound);
		Standard? standard = null;
		if (definition.StandardId.HasValue)
		{
			standard = await standards.FindAsync(definition.StandardId.Value) ?? throw new BusinessException(LibraryErrorCodes.PolicyInvalid);
			if (definition.EducationMediumStageId.HasValue && definition.EducationMediumStageId != standard.EducationMediumStageId)
				throw new BusinessException(LibraryErrorCodes.PolicyInvalid, "The selected class does not belong to the selected stage.");
			definition = definition with { EducationMediumStageId = standard.EducationMediumStageId };
		}
		if (definition.EducationMediumStageId.HasValue)
		{
			var stage = await stages.FindAsync(definition.EducationMediumStageId.Value) ?? throw new BusinessException(LibraryErrorCodes.PolicyInvalid);
			var medium = await mediums.GetAsync(stage.EducationMediumId);
			var board = await boards.GetAsync(medium.EducationBoardId);
			if (board.AcademicYearId != yearId) throw new BusinessException(LibraryErrorCodes.PolicyInvalid, "The policy target must belong to the selected academic context.");
			if (!CanManage(scope, yearId, board.Id, medium.Id, stage.Id, standard?.Id))
				throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		}
		else if (!scope.HasTenantWideScope()) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		return definition;
	}

	public async Task<LibraryPolicyOptionsDto> GetOptionsAsync(Guid yearId)
	{
		var scope = (await access.GetContextAsync(yearId)).UserScope;
		var boardRows = await boards.GetListAsync(x => x.AcademicYearId == yearId);
		var boardIds = boardRows.Select(x => x.Id).ToList();
		var mediumRows = await mediums.GetListAsync(x => boardIds.Contains(x.EducationBoardId));
		var mediumIds = mediumRows.Select(x => x.Id).ToList();
		var stageRows = await stages.GetListAsync(x => mediumIds.Contains(x.EducationMediumId));
		var stageIds = stageRows.Select(x => x.Id).ToList();
		var standardRows = await standards.GetListAsync(x => stageIds.Contains(x.EducationMediumStageId));
		var mediumById = mediumRows.ToDictionary(x => x.Id);
		var boardById = boardRows.ToDictionary(x => x.Id);
		var stageById = stageRows.ToDictionary(x => x.Id);
		bool Allowed(EducationMediumStage stage, Guid? standardId = null)
		{
			var medium = mediumById[stage.EducationMediumId];
			return CanManage(scope, yearId, medium.EducationBoardId, medium.Id, stage.Id, standardId);
		}
		string StageName(EducationMediumStage stage)
		{
			var medium = mediumById[stage.EducationMediumId];
			return $"{stage.Name} / {medium.Name} / {boardById[medium.EducationBoardId].Name}";
		}
		return new()
		{
			BookCategories = (await categories.GetListAsync()).OrderBy(x => x.Name).Select(x => new NamedIdDto { Id = x.Id, Name = x.Name }).ToList(),
			Sections = (await sections.GetListAsync()).OrderBy(x => x.SectionName).Select(x => new NamedIdDto { Id = x.Id, Name = x.SectionName ?? string.Empty }).ToList(),
			Stages = stageRows.Where(x => Allowed(x)).OrderBy(x => x.Name).Select(x => new LibraryPolicyScopeOptionDto
			{ Id = x.Id, Name = StageName(x), ParentId = x.EducationMediumId, ParentName = mediumById[x.EducationMediumId].Name }).ToList(),
			Standards = standardRows.Where(x => Allowed(stageById[x.EducationMediumStageId], x.Id)).OrderBy(x => x.Name)
				.Select(x => new LibraryPolicyScopeOptionDto { Id = x.Id, Name = x.Name, ParentId = x.EducationMediumStageId, ParentName = StageName(stageById[x.EducationMediumStageId]) }).ToList()
		};
	}

	public async Task<IQueryable<LibraryBorrowingPolicyVersion>> ApplyReadScopeAsync(
		IQueryable<LibraryBorrowingPolicyVersion> query, LibraryAcademicContext context)
	{
		if (context.UserScope.HasTenantWideScope()) return query;
		var targets = await ReadTargetsAsync(context);
		return query.Where(x => x.StandardId.HasValue && Enumerable.Contains(targets.Standards, x.StandardId.Value) ||
			!x.StandardId.HasValue && (!x.EducationMediumStageId.HasValue || Enumerable.Contains(targets.Stages, x.EducationMediumStageId.Value)));
	}

	public async Task EnsureReadableAsync(LibraryBorrowingPolicyVersion policy, LibraryAcademicContext context)
	{
		if (context.UserScope.HasTenantWideScope() || !policy.StandardId.HasValue && !policy.EducationMediumStageId.HasValue) return;
		var targets = await ReadTargetsAsync(context);
		var permitted = policy.StandardId.HasValue ? targets.Standards.Contains(policy.StandardId.Value)
			: targets.Stages.Contains(policy.EducationMediumStageId!.Value);
		if (!permitted) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}

	private async Task<(Guid[] Stages, Guid[] Standards)> ReadTargetsAsync(LibraryAcademicContext context)
	{
		var scope = context.UserScope;
		var yearIds = context.AllowedAcademicYearIds;
		var boardRows = await boards.GetListAsync(x => yearIds.Contains(x.AcademicYearId));
		var boardIds = boardRows.Select(x => x.Id).ToList();
		var mediumRows = await mediums.GetListAsync(x => boardIds.Contains(x.EducationBoardId));
		var mediumIds = mediumRows.Select(x => x.Id).ToList();
		var stageRows = await stages.GetListAsync(x => mediumIds.Contains(x.EducationMediumId));
		var stageIds = stageRows.Select(x => x.Id).ToList();
		var standardRows = await standards.GetListAsync(x => stageIds.Contains(x.EducationMediumStageId));
		var stageById = stageRows.ToDictionary(x => x.Id);
		var mediumById = mediumRows.ToDictionary(x => x.Id);
		var boardById = boardRows.ToDictionary(x => x.Id);
		bool FullTarget(EducationMediumStage stage, Guid? standardId)
		{
			var medium = mediumById[stage.EducationMediumId];
			return CanManage(scope, boardById[medium.EducationBoardId].AcademicYearId,
				medium.EducationBoardId, medium.Id, stage.Id, standardId);
		}
		var readableStandards = standardRows.Where(x => scope.StandardIds.Contains(x.Id) || FullTarget(stageById[x.EducationMediumStageId], x.Id)).ToList();
		var ancestorStages = readableStandards.Select(x => x.EducationMediumStageId).ToHashSet();
		var readableStages = stageRows.Where(x => scope.MediumStageIds.Contains(x.Id) || ancestorStages.Contains(x.Id) || FullTarget(x, null)).Select(x => x.Id).ToArray();
		return (readableStages, readableStandards.Select(x => x.Id).ToArray());
	}

	private static bool CanManage(UserScopeContext scope, Guid yearId, Guid boardId, Guid mediumId, Guid stageId, Guid? standardId) =>
		scope.HasAcademicYearWideScope(yearId) || scope.WildcardMediumBoardIds.Contains(boardId) ||
		scope.WildcardMediumStageMediumIds.Contains(mediumId) || scope.WildcardStandardMediumStageIds.Contains(stageId) ||
		standardId.HasValue && scope.WildcardDivisionStandardIds.Contains(standardId.Value);
}
