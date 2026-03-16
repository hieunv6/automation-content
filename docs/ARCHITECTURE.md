# 🏗️ Architecture Decision Records (ADR)

## ADR-001: Chọn Avalonia UI thay vì .NET MAUI

**Ngày:** 14/03/2026
**Trạng thái:** Accepted

### Context
Cần UI framework cross-platform cho Windows và macOS dùng .NET.

### Các lựa chọn đã xem xét
1. **.NET MAUI** — Framework chính thức của Microsoft
2. **Avalonia UI** — Community-driven, cross-platform
3. **WPF** — Chỉ Windows
4. **Electron.NET** — Web-based desktop

### Quyết định
Chọn **Avalonia UI 11.2** vì:
- macOS support ổn định hơn MAUI (MAUI macOS vẫn là experimental)
- XAML syntax rất giống WPF → dễ học, nhiều tài liệu
- Compiled bindings → performance tốt
- Cộng đồng active, release cycle nhanh
- Hỗ trợ cả Linux nếu cần mở rộng

### Consequences
- (+) Cross-platform thực sự hoạt động
- (+) Dark theme FluentTheme sẵn có
- (-) Ít tài liệu hơn WPF/MAUI
- (-) Một số API khác biệt so với WPF (VD: StorageProvider thay vì OpenFileDialog)

---

## ADR-002: Dùng yt-dlp CLI thay vì YouTube API

**Ngày:** 14/03/2026
**Trạng thái:** Accepted

### Context
Cần download video YouTube với nhiều quality options.

### Các lựa chọn đã xem xét
1. **YouTube Data API v3** — Official API (không hỗ trợ download)
2. **yt-dlp CLI** — Command-line tool
3. **YoutubeExplode (.NET library)** — Pure .NET YouTube client

### Quyết định
Chọn **yt-dlp CLI** vì:
- Công cụ mạnh nhất, support nhiều formats
- Cập nhật thường xuyên khi YouTube thay đổi
- Cộng đồng lớn, nhiều người dùng test
- Tách biệt logic download → dễ update không cần rebuild app
- YoutubeExplode thường bị break khi YouTube thay đổi API

### Consequences
- (+) Reliability cao nhất
- (+) Update yt-dlp độc lập
- (+) Support 1000+ websites (không chỉ YouTube)
- (-) User phải cài yt-dlp riêng
- (-) Parse stdout/stderr phức tạp hơn library call
- (-) Distribution phức tạp hơn (dependency ngoài app)

---

## ADR-003: MVVM Pattern không dùng ReactiveUI

**Ngày:** 14/03/2026
**Trạng thái:** Accepted

### Context
Cần pattern để tách business logic khỏi UI.

### Các lựa chọn
1. **ReactiveUI** — Full reactive framework
2. **CommunityToolkit.Mvvm** — Lightweight MVVM toolkit
3. **Manual INotifyPropertyChanged** — Tự implement

### Quyết định
Chọn **Manual INotifyPropertyChanged** vì:
- App đơn giản, ít properties
- Không cần overhead của reactive framework
- Dễ hiểu cho developer mới tiếp nhận
- Không thêm dependency

### Consequences
- (+) Dễ hiểu, ít magic
- (+) Ít dependencies
- (-) Boilerplate code nhiều (mỗi property cần backing field + OnPropertyChanged)
- (-) Nếu scale up → nên chuyển sang CommunityToolkit.Mvvm

### Lưu ý nếu cần Refactor
Nếu app phát triển phức tạp hơn, nên chuyển sang `CommunityToolkit.Mvvm`:
```csharp
// Trước (manual)
private string _videoTitle = string.Empty;
public string VideoTitle
{
    get => _videoTitle;
    set { _videoTitle = value; OnPropertyChanged(); }
}

// Sau (CommunityToolkit.Mvvm)
[ObservableProperty]
private string _videoTitle = string.Empty;
```

---

## ADR-004: Inline Styles thay vì Resource Dictionary

**Ngày:** 14/03/2026
**Trạng thái:** Accepted

### Context
Cần style UI elements consistently.

### Quyết định
Đặt tất cả styles inline trong `Window.Styles` của MainWindow.axaml.

### Lý do
- App chỉ có 1 window → không cần share styles giữa các views
- Dễ tìm và sửa (tất cả tại 1 chỗ)

### Nếu cần mở rộng
Di chuyển styles sang `App.axaml` hoặc tạo file riêng:
```xml
<!-- App.axaml -->
<Application.Styles>
    <FluentTheme />
    <StyleInclude Source="/Styles/ButtonStyles.axaml" />
    <StyleInclude Source="/Styles/InputStyles.axaml" />
</Application.Styles>
```

---

## ADR-005: Newtonsoft.Json thay vì System.Text.Json

**Ngày:** 14/03/2026
**Trạng thái:** Accepted

### Context
Cần parse JSON output từ yt-dlp `--dump-json`.

### Quyết định
Dùng **Newtonsoft.Json** (JObject) vì:
- yt-dlp output JSON có cấu trúc dynamic, nhiều optional fields
- `JObject.Parse()` + `obj["field"]?.Value<T>()` linh hoạt hơn
- System.Text.Json yêu cầu define class rõ ràng cho tất cả fields

### Có thể đổi sang System.Text.Json nếu:
- Muốn giảm dependencies
- Performance là ưu tiên cao
- Đã define đủ DTO classes

---

## ADR-006: Ép codec H.264 + AAC thay vì để yt-dlp tự chọn

**Ngày:** 14/03/2026
**Trạng thái:** Accepted

### Context
Video download về bằng yt-dlp không xem được trên nhiều trình phát video (QuickTime, Windows Media Player, Smart TV, điện thoại đời cũ).

### Nguyên nhân gốc
YouTube hiện nay ưu tiên cung cấp video codec **AV1** (nén tốt hơn H.264 ~30%). Khi dùng format `bestvideo[height<=720]`, yt-dlp chọn AV1 là codec chất lượng tốt nhất. Audio tương ứng là **Opus**. Cả AV1 + Opus đều chưa được hỗ trợ rộng rãi.

### Các lựa chọn đã xem xét
1. **Để nguyên (AV1+Opus)** — Chất lượng tốt nhất, nhưng không xem được trên nhiều thiết bị
2. **Ép H.264+AAC qua format filter** — Tương thích rộng nhất, chất lượng vẫn tốt
3. **Re-encode bằng ffmpeg sau khi download** — Mất thời gian, tốn CPU, có thể giảm chất lượng

### Quyết định
Chọn **Ép H.264+AAC qua format filter** bằng cách thêm `[vcodec^=avc1]` và `[acodec^=mp4a]` vào format string:
```
bestvideo[height<=720][vcodec^=avc1]+bestaudio[acodec^=mp4a]/bestvideo[height<=720]+bestaudio/best[height<=720]
```

### Consequences
- (+) Video xem được trên **mọi thiết bị** (QuickTime, Windows Media Player, TV, điện thoại)
- (+) Không cần re-encode → nhanh, không mất chất lượng
- (+) Có fallback: nếu H.264 không có sẵn, yt-dlp tự dùng format tốt nhất
- (-) File size có thể lớn hơn ~30% so với AV1 ở cùng chất lượng
- (-) Một số video YouTube có thể không có H.264 ở resolution cao (4K+)

---

## ADR-007: Chuyển từ OpenAI Whisper API sang Whisper.net (Local)

**Ngày:** 14/03/2026
**Trạng thái:** Accepted (supersedes original OpenAI Whisper API decision)

### Context
Ban đầu dùng OpenAI Whisper API (cloud) cho speech-to-text. Tuy nhiên:
- User phải tạo OpenAI account + API key → phức tạp cho non-technical users
- Tốn phí $0.006/phút
- Cần internet connection
- Giới hạn file 25MB

### Các lựa chọn đã xem xét
1. **OpenAI Whisper API** — Cloud API, trả phí ($0.006/phút) ← giải pháp cũ
2. **Whisper.net** — .NET binding cho whisper.cpp, chạy local
3. **Whisper.cpp** — C++ library, cần build native
4. **Google Speech-to-Text / Azure Speech** — Cloud APIs khác

### Quyết định
Chuyển sang **Whisper.net** (NuGet: `Whisper.net` + `Whisper.net.AllRuntimes`) vì:
- Chạy hoàn toàn local, không cần internet
- Miễn phí, không giới hạn sử dụng
- Không cần API key → UX đơn giản hơn nhiều
- Tự động download model (~150MB) lần đầu, dùng lại các lần sau
- Vẫn hỗ trợ nhiều ngôn ngữ + auto-detect
- Pure .NET integration, cài qua NuGet → dễ deploy

### Model
- Dùng model **base** (~150MB) — cân bằng giữa tốc độ và accuracy
- Lưu tại `~/Library/Application Support/AutomationContent/models/ggml-base.bin`
- Download tự động khi cần qua `WhisperGgmlDownloader`

### Consequences
- (+) Không cần API key → UX đơn giản, user chỉ cần bấm nút
- (+) Miễn phí hoàn toàn, transcribe bao nhiêu cũng được
- (+) Chạy offline, không cần internet
- (+) Không giới hạn file size (khác API 25MB limit)
- (+) Privacy tốt hơn (audio không gửi lên cloud)
- (-) Chậm hơn cloud API trên máy yếu (phụ thuộc CPU/RAM)
- (-) Model cần ~150MB disk space
- (-) Chất lượng có thể kém hơn cloud API một chút (đặc biệt với Vietnamese)
- (-) Tăng size app do bundle runtime binaries


---

## ADR-008: Dùng Edge TTS (Microsoft Edge voices) cho AI Voiceover

**Ngày:** 14/03/2026
**Trạng thái:** Accepted

### Context
Cần tạo giọng nói Vietnamese tự nhiên từ transcript text để làm voiceover cho video.

### Các lựa chọn đã xem xét
1. **Microsoft Edge TTS (edge-tts CLI)** — Miễn phí, giọng neural tự nhiên
2. **Google Cloud TTS** — Trả phí, chất lượng cao
3. **ElevenLabs** — Trả phí, giọng rất tự nhiên nhưng đắt
4. **pyttsx3 (local)** — Chạy offline nhưng giọng robot, thiếu Vietnamese support
5. **Azure Speech** — Trả phí, setup phức tạp

### Quyết định
Chọn **edge-tts** (Python CLI, Microsoft Edge Neural Voices) vì:
- Miễn phí hoàn toàn, không giới hạn
- Không cần API key
- Giọng Vietnamese neural rất tự nhiên (HoaiMyNeural, NamMinhNeural)
- Cài đặt đơn giản: `pip install edge-tts`
- Gọi qua Process giống yt-dlp pattern đã có
- Hỗ trợ điều chỉnh tốc độ (--rate parameter)

### Voices được chọn
| Voice ID | Mô tả |
|----------|--------|
| `vi-VN-HoaiMyNeural` | Nữ miền Nam — tự nhiên nhất (recommended default) |
| `vi-VN-NamMinhNeural` | Nam miền Nam |

### Chunking Strategy
- Split text tại sentence boundaries (., !, ?, \\n)
- Max 300 ký tự/chunk để tránh Edge TTS timeout
- Nếu sentence dài hơn 300 chars → split tại dấu phẩy
- Generate mỗi chunk riêng → merge bằng ffmpeg concat

### Consequences
- (+) Miễn phí, không giới hạn sử dụng
- (+) Giọng rất tự nhiên cho Vietnamese
- (+) Không cần API key
- (+) User có thể chỉnh sửa text trước khi tạo giọng nói
- (-) Cần internet connection (Edge TTS = cloud-based)
- (-) Cần cài Python + edge-tts
- (-) Chỉ có 2 giọng Vietnamese (không có giọng miền Bắc)
- (-) Không thể tùy chỉnh pitch/emotion

---

## ADR-009: Dùng Pollinations.AI cho AI Image Generation

**Ngày:** 14/03/2026
**Trạng thái:** Accepted

### Context
Cần tạo ảnh minh họa tự động từ transcript text để làm hình ảnh cho video.

### Các lựa chọn đã xem xét
1. **Pollinations.AI** — API miễn phí hoàn toàn, không cần API key, không cần đăng ký
2. **DALL-E 3 (OpenAI)** — Trả phí, chất lượng cao, cần API key
3. **Stable Diffusion (local)** — Chạy local nhưng cần GPU mạnh, setup phức tạp
4. **Midjourney** — Trả phí, chỉ có qua Discord, không có API
5. **Leonardo.AI** — Freemium, cần đăng ký, API key

### Quyết định
Chọn **Pollinations.AI** vì:
- Miễn phí hoàn toàn, không giới hạn (best for MVP)
- Không cần API key, không cần đăng ký — UX đơn giản nhất
- Hỗ trợ model Flux — chất lượng ảnh tốt
- Có cả text API (tóm tắt paragraph → prompt) và image API
- REST API đơn giản: chỉ cần GET request với URL encoded prompt

### Architecture
- **Text API** (`POST https://text.pollinations.ai/`): Tóm tắt đoạn transcript → image prompt (max 20 words)
- **Image API** (`GET https://image.pollinations.ai/prompt/[encoded]?width=1280&height=720&nologo=true&model=flux`): Tạo ảnh từ prompt
- Xử lý tuần tự (không parallel) + delay 1s giữa requests để tránh rate limiting
- Ảnh lưu vào subfolder theo tên video: `image_001.jpg`, `image_002.jpg`, ...

### Style Presets
| Style | Suffix appended to prompt |
|-------|--------------------------|
| Realistic photo | `, professional photography, 4K` |
| Digital art | `, digital illustration, vibrant colors` |
| Flat design | `, minimalist flat vector art` |
| Cinematic | `, cinematic lighting, dramatic scene` |

### Consequences
- (+) Miễn phí hoàn toàn, không cần setup gì
- (+) Không cần API key hoặc đăng ký — seamless UX
- (+) Chất lượng ảnh tốt với model Flux
- (-) Tốc độ phụ thuộc server Pollinations (có thể chậm)
- (-) Không có SLA — service có thể downtime
- (-) Không control được chất lượng ổn định
- (-) Cần internet connection

---

## ADR-010: Burn Subtitle vào Ảnh bằng SkiaSharp thay vì FFmpeg subtitles filter

**Ngày:** 16/03/2026
**Trạng thái:** Accepted

### Context
Video render sử dụng FFmpeg `subtitles` filter để hiện phụ đề lên video. Filter này yêu cầu FFmpeg được compile với `libass`. Trên nhiều máy (đặc biệt Homebrew macOS), FFmpeg mặc định **không có libass** → gây lỗi `No such filter: 'subtitles'`.

### Các lựa chọn đã xem xét
1. **FFmpeg subtitles filter** — Cần libass, không portable
2. **FFmpeg drawtext filter** — Không cần libass nhưng khó wrap text, thiếu outline
3. **Burn subtitle vào ảnh bằng SkiaSharp** — Xử lý hoàn toàn trong C#, không phụ thuộc FFmpeg build

### Quyết định
Chọn **Burn subtitle vào ảnh bằng SkiaSharp** vì:
- Không phụ thuộc vào cách FFmpeg được compile
- SkiaSharp đã là dependency (Avalonia UI sử dụng)
- Full control: font, size, outline, position, word wrap
- Chạy trên mọi platform mà không cần config thêm

### Architecture
- `SubtitleRenderer.cs`: Nhận ảnh gốc + text → resize/pad ảnh → vẽ text với outline ở dưới → xuất ảnh mới
- `VideoRenderService.cs`: Pre-process tất cả ảnh qua `SubtitleRenderer` trước khi gửi cho FFmpeg
- FFmpeg filter chỉ còn: scale + xfade transitions (không còn `subtitles` filter)

### Consequences
- (+) Hoạt động trên mọi FFmpeg build (không cần libass)
- (+) Full control về typography và layout
- (+) Không cần tạo file SRT trung gian
- (-) Tăng thời gian render (phải xử lý ảnh trước FFmpeg)
- (-) Subtitle "cứng" trên ảnh (không tắt/bật được như SRT)

---

## Flow Diagram: User Journey

```
┌─────────────────────────────────────────────────────────────┐
│                     APP LAUNCH                               │
│                                                              │
│  ┌─────────────┐                                             │
│  │  Check if    │──No──▶ Show warning:                       │
│  │  yt-dlp      │       "yt-dlp not installed"               │
│  │  installed   │                                            │
│  └──────┬───────┘                                            │
│         │Yes                                                 │
│         ▼                                                    │
│  ┌─────────────────────────────────────────────────────┐     │
│  │  STEP 1: Enter URL                                   │     │
│  │  ┌───────────────────────────┐  ┌──────────────┐     │     │
│  │  │ YouTube URL text field    │  │  Get Info ▶   │     │     │
│  │  └───────────────────────────┘  └───────┬──────┘     │     │
│  └─────────────────────────────────────────┼───────────┘     │
│                                            │                 │
│         ┌──────────────────────────────────┘                 │
│         │                                                    │
│         ▼                Invalid URL                         │
│  ┌──────────────┐──────────────────▶ Show error card         │
│  │  Validate    │                                            │
│  │  URL format  │                                            │
│  └──────┬───────┘                                            │
│         │Valid                                                │
│         ▼                                                    │
│  ┌──────────────┐        Error                               │
│  │  yt-dlp      │──────────────────▶ Show friendly error     │
│  │  --dump-json │                                            │
│  └──────┬───────┘                                            │
│         │Success                                             │
│         ▼                                                    │
│  ┌─────────────────────────────────────────────────────┐     │
│  │  STEP 2: Preview + Settings                          │     │
│  │  ┌──────────┐  Title, Channel, Duration, Views       │     │
│  │  │Thumbnail │  ┌──────────────┐  ┌──────────────┐    │     │
│  │  │  Image   │  │Quality: 720p▼│  │📁 Browse    │    │     │
│  │  └──────────┘  └──────────────┘  └──────────────┘    │     │
│  │                          ┌──────────┐ ┌──────────┐    │     │
│  │                          │✖ Cancel  │ │⬇Download │    │     │
│  │                          └──────────┘ └─────┬────┘    │     │
│  └─────────────────────────────────────────────┼────────┘     │
│                                                │              │
│         ┌──────────────────────────────────────┘              │
│         ▼                                                     │
│  ┌─────────────────────────────────────────────────────┐     │
│  │  STEP 3: Download Progress                           │     │
│  │  ████████████████████░░░░░░  72%                     │     │
│  │  72.0% of 50.00MiB • 5.00MiB/s • ETA 00:06          │     │
│  │                    ┌──────────┐                       │     │
│  │                    │✖ Cancel  │ ← kills process      │     │
│  │                    └──────────┘                       │     │
│  └──────────────────────────┬──────────────────────────┘     │
│                             │                                 │
│         ┌───────────────────┘                                 │
│         ▼                                                     │
│  ┌─────────────────────────────────────────────────────┐     │
│  │  STEP 4: Done!                                       │     │
│  │  ✅ Download Complete!                                │     │
│  │  Done! File saved to: /Users/.../Downloads/video.mp4  │     │
│  │  ┌───────────────┐                                    │     │
│  │  │ 📂 Open Folder│                                    │     │
│  │  └───────────────┘                                    │     │
│  └─────────────────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────────────┘
```
