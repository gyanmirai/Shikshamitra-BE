using System.Threading.Tasks;
using ShikshaMitra.Library.IssueReturn;
using ShikshaMitra.Library.Shared;

namespace ShikshaMitra.Library.Circulation;

public partial class LibraryCirculationLookupAppService
{
	private Task<LibraryCirculationSchoolContextDto> ReadSchoolContextAsync(LibraryAcademicContext context) =>
		schoolContexts.ReadAsync(context);
}
