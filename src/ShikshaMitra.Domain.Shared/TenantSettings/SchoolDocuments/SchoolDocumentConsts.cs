namespace ShikshaMitra.TenantSettings.SchoolDocuments;

public static class SchoolDocumentConsts
{
	public const int MaxFiles = 5;
	public const int MaxFileBytes = 5 * 1024 * 1024;
	public const int MaxNameLength = 200;
	public const int MaxNotesLength = 2000;
	public const string InvalidDetails = "ShikshaMitra:SchoolDocuments:InvalidDetails";
	public const string InvalidFiles = "ShikshaMitra:SchoolDocuments:InvalidFiles";
	public const string InvalidFile = "ShikshaMitra:SchoolDocuments:InvalidFile";
}

public enum SchoolDocumentCategory
{
	GovernmentApproval,
	PermissionNoc,
	BoardAffiliation,
	Registration,
	Other
}

public static class SchoolDocumentPermissions
{
	public const string Navigation = "ShikshaMitra.Module.InstituteSetup.SchoolDocuments";
	public const string View = "InstituteSetup.SchoolDocuments.View";
	public const string Add = "InstituteSetup.SchoolDocuments.Add";
	public const string Delete = "InstituteSetup.SchoolDocuments.Delete";
}
