namespace Shenok.Back.ContentService.Infrastructure.Messaging;

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public bool Enabled { get; set; }

    public int PollIntervalSeconds { get; set; } = 1;

    public int BatchSize { get; set; } = 20;

    public static bool IsValid(MessagingOptions options) =>
        options.PollIntervalSeconds > 0 && options.BatchSize > 0;
}
