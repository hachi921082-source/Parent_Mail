# mail-display PLAN-1: plain text, HTML, images, header notices

Planning status: complete (updated 2026-10-08). Spec: detailed-spec 6, 22; A11, A16, A18, A51, A58, A60, A66; Q39 (checkbox reset), Q62, Q63, Q69, Q74; PQ14. Wording: `ui-strings.md` (IDs in brackets).

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Preview pane content: header block, plain-text view, HTML view in WebView2, derived text, remote-image blocking and "Show images", cid images, read marking, link interception hooks (rules come from safety-warnings PLAN-1). Zoom is PLAN-2; attachment list is PLAN-3.

## 2. Behavior

- Preview layout: top row = 返信, 全員に返信, 転送 (Drafts: also 編集 [TB-24]) large buttons at the left (wired by folder-actions), the zoom controls in the middle (PLAN-2), and the 削除 button at the FAR RIGHT, apart from the others, with the red outline style `DeleteOutline` (A51; label [TB-23] 「選んだN通を削除」 while rows are checked). Below = header block (labels [PV-01]: 差出人, 宛先, CC only when the mail has CC recipients (A66), 件名, 日時; then the not-in-address-book notice and the image hint), then banner slot (suspicious-link red banner), then body, then attachment list (PLAN-3, heading [PV-14] 「添付」).
- No mail selected: the preview shows the guide [PV-12] 「左の一覧からメールを選んでください」 (centered). While one or more list rows are checked (mail-list multi-select) the preview shows [PV-13] 「N通のメールを選んでいます。「削除」を押すと、まとめてごみ箱に移します。」 and every header button except 削除 is disabled; when all checks are cleared the selected mail is shown again (A60).
- On selecting a message: reset HTML checkbox to off (plain text), reset zoom (PLAN-2), "Show images" off; ensure body (sync-cache PLAN-3); mark read (A11, Q69) via `SetSeenAsync` immediately on a USER selection and only in Inbox and Spam; a selection made automatically by the app after delete / move / reload (`SelectionSource.Auto`) is visual only and does NOT mark read (A58, Q74); Outbox items show their content read-only; Drafts show content read-only (Edit via menu/button).
- Plain mode: `TextBlock`/read-only `TextBox` inside a `ScrollViewer`, font size `AppFontSize x zoom`, selectable text, URLs detected by regex (`https?://`, `www.`, `mailto:`) and rendered as clickable inlines (A18). Content = `body_text`; if absent, `derived_text` from HTML (HTML-only mail). If both missing: [PV-03] 「このメールには表示できる本文がありません」.
- HTML mode (checkbox on; only enabled when `body_html` exists, otherwise the checkbox is disabled): WebView2 with settings: JavaScript disabled, `IsScriptEnabled=false`, `AreDefaultContextMenusEnabled=false` (replaced by copy only), `IsZoomControlEnabled=false`, `AreBrowserAcceleratorKeysEnabled=false`, `IsStatusBarEnabled=true`, download and popups blocked, `IsPasswordAutosaveEnabled=false`. HTML loaded with `NavigateToString` after preprocessing: `cid:` URLs replaced with `data:` URIs from cached inline parts; remote images blocked by injecting `<meta http-equiv="Content-Security-Policy" content="default-src 'none'; img-src data:; style-src 'unsafe-inline'; font-src data:">` unless "Show images" is on, in which case `img-src data: https: http:`. A [PV-04] 「画像を表示」 button (header; hint [PV-05] 「画像は表示されていません。」) toggles for this mail only (A16) by re-rendering. Also `WebResourceRequested` filter blocks remote loads as defense.
- HTML navigation: `NavigationStarting` always cancelled except the initial string load; link clicks are passed to `LinkClickHandler` (safety-warnings PLAN-1): suspicious -> confirm dialog; allowed -> `Process.Start` default browser (`UseShellExecute`); `mailto:` -> open compose with To prefilled (compose PLAN-1 API). New-window requests are handled the same way.
- Plain-text link click uses the same `LinkClickHandler`.
- The WebView2 user data folder is `AppPaths.WebView2Dir` (`<data root>\webview2`, Q93) (non-ASCII safe); runtime missing -> HTML checkbox disabled with a modal notice [PV-06] 「HTML表示ができません。詳しい方に相談してください。」.
- Not-in-address-book notice (D18, Q26): when the sender address is not in the displayed account's contacts, show [PV-02] 「アドレス帳に登録されていません」 in the header (in-place text); hidden for Sent/Drafts/Outbox.
- Offline/not downloaded states per sync-cache PLAN-3 (in-place texts [BN-08], [BN-10], [BN-13]).
- Text colours/sizes: preview and body use `Page` (#FFFFFF/#000000); hint text #595959; header uses `AppFontSize` (no chrome cap).

## 3. Functions

- `PreviewViewModel { Header, Mode (Text|Html), PlainContent, HtmlContent, ShowImages, HtmlCheckbox, BannerKind }`, `LoadAsync(MailMessage)`.
- `HtmlPreprocessor.Prepare(html, inlineImages, allowRemote)` returns safe HTML string.
- `LinkTextBuilder.ToInlines(text)`.
- `IContactLookup.IsKnown(accountId, address)` (address-book feature).

## 4. Tests and done criteria

- Unit: HtmlPreprocessor replaces cid, injects CSP (remote vs not), strips `<meta http-equiv=refresh>`; plain link detection (incl. Japanese text next to URL); derived-text fallback chosen.
- Manual: HTML mail with remote images shows nothing remote until 「画像を表示」; checkbox resets on selecting another mail; clicking links never navigates inside the preview.
