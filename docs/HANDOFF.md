# 📦 Automation Content — Handoff Documentation

> **Ngày tạo:** 14/03/2026
> **Trạng thái:** MVP hoàn thành, build thành công, chạy được trên macOS
> **Tác giả:** AI Assistant (Antigravity)

---

## 1. Tổng quan Project

**Automation Content** là ứng dụng desktop cross-platform (Windows/macOS) cho phép người dùng download video từ YouTube với giao diện thân thiện. App sử dụng `yt-dlp` (công cụ command-line miễn phí) để thực hiện download phía dưới.

### Chức năng chính
| # | Chức năng | Trạng thái |
|---|-----------|------------|
| 1 | Paste URL YouTube vào text field | ✅ Hoàn thành |
| 2 | Bấm "Get Info" xem preview video (title, thumbnail, duration, views, channel) | ✅ Hoàn thành |
| 3 | Chọn quality: 1080p, 720p, 480p, Audio only | ✅ Hoàn thành |
| 4 | Chọn thư mục lưu (native folder picker) | ✅ Hoàn thành |
| 5 | Download với progress bar real-time | ✅ Hoàn thành |
| 6 | Cancel download bất kỳ lúc nào | ✅ Hoàn thành |
| 7 | Thông báo "Done!" + nút "Open Folder" | ✅ Hoàn thành |
| 8 | Audio only → lưu .mp3, Video → lưu .mp4 | ✅ Hoàn thành |
| 9 | Error message thân thiện (không hiện error code kỹ thuật) | ✅ Hoàn thành |
| 10 | Transcribe video/audio → text (Whisper.net — local, miễn phí) | ✅ Hoàn thành |
| 11 | Nút "Transcribe this video" sau khi download xong | ✅ Hoàn thành |
| 12 | Drag-and-drop file .mp4/.mp3 để transcribe | ✅ Hoàn thành |
| 13 | Hiển transcript với timestamps [MM:SS] + phân đoạn tự động | ✅ Hoàn thành |
| 14 | Copy transcript / Save as .txt | ✅ Hoàn thành |
| 15 | Tự động detect ngôn ngữ (Vietnamese, English, ...) | ✅ Hoàn thành |
| 16 | Không cần API key — chạy hoàn toàn offline | ✅ Hoàn thành |
| 17 | AI Lồng tiếng (Edge TTS) — tạo giọng nói Vietnamese từ transcript | ✅ Hoàn thành |
| 18 | Chọn giọng đọc: Nữ HoaiMy / Nam NamMinh (miền Nam) | ✅ Hoàn thành |
| 19 | Chọn tốc độ đọc: Chậm / Bình thường / Nhanh | ✅ Hoàn thành |
| 20 | Chỉnh sửa transcript text trước khi tạo giọng nói | ✅ Hoàn thành |
| 21 | Progress hiển thị đoạn đang xử lý ("đoạn 3/12") | ✅ Hoàn thành |
| 22 | Nghe thử audio trong app (Play/Stop) | ✅ Hoàn thành |
| 23 | Lưu file voiceover .mp3 cùng thư mục video | ✅ Hoàn thành |
| 24 | Dịch transcript sang 12 ngôn ngữ (Google Translate — miễn phí) | ✅ Hoàn thành |
| 25 | Giọng lồng tiếng tự động thay đổi theo ngôn ngữ đã chọn | ✅ Hoàn thành |
| 26 | AI Tạo ảnh minh họa từ transcript (Pollinations.AI — miễn phí) | ✅ Hoàn thành |
| 27 | Tự động tạo prompt ảnh từ mỗi đoạn transcript | ✅ Hoàn thành |
| 28 | Chọn phong cách ảnh: Realistic / Digital art / Flat design / Cinematic | ✅ Hoàn thành |
| 29 | Chỉnh sửa prompt ảnh + tạo lại ảnh đơn lẻ | ✅ Hoàn thành |
| 30 | Hiển thị grid ảnh với paragraph text bên dưới | ✅ Hoàn thành |
| 31 | Retry button cho ảnh lỗi/timeout | ✅ Hoàn thành |
| 32 | Nút "Next: Render Video →" khi tất cả ảnh sẵn sàng | ✅ Hoàn thành |

---

## 2. Tech Stack & Lý do chọn

| Công nghệ | Phiên bản | Lý do |
|-----------|-----------|-------|
| **.NET** | 9.0 | SDK mới nhất, hỗ trợ tốt cross-platform |
| **Avalonia UI** | 11.2.3 | Framework UI cross-platform tốt nhất cho .NET (hỗ trợ Windows, macOS, Linux). Ưu điểm so với MAUI: macOS support ổn định hơn, cộng đồng active |
| **Avalonia.Themes.Fluent** | 11.2.3 | Dark Fluent theme sẵn có, hiện đại |
| **Avalonia.Fonts.Inter** | 11.2.3 | Font Inter — typography modern cho UI |
| **Newtonsoft.Json** | 13.0.4 | Parse JSON output từ yt-dlp (JObject.Parse linh hoạt hơn System.Text.Json cho dynamic JSON) |
| **Whisper.net** | 1.9.0 | Chạy Whisper model (speech-to-text) local, không cần API key, miễn phí |
| **Whisper.net.AllRuntimes** | 1.9.0 | Native runtimes cho tất cả platforms (Windows, macOS, Linux) |
| **yt-dlp** | CLI tool | Download engine, được gọi qua `System.Diagnostics.Process` |
| **ffmpeg** | CLI tool | Cần thiết để merge video+audio streams và extract audio |
| **edge-tts** | CLI tool (Python) | Microsoft Edge TTS — tạo giọng nói Vietnamese miễn phí, gọi qua Process |
| **Google Translate** | Free HTTP API | Dịch thuật miễn phí qua `translate.googleapis.com` (không cần API key) |
| **Pollinations.AI** | Free HTTP API | Tạo ảnh AI qua `image.pollinations.ai` + tóm tắt text qua `text.pollinations.ai` (hỗ trợ API key hoặc chế độ miễn phí) |

### Tại sao không dùng MAUI?
- MAUI trên macOS vẫn còn nhiều bug và hạn chế
- Avalonia UI có cộng đồng lớn hơn cho desktop apps
- Avalonia support compiled bindings tốt hơn
- XAML syntax gần giống WPF, dễ chuyển đổi

### Tại sao dùng yt-dlp CLI thay vì library?
- Không có .NET library chính thức tốt cho YouTube download
- yt-dlp là công cụ được maintain tích cực, cập nhật thường xuyên khi YouTube thay đổi API
- Gọi qua Process cho phép cập nhật yt-dlp độc lập (không cần rebuild app)
- Parse stdout/stderr đơn giản và đáng tin cậy

---

## 3. Kiến trúc

### Pattern: MVVM (Model-View-ViewModel)

```
┌──────────────────────────────────────────────────────┐
│                    VIEW (AXAML)                       │
│  MainWindow.axaml          MainWindow.axaml.cs        │
│  - UI Layout                - Event handlers          │
│  - Data Bindings            - Folder picker dialog    │
│  - Styles/Theme             - Delegates to ViewModel  │
└──────────────┬───────────────────────────────────────┘
               │ Data Binding (INotifyPropertyChanged)
               ▼
┌──────────────────────────────────────────────────────┐
│                   VIEWMODEL                           │
│  MainViewModel.cs                                     │
│  - State management (properties)                      │
│  - Business logic (GetVideoInfo, Download, Cancel)    │
│  - Computed properties (CanGetInfo, CanDownload, etc) │
│  - UI thread dispatching                              │
└──────────────┬───────────────────────────────────────┘
               │ Uses
               ▼
┌──────────────────────────────────────────────────────┐
│                    SERVICE                            │
│  YtDlpService.cs                                      │
│  - Wraps yt-dlp CLI                                   │
│  - GetVideoInfoAsync() → calls yt-dlp --dump-json     │
│  - DownloadAsync() → calls yt-dlp with progress       │
│  - CancelDownload() → kills yt-dlp process            │
│  - ParseProgress() → regex parse stdout               │
│  - GetFriendlyError() → map error → user message      │
└──────────────┬───────────────────────────────────────┘
               │ Process.Start()
               ▼
┌──────────────────────────────────────────────────────┐
│              EXTERNAL TOOLS                           │
│  yt-dlp (CLI)        ffmpeg (CLI)                     │
│  - Download engine   - Merge video+audio              │
│  - Video info        - Extract audio to MP3           │
└──────────────────────────────────────────────────────┘
```

### Models
```
VideoInfo          — Chứa metadata video (title, thumbnail, duration, channel, views)
VideoQuality       — Enum (1080p, 720p, 480p, AudioOnly)
                   — Extension methods: ToYtDlpFormat(), GetFileExtension(), ToDisplayString()
```

---

## 4. Cấu trúc thư mục

```
AutomationContent/
├── AutomationContent.csproj    # Project config, NuGet packages
├── Program.cs                   # Entry point (STAThread, Avalonia init)
├── App.cs                       # Application lifecycle
├── App.axaml                    # Theme config (FluentTheme, Dark mode)
├── MainWindow.axaml             # ⭐ Toàn bộ UI layout + styles
├── MainWindow.axaml.cs          # Event handlers (click, folder picker, drag-drop)
├── Models/
│   ├── VideoInfo.cs             # Video metadata model
│   ├── VideoQuality.cs          # Quality enum + yt-dlp format mapping
│   ├── TranscriptSegment.cs     # ⭐ Transcript segment + result models
│   └── ImageGenerationItem.cs   # ⭐ Image generation item model + ImageStyle enum
├── ViewModels/
│   └── MainViewModel.cs         # ⭐ Business logic, state management
├── Services/
│   ├── YtDlpService.cs          # ⭐ yt-dlp CLI wrapper
│   ├── WhisperService.cs        # ⭐ Whisper.net local transcription + ffmpeg audio extraction
│   ├── EdgeTtsService.cs        # ⭐ Edge TTS CLI wrapper — AI voiceover generation
│   ├── TranslationService.cs    # ⭐ Google Translate wrapper — dịch thuật miễn phí
│   └── PollinationsService.cs   # ⭐ Pollinations.AI wrapper — AI image generation miễn phí
├── docs/
│   ├── HANDOFF.md               # 📄 Tài liệu này
│   ├── ARCHITECTURE.md          # 📄 Chi tiết kiến trúc
│   └── IMPROVEMENT_IDEAS.md     # 📄 Ý tưởng cải tiến
├── README.md                    # README cho end users
└── .gitignore
```

---

## 5. Giải thích chi tiết từng file

### 5.1 `Program.cs` — Entry Point
```csharp
// Khởi tạo Avalonia app với:
// - UsePlatformDetect(): tự nhận diện OS (Windows/macOS/Linux)
// - WithInterFont(): load font Inter
// - LogToTrace(): log ra Debug output
BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
```

### 5.2 `App.axaml` — Theme
- Sử dụng `FluentTheme` (built-in Avalonia)
- `RequestedThemeVariant="Dark"` → dark mode mặc định
- Có thể đổi sang `Light` hoặc `Default` (follow OS)

### 5.3 `MainWindow.axaml` — UI Layout (⭐ File quan trọng nhất)

**Cấu trúc UI từ trên xuống:**
1. **Header** — Tên app + tagline
2. **URL Input Card** — TextBox + "Get Info" button
3. **Loading Indicator** — Indeterminate progress bar (hiện khi đang fetch info)
4. **Error Card** — Box đỏ hiện error message (ẩn/hiện bằng `IsVisible` binding)
5. **Video Preview Card** — Thumbnail + Title + Channel + Duration + Views
6. **Download Settings Card** — Quality ComboBox + Folder picker + Download/Cancel buttons
7. **Progress Card** — Progress bar + percentage + status text (hiện khi đang download)
8. **Success Card** — Box xanh hiện khi download xong + "Open Folder" button
9. **Status Bar** — Text nhỏ ở cuối hiện status

**Styles (inline trong Window.Styles):**
- `Button.primary` — Purple (#6c5ce7), hover/pressed/disabled states
- `Button.danger` — Red (#e74c3c)
- `Button.success` — Green (#00b894)
- `Button.secondary` — Gray (#2d2d4a) với border
- `TextBox.url-input` — Dark input field với focus highlight
- `ComboBox.quality-select` — Styled dropdown
- `Border.card` — Card container (rounded corners, dark background)
- `ProgressBar.download-progress` — Purple progress bar
- `TextBlock.label` — Small uppercase labels
- `TextBlock.section-title` — Section headers

**Color Palette:**
| Color | Hex | Dùng cho |
|-------|-----|----------|
| Background | `#1a1a2e` | Window background |
| Card | `#16213e` | Card containers |
| Border | `#2d2d4a` | Card borders, input borders |
| Primary | `#6c5ce7` | Buttons, accent, links |
| Primary Hover | `#7d6ff0` | Button hover state |
| Text | `#e0e0e0` | Main text |
| Text Secondary | `#8888aa` | Labels, secondary text |
| Text Tertiary | `#b0b0cc` | Section titles |
| Error | `#e74c3c` | Error states |
| Error Background | `#2d1520` | Error card background |
| Success | `#00b894` | Success states |
| Success Background | `#152d1e` | Success card background |

### 5.4 `MainWindow.axaml.cs` — Code-behind

Chỉ xử lý events, delegate logic sang ViewModel:
- `OnGetInfoClick` → `_viewModel.GetVideoInfoAsync()`
- `OnDownloadClick` → `_viewModel.DownloadAsync()`
- `OnCancelClick` → `_viewModel.CancelDownload()`
- `OnOpenFolderClick` → `_viewModel.OpenFolder()`
- `OnBrowseFolderClick` → Mở native folder picker dialog

### 5.5 `MainViewModel.cs` — Business Logic (⭐ File quan trọng)

**State Properties (tất cả dùng INotifyPropertyChanged):**
| Property | Type | Mô tả |
|----------|------|-------|
| `VideoUrl` | string | URL người dùng nhập |
| `VideoTitle` | string | Title video từ yt-dlp |
| `VideoDuration` | string | Duration đã format (MM:SS) |
| `VideoChannel` | string | Tên channel |
| `VideoViews` | string | Lượt xem đã format (1.2M views) |
| `ThumbnailBitmap` | Bitmap? | Ảnh thumbnail đã tải |
| `HasVideoInfo` | bool | Đã fetch video info chưa |
| `IsLoadingInfo` | bool | Đang fetch info |
| `IsDownloading` | bool | Đang download |
| `DownloadProgress` | double | Progress 0-100 |
| `ProgressText` | string | Text mô tả progress |
| `StatusMessage` | string | Status chung |
| `ErrorMessage` | string | Error message (hiện error card khi có) |
| `SuccessMessage` | string | Success message (hiện khi download xong) |
| `SaveFolder` | string | Thư mục lưu (default: ~/Downloads) |
| `SelectedQualityIndex` | int | Index của quality dropdown |

**Computed Properties:**
| Property | Logic |
|----------|-------|
| `CanGetInfo` | URL không rỗng AND không đang loading AND không đang download |
| `CanDownload` | Có video info AND không đang download AND có save folder |
| `CanCancel` | Đang download |

**Key Methods:**
- `GetVideoInfoAsync()` — Validate URL → gọi YtDlpService → set properties → load thumbnail
- `DownloadAsync()` — Tạo CancellationTokenSource → gọi YtDlpService → handle result
- `CancelDownload()` — Cancel token + kill yt-dlp process
- `OpenFolder()` — Mở thư mục chứa file (dùng `open` trên macOS, `explorer.exe` trên Windows)

**Threading:**
- Progress/Status callbacks từ YtDlpService chạy trên background thread
- Phải dùng `Dispatcher.UIThread.Post()` để cập nhật UI properties

### 5.6 `YtDlpService.cs` — yt-dlp Wrapper (⭐ File quan trọng)

**Khởi tạo:**
- Constructor tìm yt-dlp binary qua `FindYtDlp()` — thử các paths phổ biến
- `IsYtDlpInstalled()` — kiểm tra yt-dlp có chạy được không

**`GetVideoInfoAsync(url)`:**
```
yt-dlp --dump-json --no-download "<url>"
```
- Parse JSON output bằng JObject (Newtonsoft.Json)
- Trả về VideoInfo object
- Throw friendly error nếu thất bại

**`DownloadAsync(url, outputFolder, quality)`:**
```
yt-dlp -f "<format>" -o "<output>" --newline --progress --no-mtime [options] "<url>"
```
- Audio only: thêm `-x --audio-format mp3 --audio-quality 0`
- Video: thêm `--merge-output-format mp4`
- `--newline`: mỗi dòng progress trên 1 line (dễ parse)
- Parse progress real-time qua `OutputDataReceived` event

**Progress Parsing (Regex):**
```
Input:  [download]  45.2% of   50.00MiB at    5.00MiB/s ETA 00:06
Regex:  \[download\]\s+([\d.]+)%\s+of\s+~?([\d.]+\w+)\s+at\s+([\d.]+\w+/s)\s+ETA\s+(\S+)
Output: percent=45.2, size="50.00MiB", speed="5.00MiB/s", eta="00:06"
```

**File path detection:**
- Parse `[Merger] Merging formats into "..."` output
- Parse `[download] Destination: ...` output
- Parse `[ExtractAudio] Destination: ...` output
- Fallback: tìm file mới nhất trong output folder

**Friendly Error Mapping:**
| yt-dlp error | User message |
|-------------|--------------|
| "Video unavailable" | "This video is unavailable..." |
| "Private video" | "This video is private..." |
| "Sign in to confirm your age" | "This video requires age verification..." |
| "Unable to download webpage" | "Could not connect to YouTube..." |
| "HTTP Error 429" | "Too many requests..." |
| "HTTP Error 403" | "Access denied..." |
| "ffmpeg" | "FFmpeg is required but not found..." |

### 5.7 `VideoInfo.cs` — Model

Properties: Title, ThumbnailUrl, Duration, DurationSeconds, Channel, Url, ViewCount, UploadDate

Computed:
- `FormattedDuration` → "05:32" hoặc "01:15:30"
- `FormattedViewCount` → "1.2M views", "450K views"

### 5.8 `VideoQuality.cs` — Enum + Extensions

```csharp
// Ép codec H.264 (avc1) + AAC (mp4a) để đảm bảo tương thích mọi thiết bị.
// Nếu không tìm thấy H.264, fallback về format tốt nhất có sẵn.
VideoQuality.Quality1080p → "bestvideo[height<=1080][vcodec^=avc1]+bestaudio[acodec^=mp4a]/bestvideo[height<=1080]+bestaudio/best[height<=1080]"
VideoQuality.Quality720p  → "bestvideo[height<=720][vcodec^=avc1]+bestaudio[acodec^=mp4a]/bestvideo[height<=720]+bestaudio/best[height<=720]"
VideoQuality.Quality480p  → "bestvideo[height<=480][vcodec^=avc1]+bestaudio[acodec^=mp4a]/bestvideo[height<=480]+bestaudio/best[height<=480]"
VideoQuality.AudioOnly    → "bestaudio/best"
```

### 5.9 `TranscriptSegment.cs` — Transcript Models

- `TranscriptSegment` — 1 đoạn transcript với StartSeconds, EndSeconds, Text
- `TranscriptResult` — Kết quả toàn bộ từ Whisper API (FullText, Segments, DetectedLanguage, DurationSeconds)
- `ToFormattedText()` — Gom segments thành paragraphs (mỗi ~5s pause hoặc ~30s), thêm timestamp `[MM:SS]`

### 5.10 `WhisperService.cs` — Whisper.net Local Wrapper (⭐ File quan trọng)

**Khởi tạo:**
- Tìm ffmpeg binary qua `FindFfmpeg()` (giống cách tìm yt-dlp)
- Tạo thư mục `~/Library/Application Support/AutomationContent/models/` để lưu model

**`EnsureModelAsync()`:**
- Kiểm tra xem Whisper model (`ggml-base.bin`, ~150MB) đã có chưa
- Nếu chưa → tự động download từ Hugging Face lần đầu tiên
- Chỉ download 1 lần, lần sau dùng lại

**`ExtractAudioAsync(inputFilePath)`:**
```bash
ffmpeg -i "<input>" -ar 16000 -ac 1 -c:a pcm_s16le -y "<temp.wav>"
```
- Convert video → 16kHz mono WAV (required bởi Whisper)
- Lưu vào temp folder, tự dọn sau khi xong

**`TranscribeAsync(audioFilePath)`:**
- Chạy Whisper model trực tiếp trên máy (local inference)
- Không cần internet, không cần API key, không tốn tiền
- Tự detect ngôn ngữ qua `WithLanguageDetection()`
- Trả về segments với timestamps chi tiết
- Không có giới hạn file size (khác với API 25MB limit)

**Model Storage:**
- `~/Library/Application Support/AutomationContent/models/ggml-base.bin` (macOS)
- `%AppData%/AutomationContent/models/ggml-base.bin` (Windows)

---

## 6. Data Flow Diagrams

### Flow 1: Get Video Info
```
User pastes URL → Click "Get Info"
  → MainWindow.OnGetInfoClick()
    → MainViewModel.GetVideoInfoAsync()
      → Validate URL (regex check)
      → YtDlpService.GetVideoInfoAsync(url)
        → Process.Start("yt-dlp --dump-json --no-download <url>")
        → Parse JSON output → VideoInfo object
      → Set ViewModel properties (Title, Thumbnail, Duration, etc.)
      → LoadThumbnailAsync(thumbnailUrl)
        → HttpClient.GetByteArrayAsync(url)
        → new Bitmap(stream)
      → HasVideoInfo = true
        → UI shows Video Preview card + Download Settings card
```

### Flow 2: Download
```
User clicks "Download"
  → MainWindow.OnDownloadClick()
    → MainViewModel.DownloadAsync()
      → Create CancellationTokenSource
      → IsDownloading = true (shows progress card)
      → YtDlpService.DownloadAsync(url, folder, quality, ct)
        → Build yt-dlp args based on quality
        → Process.Start("yt-dlp -f <format> -o <output> ...")
        → OutputDataReceived event:
          → ParseProgress(line)
            → Regex match → extract percent, size, speed, ETA
            → Fire ProgressChanged event
              → ViewModel updates DownloadProgress + ProgressText
                → UI progress bar updates
        → WaitForExitAsync(ct) — supports cancellation
        → Return file path
      → SuccessMessage = "Done! File saved to: ..."
      → IsDownloading = false
```

### Flow 3: Cancel
```
User clicks "Cancel"
  → MainWindow.OnCancelClick()
    → MainViewModel.CancelDownload()
      → _downloadCts.Cancel() — triggers OperationCanceledException
      → YtDlpService.CancelDownload()
        → _currentProcess.Kill(entireProcessTree: true)
      → DownloadAsync catches OperationCanceledException
        → StatusMessage = "Download cancelled."
        → IsDownloading = false
```

### Flow 4: Transcribe Video (Local — Whisper.net)
```
User clicks "Transcribe this video" (sau khi download xong)
  HOẶC kéo thả file .mp4/.mp3 vào drop zone
  → MainWindow.OnTranscribeDownloadedClick() / OnDrop()
    → MainViewModel.TranscribeFileAsync(filePath)
      → IsTranscribing = true (hiện spinner "Đang nhận dạng giọng nói...")
      → Nếu không phải WAV: WhisperService.ExtractAudioAsync()
        → ffmpeg -i input -ar 16000 -ac 1 → temp.wav
      → WhisperService.EnsureModelAsync()
        → Check model file → nếu chưa có → download ggml-base.bin (~150MB)
      → WhisperService.TranscribeAsync(audioPath)
        → WhisperFactory.FromPath(modelPath) → tạo processor
        → processor.ProcessAsync(fileStream) → iterate segments
        → Collect segments → TranscriptResult
      → TranscriptText = result.ToFormattedText()
        → Gom segments thành paragraphs với [MM:SS] timestamps
      → UI hiện transcript trong scrollable TextBox
      → Buttons: "Copy transcript" + "Save as .txt"
      → Cleanup temp audio file
```

---

## 7. Cách chạy project

### Prerequisites
```bash
# 1. .NET 9 SDK
# Download: https://dotnet.microsoft.com/download/dotnet/9.0

# 2. yt-dlp
brew install yt-dlp          # macOS
winget install yt-dlp         # Windows

# 3. ffmpeg (cần cho merge video+audio, extract audio)
brew install ffmpeg           # macOS
winget install ffmpeg          # Windows
```

### Chạy Development
```bash
cd /Users/hieunguyen/WeekendApps/AutomationContent
dotnet restore
dotnet run
```

### Build Production
```bash
# macOS (Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained true

# macOS (Intel)
dotnet publish -c Release -r osx-x64 --self-contained true

# Windows
dotnet publish -c Release -r win-x64 --self-contained true

# Output: bin/Release/net9.0/<runtime>/publish/
```

---

## 8. Lưu ý kỹ thuật quan trọng

### 8.1 Compiled Bindings
- Avalonia 11 khuyến khích dùng compiled bindings (`x:DataType`)
- Đã set `AvaloniaUseCompiledBindingsByDefault=true` trong csproj
- Mọi binding trong AXAML cần `x:DataType` directive → đã thêm ở Window tag

### 8.2 Threading
- `YtDlpService` fire events trên background thread (vì đang đọc Process output)
- ViewModel phải dùng `Dispatcher.UIThread.Post()` để set properties trên UI thread
- Nếu không → crash hoặc UI không update

### 8.3 Process Management
- Dùng `Process.WaitForExitAsync(CancellationToken)` cho async wait
- Cancel = `_currentProcess.Kill(entireProcessTree: true)` — kill cả child processes
- `--newline` flag quan trọng: bắt yt-dlp xuất mỗi update progress trên 1 dòng mới

### 8.4 URL Validation
- Chỉ validate pattern phía client (regex check)
- Real validation xảy ra khi yt-dlp cố fetch info → friendly error nếu fail
- Hỗ trợ formats: `youtube.com/watch?v=`, `youtu.be/`, `youtube.com/shorts/`, `m.youtube.com/`

### 8.5 File Path Detection
- yt-dlp không có option trả về file path rõ ràng sau khi download
- Phải parse stdout để detect file path qua nhiều patterns:
  - `[Merger] Merging formats into "..."`
  - `[download] Destination: ...`
  - `[ExtractAudio] Destination: ...`
  - `[download] ... has already been downloaded`
- Fallback: tìm file mới nhất trong output folder

### 8.6 Output Type
- Csproj dùng `OutputType=WinExe` (không phải `Exe`)
- `WinExe` = không hiện console window trên Windows
- Trên macOS không có sự khác biệt

### 8.7 Codec Compatibility (⚠️ Quan trọng)
- YouTube hiện nay ưu tiên cung cấp video codec **AV1** (nén tốt hơn H.264 ~30%)
- Khi dùng format `bestvideo`, yt-dlp chọn AV1 vì nó có chất lượng tốt nhất
- **Vấn đề:** AV1 + Opus KHÔNG được hỗ trợ bởi nhiều player: QuickTime (macOS < 14), Windows Media Player, Smart TV đời cũ, điện thoại đời thấp
- **Giải pháp (đã áp dụng):** Dùng filter `[vcodec^=avc1]` (H.264) + `[acodec^=mp4a]` (AAC) trong format string → tương thích mọi thiết bị
- **Fallback:** Nếu H.264 không có sẵn cho resolution đó, yt-dlp sẽ tự dùng format tốt nhất có sẵn
- **Ngày fix:** 14/03/2026

---

## 9. Known Issues & Limitations

| Issue | Mô tả | Trạng thái | Giải pháp |
|-------|--------|------------|----------|
| ~~Video không xem được~~ | ~~yt-dlp chọn AV1+Opus, nhiều player không hỗ trợ~~ | ✅ Đã fix (14/03/2026) | Ép codec H.264+AAC qua format string |
| yt-dlp phải cài sẵn | App không bundle yt-dlp | ⬜ Chưa xử lý | Có thể bundle hoặc tự download yt-dlp binary |
| ffmpeg phải cài sẵn | Merge/extract cần ffmpeg | ⬜ Chưa xử lý | Có thể bundle ffmpeg-static |
| Không persist settings | Save folder reset khi restart app | ⬜ Chưa xử lý | Thêm settings file (JSON/XML) |
| Thumbnail không cache | Load lại thumbnail mỗi lần | ⬜ Chưa xử lý | Cache thumbnails to disk |
| Chỉ hỗ trợ YouTube | Regex validate chỉ check YouTube URLs | ⬜ Chưa xử lý | yt-dlp hỗ trợ nhiều site → mở rộng validation |
| Không có download history | | ⬜ Chưa xử lý | Thêm SQLite/LiteDB |
| Không có dark/light toggle | Luôn dark mode | ⬜ Chưa xử lý | Thêm theme switcher |
| Chưa có app icon | | ⬜ Chưa xử lý | Tạo icon và add vào csproj |
| Chưa có auto-update | | ⬜ Chưa xử lý | Thêm check version yt-dlp |

---

## 10. Changelog

| Ngày | Thay đổi | File ảnh hưởng |
|------|----------|----------------|
| 14/03/2026 | Khởi tạo project MVP | Tất cả |
| 14/03/2026 | Fix codec: ép H.264+AAC thay vì AV1+Opus để video xem được trên mọi thiết bị | `Models/VideoQuality.cs` |
| 14/03/2026 | Thêm feature Transcribe: speech-to-text bằng OpenAI Whisper API | `Services/WhisperService.cs`, `Models/TranscriptSegment.cs`, `ViewModels/MainViewModel.cs`, `MainWindow.axaml`, `MainWindow.axaml.cs` |
| 14/03/2026 | Chuyển Transcribe từ OpenAI Whisper API → Whisper.net (local): bỏ API key, chạy offline, miễn phí | `Services/WhisperService.cs`, `ViewModels/MainViewModel.cs`, `MainWindow.axaml`, `MainWindow.axaml.cs`, `AutomationContent.csproj` |
| 14/03/2026 | Thêm feature AI Lồng Tiếng: Edge TTS → Vietnamese speech, chọn giọng/tốc độ, audio player | `Services/EdgeTtsService.cs`, `ViewModels/MainViewModel.cs`, `MainWindow.axaml`, `MainWindow.axaml.cs` |
| 14/03/2026 | Thêm dịch thuật 12 ngôn ngữ + giọng lồng tiếng động theo ngôn ngữ | `Services/TranslationService.cs`, `Models/LanguageVoiceMap.cs`, `ViewModels/MainViewModel.cs`, `MainWindow.axaml`, `MainWindow.axaml.cs` |
| 14/03/2026 | Thêm AI Image Generation: tạo ảnh minh họa từ transcript bằng Pollinations.AI (miễn phí, không API key) | `Services/PollinationsService.cs`, `Models/ImageGenerationItem.cs`, `ViewModels/MainViewModel.cs`, `MainWindow.axaml`, `MainWindow.axaml.cs` |
| 14/03/2026 | Thêm hỗ trợ API Key cho Pollinations.AI: nhập API key tuỳ chọn để giảm rate limit, hỗ trợ cả sk_ và pk_ key | `Services/PollinationsService.cs`, `ViewModels/MainViewModel.cs`, `MainWindow.axaml` |

---

## 11. ⚠️ Quy tắc bắt buộc khi phát triển

### 🔴 LUÔN cập nhật docs khi có thay đổi code

Mỗi khi thực hiện thay đổi code, **BẮT BUỘC** phải cập nhật các file docs tương ứng:

| Loại thay đổi | File docs cần cập nhật |
|---------------|------------------------|
| Thêm/sửa/xóa feature | `docs/HANDOFF.md` (mục 1, 5), `README.md` |
| Thay đổi kiến trúc, thêm dependency, đổi pattern | `docs/ARCHITECTURE.md` |
| Fix bug quan trọng | `docs/HANDOFF.md` (mục 9 Known Issues + mục 10 Changelog) |
| Thêm ý tưởng cải tiến | `docs/IMPROVEMENT_IDEAS.md` |
| Thay đổi cách chạy/build | `docs/HANDOFF.md` (mục 7), `README.md` |

**Checklist trước khi commit:**
- [ ] Code thay đổi đã được test
- [ ] Docs đã được cập nhật phản ánh đúng code hiện tại
- [ ] Changelog đã được thêm dòng mới
- [ ] Known Issues đã được cập nhật (nếu fix bug hoặc phát hiện bug mới)
