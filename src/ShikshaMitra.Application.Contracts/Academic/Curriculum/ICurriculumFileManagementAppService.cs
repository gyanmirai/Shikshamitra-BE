using System.IO;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Academic.Curriculum;

public interface ICurriculumFileManagementAppService : IApplicationService
{
	Task<string> UploadAsync(
		string originalFileName,
		string contentType,
		long size,
		Stream stream
	);
}
