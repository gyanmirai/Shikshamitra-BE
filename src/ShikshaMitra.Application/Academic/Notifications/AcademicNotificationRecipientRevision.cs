using System;
using System.Security.Cryptography;
using System.Text.Json;

namespace ShikshaMitra.Academic.Notifications;

internal static class AcademicNotificationRecipientRevision
{
	public static string ForStaff(AcademicNotificationStaff staff) =>
		Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(staff)));
}
