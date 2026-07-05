using Bangumi.Win.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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
        using var request = CreateRequest(HttpMethod.Get, $"https://bgm.tv/user/{Uri.EscapeDataString(username)}/timeline");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        return ParseTimelineHtml(html, username);
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

    public async Task<PagedResponse<UserEpisodeCollection>> GetEpisodeCollectionsAsync(int subjectId, int offset = 0, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/v0/users/-/collections/{subjectId}/episodes?limit=1000&offset={offset}&episode_type=0");
        return await SendAsync<PagedResponse<UserEpisodeCollection>>(request, cancellationToken);
    }

    public async Task UpdateEpisodeCollectionsAsync(int subjectId, IReadOnlyList<int> episodeIds, int status, CancellationToken cancellationToken = default)
    {
        if (episodeIds.Count == 0)
        {
            return;
        }

        using var request = CreateRequest(HttpMethod.Patch, $"/v0/users/-/collections/{subjectId}/episodes");
        request.Content = JsonContent.Create(new { episode_id = episodeIds, type = status }, options: _jsonOptions);
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

    public async Task<PersonDetail> GetPersonAsync(int personId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/v0/persons/{personId}");
        return await SendAsync<PersonDetail>(request, cancellationToken);
    }

    public async Task CollectPersonAsync(int personId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/v0/persons/{personId}/collect");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(string keyword, int? type, int offset = 0, CancellationToken cancellationToken = default)
    {
        if (type == -1)
        {
            return await SearchPersonsAsync(keyword, offset, cancellationToken);
        }

        var filter = type is int subjectType ? new { type = new[] { subjectType } } : null;
        var body = new { keyword, sort = "match", filter };
        using var request = CreateRequest(HttpMethod.Post, $"/v0/search/subjects?limit=30&offset={offset}");
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

    private async Task<IReadOnlyList<SearchResultItem>> SearchPersonsAsync(string keyword, int offset, CancellationToken cancellationToken)
    {
        var body = new { keyword };
        using var request = CreateRequest(HttpMethod.Post, $"/v0/search/persons?limit=30&offset={offset}");
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

    private static IReadOnlyList<TimelineEntry> ParseTimelineHtml(string html, string username)
    {
        var entries = new List<TimelineEntry>();
        DateTimeOffset? currentDate = null;
        var timelineStart = html.IndexOf("id=\"timeline\"", StringComparison.OrdinalIgnoreCase);
        if (timelineStart >= 0)
        {
            html = html[timelineStart..];
        }

        foreach (Match match in Regex.Matches(html, @"<h4\s+class=""Header"">(?<date>.*?)</h4>|<li[^>]*class=""[^""]*tml_item[^""]*""[^>]*>(?<item>.*?)</li>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
        {
            if (match.Groups["date"].Success)
            {
                currentDate = TryParseTimelineDate(StripHtml(match.Groups["date"].Value));
                continue;
            }

            var itemHtml = match.Groups["item"].Value;
            var infoMatch = Regex.Match(itemHtml, @"<span[^>]*class=""[^""]*info_full[^""]*""[^>]*>(?<info>.*?)</span>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var info = infoMatch.Success ? infoMatch.Groups["info"].Value : itemHtml;
            var title = StripHtml(info);
            var detailMatch = Regex.Match(itemHtml, @"<p\s+class=""info\s+tip""[^>]*>(?<detail>.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var detail = detailMatch.Success ? StripHtml(detailMatch.Groups["detail"].Value) : string.Empty;
            var imageUrl = ExtractTimelineImageUrl(itemHtml);

            if (!string.IsNullOrWhiteSpace(title))
            {
                entries.Add(new TimelineEntry(title, detail, username, currentDate, imageUrl));
            }
        }

        return entries
            .OrderByDescending(item => item.CreatedAt)
            .Take(30)
            .ToList();
    }

    private static DateTimeOffset? TryParseTimelineDate(string value)
    {
        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed
            : null;
    }

    private static string StripHtml(string html)
    {
        var withoutTags = Regex.Replace(html, "<.*?>", " ", RegexOptions.Singleline);
        var decoded = WebUtility.HtmlDecode(withoutTags);
        return Regex.Replace(decoded, @"\s+", " ").Trim();
    }

    private static string ExtractTimelineImageUrl(string html)
    {
        var imageMatch = Regex.Match(html, @"<img[^>]+src=""(?<src>[^""]+)""", RegexOptions.IgnoreCase);
        if (!imageMatch.Success)
        {
            return string.Empty;
        }

        var src = WebUtility.HtmlDecode(imageMatch.Groups["src"].Value);
        return src.StartsWith("//", StringComparison.Ordinal)
            ? $"https:{src}"
            : src.StartsWith("/", StringComparison.Ordinal) ? $"https://bgm.tv{src}" : src;
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
