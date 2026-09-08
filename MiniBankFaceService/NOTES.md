# MiniBank Face Service — ghi chú tiến độ

Cập nhật: 2026-09-08

## Mục tiêu

Chuyển FaceID từ **client-side** (face-api.js, descriptor 128-d tính trong browser)
sang **server-side** (DeepFace + Facenet512, embedding 512-d, có anti-spoofing).

Lý do: `AuthService.FaceLoginAsync` hiện nhận thẳng `float[] Descriptor` do browser gửi lên,
nên toàn bộ liveness trong `face.js` không có giá trị bảo mật — ai cũng có thể POST descriptor
bằng curl để đăng nhập, không cần camera.

## Đang dừng ở đâu — sáng 2026-09-08, chiều làm tiếp

### Xong và đã kiểm

| Việc | Kiểm bằng gì |
|---|---|
| 4 task chuyển FaceID sang server-side | curl API + form thật của WebClient |
| Chống ảnh giả (MiniFASNet) | Duy Minh tự giơ ảnh trước webcam, bị chặn cả 3 lần |
| Đăng ký 3 bước + 4 field mới (SĐT, ngày sinh, CCCD, địa chỉ) | 6 ca API + 3 ca form, chạy trên SQL Server thật |
| DB tạo mới trên SQL Server kèm seed | `MiniBankDB` chưa từng tồn tại nên không phải xoá gì |

### Đã thử camera thật ✅ nhưng còn vướng kiếng 🟡

Quét vòng tròn chạy được, tô đủ 8 vạch, trái–phải không bị ngược.
**Nhưng phải tháo kiếng mới quét được.**

Chẩn đoán: kẹt ở khâu phát hiện mặt chứ không phải khâu tính hướng — công thức hướng chỉ dùng
hàm, mũi, cằm và hai khoé mắt, kiếng che tròng không ảnh hưởng mấy điểm đó. Gọng kiếng và ánh
sáng loá trên tròng làm điểm tin cậy của `tinyFaceDetector` tụt xuống dưới ngưỡng.

Đã nới hai tham số detector, **chưa thử lại với kiếng**:

| Hằng số | Cũ → Mới | Tác dụng |
|---|---|---|
| `DETECTOR_SCORE_THRESHOLD` | 0.4 → **0.2** | Chấp nhận khuôn mặt mà model kém tự tin hơn |
| `DETECTOR_INPUT_SIZE` | 320 → **416** | Ảnh đưa vào model to hơn, bắt được chi tiết khó |

Hạ ngưỡng làm tăng khả năng nhận nhầm vật không phải mặt, nhưng chấp nhận được: ảnh vẫn phải qua
MiniFASNet và Facenet512 ở server, client nhận nhầm thì cùng lắm là chụp một tấm bị server trả về
`No face detected in the image`.

Log giờ in thêm điểm số: `[face] score 0.xx strength 0.xx segment N filled x/8`.
Đeo kiếng vào quét thử, nếu vẫn không thấy mặt thì đọc `score` để biết nên hạ tiếp bao nhiêu.
Hạ tới 0.1 mà vẫn không ăn thì đổi hướng khác chứ đừng hạ nữa — lúc đó là model không hợp,
không phải ngưỡng sai.

### Việc lặt vặt còn nợ

- Xoá các dòng `console.log('[face] ...')` trong `face.js` khi hết giai đoạn tinh chỉnh
- Xoá `wwwroot/lib/face-api/face-api.min.js` nếu sau này bỏ hẳn quét ở client (1.3 MB)
- Xoá tài khoản test `sqlserver@minibank.local` và `buoc3@minibank.local` trong DB
- Người dùng đã đăng ký face bằng bản cũ (128-d) phải quét lại, không convert được

### Nhớ: phải bật 3 tiến trình mới chạy được

```bash
docker start sqlserver
cd MiniBankFaceService  && .venv/bin/uvicorn main:app --host 127.0.0.1 --port 8000
cd MiniBankWebApi       && dotnet run --launch-profile http    # http://localhost:5038/swagger
cd MiniBankWebClient    && dotnet run --launch-profile http    # http://localhost:5112
```

Quên face service thì mọi chức năng khuôn mặt trả 500 — triệu chứng trông như lỗi code.

## Task 1 — dựng face service ✅

### Đã xong

- `venv` bằng Python 3.11 (không dùng `python3` mặc định — bản 3.14 không cài được TensorFlow)
- Cài xong: tensorflow 2.16.2, tf-keras 2.16.0, deepface 0.0.95, torch 2.2.2
- `main.py`: FastAPI với `GET /health` và `POST /represent`
- Service chạy được, `GET /health` trả 200
- Model **Facenet512** đã tải xong (95MB, nằm ở `~/.deepface/weights/`)
- Model anti-spoofing đã đủ 2 file trong `~/.deepface/weights/`:
  `2.7_80x80_MiniFASNetV2.pth` và `4_0_0_80x80_MiniFASNetV1SE.pth`.
  File thứ hai DeepFace tải tự động lỗi, phải tải tay:

  ```bash
  curl -L -o ~/.deepface/weights/4_0_0_80x80_MiniFASNetV1SE.pth \
    https://github.com/minivision-ai/Silent-Face-Anti-Spoofing/raw/master/resources/anti_spoof_models/4_0_0_80x80_MiniFASNetV1SE.pth
  ```

- Log lúc khởi động đã hết warning, in đủ `Recognition model Facenet512 loaded`
  và `Anti-spoofing model loaded`
- `POST /represent` test bằng ảnh có mặt thật: HTTP 200, `is_real = true`,
  `antispoof_score ≈ 0.99`, `embedding` dài đúng 512, mất khoảng 0.37 giây

## Cả 4 task đã xong (2026-09-08)

### Task 2 — sửa DTO và FaceHelper ✅ (2026-09-08)

Rà lại 2026-09-08: có **4** DTO đang nhận `Descriptor` từ client, không phải 2 như ghi hôm qua.

| File | Sửa |
|---|---|
| `MiniBankDTOs/FaceRegisterDto.cs` | `float[] Descriptor` → `string ImageBase64` |
| `MiniBankDTOs/FaceLoginDto.cs` | như trên |
| `MiniBankDTOs/RegisterDto.cs` | `float[]? Descriptor` → `string? FaceImageBase64` (đăng ký kèm khuôn mặt) |
| `MiniBankDTOs/ConfirmOtpDto.cs` | `float[]? Descriptor` → `string? FaceImageBase64` (xác nhận chuyển tiền đáng ngờ) |
| `MiniBankWebApi/Helper/FaceHelper.cs` | `DescriptorLength` 128 → **512**; đổi Euclidean sang **cosine distance** |

`TransferService.EnsureFaceMatchesAsync` cũng so descriptor client gửi lên — cùng lỗ hổng
với `FaceLoginAsync`, phải sửa chung đợt, không để lại sau.

### Task 3 — sửa AuthService + config ✅ (2026-09-08)

- Thêm `Core/Services/IFaceApiService.cs` + `FaceApiService.cs`: gọi `POST /represent`,
  chặn khi `is_real = false`, kiểm tra embedding dài 512, trả `float[]`.
  Tên đặt theo `IMailService`/`MailService` cho đồng bộ (không dùng tên `FaceApiClient` như ghi hôm qua).
- `Core/Models/FaceRepresentResponse.cs`: đọc JSON snake_case bằng `JsonNamingPolicy.SnakeCaseLower`
- `AuthService`: `RegisterAsync`, `RegisterFaceAsync`, `FaceLoginAsync` nhận ảnh base64;
  bỏ `EnsureValidDescriptor` (việc kiểm độ dài đã dồn vào `FaceApiService`)
- `TransferService.EnsureFaceMatchesAsync` nhận ảnh base64
- `IAuthService` + `AuthController`: `RegisterFaceAsync(int, string)`
- `Program.cs`: `AddHttpClient<IFaceApiService, FaceApiService>` với `BaseAddress` + timeout
- `appsettings.json`: thêm `FaceService`, `Face:MaxDistance` 0.5 → **0.30**

Đã test thật (API chạy trên SQLite tạm, không đụng DB SQL Server):

| Ca | Kết quả |
|---|---|
| Đăng ký kèm ảnh có mặt | 201, `hasFace = true` |
| Đăng nhập bằng đúng ảnh đó | 200, có token, distance **0** |
| Đăng nhập bằng mặt người khác | 401, distance **0.96** |
| Ảnh base64 hỏng | 400, `Image is not valid base64` |

Ngưỡng 0.30 nằm giữa 0 và 0.96 nên tách được hai ca này rất rõ.

### Task 4 — sửa face.js ✅ (2026-09-08)

`face.js` chỉ còn: mở camera → chờ frame đầu → chụp một frame vào canvas →
`toDataURL('image/jpeg', 0.92)` → gán vào hidden input → bật nút submit.
Đã bỏ `waitForBlink`, `eyeAspectRatio`, `getDescriptor`, `loadModels`, overlay và `median`.

Sửa kèm:

- 4 view (`Register`, `RegisterFace`, `FaceLogin`, `Transfer/Confirm`): bỏ thẻ
  `<script src="~/lib/face-api/face-api.min.js">`, bỏ `<canvas id="faceOverlay">`,
  hidden input đổi sang `id="faceImage"`, option JS đổi `descriptorId` → `imageId`
- 3 view model: `Descriptor` → `FaceImageBase64` / `ImageBase64`
- `AccountController` và `TransferController`: xoá hàm `ParseDescriptor`, truyền thẳng chuỗi base64
- `site.css`: xoá rule `.face-frame canvas` đã thành code chết
- `site.css` + 4 view (làm thêm sau task 4): khung camera đổi thành hình tròn
  (`.face-frame-sm` 260px, `.face-frame-lg` 320px, video `object-fit: cover`) kèm
  `.face-guide` — oval xanh nét đứt làm mốc đặt mặt. Thuần CSS, không thêm JS.
  Oval **không đổi màu** khi thấy mặt vì việc detect đã dồn hết về server.

**Chưa xoá** `wwwroot/lib/face-api/face-api.min.js` (1.3 MB) và 6 file trong `wwwroot/models/`
(~7 MB). Không còn chỗ nào tham chiếu tới. Project không nằm trong git nên xoá là mất hẳn,
để Duy Minh tự quyết:

```bash
rm -rf MiniBankWebClient/wwwroot/models MiniBankWebClient/wwwroot/lib/face-api
```

## Đã test cả luồng (2026-09-08)

Chạy API trên SQLite tạm + WebClient thật, gửi ảnh dạng `data:image/jpeg;base64,...`
đúng như browser gửi:

| Ca | Kết quả |
|---|---|
| Form POST `/Account/FaceLogin` đúng mặt | 302 sang `/Transfer/Index`, session đã lưu |
| Không bấm scan (chuỗi rỗng) | Hiện `Please scan your face before logging in` |
| Ảnh không có mặt người | Hiện `No face detected in the image` (câu lỗi từ Python về tới browser nguyên vẹn) |

Tiền tố `data:image/jpeg;base64,` không phải cắt ở C# — `main.py` đã tự cắt bằng
`split(",", 1)[-1]`.

## Đăng ký 3 bước (2026-09-08, làm sau 4 task)

`Register.cshtml` chia thành 3 bước trên **cùng một trang**, chuyển bước bằng JS ẩn/hiện,
chỉ submit một lần ở bước cuối. Không giữ state giữa các request, không thêm action mới.

| Bước | Nội dung |
|---|---|
| 1. Account | Email, mật khẩu, xác nhận mật khẩu |
| 2. Personal info | Họ tên, số điện thoại, ngày sinh, số CCCD, địa chỉ |
| 3. Face ID | Quét khuôn mặt (vẫn optional) |

Thêm 4 cột vào `Account`: `PhoneNumber`, `DateOfBirth`, `IdentityNumber`, `Address`.
CCCD có unique index. Seed 4 tài khoản mẫu đã bổ sung giá trị cho 4 cột này.

Luật kiểm tra:

- Điện thoại `^0\d{9}$`, CCCD `^\d{12}$` — đặt ở DTO và view model
- Ngày sinh: chặn ngày tương lai và bắt đủ 18 tuổi, kiểm ở `AuthService.EnsureOldEnough`
- CCCD trùng: gộp chung một query với kiểm tra email trùng
  (`EnsureEmailAndIdentityAreFreeAsync`), một round-trip cho cả hai

🔴 **Đổi schema nên phải xoá DB tạo lại.** `EnsureCreated()` không tự thêm cột:

```bash
docker exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "DROP DATABASE MiniBankDB"
```

Chạy lại `MiniBankWebApi` là DB được tạo mới kèm seed. Tài khoản tự đăng ký trước đó mất hết.

## Quét vòng tròn trong modal, kiểu Face ID (2026-09-08)

Bản trước bắt quay đầu theo 4 hướng rời rạc, dùng trên laptop khó. Đổi sang kiểu iPhone Face ID:
**vòng 12 vạch quanh khung, quay đầu tới hướng nào thì vạch hướng đó chuyển xanh**, đủ 12 vạch
là xong. Camera chuyển vào **modal Bootstrap** để người dùng chỉ nhìn thấy khuôn mặt mình.

Luồng khi bấm "Scan face": mở modal → quay đầu vòng tròn cho đủ 12 vạch → đưa mặt sát lấp đầy
oval → chụp một tấm gửi server → modal tự đóng.

🔴 **Phải đợi `shown.bs.modal` rồi mới đo.** Lúc modal còn ẩn, `getBoundingClientRect()` trả về 0
nên `readOval()` ra toàn số vô nghĩa. Đã kiểm: đo sau khi modal hiện thì tâm oval ra đúng
(240, 180) và bề rộng 154.3 px gốc, khớp với bản chưa dùng modal.

Cách đổi hướng mặt thành vạch trên vòng:

```
yawOffset   = (yawRate - 0.50) / YAW_TURN_SPAN      // lệch trái phải
pitchOffset = (pitchRate - 0.45) / PITCH_TURN_SPAN  // lệch trên dưới
strength = hypot(yawOffset, pitchOffset)            // quay mạnh cỡ nào
segment  = atan2(pitchOffset, yawOffset) chia đều 12 phần   // quay về phía nào
```

Đạt `strength >= RING_TURN_THRESHOLD` thì tô vạch tương ứng, tô rồi giữ luôn không tắt.

🔴 **Nối quãng giữa hai lần đo, nếu không thì quét rất khó.** Bản đầu chỉ tô đúng vạch vừa đo
được, mà mỗi lần đo cách nhau 100–200ms — quay đầu mượt một vòng là nhảy qua mấy vạch ở giữa,
những vạch đó không bao giờ được tô, phải dừng đúng từng vị trí mới ăn.
`fillSegmentsBetween()` tô cả quãng **ngắn** nối vạch trước với vạch hiện tại.

Khi `strength` tụt xuống dưới ngưỡng (mặt về chính diện) thì đặt lại `previousSegment = null`,
để lần quay sau không nối bậy một quãng dài xuyên qua giữa vòng.

Mô phỏng trong Chrome: đưa vào 4 mẫu rời rạc (vạch 0 → 3 → 6 → 1) là **tô đủ 8/8**; còn ca
về giữa rồi quay lại chỉ tô 2 vạch, đúng như mong đợi.

Đo bằng số giả trong Chrome: chính diện `strength = 0` (không tô nhầm vạch nào), quay trái →
vạch 0, quay phải → vạch 6, ngẩng → vạch 9, cúi → vạch 3. Quay hết cỡ cho `strength ≈ 1.2`,
tức ngưỡng 0.55 chỉ cần quay khoảng nửa tầm.

Ngưỡng chỉnh ở đầu `face.js` (đã nới sau lần thử đầu vì quét khó):

| Hằng số | Ban đầu → Hiện tại | Vì sao |
|---|---|---|
| `RING_SEGMENT_COUNT` | 12 → **8** | Vạch thưa hơn, mỗi vạch một góc 45° |
| `RING_TURN_THRESHOLD` | 0.55 → **0.45** | Quay nhẹ hơn cũng ăn |
| `PITCH_TURN_SPAN` | 0.14 → **0.11** | Ngồi trước laptop khó ngẩng/cúi nhiều, nới riêng trục dọc |
| `RING_DETECT_INTERVAL_MS` | 200 → **100** | Đo dày hơn, quãng nối ngắn lại |

Modal nằm ở `Views/Shared/_FaceScanModal.cshtml`, cả 4 trang cùng gọi `<partial name="_FaceScanModal" />`.
Nhờ đó id của video, oval, vòng vạch cố định trong partial, `setupFaceCapture` chỉ còn nhận
những gì thật sự khác nhau giữa các trang: `resultId`, `imageId`, `scanButtonId`,
`submitButtonId`, `ring`, `optional`, `autoSubmit`.

Đóng modal giữa chừng thì `hidden.bs.modal` bật cờ `faceScanner.cancelled`, các vòng lặp đang
chạy thấy cờ này là dừng và tắt camera — không để camera chạy ngầm sau khi người dùng thoát.

Bật/tắt vòng quét bằng option `ring: true`. Không phải trang nào cũng bật:

| Trang | Vòng quét | Vì sao |
|---|---|---|
| Đăng ký bước 3 | ✅ | Ghi danh khuôn mặt, làm một lần |
| Đăng ký khuôn mặt | ✅ | như trên |
| Xác nhận chuyển tiền đáng ngờ | ✅ | Giao dịch rủi ro cao |
| Đăng nhập bằng mặt | ❌ chỉ cần oval | Làm hàng ngày, bắt quay vòng mỗi lần thì quá lâu |

Đây là cách ngân hàng thật làm: quét kỹ lúc ghi danh và lúc rủi ro cao, đăng nhập thường thì nhẹ.

🔴 **Phần 4 hướng chỉ là UX, không tăng bảo mật.** Client tự kiểm rồi tự quyết định gửi ảnh nào,
kẻ tấn công sửa JS bỏ qua hết vẫn gửi được một tấm. Lớp chặn ảnh giả thật sự vẫn là MiniFASNet
ở server (đã đo, xem mục dưới). Muốn 4 hướng có giá trị bảo mật thì phải để **server sinh thứ tự
ngẫu nhiên và tự kiểm từng ảnh** — chưa làm, ước lượng 3-4 task.

Vì server chỉ nhận một ảnh nên **4 tấm ở 4 hướng không được chụp lại**, chỉ kiểm tư thế rồi bỏ qua.
Cố tình không gom vào mảng để tránh code thừa không ai dùng.

Ước lượng hướng xoay từ landmark 68 điểm (bản **tiny**, 77 KB — không cần bản đầy đủ 356 KB):

| Đại lượng | Công thức | Ý nghĩa |
|---|---|---|
| `yawRate` | `(mũi.x - hàmTrái.x) / bềNgangHàm` | 0.5 là chính diện, càng lớn càng xoay sang trái người dùng |
| `pitchRate` | `(mũi.y - đườngMắt.y) / (cằm.y - đườngMắt.y)` | 0.45 là chính diện, nhỏ hơn là ngẩng, lớn hơn là cúi |

Ngưỡng ở đầu `face.js`: `YAW_TURNED_RATE` 0.60, `PITCH_UP_RATE` 0.38, `PITCH_DOWN_RATE` 0.60,
mỗi bước `POSE_TIMEOUT_MS` 15 giây, cần `POSE_STABLE_FRAME_COUNT` 2 frame liên tiếp.

Kiểm bằng landmark giả trong Chrome: chính diện (yaw 0.50 / pitch 0.45) **không khớp hướng nào**,
mũi lệch phải ảnh → `left`, lệch trái ảnh → `right`, ngẩng → `up`, cúi → `down`.

⚠️ Video **không lật gương**, nên trái-phải theo góc nhìn camera. Nếu quay đầu thật mà thấy
ngược, đảo hai dòng `left` và `right` trong `matchesPose`.

Tải lại model landmark: repo gốc đặt tên file `face_landmark_68_tiny_model-shard1` **không có
phần mở rộng**, mà ASP.NET không phục vụ file không đuôi (404). Phải đổi tên thành `.bin`
và sửa `paths` trong manifest cho khớp — thư mục `models/` đang theo quy ước này.

## Tự chụp khi mặt sát vòng tròn (2026-09-08)

Trước đó bấm "Scan face" là chụp ngay, người dùng chưa kịp đưa mặt vào. Giờ `face.js`
dò khuôn mặt mỗi 200ms và **tự chụp** khi mặt đủ lớn, nằm giữa, và giữ yên 2 frame liên tiếp.

Dùng lại `face-api.js` nhưng **chỉ model `tinyFaceDetector`** (193 KB, tải mất ~25ms).
Đã xoá 2 model không còn dùng: `face_recognition_model.bin` (6.4 MB) và
`face_landmark_68_model.bin` (356 KB) — tải lại được ở https://github.com/justadudewhohacks/face-api.js
thư mục `weights/` nếu sau này cần.

🔴 **Không phải quay lại lỗ hổng cũ.** Lỗ hổng cũ là browser *tính descriptor* rồi server tin luôn.
Lần này browser chỉ dùng detector để biết *khi nào bấm nút chụp* — ảnh vẫn gửi nguyên về server,
MiniFASNet và Facenet512 vẫn là nơi quyết định. Sửa JS bỏ qua bước canh khung thì chỉ tự làm
ảnh mình xấu đi.

Ngưỡng chỉnh ở đầu `face.js`:

| Hằng số | Giá trị | Ý nghĩa |
|---|---|---|
| `MIN_OVAL_FILL_RATE` | 0.80 | Mặt phải rộng ≥ 80% bề ngang oval |
| `MAX_OVAL_FILL_RATE` | 1.10 | Mặt không được rộng quá 110% oval (đưa sát quá thì lùi lại) |
| `MAX_OVAL_OFFSET_RATE` | 0.15 | Tâm mặt lệch tâm oval không quá 15%, xét cả ngang lẫn dọc |
| `READY_COUNTDOWN_SECONDS` | 3 | Đếm ngược 3 giây trước khi bắt đầu dò, cho người dùng kịp đưa mặt vào |
| `STABLE_FRAME_COUNT` | **3** | Phải đạt 3 frame liên tiếp (600ms) mới chụp, tránh chụp lúc đang cử động |
| `DETECT_TIMEOUT_MS` | 20000 | Quá 20 giây không đạt thì báo bấm lại |

Vòng lặp in `[face] width 0.xx offset 0.xx` ra console mỗi lần dò — mở DevTools là biết
đang thiếu bao nhiêu để chỉnh ngưỡng. Xong giai đoạn tinh chỉnh thì xoá dòng `console.log` này.

**Đo so với vòng oval, không phải so với khung hình.** Bản đầu so bề rộng mặt với cả khung
hình, nhưng sau khi phóng 1.4 lần thì oval chỉ còn chiếm **32%** khung hình — hai thứ lệch nhau,
"đủ rộng so với khung" không có nghĩa là "vừa vòng xanh".

`readOval()` đọc `getBoundingClientRect()` của chính thẻ `.face-guide` rồi đổi sang toạ độ
frame gốc mà detector làm việc. Nhờ vậy sửa CSS (đổi cỡ oval, đổi `scale`, đổi cỡ khung tròn)
thì JS tự theo, không phải chép hằng số sang hai nơi rồi quên đồng bộ.

Chỗ dễ sai: `object-fit: cover` làm **nội dung video không trùng với khung thẻ `<video>`** —
thẻ là hình vuông, nội dung là 4:3 bị cắt hai bên. Nên `contentRect()` phải tính lại vùng
nội dung thật sự trước khi quy đổi toạ độ, lấy thẳng `videoRect` là sai.

Đo thật trên trang FaceLogin (khung 320px): tâm oval ra đúng (240, 180) — chính giữa
frame 480×360; oval rộng 154.3 px gốc. Suy ra mặt phải rộng **123–170 px gốc**, tức
0.26–0.35 nếu quy về tỉ lệ khung hình.

Vì sao cần đếm ngược: webcam máy Duy Minh có dải dao động rất hẹp — ngồi bình thường
đã khoảng 0.24, đưa sát nhất mới 0.28. Chỉ dựa vào độ rộng thì không phân biệt được
"đang ngồi yên" với "đã cố ý đưa mặt vào", nên vừa hết warm-up là chụp luôn.
Siết ngưỡng lên 0.26–0.27 sẽ đổi bực này lấy bực khác (phải rướn hết cỡ mới chụp được),
nên cho thêm thời gian là cách đúng.

Đi kèm việc hạ ngưỡng: `.face-frame video` thêm `transform: scale(1.4)`. Lý do là mặt chỉ
chiếm 28% khung hình mà oval rộng tới 45%, nên mặt luôn lọt thỏm giữa oval — chữ hướng dẫn
"lấp đầy oval" thành nói dối. Phóng 1.4 lần thì mặt lấp gần đầy oval đúng như hướng dẫn.
**Chỉ phóng phần xem trước**, `capturePhoto` vẫn đọc `video.videoWidth` là frame đầy đủ,
nên ảnh gửi lên server không đổi — detector phía server vẫn có đủ khoảng trống quanh mặt.
`border-radius` và `overflow: hidden` phải chuyển lên `.face-frame` thì mới cắt được phần
video tràn ra ngoài sau khi phóng.

Sửa kèm: 4 view thêm `asp-append-version="true"` cho `face.js`. Trước đó thiếu nên Chrome
cache bản cũ, sửa JS xong mở lại trang vẫn chạy code cũ — mất thời gian tưởng code sai.

## Đo chống giả mạo bằng ảnh (2026-09-08)

Câu hỏi đặt ra: người khác cầm ảnh của mình giơ trước camera thì có đăng nhập được không?
Đã đo thật bằng webcam trên trang `/Account/FaceLogin`:

| Lượt | `is_real` | `antispoof_score` | Màn hình hiện |
|---|---|---|---|
| Mặt thật | `true` | 1.000 | Đăng nhập được |
| Giơ ảnh trước webcam | `false` | 0.996 | `This face looks like a photo or a screen` |
| Giơ ảnh lần nữa | `false` | 0.991 | như trên |
| Giơ ảnh lần nữa | `false` | 0.511 | như trên |

**Kết luận: MiniFASNet chặn được, không cần làm quét 4 góc.**

Hai điều cần nhớ khi bảo vệ:

1. `antispoof_score` là **độ tự tin vào kết luận**, không phải độ thật.
   `is_real = false, score = 0.996` nghĩa là "chắc 99.6% đây là giả".

2. Có một lượt model chỉ chắc **51%** — vẫn kết luận đúng nhưng sát ranh giới.
   Nói trung thực: chặn đúng cả 3 lần đo, nhưng biên an toàn không đều; ảnh in chất lượng
   cao hoặc màn hình tốt hơn vẫn có khả năng lọt. Đây là giới hạn chung của anti-spoofing
   một-ảnh, không phải lỗi cài đặt.

Nếu sau này muốn siết thêm thì hướng đúng là **challenge 4 góc do server sinh ngẫu nhiên**,
server kiểm từng ảnh đúng hướng bằng landmark mắt–mũi và kiểm 4 ảnh cùng một người.
Làm 4 góc mà server không kiểm thì chỉ lặp lại sai lầm của `waitForBlink` cũ.

## Hai thứ dễ quên 🔴

1. ~~`Face:MaxDistance = 0.5` sẽ sai~~ → đã đổi sang **0.30** ở task 3. Đo thật:
   cùng người distance **0**, khác người **0.96**.

2. **Dữ liệu face cũ mất hết.** `Account.FaceDescriptor` đang lưu 128-d của face-api.js,
   không convert sang 512-d được. Mọi user đã đăng ký face phải **quét lại từ đầu**.
   `FaceLoginAsync` bỏ qua các bản ghi ≠ 512 và ghi log warning, nên hệ thống không sập,
   nhưng người dùng cũ sẽ không đăng nhập bằng mặt được cho tới khi đăng ký lại.

## Ràng buộc môi trường (máy Intel macOS 26)

Không nâng version mấy cái này, sẽ gãy:

| Package | Trần | Vì sao |
|---|---|---|
| tensorflow | **2.16.2** | Từ 2.17 trở đi không còn wheel cho macOS x86_64 |
| torch | **2.2.2** | Bản cuối còn wheel cho macOS x86_64 |
| Python | **3.11** | 3.14 (mặc định của máy) không cài được TensorFlow |

## Lệnh chạy lại

Xem mục **Nhớ: phải bật 3 tiến trình mới chạy được** ở đầu file.

Lệnh hay dùng khi tắt đi chạy lại:

```bash
pkill -f MiniBankWebApi ; pkill -f MiniBankWebClient   # tắt phần .NET
pkill -f uvicorn                                       # tắt face service
```

Đọc điểm anti-spoofing của từng lượt quét trong log của face service:

```
INFO:face-service:Embedding created, antispoof score 1.000     ← mặt thật
WARNING:face-service:Spoof detected, score 0.996               ← ảnh giơ trước camera
```

## Đã sửa ngày 2026-09-07 (ngoài face service)

`MiniBankWebApi/appsettings.json`: connection string `server=.` → `server=localhost,1433`.
`server=.` là cú pháp Windows, trên macOS app crash ngay lúc `EnsureCreated()`.
Bản mới chạy được trên cả hai hệ.
