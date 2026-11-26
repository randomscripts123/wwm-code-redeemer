# WWM Code Redeemer

Ứng dụng Windows tự động nhập mã đổi thưởng **Where Winds Meet**. / Windows app that auto-enters **Where Winds Meet** exchange codes.

- Tự lấy danh sách mã đang hoạt động từ [codes.yar.gg](https://codes.yar.gg/) (chỉ mục *active*).
- Nhập mã lần lượt, có nhật ký ngay trong app, giữ **F12** để dừng.
- Nhập thêm mã từ file `.txt`, nhớ mã đã nhập.
- Nhận diện nút bằng OCR của Windows, có F9 dự phòng. Giao diện Việt / Anh.

## Cách dùng

1. Tải `WWMCodeRedeemer.exe` ở mục **Releases**, mở (Windows sẽ hỏi quyền Administrator).
2. Mở game, vào **Settings > Other** (thấy dòng *Exchange Code*).
3. Bấm **Bắt đầu**. Lần đầu app tự nhận diện nút; nếu không thấy sẽ nhắc chọn tay bằng **F9**.

Game nên để chế độ cửa sổ / borderless, ngôn ngữ tiếng Anh để OCR đọc được. Đổi kích thước cửa sổ thì bấm **Hiệu chỉnh nút** lại.

## Lưu ý

- App mô phỏng chuột và bàn phím. Dùng macro có thể vi phạm điều khoản của game và có rủi ro bị xử lý tài khoản. Tự chịu trách nhiệm khi dùng.
- Không đọc hay sửa bộ nhớ/tệp của game. Chỉ gọi API công khai `https://codes.yar.gg/api/codes`.

## Build

Cần .NET SDK 10.

```bash
cd WwmRedeemer
dotnet publish -c Release -o ../publish
```

Tự kiểm tra không mở giao diện (bản Debug): `WWMCodeRedeemer.exe --selftest [--img ảnh.png]`.
