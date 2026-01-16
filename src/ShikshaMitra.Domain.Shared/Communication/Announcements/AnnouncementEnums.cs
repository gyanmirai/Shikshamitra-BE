namespace ShikshaMitra
{
    public enum ImportanceLevel
    {
        Normal = 1,
        Urgent = 2,
        Critical = 3
    }

    public enum DurationUnit
    {
        Hour,
        Day
    }

    public enum AnnouncementStatus
    {
        Upcoming = 1,
        Published = 2,
        Completed = 3
    }

    public enum NotificationPreference
    {
        InApp,
        Email,
        Sms,
        Whatsapp
    }
}
