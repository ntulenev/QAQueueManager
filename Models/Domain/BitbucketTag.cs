namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents a normalized Bitbucket tag.
/// </summary>
/// <param name="Name">The tag name.</param>
/// <param name="TargetHash">The target commit hash.</param>
/// <param name="TaggedOn">The tag timestamp.</param>
internal sealed record BitbucketTag(
    string Name,
    string TargetHash,
    DateTimeOffset? TaggedOn);
