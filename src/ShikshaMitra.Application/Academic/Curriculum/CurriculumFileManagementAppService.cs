using System;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;

namespace ShikshaMitra.Academic.Curriculum;

public class CurriculumFileManagementAppService(
	IBlobContainer blobContainer
) : ApplicationService, ICurriculumFileManagementAppService
{
	public async Task<string> UploadAsync(string originalFileName, string contentType, long size, Stream stream)
	{
		var blobName = $"{Guid.NewGuid()}{Path.GetExtension(originalFileName)}";

		await blobContainer.SaveAsync(blobName, stream);

		return blobName;
	}
}
