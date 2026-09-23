# Lục Hải

Prototype game grand strategy 2D bằng Godot 4 .NET và C#. Bản đồ, tỉnh và quốc gia đều là dữ liệu giả tưởng tự tạo.

## Chạy dự án

1. Cài Godot phiên bản .NET 4.7.2 và .NET SDK 8 trở lên.
2. Mở thư mục này bằng bản Godot .NET.
3. Chờ Godot nhập dự án và biên dịch C#; chạy scene chính `Scenes/Main.tscn`.

Thao tác trên bản đồ: nhấp trái để chọn tỉnh, lăn chuột để phóng to/thu nhỏ, giữ chuột phải hoặc chuột giữa để kéo bản đồ, dùng WASD hoặc phím mũi tên để dịch chuyển.

## Cấu trúc

- `src/Domain`: province, country, graph và kiểm tra dữ liệu, không phụ thuộc node Godot.
- `src/Map`: bản đồ mã màu, hiển thị tỉnh, camera và picking.
- `src/Infrastructure/Persistence`: nạp JSON và PNG.
- `src/Presentation`: bootstrap scene và API đọc dữ liệu cho HUD.
- `Data`: dữ liệu giả tưởng cho 20 tỉnh, 4 quốc gia và đồ thị kết nối.
- `assets/maps/province_id_map.png`: màu RGB mã hóa province ID; biển dùng màu `#152F37`.

Tệp `tools/generate_demo_world.py` tạo lại JSON và ảnh ID map khi chỉnh sửa vùng demo. Chạy bằng Python có Pillow: `python tools/generate_demo_world.py`.

Chạy kiểm tra camera/picking bằng Godot .NET headless:

```powershell
& "<đường-dẫn-Godot-.NET>\Godot_v4.7.2-stable_mono_win64_console.exe" --headless --path . res://tests/MapPickingSmokeTest.tscn
```

## Giao diện

HUD sẽ được nạp tự động từ `Scenes/UI/GameHud.tscn`. Xem [hợp đồng tích hợp UI](docs/ANTIGRAVITY_UI_HANDOFF.md) để biết các signal và API dữ liệu.

## Lộ trình

1. Phase 1 — Bản đồ, province/country, graph, picking, hover, đọc JSON và kiểm tra dữ liệu.
2. Phase 2 — Ngày trong game, tốc độ, kinh tế và tăng trưởng dân số.
3. Phase 3 — Quân đội, tuyển quân, tìm đường và di chuyển.
4. Phase 4 — Chiến tranh, giao tranh, chiếm đóng và hòa ước.
5. Phase 5 — AI kinh tế và quân sự.
6. Phase 6 — Save/load và menu.
7. Phase 7 — Đo hiệu năng và kiểm tra thế giới lớn.
