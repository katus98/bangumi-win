using Windows.Storage;
using Windows.Security.Credentials;

namespace Bangumi.Win.Services;

public sealed class TokenStore
{
    private const string TokenKey = "BangumiAccessToken";
    private const string VaultResource = "Bangumi.Win";
    private const string VaultUserName = "BangumiAccessToken";
    private string? _accessToken;

    public TokenStore()
    {
        _accessToken = ReadFromVault();

        // Migrate tokens saved by older builds, then remove the plaintext copy.
        if (ApplicationData.Current.LocalSettings.Values[TokenKey] is string legacyToken
            && !string.IsNullOrWhiteSpace(legacyToken))
        {
            _accessToken ??= legacyToken.Trim();
            _ = WriteToVault(_accessToken);
        }

        ApplicationData.Current.LocalSettings.Values.Remove(TokenKey);
    }

    public bool HasToken => !string.IsNullOrWhiteSpace(_accessToken);

    public string? AccessToken
    {
        get => _accessToken;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                _accessToken = null;
                RemoveFromVault();
            }
            else
            {
                _accessToken = value.Trim();
                _ = WriteToVault(_accessToken);
            }

            ApplicationData.Current.LocalSettings.Values.Remove(TokenKey);
        }
    }

    public void Clear() => AccessToken = null;

    private static string? ReadFromVault()
    {
        try
        {
            var vault = new PasswordVault();
            var credential = vault.Retrieve(VaultResource, VaultUserName);
            credential.RetrievePassword();
            return credential.Password;
        }
        catch
        {
            return null;
        }
    }

    private static bool WriteToVault(string token)
    {
        try
        {
            var vault = new PasswordVault();
            RemoveFromVault();
            vault.Add(new PasswordCredential(VaultResource, VaultUserName, token));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void RemoveFromVault()
    {
        try
        {
            var vault = new PasswordVault();
            vault.Remove(vault.Retrieve(VaultResource, VaultUserName));
        }
        catch
        {
        }
    }
}
