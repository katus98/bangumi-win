using System;

namespace Bangumi.Win.Services;

public static class AppServices
{
    public static event EventHandler? AuthStateChanged;

    public static TokenStore TokenStore { get; } = new();

    public static BangumiApiClient ApiClient { get; } = new(TokenStore);

    public static void NotifyAuthStateChanged() => AuthStateChanged?.Invoke(null, EventArgs.Empty);
}
