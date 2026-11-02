using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Admissions;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	private sealed class OnboardingDetailReadContext
	{
		public string YearName { get; init; } = string.Empty;
		public string BoardName { get; init; } = string.Empty;
		public string MediumName { get; init; } = string.Empty;
		public string StandardName { get; init; } = string.Empty;
		public OnboardingListDivisionRead? Division { get; init; }
		public Student? Student { get; init; }
	}

	// Called only by GET, after the mapper's existing automatic capture/save and campus handling.
	private async Task<OnboardingDetailReadContext> LoadDetailReadContextAsync(StudentOnboardingCase item)
	{
		var years = await academicYearRepository.GetQueryableAsync();
		var boards = await boardRepository.GetQueryableAsync();
		var yearName = await AsyncExecuter.FirstOrDefaultAsync(years.Where(year => year.Id == item.AcademicYearId).Select(year => year.Name));
		var board = await AsyncExecuter.FirstOrDefaultAsync(boards.Where(row => row.Id == item.IntendedBoardId &&
			years.Any(year => year.Id == row.AcademicYearId)).Select(row => new { row.Type }));
		var mediumName = await AsyncExecuter.FirstOrDefaultAsync((await mediumRepository.GetQueryableAsync())
			.Where(medium => medium.Id == item.IntendedMediumId && years.Any(year => year.Id == medium.EducationBoard.AcademicYearId))
			.Select(medium => medium.Name));
		var standardName = await AsyncExecuter.FirstOrDefaultAsync(AdmissionStandardLabelQuery.ForDetail(await standardRepository.GetQueryableAsync(), boards)
			.Where(standard => standard.Id == item.IntendedStandardId).Select(standard => standard.Name));
		var division = item.DivisionId.HasValue ? await AsyncExecuter.FirstOrDefaultAsync((await divisionRepository.GetQueryableAsync())
			.Where(row => row.Id == item.DivisionId.Value).Select(row => new OnboardingListDivisionRead
			{
				Id = row.Id, Name = row.Name, ExtraProperties = row.ExtraProperties
			})) : null;
		// Keep tracked identity resolution for nested same-UoW callers with unsaved profile changes.
		var student = await studentRepository.GetCredentialsProfileAsync(item.StudentId, trackChanges: true);
		return new()
		{
			YearName = yearName ?? string.Empty, BoardName = board?.Type.ToString() ?? string.Empty,
			MediumName = mediumName ?? string.Empty, StandardName = standardName ?? string.Empty,
			Division = division, Student = student
		};
	}
}
