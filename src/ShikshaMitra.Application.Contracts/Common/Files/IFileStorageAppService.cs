using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Common.Files;

public interface IFileStorageAppService : IApplicationService
{
	/// <summary>
	/// Uploads a file and returns the unique identifier (e.g. "a3f7be12.png")
	/// that the caller should store and later pass to <see cref="GetAsync"/>.
	/// </summary>
	Task<string> UploadAsync(string originalFileName, byte[] fileBytes);

	/// <summary>
	/// Retrieves a previously uploaded file by its unique identifier.
	/// </summary>
	Task<FileDownloadResult> GetAsync(string uniqueFileName);
}
