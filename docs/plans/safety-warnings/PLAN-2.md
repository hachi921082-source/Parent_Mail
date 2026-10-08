# safety-warnings PLAN-2: executable file checker

Planning status: complete. Spec: detailed-spec 14, 7; A30; Q25, Q41, Q22 (extended list, spec change).

## Index

1. Scope
2. Rules
3. Functions
4. Tests and done criteria

## 1. Scope

One shared `ExecutableChecker` used by receive-side attachments (mail-display PLAN-3), attach in compose (compose PLAN-1), and forward (reply-forward).

## 2. Rules

- Extension list (case-insensitive, compared after normalization): confirmed exe, msi, bat, cmd, scr, js, vbs, ps1, jar, lnk; macro Office docm, xlsm, pptm, dotm, xltm, potm, xlam, ppam, ppsm (A30); extras hta, com, pif, reg, wsf, vbe, jse, msc, dll, cpl, iso, img, vhd, vhdx, appx, msix, wsh, wsc, ps1xml, psm1, scf, inf, gadget, application, msp, mst, sct, chm, hlp, ade, adp, mde.
- Normalization of the file name: strip trailing spaces and dots, take the last extension only (so `invoice.pdf.exe` is flagged), compare case-insensitive.
- Names containing RTL override or other bidi control characters (U+202A-202E, U+2066-2069) are flagged.
- `.zip` only: opened one level deep with `System.IO.Compression.ZipArchive`; any entry whose name matches the list (same normalization) flags the archive; an archive that cannot be read, is corrupt, or has encrypted entries is flagged as suspicious (reason `UnreadableArchive`). Nested archives are not expanded. Other archive types (rar, 7z, etc.) are not inspected.
- Result: `ExecutableCheckResult { Flagged, Reason (Extension|Macro|InArchive|UnreadableArchive|BidiName), Detail }`.
- Receive side: flagged -> strong warning, open forbidden, save only after confirmation (see mail-display PLAN-3). Attach/forward side: flagged -> blocked, never added, with dialog 「このファイルは危険な可能性があるため、添付できません。」.

## 3. Functions

- `ExecutableChecker.CheckName(string fileName)`, `CheckFile(string path)` (name + zip content), `CheckStream(string fileName, Stream)`.
- `ExecutableExtensions` constant set (single source for tests).

## 4. Tests and done criteria

- Unit: each extension category, double extensions, trailing dot/space, upper-case, bidi names, zip containing exe, zip with encrypted entry, corrupt zip, harmless zip, Japanese file names.
