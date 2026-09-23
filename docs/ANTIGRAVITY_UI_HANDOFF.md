# Bàn giao phần giao diện Phase 1

Antigravity phụ trách phần HUD trong hai tệp riêng:

- `Scenes/UI/GameHud.tscn`
- `src/UI/GameHud.cs`

Không sửa `src/Domain`, `src/Map`, `src/Infrastructure`, `src/Presentation/GameBootstrap.cs`, `Data`, `assets/maps` hoặc `Scenes/Main.tscn`. Bootstrap sẽ tự nạp `GameHud.tscn` nếu tệp đã có.

## API có sẵn

Gốc HUD nên là `Control` và script C# có thể dùng hợp đồng sau:

```csharp
public void BindSession(GameBootstrap session);
public void InitializeWorld(Godot.Collections.Dictionary summary);
public void OnProvinceSelected(int provinceId);
public void OnProvinceHovered(int provinceId);
```

`provinceId == -1` có nghĩa là chưa chọn tỉnh hoặc con trỏ đang ở ngoài vùng đất. HUD có thể đọc dữ liệu từ session:

- `GetProvinceDetails(int provinceId)` trả về tên tỉnh, quốc gia sở hữu/kiểm soát, dân số, kinh tế, phát triển, thuế, nhân lực, địa hình và màu quốc gia.
- `GetProvinceNeighborSummaries(int provinceId)` trả về các tỉnh giáp ranh từ province graph.
- `GetCountrySummaries()` trả về danh sách tên quốc gia, màu bản đồ, thủ phủ, số tỉnh và trạng thái AI.

`InitializeWorld` nhận dictionary có `provinceCount` và `countryCount`. Session phát signal `WorldReady`, `ProvinceSelected` và `ProvinceHovered`.

## Luồng chọn tỉnh

`Main.tscn` → `GameBootstrap` → `ProvinceMap` → ảnh ID PNG → `ProvinceId` → `GameBootstrap.GetProvinceDetails()` → HUD.

`ProvinceMap` chỉ phát sự kiện chọn tỉnh sau khi xử lý tọa độ qua camera; HUD không cần đọc pixel hoặc sửa domain model.
