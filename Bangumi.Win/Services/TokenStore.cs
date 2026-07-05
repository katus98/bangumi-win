using Windows.Storage;
using Windows.Security.Credentials;

namespace Bangumi.Win.Services;

public sealed class TokenStore
{
    private const string TokenKey = "BangumiAccessToken";
    private const string VaultResource = "Bangumi.Win";
    private const string VaultUserName = "BangumiAccessToken";

    public bool HasToken => !string.IsNullOrWhiteSpace(AccessToken);

    public string? AccessToken
    {
        get => ReadFromVault() ?? ApplicationData.Current.LocalSettings.Values[TokenKey] as string;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                RemoveFromVault();
                ApplicationData.Current.LocalSettings.Values.Remove(TokenKey);
            }
            else
            {
                var token = value.Trim();
                if (!WriteToVault(token))
                {
                    ApplicationData.Current.LocalSettings.Values[TokenKey] = token;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values.Remove(TokenKey);
                }
            }
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
