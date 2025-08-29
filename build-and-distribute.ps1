try {
    Write-Host "=== DAT1-GUI Build and Distribution Script ===" -ForegroundColor Green
    Write-Host ""

    Write-Host "Step 1: Cleaning previous builds..." -ForegroundColor Yellow
    try {
        if (Test-Path "dist") {
            Remove-Item "dist" -Recurse -Force -ErrorAction Stop
            Write-Host "  - Removed old dist folder" -ForegroundColor Gray
        }
        if (Test-Path "DAT1-GUI-Release.zip") {
            Remove-Item "DAT1-GUI-Release.zip" -Force -ErrorAction Stop
            Write-Host "  - Removed old release ZIP" -ForegroundColor Gray
        }
        if (Test-Path "DAT1-GUI-Release-Clean.zip") {
            Remove-Item "DAT1-GUI-Release-Clean.zip" -Force -ErrorAction Stop
            Write-Host "  - Removed old clean release ZIP" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "  Warning: Could not clean some files: $($_.Exception.Message)" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "Step 2: Building project in Release mode..." -ForegroundColor Yellow
    try {
        $buildResult = dotnet build --configuration Release
        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with exit code $LASTEXITCODE"
        }
        Write-Host "  Build successful" -ForegroundColor Green
    }
    catch {
        Write-Host "  Build failed!" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "Step 3: Creating distribution folder..." -ForegroundColor Yellow
    try {
        if (Test-Path "dist") {
            Remove-Item "dist" -Recurse -Force -ErrorAction Stop
        }
        New-Item -ItemType Directory -Name "dist" -ErrorAction Stop | Out-Null
        Write-Host "  Created dist folder" -ForegroundColor Green
    }
    catch {
        Write-Host "  Failed to create dist folder!" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "Step 4: Copying built files..." -ForegroundColor Yellow
    try {
        $sourcePath = "ModdingTool\bin\Release\net7.0-windows"
        if (-not (Test-Path $sourcePath)) {
            throw "Build output not found at: $sourcePath"
        }
        Copy-Item "$sourcePath\*" "dist\" -Recurse -ErrorAction Stop
        Write-Host "  Copied all built files" -ForegroundColor Green
    }
    catch {
        Write-Host "  Failed to copy built files!" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "Step 5: Copying documentation..." -ForegroundColor Yellow
    try {
        if (Test-Path "README.md") {
            Copy-Item "README.md" "dist\" -ErrorAction Stop
            Write-Host "  Copied README.md" -ForegroundColor Green
        } else {
            Write-Host "  Warning: README.md not found" -ForegroundColor Yellow
        }
        
        if (Test-Path "LICENSE") {
            Copy-Item "LICENSE" "dist\" -ErrorAction Stop
            Write-Host "  Copied LICENSE" -ForegroundColor Green
        } else {
            Write-Host "  Warning: LICENSE not found" -ForegroundColor Yellow
        }
    }
    catch {
        Write-Host "  Failed to copy documentation!" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "Step 6: Creating launcher script..." -ForegroundColor Yellow
    try {
        @"
@echo off
echo Starting DAT1-GUI...
DAT1-GUI.exe
pause
"@ | Out-File -FilePath "dist\Run-DAT1-GUI.bat" -Encoding ASCII -ErrorAction Stop
        Write-Host "  Created Run-DAT1-GUI.bat" -ForegroundColor Green
    }
    catch {
        Write-Host "  Failed to create launcher script!" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "Step 7: Creating installation guide..." -ForegroundColor Yellow
    try {
        @"
DAT1-GUI Installation Guide
============================

This is a .NET 7.0 Windows application for modding Insomniac Games Games (lmao).

Requirements:
- Windows 10 or later
- .NET 7.0 Runtime (if not self-contained)

Installation:
1. Extract all files to a folder of your choice
2. Run DAT1-GUI.exe or Run-DAT1-GUI.bat
3. If you get an error about missing .NET runtime, download and install .NET 7.0 Desktop Runtime from Microsoft

Usage:
- Use the application to open .toc files from Insomniac Games
- Supported games: Spider-Man Remastered, Spider-Man Miles Morales, Ratchet And Clank: Rift Apart, Spider-Man 2

For more information, see README.md
"@ | Out-File -FilePath "dist\INSTALLATION.txt" -Encoding UTF8 -ErrorAction Stop
        Write-Host "  Created INSTALLATION.txt" -ForegroundColor Green
    }
    catch {
        Write-Host "  Failed to create installation guide!" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "Step 8: Removing debug files..." -ForegroundColor Yellow
    try {
        $pdbFiles = Get-ChildItem "dist\*.pdb" -ErrorAction SilentlyContinue
        if ($pdbFiles) {
            Remove-Item "dist\*.pdb" -Force -ErrorAction Stop
            Write-Host "  Removed $($pdbFiles.Count) PDB files" -ForegroundColor Green
        } else {
            Write-Host "  No PDB files found to remove" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "  Warning: Could not remove all PDB files: $($_.Exception.Message)" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "Step 9: Cleaning up unused icon packs..." -ForegroundColor Yellow
    try {
        $keepFiles = @(
            "MahApps.Metro.IconPacks.Core.dll",
            "MahApps.Metro.IconPacks.SimpleIcons.dll", 
            "MahApps.Metro.IconPacks.Material.dll"
        )

        $iconPackFiles = Get-ChildItem "dist\MahApps.Metro.IconPacks.*.dll" -ErrorAction SilentlyContinue
        if ($iconPackFiles) {
            $removedCount = 0
            foreach ($file in $iconPackFiles) {
                if ($file.Name -notin $keepFiles) {
                    try {
                        Remove-Item $file.FullName -ErrorAction Stop
                        Write-Host "  - Removed: $($file.Name)" -ForegroundColor Gray
                        $removedCount++
                    }
                    catch {
                        Write-Host "  - Warning: Could not remove $($file.Name): $($_.Exception.Message)" -ForegroundColor Yellow
                    }
                }
            }
            Write-Host "  Removed $removedCount unused icon pack DLLs" -ForegroundColor Green
        } else {
            Write-Host "  No icon pack DLLs found" -ForegroundColor Gray
        }
    }
    catch {
        Write-Host "  Warning: Error during icon pack cleanup: $($_.Exception.Message)" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Host "Step 10: Creating distribution ZIP files..." -ForegroundColor Yellow
    try {
        if (-not (Test-Path "dist")) {
            throw "Dist folder not found"
        }
        
        $distFiles = Get-ChildItem "dist\*" -ErrorAction SilentlyContinue
        if (-not $distFiles) {
            throw "No files found in dist folder"
        }

        Compress-Archive -Path "dist\*" -DestinationPath "DAT1-GUI-Release.zip" -Force -ErrorAction Stop
        $cleanSize = [math]::Round((Get-Item "DAT1-GUI-Release.zip").Length / 1MB, 1)
        Write-Host "  Created DAT1-GUI-Release.zip ($cleanSize MB)" -ForegroundColor Green
    }
    catch {
        Write-Host "  Failed to create ZIP file!" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }

    Write-Host ""
    Write-Host "=== Build Complete! ===" -ForegroundColor Green
    Write-Host ""
    Write-Host "Distribution files created:" -ForegroundColor Cyan
    Write-Host "  DAT1-GUI-Release.zip ($cleanSize MB)" -ForegroundColor White
    Write-Host ""
    Write-Host "Distribution folder: dist\" -ForegroundColor Cyan
    Write-Host "  Files ready for shipping" -ForegroundColor White
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Test the application by running dist\DAT1-GUI.exe" -ForegroundColor White
    Write-Host "  2. Upload DAT1-GUI-Release.zip to releases" -ForegroundColor White
    Write-Host "  3. Pray that it works" -ForegroundColor White
    Write-Host ""
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
catch {
    Write-Host ""
    Write-Host "=== CRITICAL ERROR ===" -ForegroundColor Red
    Write-Host "An unexpected error occurred:" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack Trace: $($_.ScriptStackTrace)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}
