using Windows.Storage;

namespace Bangumi.Win.Services;

public sealed class TokenStore
{
    private const string TokenKey = "BangumiAccessToken";

    public bool HasToken => !string.IsNullOrWhiteSpace(AccessToken);

    public string? AccessToken
    {
        get => ApplicationData.Current.LocalSettings.Values[TokenKey] as string;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                ApplicationData.Current.LocalSettings.Values.Remove(TokenKey);
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values[TokenKey] = value.Trim();
            }
        }
    }

    public void Clear() => AccessToken = null;
}
