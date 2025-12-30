using System;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;

namespace ShikshaMitra.Academic.Curriculum;

[RemoteService(false)]
public class CurriculumFileManagementAppService(
	IBlobContainer blobContainer
) : ApplicationService, ICurriculumFileManagementAppService
{
	public async Task<string> UploadAsync(string originalFileName, Stream stream)
	{
		var blobName = $"{Guid.NewGuid()}{Path.GetExtension(originalFileName)}";

		await blobContainer.SaveAsync(blobName, stream);

		return blobName;
	}
}
