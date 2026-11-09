# Endless Pop HUD v2 — 16/10/2026

> Cập nhật 17/10: Classic hiện cũng có HUD mới và menu Pause được thiết kế lại dùng chung. Xem [Classic-UI.md](Classic-UI.md). Các mô tả “Classic giữ HUD cũ” bên dưới là trạng thái của lần bàn giao 16/10, không còn là giao diện hiện tại; luật chơi/save không đổi.

## Thay đổi

- Thông tin Đợt / Bóng còn / Lượt còn chỉ xuất hiện một lần phía trên. Điểm ván ở hàng tiêu đề, Pause là nút vàng có biểu tượng hai vạch.
- Giữ bàn và khay kéo khối trong vùng chơi riêng. Bên dưới là hai thẻ **TIẾP THEO** và **SAU NỮA**, không phủ lên tray.
- Không còn câu “Xanh: khay kế / Vàng: khay sau / Chưa kéo được”. Cả hai nhóm dùng cùng khối màu cam, có vát cạnh; nhãn nhóm thể hiện thứ tự, không bắt người chơi nhớ màu.
- **Tiếp theo** là ba khối nhận khi dùng hết khay lớn hiện tại. Dùng hết nhóm đó mới tới **Sau nữa**. Đây là thông tin để tính nước đi, không phải khay kéo thứ hai. Khay hết khối hiện “Đã hết”. Hình và hướng khối khớp deck thật.
- **Lượt** tính theo số khối đặt thành công, không phải số ô cell. Mục tiêu vẫn là xóa hết bóng trước khi hết lượt.
- Canvas điểm/kỷ lục cũ và Hud/Card chỉ ẩn khi chơi Endless; các GameObject, listener, layout, save và high score Classic vẫn giữ nguyên. Pause/Failure không bị preview che.

## Chỉnh trực tiếp trong Scene/Inspector

1. Stop Play. Mở scene Game, chọn **Blokki Pop UI**.
2. Chọn **Scene Preview = Hud**, bật **Preview Endless HUD**, bấm **Show Selected Panel in Scene** nếu cần.
3. Trong Hierarchy mở:

   ```text
   Blokki Pop UI
   └─ Endless Pop Overlay
      └─ Endless Pop Queue
         └─ Gameplay Frame
            ├─ Header
            │  ├─ Mode Title / Score
            │  ├─ Wave / Bubbles / Moves
            │  └─ Pause
            └─ Upcoming Trays
               ├─ Next Tray
               └─ Later Tray
   ```

4. Kéo **Header** hoặc **Upcoming Trays**, chỉnh RectTransform, scale, kích thước. Chọn con **Label / Value / Surface** để chỉnh chữ, font, màu, nền, bo góc. Nút Pause có Button/UnityEvent thật. Không có script dựng lại các object này lúc Play.
5. Trên **Pop Endless Queue View**: `Playfield Gap` là khoảng thở giữa panel với bàn/khay; `Reserve Playfield` cho camera chừa chỗ theo bounds thật. `Fit Device Safe Area` chỉ căn anchors root theo tai thỏ, giữ offset/size và các con. Dữ liệu động, chuỗi và màu cảnh báo ít lượt đều có trường Inspector.
6. **Gameplay Frame → Aspect Ratio Fitter** giữ cụm UI theo khung 9:16, căn giữa màn dài để các thẻ không cách bàn/khay quá xa. Đây là component layout hiện rõ trong Inspector; chỉnh tỉ lệ hoặc bỏ fitter để tự thiết kế khung. **Main Camera → Responsive Game Layout → Preview Endless Layout** cho edit preview căn camera như Play. Script chỉ thay camera, không đưa các panel con về vị trí cũ. Đặt Header phía trên và Upcoming Trays phía dưới; nếu chiếm gần hết màn hình thì không thể giữ bàn đủ lớn.
7. Tắt **Preview Endless HUD** để chỉnh Hud/Card của Classic. Lưu scene; nếu muốn áp dụng cho mọi scene dùng prefab thì dùng Overrides → Apply cho đúng đối tượng.

Installer **Tools → Blokki Pop → UI → Install Endless Pop Preview** chỉ migrate dải cũ sang v2 một lần. Chạy lại không reset các chỉnh sửa v2. Bản sao hai prefab trước thay đổi ở `.utmp/endless-ui-polish-20261016/backups/`. Không sửa hoặc lưu đè scene Home/Game bằng installer.

## Sprite và prompt

Sprite **Assets/_Project/UI/EndlessPop/bubble-gold.png** tạo bằng công cụ **imagegen tích hợp**, không dùng CLI/API key. Nền và tâm có alpha; import Single Sprite, 256px, không mipmap. Chỉ icon là bitmap; chữ, panel bo góc và hình polyomino vẫn là UI/mesh thật, không bake thành một tấm ảnh khó chỉnh.

Prompt cuối đã dùng:

> Asset type: transparent mobile puzzle game UI icon, not a screenshot or mockup. Create ONE polished, appealing golden bubble target icon for Blokki Pop, a cheerful block puzzle with sky-blue backgrounds and warm wooden board. Single round hollow soap bubble/ring with a thick honey-gold rim, luminous amber shaded lower edge, soft buttery yellow highlights upper-left, tiny glossy white curved glint. A subtle aqua iridescence inside the rim, transparent center. Friendly premium casual game painted 2.5D sprite, clean strong silhouette legible at 40 pixels. Straight-on front view, no perspective tilt. Icon centered and fills about 85 percent of a square canvas; generous transparent padding, genuinely transparent background and center, no solid background. Restrained clean detailing, no rays, stars, particles, coins, text, numbers, extra icons, logos or watermark. Deliver as an isolated reusable raster sprite for native Unity UI; not a whole interface.

## QA

`PopEndlessPresentationValidation.Run` chỉ chạy trong project QA `BlokkiPopRegression`, không chỉnh prefs của người chơi. Kiểm tra pixel bounds thật của board/tray/panel ở 720×1280, 720×1440, 720×1600, 1080×2400, 768×1024 và mô phỏng notch trên/dưới. Kiểm tra chữ không tràn, shape preview đúng, raycast không cướp thao tác khay, Pause/Resume, Classic, và giữ các offset/font/màu do designer chỉnh.

Log và screenshot native Unity ở `.utmp/endless-ui-polish-20261016/`. Đây không phải mockup ảnh AI. Chưa đóng gói/cài bản UI này lên Vivo; compile Android không thay cho test thiết bị thật.

Kết quả cuối: **562** kiểm tra UI/layout đạt, **4.986** kiểm tra Endless đạt và **558** kiểm tra hồi quy Classic/Journey/Daily/tutorial đạt. Bản có chỉnh offset/font/màu để thử tính editable và bản prefab mặc định cuối đều qua bộ layout. Runtime compile đạt Editor, Android Development và Android Release. So sánh hai prefab trước/sau xác nhận không đổi RectTransform cũ nào ngoài root dải Endless đang thay thế; hash scene Home/Game chính không đổi.

Preview bàn giao: `Docs/Previews/endless-pop-hud-v2.png`, chụp trực tiếp từ Unity 720×1600 bằng đúng prefab mặc định đã đưa vào project.
