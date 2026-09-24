namespace MajdsApp.Modules.Files;

/// <summary>Uploading needs Files.Upload; viewing/downloading others' files needs Files.View and deleting
/// others' files needs Files.Delete. Owners can always download and delete their own files.</summary>
public static class Permissions
{
    public static class Files
    {
        public const string Upload = "Files.Upload";
        public const string View = "Files.View";
        public const string Delete = "Files.Delete";
    }
}
