# Fix Icons in PromptDetailsPage.xaml
# This script fixes incorrect MaterialIcons glyphs and FontFamily typos

$filePath = "Features\Prompts\Pages\PromptDetailsPage.xaml"
$content = Get-Content $filePath -Raw

Write-Host "?? Fixing PromptDetailsPage.xaml icons..." -ForegroundColor Yellow

# Fix 1: FontFamily typo (dot to dash)
$content = $content -replace 'FontFamily="MaterialIconsOutlined\.Regular"', 'FontFamily="MaterialIconsOutlined-Regular"'
Write-Host "? Fixed FontFamily typo (MaterialIconsOutlined.Regular ? MaterialIconsOutlined-Regular)" -ForegroundColor Green

# Fix 2: ChatGPT icon (line ~248)
# Context: <Button ... Text="ChatGPT" ... <FontImageSource ... Glyph="&#xf06c;"
$pattern1 = '(<Button[^>]*Text="ChatGPT"[^>]*>[\s\S]*?<FontImageSource[^>]*Glyph=")&#xf06c;(")'
$content = $content -replace $pattern1, '$1&#xe0ca;$2'
Write-Host "? Fixed ChatGPT icon (&#xf06c; ? &#xe0ca;)" -ForegroundColor Green

# Fix 3: Gemini icon (line ~269)
$pattern2 = '(<Button[^>]*Text="Gemini"[^>]*>[\s\S]*?<FontImageSource[^>]*Glyph=")&#xf06c;(")'
$content = $content -replace $pattern2, '$1&#xe86c;$2'
Write-Host "? Fixed Gemini icon (&#xf06c; ? &#xe86c;)" -ForegroundColor Green

# Fix 4: Grok icon (line ~290)
$pattern3 = '(<Button[^>]*Text="Grok"[^>]*>[\s\S]*?<FontImageSource[^>]*Glyph=")&#xf06c;(")'
$content = $content -replace $pattern3, '$1&#xe887;$2'
Write-Host "? Fixed Grok icon (&#xf06c; ? &#xe887;)" -ForegroundColor Green

# Fix 5: Copilot icon (line ~311)
$pattern4 = '(<Button[^>]*Text="Copilot"[^>]*>[\s\S]*?<FontImageSource[^>]*Glyph=")&#xf06c;(")'
$content = $content -replace $pattern4, '$1&#xf02b;$2'
Write-Host "? Fixed Copilot icon (&#xf06c; ? &#xf02b;)" -ForegroundColor Green

# Write back to file
$content | Set-Content $filePath -NoNewline

Write-Host ""
Write-Host "?? All fixes applied successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "?? Summary:" -ForegroundColor Cyan
Write-Host "  - FontFamily typo fixed" -ForegroundColor White
Write-Host "  - ChatGPT icon: &#xe0ca; (chat)" -ForegroundColor White
Write-Host "  - Gemini icon: &#xe86c; (star)" -ForegroundColor White
Write-Host "  - Grok icon: &#xe887; (robot)" -ForegroundColor White
Write-Host "  - Copilot icon: &#xf02b; (help)" -ForegroundColor White
Write-Host ""
Write-Host "??? Next step: Run build to verify" -ForegroundColor Yellow
