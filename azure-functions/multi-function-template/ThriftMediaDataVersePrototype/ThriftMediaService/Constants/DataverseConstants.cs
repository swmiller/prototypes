namespace ThriftMediaService.Constants;

public static class DataverseConstants
{
    public static class Tables
    {
        public const string Store = "cr1b3_store";
        public const string Media = "cr1b3_media";
    }

    public static class StoreColumns
    {
        public const string Name = "cr1b3_storename";
        public const string Address = "cr1b3_address";
        public const string City = "cr1b3_city";
        public const string State = "cr1b3_state";
        public const string ZipCode = "cr1b3_zipcode";
        public const string Phone = "cr1b3_phone";
    }

    public static class MediaColumns
    {
        public const string Title = "cr1b3_title";
        public const string Description = "cr1b3_description";
        public const string MediaType = "cr1b3_mediatype";
        public const string Url = "cr1b3_url";
        public const string StoreId = "cr1b3_storeid";
    }

    public static class CommonColumns
    {
        public const string CreatedOn = "createdon";
        public const string ModifiedOn = "modifiedon";
    }
}
