using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.Academic.Notifications;

[RemoteService(false)]
public sealed class AcademicNotificationRuleScopeAccess(IRepository<AcademicYear, Guid> years,
	IRepository<EducationBoard, Guid> boards, IRepository<EducationMedium, Guid> mediums,
	IRepository<EducationMediumStage, Guid> stages, IRepository<Standard, Guid> classes,
	IRepository<Division, Guid> divisions, IAsyncQueryableExecuter queries) : ITransientDependency
{
	internal async Task<NotificationRuleScope> RequireAsync(AcademicNotificationScopeDto? requested, Guid tenantId,
		UserScopeContext callerScope, CancellationToken cancellationToken)
	{
		if (requested == null) throw new BusinessException(NotificationErrorCodes.RuleConfigurationInvalid);
		NotificationRuleScope scope;
		try
		{
			scope = NotificationRuleScope.Create(requested.AcademicYearId, requested.BoardId, requested.MediumId,
				requested.ClassId, requested.DivisionId);
		}
		catch (ArgumentException)
		{
			throw new BusinessException(NotificationErrorCodes.RuleConfigurationInvalid);
		}

		if (scope.AcademicYearId is Guid year && !await queries.AnyAsync((await years.GetQueryableAsync()).Where(x =>
			x.Id == year && x.TenantId == tenantId), cancellationToken))
			throw new AbpAuthorizationException();
		if (scope.BoardId is Guid board && !await queries.AnyAsync((await boards.GetQueryableAsync()).Where(x =>
			x.Id == board && x.TenantId == tenantId && x.AcademicYearId == scope.AcademicYearId), cancellationToken))
			throw new AbpAuthorizationException();
		if (scope.MediumId is Guid medium && !await queries.AnyAsync((await mediums.GetQueryableAsync()).Where(x =>
			x.Id == medium && x.TenantId == tenantId && x.EducationBoardId == scope.BoardId), cancellationToken))
			throw new AbpAuthorizationException();

		Guid? stageId = null;
		if (scope.ClassId is Guid classId)
		{
			var classStage = from standard in await classes.GetQueryableAsync()
				join stage in await stages.GetQueryableAsync() on standard.EducationMediumStageId equals stage.Id
				where standard.Id == classId && standard.TenantId == tenantId && stage.TenantId == tenantId &&
					stage.EducationMediumId == scope.MediumId
				select (Guid?)stage.Id;
			stageId = await queries.SingleOrDefaultAsync(classStage, cancellationToken);
			if (!stageId.HasValue) throw new AbpAuthorizationException();
		}
		if (scope.DivisionId is Guid division && !await queries.AnyAsync((await divisions.GetQueryableAsync()).Where(x =>
			x.Id == division && x.TenantId == tenantId && x.StandardId == scope.ClassId), cancellationToken))
			throw new AbpAuthorizationException();

		var placement = new RulePlacement(scope.AcademicYearId, scope.BoardId, scope.MediumId, stageId, scope.ClassId, scope.DivisionId);
		if (!StudentRecordScope.HasAccess(placement, callerScope)) throw new AbpAuthorizationException();
		return scope;
	}

	private sealed record RulePlacement(Guid? AcademicYearId, Guid? EducationBoardId, Guid? EducationMediumId,
		Guid? EducationMediumStageId, Guid? StandardId, Guid? DivisionId) : IUserScopedEntity;
}
