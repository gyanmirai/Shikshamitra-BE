using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Library.Books;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Library.Shared;

[RemoteService(false)]
public class LibraryStaffScopeCaptureService(IRepository<LibraryBorrowerScopeCapture, Guid> captures,
	IRepository<Employee, Guid> employees, IRepository<EmployeeOrganizationAssignment, Guid> assignments,
	IRepository<EducationMediumStage, Guid> stages, IRepository<EducationMedium, Guid> mediums,
	IRepository<EducationBoard, Guid> boards, IRepository<AcademicYear, Guid> years, ILibraryInventoryLock inventoryLock)
	: ApplicationService, ITransientDependency
{
	public async Task<LibraryBorrowerScopeCapture> CaptureAsync(LibraryBorrowerScopeSourceKind kind, Guid sourceId, Guid employeeId,
		Guid academicYearId, LibraryBorrowerScopeProvenance provenance, LibraryOperationActor actor)
	{
		if (!CurrentTenant.Id.HasValue || !await employees.AnyAsync(row => row.Id == employeeId && row.TenantId == CurrentTenant.Id) ||
			!await years.AnyAsync(row => row.Id == academicYearId && row.TenantId == CurrentTenant.Id))
			throw new BusinessException(LibraryErrorCodes.ContextInvalid);
		await inventoryLock.AcquireAsync(CurrentTenant.Id);
		var version = await AsyncExecuter.FirstOrDefaultAsync((await captures.GetQueryableAsync()).Where(row =>
			row.TenantId == CurrentTenant.Id && row.SourceKind == kind && row.SourceId == sourceId)
			.OrderByDescending(row => row.Version).Select(row => row.Version));
		var actualAssignments = (await assignments.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id && row.EmployeeId == employeeId);
		var actualStages = (await stages.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id);
		var actualMediums = (await mediums.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id);
		var actualBoards = (await boards.GetQueryableAsync()).Where(row => row.TenantId == CurrentTenant.Id);
		var tuples = await AsyncExecuter.ToListAsync(
			from assignment in actualAssignments
			join stage in actualStages on assignment.EducationMediumStageId equals stage.Id into stageRows
			from stage in stageRows.DefaultIfEmpty()
			join medium in actualMediums on (stage == null ? Guid.Empty : stage.EducationMediumId) equals medium.Id into mediumRows
			from medium in mediumRows.DefaultIfEmpty()
			join board in actualBoards on (medium == null ? Guid.Empty : medium.EducationBoardId) equals board.Id into boardRows
			from board in boardRows.DefaultIfEmpty()
			select new LibraryBorrowerAssignmentEvidence(assignment.Id, assignment.EducationMediumStageId,
				stage == null ? null : stage.EducationMediumId, medium == null ? null : medium.EducationBoardId,
				board == null ? null : board.AcademicYearId));
		var capturedAt = new DateTimeOffset(Clock.Now.ToUniversalTime());
		var audit = JsonSerializer.Serialize(new { Version = 1, Source = "EmployeeOrganizationAssignments", EmployeeId = employeeId,
			AcademicYearId = academicYearId, Assignments = tuples });
		var capture = new LibraryBorrowerScopeCapture(GuidGenerator.Create(), CurrentTenant.Id.Value, kind, sourceId, employeeId,
			academicYearId, checked(version + 1), provenance, actor, capturedAt, tuples, audit);
		return await captures.InsertAsync(capture, true);
	}
}
