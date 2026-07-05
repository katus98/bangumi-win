using System.Collections.Generic;

namespace Bangumi.Win.Models;

public static class BangumiConstants
{
    public static IReadOnlyList<OptionItem<int?>> SubjectTypes { get; } =
    [
        new("全部", null),
        new("动画", 2),
        new("书籍", 1),
        new("音乐", 3),
        new("游戏", 4),
        new("三次元", 6),
    ];

    public static IReadOnlyList<OptionItem<int?>> CollectionSubjectTypes { get; } =
    [
        new("动画", 2),
        new("书籍", 1),
        new("音乐", 3),
        new("游戏", 4),
        new("三次元", 6),
    ];

    public static IReadOnlyList<OptionItem<int?>> SearchTypes { get; } =
    [
        new("全部", null),
        new("动画", 2),
        new("书籍", 1),
        new("音乐", 3),
        new("游戏", 4),
        new("三次元", 6),
        new("人物", -1),
    ];

    public static IReadOnlyList<OptionItem<int?>> CollectionStatuses { get; } =
    [
        new("全部", null),
        new("在看/在读/在玩", 3),
        new("想看/想读/想玩", 1),
        new("看过/读过/玩过", 2),
        new("搁置", 4),
        new("抛弃", 5),
    ];

    public static IReadOnlyList<OptionItem<int>> EditableCollectionStatuses { get; } =
    [
        new("想看/想读/想玩", 1),
        new("看过/读过/玩过", 2),
        new("在看/在读/在玩", 3),
        new("搁置", 4),
        new("抛弃", 5),
    ];

    public static IReadOnlyList<OptionItem<int?>> GetCollectionStatuses(int? subjectType)
    {
        return
        [
            new("全部", null),
            new(CollectionStatusLabel(subjectType, 3), 3),
            new(CollectionStatusLabel(subjectType, 1), 1),
            new(CollectionStatusLabel(subjectType, 2), 2),
            new("搁置", 4),
            new("抛弃", 5),
        ];
    }

    public static IReadOnlyList<OptionItem<int>> GetEditableCollectionStatuses(int? subjectType)
    {
        return
        [
            new(CollectionStatusLabel(subjectType, 3), 3),
            new(CollectionStatusLabel(subjectType, 1), 1),
            new(CollectionStatusLabel(subjectType, 2), 2),
            new("搁置", 4),
            new("抛弃", 5),
        ];
    }

    public static string CollectionStatusLabel(int? subjectType, int status)
    {
        return (subjectType, status) switch
        {
            (1, 1) => "想读",
            (1, 2) => "读过",
            (1, 3) => "在读",
            (2, 1) => "想看",
            (2, 2) => "看过",
            (2, 3) => "在看",
            (3, 1) => "想听",
            (3, 2) => "听过",
            (3, 3) => "在听",
            (4, 1) => "想玩",
            (4, 2) => "玩过",
            (4, 3) => "在玩",
            (6, 1) => "想看",
            (6, 2) => "看过",
            (6, 3) => "在看",
            (_, 1) => "想收藏",
            (_, 2) => "已收藏",
            (_, 3) => "在进行",
            (_, 4) => "搁置",
            (_, 5) => "抛弃",
            _ => "收藏"
        };
    }
}
