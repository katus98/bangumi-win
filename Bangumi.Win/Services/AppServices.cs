using Bangumi.Win.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Bangumi.Win.Services;

public static class AppServices
{
    private static readonly SemaphoreSlim UserGate = new(1, 1);
    private static BangumiUser? _currentUser;
    private static int _authVersion;

    public static event EventHandler? AuthStateChanged;

    public static TokenStore TokenStore { get; } = new();

    public static BangumiApiClient ApiClient { get; } = new(TokenStore);

    public static AppSettings Settings { get; } = new();

    public static ContentSafetyService ContentSafety { get; } = new(ApiClient, Settings);

    public static BangumiUser? CurrentUser => _currentUser;

    public static async Task<BangumiUser> SignInAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException("Access token 不能为空。", nameof(accessToken));
        }

        await UserGate.WaitAsync(cancellationToken);
        try
        {
            var user = await ApiClient.GetMeAsync(accessToken.Trim(), cancellationToken);
            TokenStore.AccessToken = accessToken;
            _currentUser = user;
            Interlocked.Increment(ref _authVersion);
            AuthStateChanged?.Invoke(null, EventArgs.Empty);
            return user;
        }
        finally
        {
            UserGate.Release();
        }
    }

    public static async Task<BangumiUser> GetCurrentUserAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        if (!forceRefresh && _currentUser is not null)
        {
            return _currentUser;
        }

        var accessToken = TokenStore.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("当前未登录。请先添加 Bangumi access token。");
        }

        await UserGate.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && _currentUser is not null)
            {
                return _currentUser;
            }

            var version = Volatile.Read(ref _authVersion);
            try
            {
                var user = await ApiClient.GetMeAsync(cancellationToken);
                if (version != Volatile.Read(ref _authVersion) || accessToken != TokenStore.AccessToken)
                {
                    throw new OperationCanceledException("登录状态已发生变化。", cancellationToken);
                }

                _currentUser = user;
                return user;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                SignOutCore(notify: true);
                throw;
            }
        }
        finally
        {
            UserGate.Release();
        }
    }

    public static void SignOut() => SignOutCore(notify: true);

    private static void SignOutCore(bool notify)
    {
        var hadSession = TokenStore.HasToken || _currentUser is not null;
        TokenStore.Clear();
        _currentUser = null;
        Interlocked.Increment(ref _authVersion);
        if (notify && hadSession)
        {
            AuthStateChanged?.Invoke(null, EventArgs.Empty);
        }
    }
}
