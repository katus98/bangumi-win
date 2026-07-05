namespace Bangumi.Win.Models;

public sealed record OptionItem<T>(string Name, T Value)
{
    public override string ToString() => Name;
}
