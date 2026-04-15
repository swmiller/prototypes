# ===== Step 1: Get OAuth Token =====
$tenantId = "YOUR_TENANT_ID"
$clientId = "YOUR_CLIENT_ID"
$clientSecret = "YOUR_CLIENT_SECRET"
$envUrl = "https://YOUR_ORG.crm.dynamics.com"

$body = @{
    client_id     = $clientId
    client_secret = $clientSecret
    grant_type    = "client_credentials"
    scope         = "$envUrl/.default"
}

$tokenResponse = Invoke-RestMethod `
    -Method Post `
    -Uri "https://login.microsoftonline.com/$tenantId/oauth2/v2.0/token" `
    -Body $body `
    -ContentType "application/x-www-form-urlencoded"

$accessToken = $tokenResponse.access_token

# ===== Step 2: Call Dataverse WhoAmI =====
$headers = @{
    Authorization = "Bearer $accessToken"
    Accept        = "application/json"
}

Invoke-RestMethod `
    -Method Get `
    -Uri "$envUrl/api/data/v9.2/WhoAmI" `
    -Headers $headers
