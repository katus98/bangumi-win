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
        new("想看/想读/想玩", 1),
        new("看过/读过/玩过", 2),
        new("在看/在读/在玩", 3),
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
}
