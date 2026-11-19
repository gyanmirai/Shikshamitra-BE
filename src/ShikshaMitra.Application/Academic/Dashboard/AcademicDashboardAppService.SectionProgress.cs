using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Syllabus;

namespace ShikshaMitra.Academic.Dashboard;

public partial class AcademicDashboardAppService
{
	private async Task<IQueryable<ChapterProgress>> GetTenantDashboardProgressQueryAsync()
	{
		var progress = await _chapterProgressRepository.GetQueryableAsync();
		var chapters = await _chapterRepository.GetQueryableAsync();
		// Progress has no TenantId; its owning chapter supplies the tenant boundary.
		return progress.Where(item => chapters.Any(chapter => chapter.Id == item.ChapterId));
	}
}
