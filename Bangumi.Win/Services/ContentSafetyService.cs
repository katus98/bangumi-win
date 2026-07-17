using Bangumi.Win.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace Bangumi.Win.Services;

public sealed class ContentSafetyService
{
    private const string HiddenContentKey = "HiddenContentKeys";
    private const int MaximumHiddenItems = 100;
    private static readonly Uri ReportBaseUri = new("https://github.com/katus98/bangumi-win/issues/new");
    private readonly BangumiApiClient _apiClient;
    private readonly AppSettings _settings;
    private readonly ConcurrentDictionary<int, bool> _subjectNsfwCache = new();
    private readonly LinkedList<string> _hiddenContent = [];
    private readonly HashSet<string> _hiddenContentSet = new(StringComparer.Ordinal);

    public ContentSafetyService(BangumiApiClient apiClient, AppSettings settings)
    {
        _apiClient = apiClient;
        _settings = settings;
        LoadHiddenContent();
    }

    public event EventHandler? HiddenContentChanged;

    public int HiddenContentCount => _hiddenContentSet.Count;

    public bool IsSubjectVisible(SubjectSummary subject) => _settings.ShowNsfwContent || !subject.Nsfw;

    public bool IsSearchResultVisible(SearchResultItem item) => item.IsPerson || _settings.ShowNsfwContent || !item.IsNsfw;

    public bool IsCommentHidden(int subjectId, SubjectComment comment) => IsHidden(GetCommentKey(subjectId, comment));

    public bool IsTimelineEntryHidden(TimelineEntry entry) => IsHidden(GetTimelineKey(entry));

    public void HideComment(int subjectId, SubjectComment comment) => Hide(GetCommentKey(subjectId, comment));

    public void HideTimelineEntry(TimelineEntry entry) => Hide(GetTimelineKey(entry));

    public void ClearHiddenContent()
    {
        if (_hiddenContentSet.Count == 0)
        {
            return;
        }

        _hiddenContent.Clear();
        _hiddenContentSet.Clear();
        ApplicationData.Current.LocalSettings.Values.Remove(HiddenContentKey);
        HiddenContentChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<IReadOnlyList<TimelineEntry>> FilterTimelineAsync(
        IReadOnlyList<TimelineEntry> entries,
        CancellationToken cancellationToken)
    {
        var visibleEntries = entries.Where(entry => !IsTimelineEntryHidden(entry)).ToList();
        if (_settings.ShowNsfwContent)
        {
            return visibleEntries;
        }

        var uncachedSubjectIds = visibleEntries
            .Where(entry => entry.SubjectId is not null)
            .Select(entry => entry.SubjectId!.Value)
            .Distinct()
            .Where(subjectId => !_subjectNsfwCache.ContainsKey(subjectId))
            .ToList();

        await Parallel.ForEachAsync(
            uncachedSubjectIds,
            new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = 4 },
            async (subjectId, token) =>
            {
                try
                {
                    var subject = await _apiClient.GetSubjectAsync(subjectId, token);
                    _subjectNsfwCache[subjectId] = subject.Nsfw;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // A classification lookup must not make an otherwise available timeline unusable.
                }
            });

        return visibleEntries
            .Where(entry => entry.SubjectId is not int subjectId
                || !_subjectNsfwCache.TryGetValue(subjectId, out var isNsfw)
                || !isNsfw)
            .ToList();
    }

    public Uri CreateReportUri(string contentType, int subjectId, string localContentId)
    {
        var title = $"[内容举报] {contentType} / 条目 {subjectId}";
        var body = $"内容类型：{contentType}\n条目 ID：{subjectId}\n本地内容标识：{localContentId}\n\n请说明该内容违反了哪一项内容准则。请勿填写 Bangumi access token 或其他敏感个人信息。";
        return new Uri($"{ReportBaseUri}?template=content-report.md&title={Uri.EscapeDataString(title)}&body={Uri.EscapeDataString(body)}");
    }

    public static Uri CreateBangumiSubjectUri(int subjectId) => new($"https://bgm.tv/subject/{subjectId}");

    public static string GetCommentPublicId(int subjectId, SubjectComment comment) =>
        ShortHash(GetCommentKey(subjectId, comment));

    public static string GetTimelinePublicId(TimelineEntry entry) => ShortHash(GetTimelineKey(entry));

    private bool IsHidden(string key) => _hiddenContentSet.Contains(key);

    private void Hide(string key)
    {
        if (!_hiddenContentSet.Add(key))
        {
            return;
        }

        _hiddenContent.AddLast(key);
        while (_hiddenContent.Count > MaximumHiddenItems && _hiddenContent.First is not null)
        {
            _hiddenContentSet.Remove(_hiddenContent.First.Value);
            _hiddenContent.RemoveFirst();
        }

        SaveHiddenContent();
        HiddenContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LoadHiddenContent()
    {
        if (ApplicationData.Current.LocalSettings.Values[HiddenContentKey] is not string json)
        {
            return;
        }

        try
        {
            foreach (var key in (JsonSerializer.Deserialize<List<string>>(json) ?? []).TakeLast(MaximumHiddenItems))
            {
                if (!string.IsNullOrWhiteSpace(key) && _hiddenContentSet.Add(key))
                {
                    _hiddenContent.AddLast(key);
                }
            }
        }
        catch (JsonException)
        {
            ApplicationData.Current.LocalSettings.Values.Remove(HiddenContentKey);
        }
    }

    private void SaveHiddenContent()
    {
        ApplicationData.Current.LocalSettings.Values[HiddenContentKey] = JsonSerializer.Serialize(_hiddenContent.ToArray());
    }

    private static string GetCommentKey(int subjectId, SubjectComment comment) =>
        Hash($"comment|{subjectId}|{comment.SourceId}|{comment.UserName}|{comment.CreatedAt:O}|{comment.Content}");

    private static string GetTimelineKey(TimelineEntry entry) =>
        Hash($"timeline|{entry.UserName}|{entry.SubjectId}|{entry.CreatedAt:O}|{entry.Title}|{entry.Detail}");

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string ShortHash(string value) => value.Length <= 12 ? value : value[..12];
}
