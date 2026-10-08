# safety-warnings PLAN-1: suspicious links

Planning status: complete. Spec: detailed-spec 14; A29; PQ13, PQ14, Q23, Q24; A56. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Rules
3. UI
4. Functions
5. Tests and done criteria

## 1. Scope

Link analysis (`LinkAnalyzer`), the red banner, the click-confirmation dialog, and the link click handler used by plain and HTML views.

## 2. Rules

A link is suspicious if any holds:
1. Shown text differs from the real target: when the anchor text itself looks like a URL or host (contains `://`, `www.`, or a dotted host pattern) and its normalized host differs from the target host (compare registrable host case-insensitively, ignoring `www.` prefix).
2. Host is an IP address (IPv4, IPv6, or decimal/hex/octal integer forms).
3. Host equals or is a subdomain of a shortener: bit.ly, t.co, tinyurl.com, goo.gl, ow.ly, is.gd, buff.ly, cutt.ly, rebrand.ly, t.ly, lnkd.in, amzn.to, ift.tt, shorturl.at, rb.gy, bit.do, j.mp, x.gd, tiny.cc, s.id (code constant `Shorteners`, A29).
4. Scheme is not https (`http`, `ftp`, other). `mailto:` and `tel:` are not analyzed as links (never suspicious).
5. Look-alike host: any label starting with `xn--`, any non-ASCII character in the host, or a label mixing scripts (e.g. Latin with Cyrillic/Greek). No brand-similarity check.
- Parsing failure of a URL = suspicious.
- Evaluation scope (PQ14): the HTML part (all `<a href>` with anchor text) and the text part (URLs in text; rules 2-5 only, since text has no separate anchor) of the message, independent of the current display mode. Computed once when the mail is shown and cached per message in memory.

## 3. UI

- Banner (Q24): red banner at the top of the mail body, text [SW-01] 「このメールには、あやしいリンクが含まれています。リンクを開かないでください。」 in bold white on #A80000 (`BannerError`); visible whenever the evaluation finds any suspicious link.
- Link click: allowed (not suspicious) -> opened in the default browser immediately, no dialog (PQ14). Suspicious -> modal confirm dialog [WD-26] (title [SW-02] 「リンク先の確認」, text [SW-03] 「このリンクは本当のリンク先が違うかもしれません。リンク先: <URL>」 showing the real destination: full URL, wrapped, non-ASCII shown as punycode and decoded) with buttons [SW-04] 「それでも開く」 and 「やめる」*, default = やめる (Enter and Esc = やめる); opening goes to the default browser.
- `mailto:` click -> compose window with To prefilled (account = displayed account).
- Only `http`, `https`, `mailto` are ever launched; other schemes (file, javascript, ms-*) are refused with modal notice [SW-05] 「このリンクは開けません」 (title 「お知らせ」, button 「OK」).

## 4. Functions

- `LinkAnalyzer.AnalyzeHtml(string html)` and `AnalyzeText(string text)` return `LinkAnalysis { bool AnySuspicious; IReadOnlyList<LinkFinding> }`; `LinkAnalyzer.IsSuspicious(Uri target, string? anchorText)`.
- `HostChecks.IsIpHost`, `IsShortener`, `IsLookAlike`, `HasMixedScript` (Unicode script by `UnicodeCategory`/ranges).
- `LinkClickHandler.HandleAsync(string url, string? anchorText)`.

## 5. Tests and done criteria

- Unit: each rule (text mismatch, IPs in several forms, shortener exact/subdomain, http, punycode, Cyrillic-mixed host, unparsable), non-suspicious https link, `mailto` handling, refused schemes.
- Manual: crafted HTML test mail with each case shows banner and dialog with default Cancel.
