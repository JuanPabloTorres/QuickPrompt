# QuickPrompt - Design Token Verification Script
# Phase 2.2 - Hardcoded Value Elimination
# Scans for hardcoded colors, font sizes, and spacing values

Write-Host "?? QuickPrompt Design Token Verification" -ForegroundColor Cyan
Write-Host "Phase 2.2 - Hardcoded Value Elimination`n" -ForegroundColor Cyan

# Configuration
$projectRoot = $PSScriptRoot
$excludeDirs = @("obj", "bin", ".git", "docs", "Resources/Styles")
$excludeFiles = @("Colors.xaml", "AppColors.xaml", "Typography.xaml", "Spacing.xaml", "Shadows.xaml", "Tokens.xaml", "Styles.xaml")

# Initialize counters
$totalViolations = 0
$violations = @{
    HardcodedColorsCS = @()
    HardcodedColorsXAML = @()
    InlineFontSizes = @()
    ArbitrarySpacing = @()
}

Write-Host "?? Scanning directory: $projectRoot`n" -ForegroundColor Gray

# ============================================================================
# 1. HARDCODED COLORS IN C# FILES
# ============================================================================

Write-Host "?? [1/4] Scanning for hardcoded colors in C# files..." -ForegroundColor Yellow

$csFiles = Get-ChildItem -Path $projectRoot -Recurse -Include *.cs -Exclude *AssemblyInfo.cs,*.g.cs,*.designer.cs | 
    Where-Object { 
        $path = $_.FullName
        $exclude = $false
        foreach ($dir in $excludeDirs) {
            if ($path -like "*\$dir\*") {
                $exclude = $true
                break
            }
        }
        -not $exclude
    }

foreach ($file in $csFiles) {
    $content = Get-Content $file.FullName -Raw
    $relPath = $file.FullName.Replace($projectRoot, "").TrimStart('\')
    
    # Pattern 1: Color.FromArgb("#RRGGBB")
    $matches = [regex]::Matches($content, 'Color\.FromArgb\s*\(\s*"#[0-9A-Fa-f]{6}"\s*\)')
    foreach ($match in $matches) {
        $violations.HardcodedColorsCS += [PSCustomObject]@{
            File = $relPath
            Line = ($content.Substring(0, $match.Index) -split "`n").Count
            Code = $match.Value
            Type = "Color.FromArgb"
        }
        $totalViolations++
    }
    
    # Pattern 2: Color.FromArgb(255, R, G, B)
    $matches = [regex]::Matches($content, 'Color\.FromArgb\s*\(\s*\d+\s*,\s*\d+\s*,\s*\d+\s*,\s*\d+\s*\)')
    foreach ($match in $matches) {
        $violations.HardcodedColorsCS += [PSCustomObject]@{
            File = $relPath
            Line = ($content.Substring(0, $match.Index) -split "`n").Count
            Code = $match.Value
            Type = "Color.FromArgb(RGBA)"
        }
        $totalViolations++
    }
    
    # Pattern 3: Colors.ColorName (direct reference instead of resource)
    $matches = [regex]::Matches($content, 'Colors\.\w+')
    foreach ($match in $matches) {
        # Skip if it's in a comment or string
        $lineStart = $content.LastIndexOf("`n", $match.Index) + 1
        $lineEnd = $content.IndexOf("`n", $match.Index)
        if ($lineEnd -eq -1) { $lineEnd = $content.Length }
        $line = $content.Substring($lineStart, $lineEnd - $lineStart)
        
        if ($line -notmatch '^\s*//' -and $line -notmatch '^\s*\*') {
            $violations.HardcodedColorsCS += [PSCustomObject]@{
                File = $relPath
                Line = ($content.Substring(0, $match.Index) -split "`n").Count
                Code = $match.Value
                Type = "Colors.ColorName"
            }
            $totalViolations++
        }
    }
}

Write-Host "   Found: $($violations.HardcodedColorsCS.Count) hardcoded colors in C#" -ForegroundColor $(if($violations.HardcodedColorsCS.Count -eq 0){"Green"}else{"Red"})

# ============================================================================
# 2. HARDCODED COLORS IN XAML FILES
# ============================================================================

Write-Host "?? [2/4] Scanning for hardcoded colors in XAML files..." -ForegroundColor Yellow

$xamlFiles = Get-ChildItem -Path $projectRoot -Recurse -Include *.xaml | 
    Where-Object { 
        $path = $_.FullName
        $name = $_.Name
        $exclude = $false
        
        # Exclude directories
        foreach ($dir in $excludeDirs) {
            if ($path -like "*\$dir\*") {
                $exclude = $true
                break
            }
        }
        
        # Exclude specific files
        foreach ($excludeFile in $excludeFiles) {
            if ($name -eq $excludeFile) {
                $exclude = $true
                break
            }
        }
        
        -not $exclude
    }

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw
    $relPath = $file.FullName.Replace($projectRoot, "").TrimStart('\')
    
    # Pattern: Color="#RRGGBB" or BackgroundColor="#RRGGBB" etc.
    $colorProps = @("Color", "BackgroundColor", "TextColor", "BorderColor", "StrokeColor", "Stroke", "Fill")
    foreach ($prop in $colorProps) {
        $pattern = "$prop\s*=\s*""#[0-9A-Fa-f]{6}"""
        $matches = [regex]::Matches($content, $pattern)
        foreach ($match in $matches) {
            $violations.HardcodedColorsXAML += [PSCustomObject]@{
                File = $relPath
                Line = ($content.Substring(0, $match.Index) -split "`n").Count
                Code = $match.Value
                Type = "Inline Hex Color"
            }
            $totalViolations++
        }
    }
}

Write-Host "   Found: $($violations.HardcodedColorsXAML.Count) hardcoded colors in XAML" -ForegroundColor $(if($violations.HardcodedColorsXAML.Count -eq 0){"Green"}else{"Red"})

# ============================================================================
# 3. INLINE FONT SIZES IN XAML
# ============================================================================

Write-Host "?? [3/4] Scanning for inline font sizes in XAML files..." -ForegroundColor Yellow

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw
    $relPath = $file.FullName.Replace($projectRoot, "").TrimStart('\')
    
    # Pattern: FontSize="number" (not using StaticResource)
    $pattern = 'FontSize\s*=\s*"\d+"'
    $matches = [regex]::Matches($content, $pattern)
    foreach ($match in $matches) {
        # Check if this line already has a Style attribute (which might set FontSize)
        $lineStart = $content.LastIndexOf("<", $match.Index)
        $lineEnd = $content.IndexOf(">", $match.Index)
        if ($lineEnd -eq -1) { $lineEnd = $content.Length }
        $element = $content.Substring($lineStart, $lineEnd - $lineStart)
        
        # Allow if it's in a Style definition itself
        if ($element -notmatch '<Style\s+' -and $element -notmatch '<Setter\s+') {
            $violations.InlineFontSizes += [PSCustomObject]@{
                File = $relPath
                Line = ($content.Substring(0, $match.Index) -split "`n").Count
                Code = $match.Value
                Type = "Inline FontSize"
            }
            $totalViolations++
        }
    }
}

Write-Host "   Found: $($violations.InlineFontSizes.Count) inline font sizes" -ForegroundColor $(if($violations.InlineFontSizes.Count -eq 0){"Green"}else{"Red"})

# ============================================================================
# 4. ARBITRARY SPACING IN XAML
# ============================================================================

Write-Host "?? [4/4] Scanning for arbitrary spacing values in XAML files..." -ForegroundColor Yellow

foreach ($file in $xamlFiles) {
    $content = Get-Content $file.FullName -Raw
    $relPath = $file.FullName.Replace($projectRoot, "").TrimStart('\')
    
    # Pattern: Margin="arbitrary" or Padding="arbitrary" (not using StaticResource)
    $spacingProps = @("Margin", "Padding", "Spacing", "RowSpacing", "ColumnSpacing")
    foreach ($prop in $spacingProps) {
        # Look for inline values like Margin="10" or Margin="10,20"
        $pattern = "$prop\s*=\s*""\d+(?:,\d+)*"""
        $matches = [regex]::Matches($content, $pattern)
        foreach ($match in $matches) {
            # Check if this is in a Style or Setter
            $lineStart = $content.LastIndexOf("<", $match.Index)
            $lineEnd = $content.IndexOf(">", $match.Index)
            if ($lineEnd -eq -1) { $lineEnd = $content.Length }
            $element = $content.Substring($lineStart, $lineEnd - $lineStart)
            
            # Allow if it's in a Style definition or if value is 0
            if ($element -notmatch '<Style\s+' -and $element -notmatch '<Setter\s+' -and $match.Value -notmatch '="0"') {
                $violations.ArbitrarySpacing += [PSCustomObject]@{
                    File = $relPath
                    Line = ($content.Substring(0, $match.Index) -split "`n").Count
                    Code = $match.Value
                    Type = "Inline Spacing"
                }
                $totalViolations++
            }
        }
    }
}

Write-Host "   Found: $($violations.ArbitrarySpacing.Count) arbitrary spacing values`n" -ForegroundColor $(if($violations.ArbitrarySpacing.Count -eq 0){"Green"}else{"Red"})

# ============================================================================
# REPORT GENERATION
# ============================================================================

Write-Host "?? VERIFICATION RESULTS" -ForegroundColor Cyan
Write-Host "=" * 80 -ForegroundColor Gray

Write-Host "`n?? HARDCODED COLORS (C#): " -NoNewline
Write-Host "$($violations.HardcodedColorsCS.Count)" -ForegroundColor $(if($violations.HardcodedColorsCS.Count -eq 0){"Green"}else{"Red"})

if ($violations.HardcodedColorsCS.Count -gt 0) {
    $violations.HardcodedColorsCS | Group-Object File | ForEach-Object {
        Write-Host "   ?? $($_.Name): $($_.Count) violation(s)" -ForegroundColor Yellow
        $_.Group | ForEach-Object {
            Write-Host "      Line $($_.Line): $($_.Code)" -ForegroundColor Gray
        }
    }
}

Write-Host "`n?? HARDCODED COLORS (XAML): " -NoNewline
Write-Host "$($violations.HardcodedColorsXAML.Count)" -ForegroundColor $(if($violations.HardcodedColorsXAML.Count -eq 0){"Green"}else{"Red"})

if ($violations.HardcodedColorsXAML.Count -gt 0) {
    $violations.HardcodedColorsXAML | Group-Object File | ForEach-Object {
        Write-Host "   ?? $($_.Name): $($_.Count) violation(s)" -ForegroundColor Yellow
        $_.Group | ForEach-Object {
            Write-Host "      Line $($_.Line): $($_.Code)" -ForegroundColor Gray
        }
    }
}

Write-Host "`n?? INLINE FONT SIZES: " -NoNewline
Write-Host "$($violations.InlineFontSizes.Count)" -ForegroundColor $(if($violations.InlineFontSizes.Count -eq 0){"Green"}else{"Red"})

if ($violations.InlineFontSizes.Count -gt 0) {
    $violations.InlineFontSizes | Group-Object File | ForEach-Object {
        Write-Host "   ?? $($_.Name): $($_.Count) violation(s)" -ForegroundColor Yellow
        $_.Group | Select-Object -First 5 | ForEach-Object {
            Write-Host "      Line $($_.Line): $($_.Code)" -ForegroundColor Gray
        }
        if ($_.Count -gt 5) {
            Write-Host "      ... and $($_.Count - 5) more" -ForegroundColor Gray
        }
    }
}

Write-Host "`n?? ARBITRARY SPACING: " -NoNewline
Write-Host "$($violations.ArbitrarySpacing.Count)" -ForegroundColor $(if($violations.ArbitrarySpacing.Count -eq 0){"Green"}else{"Red"})

if ($violations.ArbitrarySpacing.Count -gt 0) {
    $violations.ArbitrarySpacing | Group-Object File | ForEach-Object {
        Write-Host "   ?? $($_.Name): $($_.Count) violation(s)" -ForegroundColor Yellow
        $_.Group | Select-Object -First 5 | ForEach-Object {
            Write-Host "      Line $($_.Line): $($_.Code)" -ForegroundColor Gray
        }
        if ($_.Count -gt 5) {
            Write-Host "      ... and $($_.Count - 5) more" -ForegroundColor Gray
        }
    }
}

Write-Host "`n" + ("=" * 80) -ForegroundColor Gray
Write-Host "TOTAL VIOLATIONS: " -NoNewline
Write-Host $totalViolations -ForegroundColor $(if($totalViolations -eq 0){"Green"}else{"Red"})

if ($totalViolations -eq 0) {
    Write-Host "`n? SUCCESS: Zero hardcoded values detected!" -ForegroundColor Green
    Write-Host "   All design tokens are being used correctly." -ForegroundColor Green
    exit 0
} else {
    Write-Host "`n? FAIL: $totalViolations hardcoded value(s) found." -ForegroundColor Red
    Write-Host "   Please replace hardcoded values with Design System tokens." -ForegroundColor Yellow
    Write-Host "`n?? Quick Fixes:" -ForegroundColor Cyan
    Write-Host "   • C# Colors: Use Application.Current.Resources[""ColorName""]" -ForegroundColor Gray
    Write-Host "   • XAML Colors: Use {StaticResource ColorName}" -ForegroundColor Gray
    Write-Host "   • Font Sizes: Use Style=""{StaticResource TextStyleName}""" -ForegroundColor Gray
    Write-Host "   • Spacing: Use {StaticResource SpacingMd} or {StaticResource ThicknessMd}" -ForegroundColor Gray
    exit 1
}
