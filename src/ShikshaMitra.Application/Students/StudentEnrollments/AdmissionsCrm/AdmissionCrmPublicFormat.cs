using System.Text.Json;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmPublicFormat
{
	internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
