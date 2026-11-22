using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

public partial class StudentIdCardAppService
{
	private IRepository<StudentIdCardAssignment, Guid> Assignments =>
		LazyServiceProvider.LazyGetRequiredService<IRepository<StudentIdCardAssignment, Guid>>();

	public async Task<PagedResultDto<StudentIdCardAssignmentDto>> GetAssignmentsAsync(StudentIdCardAssignmentListQueryDto input)
	{
		await EnsureTemplateActorAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View);
		ValidateAssignmentPage(input);
		if (input.AcademicYearId == Guid.Empty || input.BoardId == Guid.Empty || input.MediumId == Guid.Empty)
			throw new BusinessException(StudentErrorCodes.InvalidIdAssignment);
		var options = await GetAssignmentOptionsAsync(input.AcademicYearId);
		if (!options.Boards.Any(board => board.Id == input.BoardId && board.ParentId == input.AcademicYearId) ||
			!options.Mediums.Any(medium => medium.Id == input.MediumId && medium.ParentId == input.BoardId))
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		var scope = await LazyServiceProvider.LazyGetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		var stages = options.Stages.ToDictionary(stage => stage.Id, stage => stage.Name);
		var search = input.Search?.Trim();
		var classes = options.Standards.Where(standard => standard.ParentId == input.MediumId &&
			(!input.StandardId.HasValue || standard.Id == input.StandardId.Value) &&
			(string.IsNullOrEmpty(search) || standard.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
				standard.StageId.HasValue && stages.GetValueOrDefault(standard.StageId.Value)?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)).ToList();
		var page = classes.Skip(input.SkipCount).Take(input.MaxResultCount)
			.Select(standard => new StudentIdCardAssignmentDto
			{
				StandardId = standard.Id,
				StandardName = standard.Name,
				StageName = standard.StageId.HasValue ? stages.GetValueOrDefault(standard.StageId.Value) : null,
				CanManage = CanManageAssignment(scope, input.AcademicYearId, input.BoardId, input.MediumId, standard)
			}).ToList();
		if (page.Count == 0) return new(classes.Count, page);
		var standards = page.Select(item => item.StandardId).ToArray();
		var rules = await AsyncExecuter.ToListAsync((await GetAssignmentRowsAsync(input.AcademicYearId))
			.Where(row => Enumerable.Contains(standards, row.StandardId)));
		var templateIds = rules.Select(rule => rule.TemplateId).Distinct().ToArray();
		var matchingTemplates = await AsyncExecuter.ToListAsync((await templates.GetQueryableAsync())
			.Where(row => row.TenantId == CurrentTenant.Id && Enumerable.Contains(templateIds, row.Id)));
		var rulesByStandard = rules.ToDictionary(rule => rule.StandardId);
		var templatesById = matchingTemplates.ToDictionary(row => row.Id);
		foreach (var item in page)
			if (rulesByStandard.TryGetValue(item.StandardId, out var rule) && templatesById.TryGetValue(rule.TemplateId, out var template))
				SetAssignment(item, rule, template);
		return new(classes.Count, page);
	}

	public async Task<PagedResultDto<StudentIdCardAssignmentRosterStudentDto>> GetAssignmentRosterAsync(
		Guid academicYearId, Guid standardId, StudentIdCardAssignmentRosterQueryDto input)
	{
		await EnsureTemplateActorAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View);
		ValidateAssignmentPage(input);
		await EnsureAssignmentClassAsync(academicYearId, standardId, divisionId: input.DivisionId);
		var population = await GetAssignmentStudentPopulationAsync(academicYearId, standardId);
		if (input.DivisionId.HasValue) population = population.Where(student => student.DivisionId == input.DivisionId);
		var validCards = await GetValidAssignmentCardsAsync(academicYearId);
		var query = population.Select(student => new StudentIdCardAssignmentRosterStudentDto
		{
			StudentId = student.Id,
			StudentName = ((student.FirstName ?? "") + (student.MiddleName != null && student.MiddleName != "" ? " " + student.MiddleName : "") +
				(student.LastName != null && student.LastName != "" ? " " + student.LastName : "")).Trim(),
			GrNumber = student.GrNumber ?? (student.GrNo.HasValue ? student.GrNo.Value.ToString() : null),
			DivisionName = student.Division != null ? student.Division.Name : null,
			HasValidIssuedCard = validCards.Any(card => card.StudentId == student.Id)
		});
		var search = input.Search?.Trim().ToLowerInvariant();
		if (!string.IsNullOrEmpty(search))
			query = query.Where(student => student.StudentName.ToLower().Contains(search) ||
				student.GrNumber != null && student.GrNumber.ToLower().Contains(search));
		var total = await AsyncExecuter.CountAsync(query);
		var students = await AsyncExecuter.ToListAsync(query.OrderBy(student => student.StudentName)
			.ThenBy(student => student.StudentId).Skip(input.SkipCount).Take(input.MaxResultCount));
		return new(total, students);
	}

	private async Task<IQueryable<Student>> GetAssignmentStudentPopulationAsync(Guid year, Guid standard)
	{
		var scope = LazyServiceProvider.LazyGetRequiredService<StudentRecordsEnrollmentScopeService>();
		var students = LazyServiceProvider.LazyGetRequiredService<IStudentRepository>();
		var population = await scope.ApplyStudentScopeAsync(await students.GetQueryableWithoutAutoIncludesAsync());
		var policy = (await policies.ResolveAsync(year)).Policy;
		if (policy.IsMasked("Identity", await access.GetDatabaseRoleNamesAsync())) return population.Where(_ => false);
		var enrollment = await scope.GetQueryableAsync();
		population = population.Where(student => student.AcademicYearId == year && student.StandardId == standard &&
			student.Status == StudentLifeCycleStatusEnum.Enrolled && enrollment.Any(row => row.StudentId == student.Id &&
				row.AcademicYearId == year && row.StandardId == standard && row.DivisionId == student.DivisionId && row.IsActive));
		return await access.FilterFullAccessStudentsAsync(population, policy);
	}

	private async Task<IQueryable<StudentIdCard>> GetValidAssignmentCardsAsync(Guid year) =>
		(await cards.GetQueryableAsync()).Where(card => card.TenantId == CurrentTenant.Id &&
			card.AcademicYearId == year && (card.Status == "Issued" || card.Status == "Printed"));

	private async Task<StudentIdCardAssignmentDto> EnsureAssignmentClassAsync(Guid year, Guid standardId, bool manage = false, Guid? divisionId = null)
	{
		if (year == Guid.Empty || standardId == Guid.Empty || divisionId == Guid.Empty)
			throw new BusinessException(StudentErrorCodes.InvalidIdAssignment);
		var options = await GetAssignmentOptionsAsync(year);
		var standard = options.Standards.Find(item => item.Id == standardId);
		var medium = options.Mediums.Find(item => item.Id == standard?.ParentId);
		var board = options.Boards.Find(item => item.Id == medium?.ParentId && item.ParentId == year);
		if (standard == null || medium == null || board == null ||
			divisionId.HasValue && !options.Divisions.Any(item => item.Id == divisionId && item.ParentId == standardId))
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		var scope = await LazyServiceProvider.LazyGetRequiredService<IUserScopeAccessService>().GetCurrentUserScopeAsync();
		var canManage = CanManageAssignment(scope, year, board.Id, medium.Id, standard);
		if (manage && !canManage) throw new BusinessException(UserScopeErrorCodes.AccessDenied);
		return new()
		{
			StandardId = standard.Id,
			StandardName = standard.Name,
			StageName = options.Stages.Find(item => item.Id == standard.StageId)?.Name,
			CanManage = canManage
		};
	}

	private Task<StudentDirectoryOptionsDto> GetAssignmentOptionsAsync(Guid year) =>
		LazyServiceProvider.LazyGetRequiredService<StudentDirectoryOptionsService>().GetAsync(year);

	private static bool CanManageAssignment(UserScopeContext scope, Guid year, Guid board, Guid medium, StudentDirectoryOptionDto standard) =>
		scope.IsAdmin || scope.HasTenantWideScope() || scope.AcademicYearIds.Contains(year) &&
		(scope.WildcardBoardAcademicYearIds.Contains(year) || scope.WildcardMediumBoardIds.Contains(board) ||
			scope.WildcardMediumStageMediumIds.Contains(medium) ||
			standard.StageId.HasValue && scope.WildcardStandardMediumStageIds.Contains(standard.StageId.Value) ||
			scope.WildcardDivisionStandardIds.Contains(standard.Id));

	private static void ValidateAssignmentPage(StudentIdCardAssignmentPageQueryDto input)
	{
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100 || input.Search?.Length > 150)
			throw new BusinessException(StudentErrorCodes.InvalidIdAssignment);
	}

	private async Task<IQueryable<StudentIdCardAssignment>> GetAssignmentRowsAsync(Guid year) =>
		(await Assignments.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && row.AcademicYearId == year);

	private static void SetAssignment(StudentIdCardAssignmentDto item, StudentIdCardAssignment rule, StudentIdCardTemplate template)
	{
		item.TemplateId = rule.TemplateId;
		item.TemplateName = MapTemplate(template).Name;
		item.ConcurrencyStamp = rule.ConcurrencyStamp;
		item.TemplateConcurrencyStamp = template.ConcurrencyStamp;
	}
}
