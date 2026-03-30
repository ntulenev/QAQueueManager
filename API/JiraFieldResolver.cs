using QAQueueManager.Abstractions;
using QAQueueManager.Transport;

namespace QAQueueManager.API;

/// <summary>
/// Resolves configured Jira field aliases to concrete API field names.
/// </summary>
internal sealed class JiraFieldResolver : IJiraFieldResolver
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JiraFieldResolver"/> class.
    /// </summary>
    /// <param name="transport">The Jira transport.</param>
    public JiraFieldResolver(JiraTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    /// <inheritdoc />
    public async Task<string> ResolveRequiredFieldAsync(string configuredField, CancellationToken cancellationToken)
    {
        var fields = await ResolveFieldsAsync(configuredField, cancellationToken).ConfigureAwait(false);
        return fields.Count == 0
            ? throw new InvalidOperationException($"Unable to resolve Jira field '{configuredField}'.")
            : fields[0];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ResolveOptionalFieldsAsync(
        string? configuredFields,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuredFields))
        {
            return [];
        }

        var result = new List<string>();
        var aliases = configuredFields
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var alias in aliases)
        {
            var resolvedFields = await ResolveFieldsAsync(alias, cancellationToken).ConfigureAwait(false);
            foreach (var field in resolvedFields)
            {
                if (!result.Contains(field, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(field);
                }
            }
        }

        return result;
    }

    private async Task<IReadOnlyList<string>> ResolveFieldsAsync(
        string configuredField,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredField);

        var trimmedField = configuredField.Trim();
        var alias = NormalizeAlias(trimmedField);
        if (_resolvedFields.TryGetValue(alias, out var cachedFields))
        {
            return cachedFields;
        }

        if (trimmedField.StartsWith(CUSTOM_FIELD_PREFIX, StringComparison.OrdinalIgnoreCase))
        {
            _resolvedFields[alias] = [trimmedField];
            return _resolvedFields[alias];
        }

        var lookup = await GetFieldLookupAsync(cancellationToken).ConfigureAwait(false);
        if (!lookup.TryGetValue(alias, out var apiFields) || apiFields.Count == 0)
        {
            throw new InvalidOperationException($"Unable to resolve Jira field '{configuredField}'.");
        }

        _resolvedFields[alias] = apiFields;
        return apiFields;
    }

    private async Task<Dictionary<string, IReadOnlyList<string>>> GetFieldLookupAsync(CancellationToken cancellationToken)
    {
        if (_fieldLookup is not null)
        {
            return _fieldLookup;
        }

        var fields = await _transport
            .GetAsync<List<JiraFieldDefinitionResponse>>(new Uri("rest/api/3/field", UriKind.Relative), cancellationToken)
            .ConfigureAwait(false)
            ?? [];

        _fieldLookup = BuildFieldLookup(fields);
        return _fieldLookup;
    }

    private static string NormalizeAlias(string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);

        var value = alias.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1].Trim();
        }

        return value;
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildFieldLookup(
        IEnumerable<JiraFieldDefinitionResponse> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var field in fields)
        {
            var apiField = !string.IsNullOrWhiteSpace(field.Key)
                ? field.Key.Trim()
                : field.Id?.Trim();
            if (string.IsNullOrWhiteSpace(apiField))
            {
                continue;
            }

            AddAlias(result, field.Id, apiField);
            AddAlias(result, field.Key, apiField);
            AddAlias(result, field.Name, apiField);

            foreach (var clauseName in field.ClauseNames)
            {
                AddAlias(result, clauseName, apiField);
            }
        }

        return result.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static void AddAlias(Dictionary<string, List<string>> lookup, string? alias, string apiField)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return;
        }

        var normalizedAlias = NormalizeAlias(alias);
        if (!lookup.TryGetValue(normalizedAlias, out var fields))
        {
            fields = [];
            lookup[normalizedAlias] = fields;
        }

        if (!fields.Contains(apiField, StringComparer.OrdinalIgnoreCase))
        {
            fields.Add(apiField);
        }
    }

    private readonly JiraTransport _transport;
    private Dictionary<string, IReadOnlyList<string>>? _fieldLookup;
    private readonly Dictionary<string, IReadOnlyList<string>> _resolvedFields = new(StringComparer.OrdinalIgnoreCase);
    private const string CUSTOM_FIELD_PREFIX = "customfield_";
}
