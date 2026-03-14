# 🚀 Improvement Ideas — Automation Content

Danh sách ý tưởng cải tiến, sắp xếp theo mức ưu tiên.

> ⚠️ **QUY TẮC BẮT BUỘC:** Mỗi khi thay đổi code, **PHẢI** cập nhật docs tương ứng.
> Xem chi tiết tại `HANDOFF.md` mục 11.

---

## Ưu tiên Cao (Nên làm sớm)

### 1. Bundle yt-dlp và ffmpeg
**Vấn đề:** User phải cài yt-dlp/ffmpeg riêng → cản trở người dùng mới.

**Giải pháp:**
- Download yt-dlp binary (single file) vào thư mục app
- Download ffmpeg-static vào thư mục app
- Tự động detect và báo nếu thiếu, offer auto-download

**Kỹ thuật:**
```csharp
// Auto-download yt-dlp nếu chưa có
var ytDlpPath = Path.Combine(AppDataPath, "yt-dlp");
if (!File.Exists(ytDlpPath))
{
    var downloadUrl = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        ? "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp_macos"
        : "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    await DownloadFileAsync(downloadUrl, ytDlpPath);
}
```

### 2. Settings Persistence
**Vấn đề:** Save folder reset mỗi lần restart app.

**Giải pháp:**
- Lưu settings vào JSON file trong AppData folder
- Lưu: save folder, default quality, window size/position
- Load khi khởi động

**File:**
```
~/Library/Application Support/AutomationContent/settings.json  (macOS)
%AppData%/AutomationContent/settings.json                       (Windows)
```

### 3. Download History
**Vấn đề:** Không biết đã download video nào.

**Giải pháp:**
- Thêm tab "History" với danh sách downloads
- Lưu vào SQLite hoặc LiteDB
- Mỗi record: title, URL, quality, file path, timestamp, file size

### 4. App Icon
**Vấn đề:** App chưa có custom icon.

**Giải pháp:**
- Tạo icon 1024x1024 PNG
- Convert sang .icns (macOS) và .ico (Windows)
- Thêm vào csproj:
```xml
<PropertyGroup>
    <ApplicationIcon>Assets/app.ico</ApplicationIcon>
</PropertyGroup>
```

---

## Ưu tiên Trung bình (Nice to have)

### 5. Hỗ trợ nhiều platform hơn
**Mở rộng validation để support:**
- TikTok
- Instagram Reels
- Twitter/X videos
- Facebook video
- Vimeo

yt-dlp đã hỗ trợ 1000+ sites → chỉ cần mở rộng URL validation.

### 6. Batch Download (Tải nhiều video)
- Cho phép paste nhiều URLs (1 URL / dòng)
- Hiện danh sách với progress riêng cho mỗi video
- Download tuần tự hoặc song song (configurable)

### 7. Dark/Light Theme Toggle
```xml
<!-- Thêm toggle trong UI -->
<ToggleSwitch Content="Dark Mode"
              IsChecked="{Binding IsDarkMode}"
              OnContent="🌙" OffContent="☀️" />
```

### 8. Playlist Support
- Detect YouTube playlist URL
- List tất cả video trong playlist
- Cho phép chọn video nào muốn download
- Download tất cả hoặc từng video

### 9. Subtitle Download
```bash
# yt-dlp đã support
yt-dlp --write-sub --sub-lang en,vi --convert-subs srt "<url>"
```
- Thêm checkbox "Download subtitles"
- Chọn language

### 10. Preview Audio/Video trước khi download
- Thêm nút Play để nghe thử (stream trực tiếp)
- Dùng LibVLCSharp hoặc Avalonia.Media

---

## Ưu tiên Thấp (Long-term)

### 11. Auto-update yt-dlp
```bash
yt-dlp -U  # Self-update command
```
- Kiểm tra và tự cập nhật yt-dlp khi có version mới
- Hiện notification khi có update

### 12. Converter Tool
- Convert format sau khi download (MP4→AVI, MP3→WAV, etc.)
- Trim video (cắt đoạn)
- Extract audio từ video đã download

### 13. Scheduled Download
- Hẹn giờ download
- Download khi internet rảnh (ví dụ: ban đêm)

### 14. Keyboard Shortcuts
| Shortcut | Action |
|----------|--------|
| Ctrl+V / Cmd+V | Auto-paste URL vào text field |
| Ctrl+Enter | Get Info |
| Ctrl+D | Download |
| Escape | Cancel download |
| Ctrl+O | Open save folder |

### 15. System Tray / Menu Bar
- Thu nhỏ xuống system tray (Windows) hoặc menu bar (macOS)
- Show progress notification
- Quick paste & download từ clipboard

### 16. Drag & Drop URL
- Kéo URL từ browser thả vào app
- Auto-detect và fetch info

---

## Technical Improvements

### T1. Migrate to CommunityToolkit.Mvvm
```bash
dotnet add package CommunityToolkit.Mvvm
```
- Giảm boilerplate code với `[ObservableProperty]`, `[RelayCommand]`
- Source generators → runtime performance tốt hơn

### T2. Unit Tests
- Test `YtDlpService.IsValidYouTubeUrl()` với nhiều URL formats
- Test `YtDlpService.ParseProgress()` với sample yt-dlp output
- Test `YtDlpService.GetFriendlyError()` mapping
- Test `VideoInfo.FormattedDuration`, `FormattedViewCount`
- Test ViewModel state transitions

### T3. Dependency Injection
```csharp
// Thay vì tạo service trực tiếp trong ViewModel
var services = new ServiceCollection();
services.AddSingleton<IYtDlpService, YtDlpService>();
services.AddTransient<MainViewModel>();
```

### T4. Logging
```bash
dotnet add package Microsoft.Extensions.Logging
dotnet add package Serilog.Extensions.Logging
```
- Log mọi yt-dlp command + output
- Log errors chi tiết
- Hữu ích cho debugging

### T5. Localization (i18n)
- Hỗ trợ đa ngôn ngữ (Vietnamese, English, etc.)
- Dùng .resx resource files hoặc JSON i18n

### T6. CI/CD Pipeline
```yaml
# GitHub Actions example
- dotnet restore
- dotnet build --configuration Release
- dotnet publish -r osx-arm64 --self-contained
- dotnet publish -r win-x64 --self-contained
# Create DMG for macOS, MSI/NSIS for Windows
```

### T7. Code Signing
- macOS: Apple Developer certificate + notarization
- Windows: Code signing certificate
- Cần thiết để distribute app không bị block bởi OS

### T8. Error Telemetry (Optional)
- Gửi crash reports ẩn danh
- Giúp phát hiện bugs nhanh hơn
- Dùng Sentry hoặc AppCenter

### T9. Docs Automation (Pre-commit hook)
- Thêm pre-commit hook nhắc nhở cập nhật docs khi có thay đổi code
- Có thể dùng `husky` hoặc custom git hook
- Check xem file trong `docs/` có được modified cùng với code changes không
- Tự động ghi timestamp vào Changelog

```bash
# .git/hooks/pre-commit (ví dụ)
#!/bin/bash
CODE_CHANGED=$(git diff --cached --name-only | grep -E '\.(cs|axaml|csproj)$' | wc -l)
DOCS_CHANGED=$(git diff --cached --name-only | grep -E '^docs/' | wc -l)
if [ "$CODE_CHANGED" -gt 0 ] && [ "$DOCS_CHANGED" -eq 0 ]; then
    echo "⚠️  Code changed but docs not updated! Please update docs/"
    echo "   See HANDOFF.md section 11 for guidelines."
    exit 1
fi
```
