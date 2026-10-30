using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Setup;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.Divisions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Dashboard;

public record AcademicInsightDivision(Guid Id, Guid StandardId, Guid StageId, string Label);
public record AcademicInsightsContext(AcademicInsightsInput Input, Guid TenantId, UserScopeContext Scope, bool WholeMedium, List<AcademicInsightDivision> Divisions)
{
	public Guid[] DivisionIds => Divisions.Select(x => x.Id).ToArray();
	public Guid[] StandardIds => Divisions.Select(x => x.StandardId).Distinct().ToArray();
}

[RemoteService(false)]
public class AcademicInsightsContextReader(AcademicSetupAccess identity, IUserScopeAccessService scopes,
	IRepository<EducationBoard, Guid> boards, IRepository<EducationMedium, Guid> mediums,
	IRepository<EducationMediumStage, Guid> stages, IRepository<Standard, Guid> standards,
	IRepository<Division, Guid> divisions) : ApplicationService
{
	public async Task<AcademicInsightsContext> ReadAsync(AcademicInsightsInput input)
	{
		var tenant = await identity.RequireIdentityAsync();
		if (!await boards.AnyAsync(x => x.TenantId == tenant && x.Id == input.BoardId && x.AcademicYearId == input.AcademicYearId) ||
			!await mediums.AnyAsync(x => x.TenantId == tenant && x.Id == input.EducationMediumId && x.EducationBoardId == input.BoardId))
			throw new BusinessException("Academic:Insights:InvalidContext");
		var scope = await scopes.GetCurrentUserScopeAsync();
		if (scope.TenantId != tenant || !(scope.HasAcademicYearWideScope(input.AcademicYearId) || scope.AcademicYearIds.Contains(input.AcademicYearId)))
			throw new AbpAuthorizationException();
		var query = from d in await divisions.GetQueryableAsync()
			join s in await standards.GetQueryableAsync() on d.StandardId equals s.Id
			join stage in await stages.GetQueryableAsync() on s.EducationMediumStageId equals stage.Id
			where d.TenantId == tenant && s.TenantId == tenant && stage.TenantId == tenant && stage.EducationMediumId == input.EducationMediumId &&
				(!input.StandardId.HasValue || s.Id == input.StandardId) && (!input.DivisionId.HasValue || d.Id == input.DivisionId)
			select new AcademicInsightDivision(d.Id, s.Id, stage.Id, s.Name + " / " + d.Name);
		var rows = await AsyncExecuter.ToListAsync(query);
		var allowed = rows.Where(x => AcademicInsightsScopePolicy.CanReadDivision(scope, input.AcademicYearId, input.BoardId,
			input.EducationMediumId, x.StageId, x.StandardId, x.Id)).ToList();
		var whole = AcademicInsightsScopePolicy.HasWholeMedium(scope, input.AcademicYearId, input.BoardId, input.EducationMediumId);
		if ((!whole && allowed.Count == 0) || input.DivisionId.HasValue && allowed.Count == 0 || input.StandardId.HasValue && rows.Count == 0)
			throw new AbpAuthorizationException();
		return new(input, tenant, scope, whole && !input.StandardId.HasValue && !input.DivisionId.HasValue, allowed);
	}
}
