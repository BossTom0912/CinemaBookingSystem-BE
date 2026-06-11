# SePay Webhook Test Note

## Real webhook signature command

Use this when testing the real endpoint:

```http
POST /api/Payment/sepay-webhook
```

PowerShell command to generate `x-sepay-timestamp` and `x-sepay-signature`:

```powershell
$body = @'
{
  "content": "Thanh toan ve phim TSEPAY150K1",
  "transferAmount": 150000,
  "referenceCode": "SEPAY_BANK_REF_150K_001"
}
'@

$secret = "<your-local-webhook-secret>"
$timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds().ToString()
$data = "$timestamp.$body"

$hmac = [System.Security.Cryptography.HMACSHA256]::new([Text.Encoding]::UTF8.GetBytes($secret))
$hash = $hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($data))
$signature = "sha256=" + (($hash | ForEach-Object { $_.ToString("x2") }) -join "")

"x-sepay-timestamp: $timestamp"
"x-sepay-signature: $signature"
```

The request body in Swagger/Postman must be exactly the same as `$body`, including spaces and line breaks.
