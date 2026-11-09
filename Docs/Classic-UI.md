# Classic HUD và Pause — 17/10/2026

## Thiết kế

- Bỏ panel mô tả “Classic Endless / No move limit…” ở đáy màn hình và nút Pause chữ nhỏ.
- Header: huy hiệu kỷ lục có vương miện, điểm ván lớn ở giữa, nút tạm dừng tròn vàng với hai vạch. Khi vượt kỷ lục đầu ván có nhãn “KỶ LỤC MỚI!”.
- Dưới khay chỉ còn hai huy hiệu nhỏ: **Hàng đã xóa** và **Combo cao nhất**. Combo ở đây là thành tích tốt nhất trong ván, không giả làm combo hiện tại.
- Menu Pause đồng bộ teal/vàng, có Chơi tiếp, Về trang chủ, Âm thanh, Rung, thanh âm lượng SFX, Giao diện và Riêng tư; có nút đóng riêng. Mọi mục vẫn dùng luồng save/navigation/privacy cũ.
- Hình bo góc, vương miện, Pause và các icon là mesh/vector UI, tái sử dụng hệ `PopHomeGraphic`. Không bake chữ/nút thành ảnh và không cần tải thêm SDK/font.

## Chỉnh trong Inspector

Stop Play, chọn **Blokki Pop UI** trong scene Game. Chọn **Scene Preview = Hud**, tắt **Preview Endless HUD** để xem Classic. Chọn đối tượng con:

```text
Blokki Pop UI
├─ Classic Overlay                         [PopClassicHUDView]
│  └─ Gameplay Frame                      [Aspect Ratio Fitter 9:16]
│     ├─ Header
│     │  ├─ Best / Crown / Label / Value
│     │  ├─ Mode / Score Label / Score / New Record
│     │  └─ Pause
│     └─ Stats
│        ├─ Lines
│        └─ Best Combo
└─ Pause                                  [PopUIView + PopPausePresentation]
   └─ Modal Safe Area
      └─ Modal Frame
         └─ Card
```

- Kéo **Header**, **Stats** và các con để chỉnh vị trí/kích thước/font/màu; runtime không đặt lại chúng. Dữ liệu điểm/kỷ lục/lượt xóa thay đổi theo ván là chủ đích.
- **Classic Overlay → PopClassicHUDView**: `Playfield Gap`, `Reserve Playfield`, `Fit Device Safe Area`, chuỗi thông báo kỷ lục và tiền tố combo đều chỉnh được.
- **Gameplay Frame → Aspect Ratio Fitter** giữ cụm HUD gọn trên màn dài. Đây là layout component hiện rõ, có thể chỉnh/tắt; các panel con giữ offset do designer đặt.
- Camera đọc bounds thật của Header/Stats để chừa chỗ cho board/tray. Chỉ camera đổi để vừa màn hình; không ghi đè tọa độ UI con. Edit preview dùng cùng cách căn camera như Play.
- Chọn **Scene Preview = Pause** để sửa menu tạm dừng trước Play. **PopPausePresentation** giữ các nhãn tiếng Việt, tham chiếu âm thanh/rung/slider và safe area. Màu/bo góc trên `Surface`, font trên `Label` đều là object thật.
- Bật **Preview Endless HUD** để chuyển sang chỉnh HUD Endless Pop đã làm trước đó. Journey/Daily/Tutorial giữ panel mục tiêu/hướng dẫn riêng, không bật lại bộ điểm/kỷ lục/bánh răng legacy đã tắt.
- Lưu scene để giữ scene override; dùng Overrides → Apply đúng đối tượng nếu muốn cập nhật prefab dùng chung. Không Apply/Revert toàn bộ scene khi chỉ muốn sửa một chữ hoặc một panel.

## Cài đặt và phạm vi

Menu **Tools → Blokki Pop → UI → Install Classic HUD and Pause Sheet** thêm vào cả hai prefab UI. Chạy lại giữ các chỉnh sửa đã có, không rebuild HUD/Pause mới. Bản sao trước nâng cấp: `.utmp/classic-ui-polish-20261017/backups/`.

Không đổi luật chơi/sinh khối/quảng cáo và không xóa save/high score. **Sửa ngày 18/10:** HUD Classic mới được giữ phía sau Game Over/đề nghị hồi sinh, với điểm cuối ván và nút Pause không tương tác khi đã thua. Không khôi phục lại artwork cũ ở trạng thái kết thúc.

Trong scene Game, `UI/Image` (điểm), `UI/Image (1)` (kỷ lục) và `UI/Button` (bánh răng) được tắt Active thật; giữ nguyên đối tượng/tham chiếu để khôi phục khi cần, không xóa. `UI` gốc vẫn bật vì còn chứa Combo Popup và Game Over Panel. Game Over Panel có Canvas override sorting riêng (mặc định 1900, trên HUD 1800) và GraphicRaycaster để lớp dimmer che đúng HUD mới và nút popup vẫn bấm được. Đề nghị hồi sinh cũng được xếp trên HUD mới. Hiệu ứng điểm bay nhắm tới điểm mới đang nhìn thấy, không bay về icon coin đã tắt.

Pause mới là nhánh riêng ngay dưới canvas, không dùng lại RectTransform của Pause cũ. Scene cũ có override Y để bù vị trí Safe Area; tái sử dụng/reparent transform đó sẽ đẩy popup mới ra khỏi màn hình. Bản cũ vẫn giữ nguyên dưới Safe Area, tên **Legacy Pause (preserved)** và tắt; danh sách `Views` chỉ trỏ tới Pause mới. Không reset Safe Area hoặc xóa scene override của mày.

Âm thanh và rung dùng đúng preference chung với Home; slider SFX giữ hành vi điều chỉnh volume hiện có của `AudioManager`. Không thêm hệ lưu âm lượng mới. Pause vẫn flush save, giữ bàn/khay/điểm/RNG, và trả đúng time scale khi đóng.

## Kiểm tra

`PopClassicPresentationValidation.Run` chỉ chạy trong project QA `BlokkiPopRegression`. Test pixel bounds ở 720×1280, 720×1440, 720×1600, 1080×2400, 768×1024 và notch; xác nhận không đè board/tray, không cướp raycast, giữ các edit font/màu/offset, click Pause bằng chuột thật, sound/haptics/volume, save/resume, điểm/kỷ lục, target điểm bay và Game Over.

- Classic presentation: **399 checks PASS** (bao gồm cả Pause ở năm kích thước, chỉnh offset Card trong prefab và chạy lại installer).
- Gameplay regression: **558 checks PASS**, 20 màn Journey thắng thật, Daily/tutorial, Classic exact resume và ba chu kỳ Play/Stop khi tắt Domain Reload.
- Endless presentation regression: **563 checks PASS**, năm tỉ lệ màn hình, notch, Pause dùng chung và chuyển về Classic không rò HUD.
- Script compile: **Editor, Android Development, Android Release PASS**. Đây là kiểm tra C#, không phải build APK.
- Bản mặc định cuối chạy `RunRelease` cũng **399 checks PASS**, không mang offset/font test vào prefab giao cho mày. So sánh với backup: **691 RectTransform cũ giữ nguyên geometry**, cả Home/Game scene giữ nguyên SHA256. Chỉ hai prefab UI được thay bằng bản đã kiểm tra.
- Log Unity có lỗi index khởi động từ `UnityEditor.Search.SearchDatabase` đã biết; bộ test loại riêng stack này, không bỏ qua lỗi runtime game. Cảnh báo icon Android Legacy là cảnh báo cũ, không phải lỗi compile.

Log/ảnh native Unity: `.utmp/classic-ui-polish-20261017/`. Không chạy bài QA xóa/reset prefs trên project chính. Chưa đóng gói/cài UI này lên Vivo; compile Android không thay cho kiểm tra thiết bị thật.

Ảnh bàn giao chụp trực tiếp từ Unity, không phải mockup: [Classic HUD](Previews/classic-hud.png), [Pause](Previews/classic-pause.png).

## Sửa lỗi HUD phía sau Game Over — 18/10/2026

Nguyên nhân: `PopClassicHUDView.Refresh` chỉ hiện HUD khi `IsActive && !IsGameOver`, còn `ApplyModePresentation` bật lại toàn bộ graphic legacy khi thua. Hai điều kiện đó khiến popup đúng nhưng nền HUD phía sau lại trở về bản cũ.

- HUD mới dựa vào ván đã khởi tạo (`IsInitialized`), giữ điểm/kỷ lục/hàng/combo cuối ván; Pause bị vô hiệu hóa khi ván kết thúc.
- Không khôi phục graphic legacy khi Game Over; ba nhánh cũ được tắt Active thật trong scene.
- Popup có Canvas sorting riêng và GraphicRaycaster; dimmer phủ cả HUD mới nhưng nút Chơi lại/Home vẫn nhận raycast.
- Giữ host `UI` hoạt động cho Game Over/Combo; không tắt nhầm cả gốc. Build guard báo lỗi rõ nếu host hoặc Canvas/raycaster kết quả bị gỡ/tắt.
- Không thay Transform/RectTransform/camera của scene, không sửa bố cục hai prefab UI.

Kiểm tra sửa lỗi: **460 checks PASS**, 5 tỉ lệ màn hình/notch; Pause/settings/resume, HUD khi thua, kết quả/đề nghị hồi sinh nằm trên HUD, nút Restart/Home (raycast và EventSystem pointer handlers), chơi ván mới không bật lại UI cũ. C# compile sạch cho Editor/Android Development/Android Release. Log và ảnh native: `.utmp/gameover-hud-20261018/`.

Regression sau khi tắt các nhánh cũ: gameplay/progression **564 checks PASS** (20 màn, Daily, tutorial, save/resume, 3 vòng Play/Stop); Endless UI **549 checks PASS** trên 5 tỉ lệ màn hình. Home và hai prefab UI không bị ghi lại; 94 block Transform/RectTransform/camera trong Game giữ nguyên so với trước lượt sửa.

![Game Over trên HUD mới](Previews/game-over-new-hud.png)
