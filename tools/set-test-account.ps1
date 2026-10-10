# テスト用 Gmail アカウントの情報を、ユーザー環境変数に登録する。
# 値は画面に表示せず、ファイルにも書かない（パスワードは入力時に伏せ字になる）。
# 使い方: PowerShell で `.\tools\set-test-account.ps1` を実行する。
# 解除: `.\tools\set-test-account.ps1 -Clear`

param([switch]$Clear)

$userVar = 'PARENTMAIL_TEST_USER'
$passVar = 'PARENTMAIL_TEST_APPPASSWORD'

if ($Clear) {
    [Environment]::SetEnvironmentVariable($userVar, $null, 'User')
    [Environment]::SetEnvironmentVariable($passVar, $null, 'User')
    Write-Host '環境変数を削除した。'
    return
}

$user = Read-Host 'テスト用 Gmail アドレス'
$secure = Read-Host 'アプリパスワード（16 文字。入力は表示されない）' -AsSecureString

# SecureString を平文に戻して登録する（登録後はすぐにメモリから消す）。
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
try {
    $pass = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}

# Google が表示する空白区切りのまま貼り付けても動くよう、空白を除く。
$pass = $pass -replace '\s', ''

if ($user -notmatch '^[^@\s]+@gmail\.com$') {
    Write-Host 'Gmail アドレスの形式ではない。中止する。'
    return
}
if ($pass.Length -ne 16) {
    Write-Host 'アプリパスワードが 16 文字ではない。中止する。'
    return
}

[Environment]::SetEnvironmentVariable($userVar, $user, 'User')
[Environment]::SetEnvironmentVariable($passVar, $pass, 'User')
$pass = $null

Write-Host '登録した。VS Code とターミナルを開き直すと反映される。'
Write-Host '確認: [bool]$env:PARENTMAIL_TEST_USER; [bool]$env:PARENTMAIL_TEST_APPPASSWORD'
