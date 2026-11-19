using System.Linq;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Preserves the original Standard repository's required-parent visibility without hydrating hierarchy graphs.</summary>
internal static class AdmissionStandardLabelQuery
{
	// Default list reads use the model's required hierarchy, including its AcademicYear AutoInclude.
	internal static IQueryable<Standard> ForList(IQueryable<Standard> query, IQueryable<AcademicYear> years) => query.Where(standard =>
		years.Any(year => year.Id == standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId));

	// Default detail reads use EfCoreStandardRepository.WithDetailsAsync, whose required chain ends at Board.
	internal static IQueryable<Standard> ForDetail(IQueryable<Standard> query, IQueryable<EducationBoard> boards) => query.Where(standard =>
		boards.Any(board => board.Id == standard.EducationMediumStage.EducationMedium.EducationBoardId));
}
