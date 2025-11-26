using System.Collections.Generic;

namespace ShikshaMitra.Constants;

public static class ShikshaMitraModulePermissions
{
    public const string Dashboard = "ShikshaMitra.Module.Dashboard";
    public const string Students = "ShikshaMitra.Module.Students";
    public const string Academics = "ShikshaMitra.Module.Academics";
    public const string ClassManagement = "ShikshaMitra.Module.ClassManagement";
    public const string HR = "ShikshaMitra.Module.HR";
    public const string Finance = "ShikshaMitra.Module.Finance";
    public const string Communication = "ShikshaMitra.Module.Communication";
    public const string Calendar = "ShikshaMitra.Module.Calender";
    public const string Library = "ShikshaMitra.Module.Library";
    public const string Transport = "ShikshaMitra.Module.Transport";
    public const string ResourceManagement = "ShikshaMitra.Module.ResourceManagement";
    public const string Hostel = "ShikshaMitra.Module.Hostel";
    public const string Security = "ShikshaMitra.Module.Security";
    public const string SecurityGuard = "ShikshaMitra.Module.SecurityGuard";
    public const string InstituteSetup = "ShikshaMitra.Module.InstituteSetup";

    public static readonly string[] All =
    {
        Dashboard,
        Students,
        Academics,
        ClassManagement,
        HR,
        Finance,
        Communication,
        Calendar,
        Library,
        Transport,
        ResourceManagement,
        Hostel,
        Security,
        SecurityGuard,
        InstituteSetup
    };

    public static readonly Dictionary<string, string> DisplayNames = new()
    {
        { Dashboard, "Permission:Module.Dashboard" },
        { Students, "Permission:Module.Students" },
        { Academics, "Permission:Module.Academics" },
        { ClassManagement, "Permission:Module.ClassManagement" },
        { HR, "Permission:Module.HR" },
        { Finance, "Permission:Module.Finance" },
        { Communication, "Permission:Module.Communication" },
        { Calendar, "Permission:Module.Calendar" },
        { Library, "Permission:Module.Library" },
        { Transport, "Permission:Module.Transport" },
        { ResourceManagement, "Permission:Module.ResourceManagement" },
        { Hostel, "Permission:Module.Hostel" },
        { Security, "Permission:Module.Security" },
        { SecurityGuard, "Permission:Module.SecurityGuard" },
        { InstituteSetup, "Permission:Module.InstituteSetup" }
    };
}
