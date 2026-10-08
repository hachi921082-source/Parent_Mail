# mail-display PLAN-1: plain text, HTML, images, header notices

Planning status: complete. Spec: detailed-spec 6; A11, A16, A18; Q39 (checkbox reset), Q14.

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Preview pane content: header block, plain-text view, HTML view in WebView2, derived text, remote-image blocking and "Show images", cid images, read marking, link interception hooks (rules come from safety-warnings PLAN-1). Zoom is PLAN-2; attachment list is PLAN-3.

## 2. Behavior

- Preview layout: top row = Reply, Reply all, Forward, Delete large buttons (wired by folder-actions) and zoom controls (PLAN-2); below = header block (From, To, Subject, Date, not-in-address-book notice) then banner slot (suspicious-link banner) then body, then attachment list (PLAN-3).
- On selecting a message: reset HTML checkbox to off (plain text), reset zoom (PLAN-2), "Show images" off; ensure body (sync-cache PLAN-3); mark read once the body is shown (A11) via `SetSeenAsync`; Outbox items show their content read-only; Drafts show content read-only (Edit via menu/button).
- Plain mode: `TextBlock`/read-only `TextBox` inside a `ScrollViewer`, font size `AppFontSize x zoom`, selectable text, URLs detected by regex (`https?://`, `www.`, `mailto:`) and rendered as clickable inlines (A18). Content = `body_text`; if absent, `derived_text` from HTML (HTML-only mail). If both missing: 「このメールには表示できる本文がありません」.
- HTML mode (checkbox on; only enabled when `body_html` exists, otherwise the checkbox is disabled): WebView2 with settings: JavaScript disabled, `IsScriptEnabled=false`, `AreDefaultContextMenusEnabled=false` (replaced by copy only), `IsZoomControlEnabled=false`, `AreBrowserAcceleratorKeysEnabled=false`, `IsStatusBarEnabled=true`, download and popups blocked, `IsPasswordAutosaveEnabled=false`. HTML loaded with `NavigateToString` after preprocessing: `cid:` URLs replaced with `data:` URIs from cached inline parts; remote images blocked by injecting `<meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline'; font-src data:">` unless "Show images" is on, in which case `img-src data: https: http:`. A 「画像を表示」 button (header) toggles for this mail only (A16) by re-rendering. Also `WebResourceRequested` filter blocks remote loads as defense.
- HTML navigation: `NavigationStarting` always cancelled except the initial string load; link clicks are passed to `LinkClickHandler` (safety-warnings PLAN-1): suspicious -> confirm dialog; allowed -> `Process.Start` default browser (`UseShellExecute`); `mailto:` -> open compose with To prefilled (compose PLAN-1 API). New-window requests are handled the same way.
- Plain-text link click uses the same `LinkClickHandler`.
- The WebView2 user data folder is `%LOCALAPPDATA%\Parent_Mail\webview2` (non-ASCII safe); runtime missing -> HTML checkbox disabled with a notice 「HTML表示ができません。詳しい方に相談してください。」.
- Not-in-address-book notice (D18, Q26): when the sender address is not in the displayed account's contacts, show 「アドレス帳に登録されていません」 in the header; hidden for Sent/Drafts/Outbox.
- Offline/not downloaded states per sync-cache PLAN-3.

## 3. Functions

- `PreviewViewModel { Header, Mode (Text|Html), PlainContent, HtmlContent, ShowImages, HtmlCheckbox, BannerKind }`, `LoadAsync(MailMessage)`.
- `HtmlPreprocessor.Prepare(html, inlineImages, allowRemote)` returns safe HTML string.
- `LinkTextBuilder.ToInlines(text)`.
- `IContactLookup.IsKnown(accountId, address)` (address-book feature).

## 4. Tests and done criteria

- Unit: HtmlPreprocessor replaces cid, injects CSP (remote vs not), strips `<meta http-equiv=refresh>`; plain link detection (incl. Japanese text next to URL); derived-text fallback chosen.
- Manual: HTML mail with remote images shows nothing remote until 「画像を表示」; checkbox resets on selecting another mail; clicking links never navigates inside the preview.
