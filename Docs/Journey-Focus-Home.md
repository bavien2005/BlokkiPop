# Home Journey Focus — 15/10/2026

Home được chuyển sang bố cục teal/vàng đã chọn: logo một dòng, bàn nhỏ có animation thả khối, đường tiến trình bốn nút, CTA Journey, tiếp tục Classic và ba lối tắt phía dưới. Không còn thẻ menu đặc chắn giữa màn hình hoặc Tap to Start trùng chức năng.

## Chỉnh trực tiếp, không cần Play

Mở `Assets/_Project/Scenes/Home.unity`, chọn `Blokki Pop UI` trong Hierarchy. Instance Home dùng prefab riêng `Assets/_Project/Prefabs/UI/PopJourneyFocusUI.prefab`. Scene Game vẫn dùng `PopGameplayUI.prefab`; đổi Home không ghi đè vị trí HUD trong Game.

```text
Blokki Pop UI
├─ Journey Focus Backdrop                 nền gradient + hai mảng trang trí
└─ Safe Area
   └─ Home [PopUIView, PopJourneyHomeView]
      └─ Card                            không có nền panel che giữa màn hình
         ├─ Best / Stars Icon / Stars / Settings
         ├─ Brand and Mini Board
         │  ├─ Logo / Logo Shadow / Logo Text
         │  ├─ Tagline
         │  └─ Mini Board                 16 ô nền + 6 khối màu
         ├─ Navigation
         │  ├─ Levels / Journey Track / Progress01…04
         │  ├─ Journey Button Shadow / Journey
         │  ├─ Continue
         │  └─ Classic / Daily / Rewards
         └─ Settings Sheet               bật Preview Settings để chỉnh
```

- Vị trí, kích thước, anchors: chỉnh **Rect Transform** của từng đối tượng. Runtime không dựng lại cây UI hoặc đưa các nút về tọa độ mặc định.
- Font, cỡ chữ, màu chữ: chỉnh **TextMeshPro - Text (UI)**. Với chữ động, `PopUIView → Elements → Update Text` quyết định code có cập nhật nội dung không; tắt nếu muốn giữ chữ tự đặt.
- Nội dung tiếng Việt mặc định và màu trạng thái tiến trình: **Pop Journey Home View** trên `Safe Area/Home`. Dữ liệu điểm/sao/màn đang mở vẫn được cập nhật theo save.
- Nền, bo góc và icon nét: **Pop Home Graphic**. Đây là đồ họa UI native có thể đổi màu/shape/radius, không phải ảnh chụp dính cả giao diện. Nền có màu đầu/cuối gradient riêng.
- Preview bảng cài đặt: **Home → Pop Journey Home View → Preview Settings**. Các trang Journey/Daily/Rewards vẫn preview bằng **Blokki Pop UI → Scene Preview → Show Selected Panel in Scene**.
- **Select UI Prefab** trong Inspector chọn prefab của chính instance hiện tại, không luôn trỏ nhầm về prefab Game cũ.
- Muốn chỉnh mọi instance của Home: mở prefab mới. Muốn chỉnh riêng Home scene: chỉnh scene và lưu override. Không chạy lại trình tạo UI để áp bố cục mặc định.

## Các nút thực sự làm gì

- **CHƠI POP JOURNEY**: mở mục tiêu màn chưa hoàn thành tiếp theo; nếu có ván Journey hợp lệ đang lưu thì ưu tiên màn đó. Không tự bỏ qua màn khóa.
- Bốn nút tiến trình: sao = đã hoàn thành, play vàng = màn đề xuất, khóa = chưa mở. Chạm nút đã mở để xem/chơi màn tương ứng. Chạm tiêu đề hành trình để mở danh sách đầy đủ.
- **Tiếp tục ván Classic**: chỉ xuất hiện khi có save Classic hợp lệ; tiếp tục đúng bàn/khay/điểm/trạng thái sinh khối, không tạo ván giả. **CLASSIC** phía dưới bắt đầu ván mới và vẫn hỏi xác nhận nếu sẽ thay ván đang lưu.
- **DAILY**, **GIAO DIỆN** nối với Daily/rewards hiện có, không phải nút minh họa.
- Gear: bật/tắt âm thanh, rung, hướng dẫn chơi và quyền riêng tư. Các lựa chọn âm thanh/rung được lưu trên thiết bị.
- Best và số sao lấy từ dữ liệu thật. Các con số trong ảnh QA thuộc profile thử riêng, không được ghi vào save người dùng.

## Animation và bảo toàn chỉnh sửa

`Blokki Pop Intro` vẫn giữ preloader và logic vào Game; các reference animation chuyển sang sáu khối trong Home mới. Canvas intro cũ được giữ nhưng tắt, không bị xóa. Lần đầu vào Home có animation ngắn; quay lại Home trong cùng phiên chơi không bắt xem intro lại. Không còn yêu cầu chạm màn hình trống để vào Game.

Chỉnh thời gian rơi/độ rơi/stagger trên **Blokki Pop Intro Controller**. `Apply Inspector Font Sizes` tắt trong Home mới để cỡ chữ designer không bị script cũ ghi đè. Animation giữ lại scale/điểm đáp do designer chỉnh.

Installer chỉ thay Home, không thay scene Game/HUD hoặc xóa PlayerPrefs. Bản Home trước khi chuyển nằm ở `.utmp/journey-home-20261015/backups/Home.unity`. Chạy installer khi đã có Home mới không dựng lại layout; scene đang sửa chưa lưu sẽ bị từ chối để tránh mất thay đổi.

## Kiểm chứng

Ảnh render và kết quả kiểm thử nằm tại `.utmp/journey-home-20261015/`. Kiểm thử dùng Unity project/profile QA riêng; không chơi thử bằng cách xóa save thật. Phần cập nhật này là scene/code trên laptop; kiểm tra quảng cáo/hiệu năng/touch trên Vivo cần APK mới, không thể kết luận chỉ từ ảnh trong Editor.

Đã đạt **41 kiểm tra Home** (`home-final.log`): điểm/sao thật, trạng thái bốn node, khóa màn, vùng chạm UI (kể cả nút nền trong suốt), Continue có/không có save, Journey/Daily/Rewards, âm thanh/rung, giữ settings khi intro kết thúc và quay lại Home không lặp intro. **558 kiểm tra gameplay** (`gameplay-final.log`) cũng đạt: kéo thả chuột/cảm ứng mô phỏng qua Input System, 20 màn Journey thắng trong scene Game, Daily, tutorial, resume chính xác và ba vòng Play/Stop tắt Domain Reload. Ảnh `home.png`/`settings.png` được chụp từ Unity chạy trong project QA, không phải mockup HTML.
