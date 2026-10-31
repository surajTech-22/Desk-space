using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace DeskSpace;

// R202: deliberate, unlocked-vault site lookup only. Exact HTTPS hostname,
// never substring/suffix matching; nothing auto-types or copies a password.
// Matching returns display metadata only, never Secret or Password references.
internal static class DeskSpaceVaultContextV202
{
    internal sealed record Suggestion(Guid Id,string Label,string Host);
    internal static string? CanonicalHost(string? input,bool activePage)
    {
        if(string.IsNullOrWhiteSpace(input)||input.Length>2048 ||
           input.Any(char.IsControl))return null;
        string url=input.Trim();
        if(!url.Contains("://",StringComparison.Ordinal))
        {
            if(activePage)return null;
            url="https://"+url;
        }
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||
           uri.Scheme!=Uri.UriSchemeHttps ||
           !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort ||
           !string.IsNullOrEmpty(uri.Fragment) ||
           IPAddress.TryParse(uri.Host,out _) ||
           uri.IdnHost.Length>253 || !uri.IdnHost.Contains('.'))
            return null;
        string host=uri.IdnHost.ToLowerInvariant().TrimEnd('.');
        if(host.Length<4 || host.Split('.').Any(label=>
            label.Length is <1 or >63 ||
            label[0]=='-' || label[^1]=='-' ||
            label.Any(c=>!char.IsLetterOrDigit(c)&&c!='-')))
            return null;
        return host;
    }
    internal static IReadOnlyList<Suggestion> Match(
        DeskSpaceVaultV10 vault,string activeUrl)
    {
        if(vault==null||!vault.IsUnlocked)return Array.Empty<Suggestion>();
        var actual=CanonicalHost(activeUrl,true);
        if(actual==null)return Array.Empty<Suggestion>();
        return vault.Entries.Where(entry=>
                string.Equals(CanonicalHost(entry.Website,false),actual,
                    StringComparison.Ordinal))
            .Take(20)
            .Select(entry=>new Suggestion(entry.Id,entry.Label,actual))
            .ToArray();
    }
}
