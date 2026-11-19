using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Exams;
using ShikshaMitra.Communication.Notifications;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Notifications;

internal sealed class ExamNotificationDivision
{
	internal Exam Exam { get; init; } = null!;
	internal Guid YearId { get; init; }
	internal Guid BoardId { get; init; }
	internal Guid MediumId { get; init; }
	internal Guid ClassId { get; init; }
	internal Guid DivisionId { get; init; }
	internal NotificationSourceScopeDto Scope => new(YearId, BoardId, MediumId, ClassId, DivisionId);
}

[RemoteService(false)]
public sealed class ExamNotificationPlacement(IRepository<Standard, Guid> standards, IRepository<Division, Guid> divisions)
	: ITransientDependency
{
	internal async Task<IQueryable<ExamNotificationDivision>> QueryAsync(Guid tenantId, IQueryable<Exam> exams) =>
		from exam in exams
		from section in exam.ExamDivisions
		join standard in await standards.GetQueryableAsync() on exam.StandardId equals standard.Id
		join division in await divisions.GetQueryableAsync() on section.DivisionId equals division.Id
		let stage = standard.EducationMediumStage
		let medium = stage.EducationMedium
		let board = medium.EducationBoard
		where exam.TenantId == tenantId && !exam.IsDeleted && standard.TenantId == tenantId && !standard.IsDeleted &&
			division.TenantId == tenantId && !division.IsDeleted && division.StandardId == standard.Id &&
			stage.TenantId == tenantId && medium.TenantId == tenantId && board.TenantId == tenantId &&
			board.AcademicYear.TenantId == tenantId
		select new ExamNotificationDivision
		{
			Exam = exam, YearId = board.AcademicYearId, BoardId = board.Id, MediumId = medium.Id,
			ClassId = standard.Id, DivisionId = division.Id
		};
}
