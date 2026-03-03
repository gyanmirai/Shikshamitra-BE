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
	public async Task<string> UploadAsync(string fileName, byte[] fileBytes)
	{
		var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

		if (!Directory.Exists(uploadFolder))
		{
			Directory.CreateDirectory(uploadFolder);
		}

		var filePath = Path.Combine(uploadFolder, fileName);

		await File.WriteAllBytesAsync(filePath, fileBytes);

		return $"/uploads/{fileName}";
	}


}
