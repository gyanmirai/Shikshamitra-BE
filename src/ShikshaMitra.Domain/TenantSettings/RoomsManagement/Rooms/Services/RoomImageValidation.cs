using System.IO;
using ShikshaMitra.Common;
using Volo.Abp;

namespace ShikshaMitra.TenantSettings.RoomsManagement.Rooms;

public static class RoomImageValidation
{
	public static string Validate(string fileName, byte[] content)
	{
		var extension = Path.GetExtension(fileName).ToLowerInvariant();
		if (extension is not (".jpg" or ".jpeg" or ".png"))
			throw new UserFriendlyException("Room image must be a JPG or PNG file up to 5 MB.");
		FileValidationService.ValidateProfilePictureUpload(fileName, content);
		FileValidationService.ValidateDocumentFileSignature(content, extension);
		return extension == ".png" ? "image/png" : "image/jpeg";
	}
}
