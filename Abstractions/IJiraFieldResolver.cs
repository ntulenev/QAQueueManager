namespace QAQueueManager.Abstractions;

/// <summary>
/// Resolves configured Jira field aliases to concrete API field names.
/// </summary>
internal interface IJiraFieldResolver
{
    /// <summary>
    /// Resolves a required configured Jira field.
    /// </summary>
    /// <param name="configuredField">The configured field alias or API field name.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The resolved API field name.</returns>
    Task<string> ResolveRequiredFieldAsync(string configuredField, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves zero or more optional configured Jira fields.
    /// </summary>
    /// <param name="configuredFields">The configured field aliases separated by commas or semicolons.</param>
    /// <param name="cancellationToken">The cancellation token for the operation.</param>
    /// <returns>The resolved API field names.</returns>
    Task<IReadOnlyList<string>> ResolveOptionalFieldsAsync(string? configuredFields, CancellationToken cancellationToken);
}
