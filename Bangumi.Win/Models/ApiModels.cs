using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Bangumi.Win.Models;

public sealed record BangumiUser(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("nickname")] string Nickname,
    [property: JsonPropertyName("avatar")] BangumiAvatar? Avatar);

public sealed record BangumiAvatar(
    [property: JsonPropertyName("large")] string? Large,
    [property: JsonPropertyName("medium")] string? Medium,
    [property: JsonPropertyName("small")] string? Small);

public sealed record PagedResponse<T>(
    [property: JsonPropertyName("data")] List<T> Data,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("offset")] int Offset);

public sealed record SubjectSummary(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("name_cn")] string? NameCn,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("images")] SubjectImages? Images,
    [property: JsonPropertyName("eps")] int? Eps,
    [property: JsonPropertyName("volumes")] int? Volumes,
    [property: JsonPropertyName("score")] double? Score,
    [property: JsonPropertyName("tags")] List<SubjectTag>? Tags)
{
    public string DisplayName => string.IsNullOrWhiteSpace(NameCn) ? Name : NameCn!;
    public string Subtitle => string.IsNullOrWhiteSpace(NameCn) || NameCn == Name ? TypeLabel : $"{Name} · {TypeLabel}";
    public string ImageUrl => Images?.Medium ?? Images?.Common ?? Images?.Small ?? string.Empty;
    public string TypeLabel => Type switch
    {
        1 => "书籍",
        2 => "动画",
        3 => "音乐",
        4 => "游戏",
        6 => "三次元",
        _ => "条目"
    };
}

public sealed record SubjectImages(
    [property: JsonPropertyName("large")] string? Large,
    [property: JsonPropertyName("common")] string? Common,
    [property: JsonPropertyName("medium")] string? Medium,
    [property: JsonPropertyName("small")] string? Small,
    [property: JsonPropertyName("grid")] string? Grid);

public sealed record SubjectTag(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("count")] int Count)
{
    public string DisplayText => Count > 0 ? $"{Name} {Count}" : Name;
}

public sealed record SubjectCollection(
    [property: JsonPropertyName("subject")] SubjectSummary Subject,
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("rate")] int? Rate,
    [property: JsonPropertyName("comment")] string? Comment,
    [property: JsonPropertyName("ep_status")] int? EpStatus,
    [property: JsonPropertyName("vol_status")] int? VolStatus,
    [property: JsonPropertyName("updated_at")] DateTimeOffset? UpdatedAt)
{
    public string StatusLabel => BangumiConstants.CollectionStatusLabel(Subject.Type, Type);

    public string ProgressLabel
    {
        get
        {
            var parts = new List<string>();
            if (Subject.Eps is > 0)
            {
                if (Subject.Type == 2)
                {
                    var verb = Type == 2 ? "看过" : Type == 3 ? "看到" : "进度";
                    parts.Add($"{verb} {EpStatus ?? 0} 集");
                }
                else
                {
                    parts.Add($"话数 {EpStatus ?? 0}/{Subject.Eps}");
                }
            }

            if (Subject.Volumes is > 0)
            {
                parts.Add($"卷 {VolStatus ?? 0}/{Subject.Volumes}");
            }

            return parts.Count == 0 ? "暂无进度信息" : string.Join(" · ", parts);
        }
    }
}

public sealed record TimelineEntry(string Title, string Detail, string UserName, DateTimeOffset? CreatedAt, string ImageUrl, int? SubjectId)
{
    public string TimeText => CreatedAt?.LocalDateTime.ToString("yyyy-MM-dd HH:mm") ?? "暂无时间";
    public bool HasImage => !string.IsNullOrWhiteSpace(ImageUrl);
    public bool HasSubject => SubjectId is not null;
}

public sealed record SearchResultItem(int Id, string DisplayName, string Subtitle, string Summary, string ImageUrl, int? SubjectType, bool IsPerson)
{
    public string KindLabel => IsPerson ? "人物" : "条目";
}

public sealed record EpisodeSummary(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("name_cn")] string? NameCn,
    [property: JsonPropertyName("sort")] double Sort,
    [property: JsonPropertyName("ep")] double? Ep,
    [property: JsonPropertyName("airdate")] string? Airdate,
    [property: JsonPropertyName("duration")] string? Duration,
    [property: JsonPropertyName("desc")] string? Description)
{
    public string DisplayName => string.IsNullOrWhiteSpace(NameCn) ? Name : NameCn!;
    public string NumberText => Type == 0 ? $"第 {Ep ?? Sort:0.##} 话" : EpisodeTypeLabel;
    public string EpisodeTypeLabel => Type switch
    {
        0 => "本篇",
        1 => "SP",
        2 => "OP",
        3 => "ED",
        _ => "章节"
    };
}

public sealed record UserEpisodeCollection(
    [property: JsonPropertyName("episode")] EpisodeSummary Episode,
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("updated_at")] long UpdatedAt)
{
    public string StatusLabel => Type switch
    {
        0 => "未收藏",
        1 => "想看",
        2 => "看过",
        3 => "抛弃",
        _ => "未知"
    };

    public string ActionLabel => Type == 0 ? "未收藏" : StatusLabel;

    public bool IsDone => Type == 2;
}

public sealed record SubjectComment(string UserName, string Content, DateTimeOffset? CreatedAt, int? Rate)
{
    public string MetaText
    {
        get
        {
            var parts = new List<string> { string.IsNullOrWhiteSpace(UserName) ? "Bangumi 用户" : UserName };
            if (Rate is > 0)
            {
                parts.Add($"评分 {Rate}");
            }

            if (CreatedAt is DateTimeOffset createdAt)
            {
                parts.Add(createdAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
            }

            return string.Join(" · ", parts);
        }
    }
}

public sealed record PersonDetail(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("type")] int Type,
    [property: JsonPropertyName("career")] List<string>? Career,
    [property: JsonPropertyName("images")] SubjectImages? Images,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("short_summary")] string? ShortSummary,
    [property: JsonPropertyName("locked")] bool? Locked)
{
    public string CareerText => Career is { Count: > 0 } ? string.Join(" / ", Career) : PersonTypeLabel;
    public string ImageUrl => Images?.Medium ?? Images?.Grid ?? Images?.Small ?? string.Empty;
    public string PersonTypeLabel => Type switch
    {
        1 => "个人",
        2 => "公司",
        3 => "组合",
        _ => "人物"
    };
}
