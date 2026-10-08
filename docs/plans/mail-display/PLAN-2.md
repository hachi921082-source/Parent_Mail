# mail-display PLAN-2: preview zoom and text-size mapping

Planning status: complete. Spec: detailed-spec 6; A33-A37, A41 (changed, see `docs/plans/README.md` section 10); Q9, Q10, Q17.

## Index

1. Scope
2. Behavior
3. Functions
4. Tests and done criteria

## 1. Scope

Temporary preview zoom controls and the mapping of text size and zoom to plain-text font size and WebView2 ZoomFactor.

## 2. Behavior

- Controls next to the header buttons: large 「拡大」(+), 「縮小」(-), 「元に戻す」(reset) buttons and Ctrl+mouse wheel over the preview (plain view: `PreviewMouseWheel`; HTML view: WebView2 wheel events are captured via the host's `PreviewMouseWheel` while Ctrl is held and via `WebView2.CoreWebView2.Settings.IsZoomControlEnabled=false` plus `AreBrowserAcceleratorKeysEnabled=false` so Ctrl+wheel/Ctrl+plus/minus do not zoom natively; Ctrl+wheel reaching the app is translated to a zoom step). Ctrl+0 = reset, Ctrl+plus/minus also step.
- Range 100%-300%, step 25%; the buttons disable at the limits; label shows the percentage (「150%」).
- Zoom applies only while the current mail is displayed: it resets to 100% when the selected mail changes, or the folder or account changes (changed from A36/A37). It is never persisted and not stored in settings. Does not affect compose windows or other panes.
- Plain mode: `font size = TextSizePt * 96/72 * zoom` DIP.
- HTML mode: `ZoomFactor = Round(Clamp(TextSizePt / 12 * zoom, 0.25, 5.0), 2)` (reference 12 pt, Q10), set on `CoreWebView2Controller.ZoomFactor` after each navigation completes (NavigationCompleted) and whenever text size or zoom changes. At 18 pt default and 100% zoom the factor is 1.50.
- A text-size change in settings updates both modes immediately.

## 3. Functions

- `PreviewZoom { double Value; StepUp(); StepDown(); Reset(); }` (clamped to [1.0, 3.0], step 0.25; floating-point safe via integer percent).
- `ITextScale.GetWebViewZoom(zoom)`, `GetPlainTextSizeDip(zoom)` (implemented in settings).
- `PreviewViewModel.OnSelectionChanged -> zoom.Reset()`.

## 4. Tests and done criteria

- Unit: PreviewZoom limits and steps; reset on mail/folder/account change; ZoomFactor cases (18pt/1.0 -> 1.50, 18pt/3.0 -> 4.50, 48pt/3.0 -> 5.00 clamped, 12pt/1.0 -> 1.00); plain size case.
- Manual: Ctrl+wheel in HTML mode zooms only via the app (native zoom does not apply twice); zoom resets when switching mails; relaunch starts at 100%.
