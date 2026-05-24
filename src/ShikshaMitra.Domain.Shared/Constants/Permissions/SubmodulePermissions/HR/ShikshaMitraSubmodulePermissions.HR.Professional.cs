using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
public static partial class HR
{
public static class ProfessionalManagement
{
private const string SubmoduleName = $"{ModuleName}.ProfessionalManagement";
private const string SubmoduleDisplayName = $"{DisplayName}.ProfessionalManagement";

public const string View = $"{SubmoduleName}.View";
public const string Add = $"{SubmoduleName}.Add";

public static readonly string[] All = { View, Add };
public static readonly Dictionary<string, string> DisplayNames = new()
{
{ SubmoduleName, SubmoduleDisplayName },
{ View, $"{SubmoduleDisplayName}.View" },
{ Add, $"{SubmoduleDisplayName}.Add" }
};
}
}
}
