# Lục Hải

Prototype game grand strategy 2D bằng Godot 4 .NET và C#. Bản đồ, tỉnh và quốc gia đều là dữ liệu giả tưởng tự tạo.

## Chạy dự án

1. Cài Godot phiên bản .NET 4.7.2 và .NET SDK 8 trở lên.
2. Mở thư mục này bằng bản Godot .NET.
3. Chờ Godot nhập dự án và biên dịch C#; chạy scene chính `Scenes/Main.tscn`.

Thao tác trên bản đồ: nhấp trái để chọn tỉnh, lăn chuột để phóng to/thu nhỏ, giữ chuột phải hoặc chuột giữa để kéo bản đồ, dùng WASD hoặc phím mũi tên để dịch chuyển.

Mốc mô phỏng hiện có ngày bắt đầu 01/01/1444, nút tạm dừng và tốc độ 1–5 ngày mỗi giây. Thuế tỉnh được cộng vào ngân khố quốc gia mỗi ngày; dân số tăng theo mức 0,6% mỗi năm và nhân lực được cập nhật theo dân số.

Mốc quân đội: chọn tỉnh mình kiểm soát để tuyển 1.000 binh sĩ (chi phí 500 ngân khố). Nhấp vào dấu quân trên bản đồ để chọn đội quân, chọn tỉnh đích rồi nhấn **Điều quân tới tỉnh đang chọn**. Quân chỉ đi qua tỉnh do quốc gia mình kiểm soát hoặc lãnh thổ của đối thủ đang có chiến tranh; địa hình làm thay đổi số ngày di chuyển.

Mốc chiến tranh: mở **Vương Triều** để tuyên chiến hoặc ký hòa ước với AI. Đội quân giao chiến khi hai bên gặp nhau; quân thắng có thể chiếm quyền kiểm soát tỉnh nhưng chưa đổi chủ sở hữu. Hòa ước dùng điểm chiến tranh để xử lý tỉnh đang bị chiếm (20 điểm cho mỗi tỉnh nhượng lại); phần còn lại trở về chủ sở hữu ban đầu.

AI đánh giá tình hình mỗi 7 ngày game bằng điểm tiện ích có nhiễu ngẫu nhiên nhỏ, tái lập được từ `randomSeed` trong `Data/game_settings.json`. AI có thể tuyển quân, tuyên chiến với láng giềng, bảo vệ tỉnh bị chiếm hoặc đưa quân tấn công.

Mốc lưu game: menu **Bắt đầu chiến dịch / Tiếp tục chiến dịch / Cài đặt** mở khi chạy game. Nút **Lưu** ghi đè ô `autosave`; nút **Menu** lưu trước khi quay về. Bản lưu JSON nằm trong thư mục dữ liệu người dùng của Godot và chứa ngày, tốc độ, seed, quốc gia, tỉnh, quân, chiến tranh và lịch AI.

## Cấu trúc

- `src/Domain`: province, country, graph và kiểm tra dữ liệu, không phụ thuộc node Godot.
- `src/Map`: bản đồ mã màu, hiển thị tỉnh, camera và picking.
- `src/Infrastructure/Persistence`: nạp JSON và PNG.
- `src/Presentation`: bootstrap scene và API đọc dữ liệu cho HUD.
- `src/Core` và `src/Simulation`: thời gian game, tốc độ, kinh tế và dân số.
- `src/Domain/Armies`, `src/Domain/Diplomacy`, `src/Map/ProvincePathfinder.cs` và `src/Simulation/Military`: đội quân, chiến tranh, A*, tuyển quân, giao tranh và chiếm đóng.
- `src/Simulation/AI`: chấm điểm hành động kinh tế/quân sự và ra quyết định theo chu kỳ 7 ngày.
- `src/Infrastructure/Persistence/JsonSaveGameRepository.cs`: lưu/tải JSON theo ô lưu, có kiểm tra dữ liệu trước khi khôi phục.
- `src/Presentation/GameLauncher.cs`: menu chính, tiếp tục autosave và cài đặt độ phân giải cửa sổ.
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
2. Phase 2 — Ngày trong game, tốc độ, kinh tế và tăng trưởng dân số (đã triển khai).
3. Phase 3 — Quân đội, tuyển quân, tìm đường và di chuyển (đã triển khai).
4. Phase 4 — Chiến tranh, giao tranh, chiếm đóng và hòa ước (đã triển khai).
5. Phase 5 — AI kinh tế và quân sự (đã triển khai).
6. Phase 6 — Save/load và menu (đã triển khai).
7. Phase 7 — Đo hiệu năng và kiểm tra thế giới lớn.
