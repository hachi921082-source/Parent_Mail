namespace ParentMail.Core.SafetyWarnings;

/// <summary>URL を既定のブラウザで開く。テストでは差し替える。</summary>
public interface IExternalLauncher
{
    void OpenUrl(string url);
}
