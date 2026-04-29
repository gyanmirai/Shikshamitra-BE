using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static partial class ShikshaMitraSubmodulePermissions
{
    public static partial class HR
    {
        public static class LeaveManagement_LeaveCenter
        {
            private const string SubmoduleName = $"{ModuleName}.LeaveManagement.LeaveCenter";
            private const string SubmoduleDisplayName = $"{DisplayName}.LeaveManagement.LeaveCenter";

            public const string View = $"{SubmoduleName}.View";
            public const string Add = $"{SubmoduleName}.Add";
            public const string Delete = $"{SubmoduleName}.Delete";

            public static readonly string[] All = { View, Add, Delete };
            public static readonly Dictionary<string, string> DisplayNames = new()
            {
                { SubmoduleName, SubmoduleDisplayName },
                { View, $"{SubmoduleDisplayName}.View" },
                { Add, $"{SubmoduleDisplayName}.Add" },
                { Delete, $"{SubmoduleDisplayName}.Delete" }
            };
        }

        public static class LeaveManagement_SettingLeaveType
        {
            private const string SubmoduleName = $"{ModuleName}.LeaveManagement.Setting.LeaveType";
            private const string SubmoduleDisplayName = $"{DisplayName}.LeaveManagement.Setting.LeaveType";

            public const string View = $"{SubmoduleName}.View";
            public const string Add = $"{SubmoduleName}.Add";
            public const string Delete = $"{SubmoduleName}.Delete";

            public static readonly string[] All = { View, Add, Delete };
            public static readonly Dictionary<string, string> DisplayNames = new()
            {
                { SubmoduleName, SubmoduleDisplayName },
                { View, $"{SubmoduleDisplayName}.View" },
                { Add, $"{SubmoduleDisplayName}.Add" },
                { Delete, $"{SubmoduleDisplayName}.Delete" }
            };
        }

        public static class LeaveManagement_SettingLeavePolicy
        {
            private const string SubmoduleName = $"{ModuleName}.LeaveManagement.Setting.LeavePolicy";
            private const string SubmoduleDisplayName = $"{DisplayName}.LeaveManagement.Setting.LeavePolicy";

            public const string View = $"{SubmoduleName}.View";
            public const string Add = $"{SubmoduleName}.Add";
            public const string Delete = $"{SubmoduleName}.Delete";

            public static readonly string[] All = { View, Add, Delete };
            public static readonly Dictionary<string, string> DisplayNames = new()
            {
                { SubmoduleName, SubmoduleDisplayName },
                { View, $"{SubmoduleDisplayName}.View" },
                { Add, $"{SubmoduleDisplayName}.Add" },
                { Delete, $"{SubmoduleDisplayName}.Delete" }
            };
        }
    }
}
