param([string]$MailDirectory = (Join-Path $PSScriptRoot '../.artifacts/mail'))
# Chỉ đọc mailbox Development local; không gửi email hoặc in secret cấu hình.
$ErrorActionPreference = 'Stop'
$mail = Get-ChildItem -LiteralPath $MailDirectory -Filter '*.eml' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($null -eq $mail) { throw 'Chưa có email local. Hãy register hoặc gửi lại email xác nhận trước.' }
$mime = Get-Content -Raw -LiteralPath $mail.FullName
$separator = [regex]::Match($mime, '\r*\n\r*\n')
if (!$separator.Success) { throw 'Email không đúng định dạng MIME.' }
$headers = $mime.Substring(0, $separator.Index)
$body = $mime.Substring($separator.Index + $separator.Length).Trim()
if ($headers -match '(?im)^Content-Transfer-Encoding:\s*base64') {
    $body = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($body))
}
elseif ($headers -match '(?im)^Content-Transfer-Encoding:\s*quoted-printable') {
    $body = [regex]::Replace($body, '=\r*\n', '')
    $body = [regex]::Replace($body, '=([0-9A-Fa-f]{2})', {
        param($match) [string][char][Convert]::ToInt32($match.Groups[1].Value, 16)
    })
}
Write-Output $body
