using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

[RemoteService(false)]
public class PayslipLogoService(IBlobContainer blobs, IUserScopeService scopes) : ApplicationService
{
	public const int MaxBytes = 2 * 1024 * 1024;
	public async Task<string> UploadAsync(string contentType, byte[] bytes)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add);
		if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue || !HrUserScopeAccess.HasHrWideAccess(await scopes.GetUserScopeAsync(CurrentUser.Id.Value)))
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		ValidateImage(contentType, bytes);
		var name = $"payroll-logos/{CurrentTenant.Id}/{GuidGenerator.Create():N}";
		await blobs.SaveAsync(name, bytes, overrideExisting: false);
		return name;
	}
	public async Task<byte[]> GetAsync(string name)
	{
		new SalaryDocumentPresentation { LogoBlobName = name }.Validate(CurrentTenant.Id);
		if (!await blobs.ExistsAsync(name)) throw new BusinessException("HR:Payslip:LogoInvalid");
		try
		{
			await using var stream = await blobs.GetAsync(name);
			using var memory = new MemoryStream();
			await stream.CopyToAsync(memory);
			var bytes = memory.ToArray();
			ValidateImage(bytes.Length > 0 && bytes[0] == 0x89 ? "image/png" : "image/jpeg", bytes);
			return bytes;
		}
		catch (FileNotFoundException) { throw new BusinessException("HR:Payslip:LogoInvalid"); }
	}
	internal static void ValidateImage(string type, byte[] bytes)
	{
		if (bytes.Length is < 24 or > MaxBytes) throw new BusinessException("HR:Payslip:LogoInvalid");
		int width = 0, height = 0;
		if (type == "image/png" && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
		{
			width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
			height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
		}
		else if (type == "image/jpeg" && bytes[0] == 0xff && bytes[1] == 0xd8)
		{
			for (var offset = 2; offset + 9 < bytes.Length;)
			{
				if (bytes[offset++] != 0xff) break;
				while (offset < bytes.Length && bytes[offset] == 0xff) offset++;
				if (offset + 2 >= bytes.Length) break;
				var marker = bytes[offset++];
				if (marker is 0xd9 or 0xda) break;
				if (marker is 0x01 or >= 0xd0 and <= 0xd7) continue;
				var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
				if (length < 2 || offset + length > bytes.Length) break;
				if (marker is 0xc0 or 0xc1 or 0xc2 && length >= 8)
				{
					height = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 3, 2));
					width = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset + 5, 2));
					break;
				}
				offset += length;
			}
		}
		if (width <= 0 || height <= 0 || width > 4000 || height > 4000 || (long)width * height > 4_000_000)
			throw new BusinessException("HR:Payslip:LogoInvalid");
		try { using var image = QuestPDF.Infrastructure.Image.FromBinaryData(bytes); }
		catch (Exception) { throw new BusinessException("HR:Payslip:LogoInvalid"); }
	}
}
