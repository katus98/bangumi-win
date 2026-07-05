using Bangumi.Win.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Bangumi.Win.Services;

public sealed class BangumiApiClient
{
    private static readonly Uri BaseUri = new("https://api.bgm.tv");
    private readonly HttpClient _httpClient;
    private readonly TokenStore _tokenStore;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public BangumiApiClient(TokenStore tokenStore)
    {
        _tokenStore = tokenStore;
        _httpClient = new HttpClient { BaseAddress = BaseUri };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Bangumi.Win/0.1.0 (https://github.com/katus/bangumi-win)");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<BangumiUser> GetMeAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "/v0/me");
        return await SendAsync<BangumiUser>(request, cancellationToken);
    }

    public async Task<IReadOnlyList<TimelineEntry>> GetTimelineAsync(string username, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/v0/users/{Uri.EscapeDataString(username)}/timeline?limit=30");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var array = document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement
            : document.RootElement.TryGetProperty("data", out var data) ? data : default;

        if (array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Select(ParseTimelineEntry)
            .OrderByDescending(item => item.CreatedAt)
            .ToList();
    }

    public async Task<PagedResponse<SubjectCollection>> GetCollectionsAsync(string username, int? subjectType, int? collectionType, int offset, CancellationToken cancellationToken = default)
    {
        var query = new List<string> { "limit=30", $"offset={offset}" };
        if (subjectType is int st)
        {
            query.Add($"subject_type={st}");
        }

        if (collectionType is int ct)
        {
            query.Add($"type={ct}");
        }

        using var request = CreateRequest(HttpMethod.Get, $"/v0/users/{Uri.EscapeDataString(username)}/collections?{string.Join("&", query)}");
        return await SendAsync<PagedResponse<SubjectCollection>>(request, cancellationToken);
    }

    public async Task UpdateCollectionAsync(int subjectId, int status, int? epStatus, int? volStatus, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = status,
            ["ep_status"] = epStatus,
            ["vol_status"] = volStatus
        };

        using var request = CreateRequest(HttpMethod.Patch, $"/v0/users/-/collections/{subjectId}");
        request.Content = JsonContent.Create(body, options: _jsonOptions);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task AddCollectionAsync(int subjectId, int status = 3, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/v0/users/-/collections/{subjectId}");
        request.Content = JsonContent.Create(new { type = status }, options: _jsonOptions);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<SubjectSummary> GetSubjectAsync(int subjectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/v0/subjects/{subjectId}");
        return await SendAsync<SubjectSummary>(request, cancellationToken);
    }

    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(string keyword, int? type, CancellationToken cancellationToken = default)
    {
        if (type == -1)
        {
            return await SearchPersonsAsync(keyword, cancellationToken);
        }

        var filter = type is int subjectType ? new { type = new[] { subjectType } } : null;
        var body = new { keyword, sort = "match", filter };
        using var request = CreateRequest(HttpMethod.Post, "/v0/search/subjects?limit=30&offset=0");
        request.Content = JsonContent.Create(body, options: _jsonOptions);

        var result = await SendAsync<PagedResponse<SubjectSummary>>(request, cancellationToken);
        return result.Data.Select(subject => new SearchResultItem(
            subject.Id,
            subject.DisplayName,
            subject.Subtitle,
            subject.Summary ?? string.Empty,
            subject.ImageUrl,
            subject.Type,
            false)).ToList();
    }

    private async Task<IReadOnlyList<SearchResultItem>> SearchPersonsAsync(string keyword, CancellationToken cancellationToken)
    {
        var body = new { keyword };
        using var request = CreateRequest(HttpMethod.Post, "/v0/search/persons?limit=30&offset=0");
        request.Content = JsonContent.Create(body, options: _jsonOptions);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return data.EnumerateArray().Select(person =>
        {
            var id = GetInt(person, "id") ?? 0;
            var name = GetString(person, "name") ?? "未命名人物";
            var career = person.TryGetProperty("career", out var c) && c.ValueKind == JsonValueKind.Array
                ? string.Join(" / ", c.EnumerateArray().Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item)))
                : "人物";
            var summary = GetString(person, "summary") ?? string.Empty;
            var image = person.TryGetProperty("images", out var images) ? GetString(images, "medium") ?? GetString(images, "grid") ?? string.Empty : string.Empty;
            return new SearchResultItem(id, name, career, summary, image, null, true);
        }).ToList();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        if (_tokenStore.AccessToken is { Length: > 0 } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Bangumi API returned an empty response.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException($"Bangumi API request failed: {(int)response.StatusCode} {response.ReasonPhrase}. {detail}");
    }

    private static TimelineEntry ParseTimelineEntry(JsonElement item)
    {
        var title = GetString(item, "title")
            ?? GetString(item, "message")
            ?? GetString(item, "content")
            ?? "时间胶囊动态";
        var detail = GetString(item, "desc")
            ?? GetString(item, "detail")
            ?? (item.TryGetProperty("data", out var data) ? data.ToString() : string.Empty);
        var userName = item.TryGetProperty("user", out var user)
            ? GetString(user, "nickname") ?? GetString(user, "username") ?? string.Empty
            : string.Empty;
        var createdAt = GetDate(item, "created_at") ?? GetDate(item, "created") ?? GetUnixDate(item, "timestamp");

        return new TimelineEntry(title, detail, userName, createdAt);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static DateTimeOffset? GetDate(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(property.GetString(), out var value)
                ? value
                : null;
    }

    private static DateTimeOffset? GetUnixDate(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt64(out var value)
            ? DateTimeOffset.FromUnixTimeSeconds(value)
            : null;
    }
}
