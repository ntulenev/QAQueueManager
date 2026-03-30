namespace QAQueueManager.Models.Domain;

/// <summary>
/// Represents the stable identity of a repository inside the QA report domain.
/// </summary>
internal readonly record struct RepositoryRef(
    RepositoryFullName FullName,
    RepositorySlug Slug)
{
    /// <summary>
    /// Gets the fallback repository identity used when the repository cannot be resolved.
    /// </summary>
    public static RepositoryRef Unknown { get; } = new(RepositoryFullName.Unknown, RepositorySlug.Unknown);

    /// <summary>
    /// Creates a repository identity from the supplied full repository name.
    /// </summary>
    /// <param name="repositoryFullName">The repository full name.</param>
    /// <returns>The derived repository identity.</returns>
    public static RepositoryRef FromFullName(RepositoryFullName repositoryFullName)
    {
        return new RepositoryRef(
            repositoryFullName,
            RepositorySlug.FromRepositoryFullName(repositoryFullName));
    }

    /// <summary>
    /// Gets the repository full name.
    /// </summary>
    public RepositoryFullName RepositoryFullName => FullName;

    /// <summary>
    /// Gets the repository slug.
    /// </summary>
    public RepositorySlug RepositorySlug => Slug;

    /// <inheritdoc />
    public override string ToString() => FullName.Value;
}
