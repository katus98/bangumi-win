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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bangumi.Win.Services;

public sealed class BangumiApiClient
{
    private static readonly Uri BaseUri = new("https://api.bgm.tv");
    private static readonly string UserAgent = $"Bangumi.Win/{typeof(BangumiApiClient).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"} (https://github.com/katus98/bangumi-win)";
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
        _httpClient = new HttpClient
        {
            BaseAddress = BaseUri,
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<BangumiUser> GetMeAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "/v0/me", requiresAuthentication: true);
        return await SendAsync<BangumiUser>(request, cancellationToken);
    }

    public async Task<BangumiUser> GetMeAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "/v0/me", requiresAuthentication: true, accessToken);
        return await SendAsync<BangumiUser>(request, cancellationToken);
    }

    public async Task<IReadOnlyList<TimelineEntry>> GetTimelineAsync(string username, int page = 1, CancellationToken cancellationToken = default)
    {
        var path = page <= 1
            ? $"https://bgm.tv/user/{Uri.EscapeDataString(username)}/timeline"
            : $"https://bgm.tv/user/{Uri.EscapeDataString(username)}/timeline?page={page}";
        using var request = CreateRequest(HttpMethod.Get, path);
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

        using var request = CreateRequest(HttpMethod.Get, $"/v0/users/{Uri.EscapeDataString(username)}/collections?{string.Join("&", query)}", requiresAuthentication: true);
        return await SendAsync<PagedResponse<SubjectCollection>>(request, cancellationToken);
    }

    public async Task<SubjectCollection> GetCollectionAsync(string username, int subjectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/v0/users/{Uri.EscapeDataString(username)}/collections/{subjectId}", requiresAuthentication: true);
        return await SendAsync<SubjectCollection>(request, cancellationToken);
    }

    public async Task UpdateCollectionAsync(int subjectId, int status, int? epStatus, int? volStatus, CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["type"] = status
        };

        if (epStatus is not null)
        {
            body["ep_status"] = epStatus;
        }

        if (volStatus is not null)
        {
            body["vol_status"] = volStatus;
        }

        using var request = CreateRequest(HttpMethod.Post, $"/v0/users/-/collections/{subjectId}", requiresAuthentication: true);
        request.Content = CreateJsonContent(body);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<PagedResponse<UserEpisodeCollection>> GetEpisodeCollectionsAsync(int subjectId, int offset = 0, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/v0/users/-/collections/{subjectId}/episodes?limit=1000&offset={offset}&episode_type=0", requiresAuthentication: true);
        return await SendAsync<PagedResponse<UserEpisodeCollection>>(request, cancellationToken);
    }

    public async Task UpdateEpisodeCollectionsAsync(int subjectId, IReadOnlyList<int> episodeIds, int status, CancellationToken cancellationToken = default)
    {
        if (episodeIds.Count == 0)
        {
            return;
        }

        using var request = CreateRequest(HttpMethod.Patch, $"/v0/users/-/collections/{subjectId}/episodes", requiresAuthentication: true);
        request.Content = CreateJsonContent(new { episode_id = episodeIds, type = status });
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteCollectionAsync(int subjectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/v0/users/-/collections/{subjectId}", requiresAuthentication: true);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<SubjectSummary> GetSubjectAsync(int subjectId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/v0/subjects/{subjectId}");
        return await SendAsync<SubjectSummary>(request, cancellationToken);
    }

    public async Task<PagedResponse<SubjectComment>> GetSubjectCommentsAsync(int subjectId, int offset = 0, CancellationToken cancellationToken = default)
    {
        const int limit = 20;
        var page = Math.Max(1, (offset / limit) + 1);
        var path = page <= 1
            ? $"https://bgm.tv/subject/{subjectId}/comments"
            : $"https://bgm.tv/subject/{subjectId}/comments?page={page}";
        using var request = CreateRequest(HttpMethod.Get, path);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var comments = ParseSubjectCommentsHtml(html);
        var hasMore = comments.Count >= limit;
        var total = offset + comments.Count + (hasMore ? limit : 0);
        return new PagedResponse<SubjectComment>(comments, total, limit, offset);
    }

    public async Task<PersonDetail> GetPersonAsync(int personId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/v0/persons/{personId}");
        return await SendAsync<PersonDetail>(request, cancellationToken);
    }

    public async Task CollectPersonAsync(int personId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/v0/persons/{personId}/collect", requiresAuthentication: true);
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
        using var request = CreateRequest(HttpMethod.Post, $"/v0/search/subjects?limit=20&offset={offset}");
        request.Content = JsonContent.Create(body, options: _jsonOptions);

        var result = await SendAsync<PagedResponse<SubjectSummary>>(request, cancellationToken);
        return result.Data.Select(subject => new SearchResultItem(
            subject.Id,
            subject.DisplayName,
            subject.Subtitle,
            subject.Summary ?? string.Empty,
            subject.ImageUrl,
            subject.Type,
            false,
            subject.Nsfw)).ToList();
    }

    private async Task<IReadOnlyList<SearchResultItem>> SearchPersonsAsync(string keyword, int offset, CancellationToken cancellationToken)
    {
        var body = new { keyword };
        using var request = CreateRequest(HttpMethod.Post, $"/v0/search/persons?limit=20&offset={offset}");
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
            return new SearchResultItem(id, name, career, summary, image, null, true, false);
        }).ToList();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, bool requiresAuthentication = false, string? accessToken = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (requiresAuthentication && (accessToken ?? _tokenStore.AccessToken) is { Length: > 0 } token)
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

    private StringContent CreateJsonContent<T>(T value)
    {
        var content = new StringContent(JsonSerializer.Serialize(value, _jsonOptions), Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var detail = TryGetErrorDetail(body);
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "登录已失效，请重新登录。",
            HttpStatusCode.Forbidden => "当前账号没有执行此操作的权限。",
            HttpStatusCode.NotFound => "请求的内容不存在。",
            HttpStatusCode.TooManyRequests => "请求过于频繁，请稍后重试。",
            >= HttpStatusCode.InternalServerError => $"Bangumi 服务暂时不可用（{(int)response.StatusCode}）。",
            _ => $"Bangumi 请求失败（{(int)response.StatusCode}）。"
        };

        if (!string.IsNullOrWhiteSpace(detail)
            && response.StatusCode is not HttpStatusCode.Unauthorized
            && response.StatusCode is not HttpStatusCode.Forbidden)
        {
            message = $"{message} {detail}";
        }

        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private static string? TryGetErrorDetail(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || body.AsSpan().TrimStart().StartsWith("<"))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            foreach (var propertyName in new[] { "description", "message", "error" })
            {
                if (document.RootElement.TryGetProperty(propertyName, out var value)
                    && value.ValueKind == JsonValueKind.String
                    && value.GetString() is { Length: > 0 } detail)
                {
                    detail = detail.Trim();
                    return detail.Length <= 160 ? detail : $"{detail[..157]}...";
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
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

        foreach (Match match in Regex.Matches(html, @"<h4[^>]*class=""[^""]*Header[^""]*""[^>]*>(?<date>.*?)</h4>|<li[^>]*class=""[^""]*tml_item[^""]*""[^>]*>(?<item>.*?)</li>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
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
            var subjectId = ExtractTimelineSubjectId(itemHtml);
            var createdAt = ExtractTimelineCreatedAt(itemHtml) ?? currentDate;

            if (!string.IsNullOrWhiteSpace(title))
            {
                entries.Add(new TimelineEntry(title, detail, username, createdAt, imageUrl, subjectId));
            }
        }

        return entries
            .OrderByDescending(item => item.CreatedAt ?? DateTimeOffset.MinValue)
            .Take(30)
            .ToList();
    }

    private static DateTimeOffset? TryParseTimelineDate(string value)
    {
        if (value.Contains("今天", StringComparison.Ordinal))
        {
            return DateTimeOffset.Now.Date;
        }

        if (value.Contains("昨天", StringComparison.Ordinal))
        {
            return DateTimeOffset.Now.Date.AddDays(-1);
        }

        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed;
        }

        var match = Regex.Match(value, @"(?<year>\d{4})\D+(?<month>\d{1,2})\D+(?<day>\d{1,2})");
        if (!match.Success)
        {
            return null;
        }

        var year = int.Parse(match.Groups["year"].Value);
        var month = int.Parse(match.Groups["month"].Value);
        var day = int.Parse(match.Groups["day"].Value);
        return new DateTimeOffset(year, month, day, 0, 0, 0, DateTimeOffset.Now.Offset);
    }

    private static List<SubjectComment> ParseSubjectCommentsHtml(string html)
    {
        var comments = new List<SubjectComment>();
        foreach (Match match in Regex.Matches(html, @"<div\s+class=""item\s+clearit""[\s\S]*?(?=<div\s+class=""item\s+clearit""|<div\s+id=""footer""|$)", RegexOptions.IgnoreCase))
        {
            var itemHtml = match.Value;
            var commentMatch = Regex.Match(itemHtml, @"<p\s+class=""comment""[^>]*>(?<comment>[\s\S]*?)</p>", RegexOptions.IgnoreCase);
            if (!commentMatch.Success)
            {
                continue;
            }

            var userMatch = Regex.Match(itemHtml, @"<a\s+href=""/user/[^""]+""\s+class=""l""[^>]*>(?<user>[\s\S]*?)</a>", RegexOptions.IgnoreCase);
            var dateMatch = Regex.Match(itemHtml, @"<small\s+class=""grey"">\s*@\s*(?<date>.*?)</small>", RegexOptions.IgnoreCase);
            var rateMatch = Regex.Match(itemHtml, @"stars(?<rate>\d+)", RegexOptions.IgnoreCase);
            var sourceIdMatch = Regex.Match(itemHtml, @"\bid=""(?<id>(?:post_)?\d+)""", RegexOptions.IgnoreCase);
            var rate = rateMatch.Success && int.TryParse(rateMatch.Groups["rate"].Value, out var parsedRate) ? parsedRate : (int?)null;
            var createdAt = dateMatch.Success && DateTimeOffset.TryParse(StripHtml(dateMatch.Groups["date"].Value), out var parsedDate)
                ? parsedDate
                : (DateTimeOffset?)null;
            comments.Add(new SubjectComment(
                userMatch.Success ? StripHtml(userMatch.Groups["user"].Value) : string.Empty,
                StripHtml(commentMatch.Groups["comment"].Value),
                createdAt,
                rate,
                sourceIdMatch.Success ? sourceIdMatch.Groups["id"].Value : string.Empty));
        }

        return comments;
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

    private static int? ExtractTimelineSubjectId(string html)
    {
        var dataMatch = Regex.Match(html, @"data-subject-id=""(?<id>\d+)""", RegexOptions.IgnoreCase);
        if (dataMatch.Success && int.TryParse(dataMatch.Groups["id"].Value, out var dataId))
        {
            return dataId;
        }

        var hrefMatch = Regex.Match(html, @"/subject/(?<id>\d+)", RegexOptions.IgnoreCase);
        return hrefMatch.Success && int.TryParse(hrefMatch.Groups["id"].Value, out var hrefId) ? hrefId : null;
    }

    private static DateTimeOffset? ExtractTimelineCreatedAt(string html)
    {
        var match = Regex.Match(html, @"<span[^>]+title=""(?<date>\d{4}[-/]\d{1,2}[-/]\d{1,2}\s+\d{1,2}:\d{2})""[^>]*class=""[^""]*titleTip[^""]*""", RegexOptions.IgnoreCase);
        return match.Success && DateTimeOffset.TryParse(match.Groups["date"].Value, out var parsed)
            ? parsed
            : null;
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
