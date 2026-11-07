namespace Reviews.Infrastructure.Persistence;
public static class MongoCollections
{
    public const string Reviews = "reviews";
    public const string ReviewReports = "reviewReports";
    public const string ProductRatingSummaries = "productRatingSummaries";
    public const string ReviewEligibility = "reviewEligibility";
    public const string Outbox = "outbox";
    public const string Inbox = "inbox";
}