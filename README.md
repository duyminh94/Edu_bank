# MiniBank

Đồ án cuối kỳ — ứng dụng ngân hàng đơn giản: đăng ký, đăng nhập, chuyển tiền có OTP,
và đăng nhập bằng khuôn mặt.

Điểm chính về mặt kỹ thuật: **toàn bộ việc nhận dạng khuôn mặt chạy ở server**, không phải
trong browser. Browser chỉ chụp ảnh và gửi lên; server dùng DeepFace + Facenet512 để sinh
embedding 512 chiều, và MiniFASNet để chặn ảnh in hoặc ảnh chụp màn hình.

## Cấu trúc

| Thư mục | Vai trò |
|---|---|
| `MiniBankWebApi` | REST API, ASP.NET Core 10, Entity Framework, JWT |
| `MiniBankWebClient` | Giao diện MVC, gọi sang API |
| `MiniBankDTOs` | Class dùng chung giữa API và client |
| `MiniBankFaceService` | Service Python (FastAPI) chạy DeepFace, cổng 8000 |

## Cần có trước

- .NET 10 SDK
- SQL Server (Docker hoặc bản cài trên máy)
- Python 3.11 — không dùng bản mới hơn, TensorFlow chưa hỗ trợ

## Chạy lần đầu

Phải bật **ba** tiến trình, thiếu cái nào cũng không chạy đủ chức năng.

### 1. SQL Server

```bash
docker start sqlserver
```

Chưa có container thì tạo mới, nhớ mật khẩu phải khớp với `ConnectionStrings:SqlServer`
trong `MiniBankWebApi/appsettings.json`.

Database `MiniBankDB` **không cần tạo tay** — `EnsureCreated()` tự tạo kèm 4 tài khoản mẫu
lúc API khởi động lần đầu.

> Đổi cấu trúc bảng (thêm cột) thì `EnsureCreated()` **không tự thêm cột**, phải xoá database
> rồi chạy lại API.

### 2. Face service (Python)

```bash
cd MiniBankFaceService
python3.11 -m venv .venv
.venv/bin/pip install -r requirements.txt          # Windows: .venv\Scripts\pip
.venv/bin/uvicorn main:app --host 127.0.0.1 --port 8000
```

Lần chạy đầu service tự tải model Facenet512 (95 MB) về `~/.deepface/weights/`
(Windows: `C:\Users\<tên>\.deepface\weights\`).

**Một model không tự tải được**, phải tải tay vào đúng thư mục đó:

```
https://github.com/minivision-ai/Silent-Face-Anti-Spoofing/raw/master/resources/anti_spoof_models/4_0_0_80x80_MiniFASNetV1SE.pth
```

Thiếu file này thì mọi request `POST /represent` trả lỗi 500.

Khởi động xong log phải in đủ hai dòng:

```
INFO:face-service:Recognition model Facenet512 loaded
INFO:face-service:Anti-spoofing model loaded
```

Kiểm nhanh: `curl http://127.0.0.1:8000/health`

### 3. API và giao diện

```bash
cd MiniBankWebApi    && dotnet run --launch-profile http    # http://localhost:5038/swagger
cd MiniBankWebClient && dotnet run --launch-profile http    # http://localhost:5112
```

Mở `http://localhost:5112` để dùng.

## Lưu ý khi chuyển từ macOS sang Windows

`requirements.txt` đang ghim `tensorflow==2.16.2` và `torch==2.2.2` vì đó là **bản cuối cùng
còn wheel cho macOS Intel**. Windows không bị giới hạn này. Nếu `pip` báo không tìm được
bản phù hợp, cứ bỏ số version đi và để pip chọn bản mới nhất.

Thư mục `.venv` không nằm trong repo, mỗi máy tự tạo lại.

## Ghi chú chi tiết

`MiniBankFaceService/NOTES.md` — nhật ký quá trình làm, các quyết định kỹ thuật, số liệu đo
thật, và những ngưỡng có thể chỉnh trong `face.js`.
