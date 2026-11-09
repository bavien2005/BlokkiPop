# Endless Pop — 16/10/2026

> Update 20/10: separate first-play bubble/next-tray lesson, structural puzzle scoring/lane variety and dedicated pop/wave feedback. Inspector locations, current QA and human-playtest checklist: [Endless-Pop-Product.md](Endless-Pop-Product.md). Existing saved puzzles keep their exact deck and targets.

Một ván giải đố liên tục trên cùng bàn 8×8. Mỗi đợt có bóng đánh dấu, bộ khối cố định và giới hạn lượt. Xóa hết bóng thì chuyển đợt ngay trong scene Game; không có bảng Victory bắt bấm Next. Hết lượt hoặc không khối còn lại nào đặt được thì kết thúc ván. Nước cuối vừa hết lượt vừa xóa hết bóng vẫn qua đợt.

## Luật và đường vào

- Home: nút chính mở **Endless Pop**, có **Tiếp tục** và **Ván mới** riêng. Nếu có save, chơi mới yêu cầu xác nhận. Bấm Back ở xác nhận không thay đổi save.
- Bốn node Home biểu diễn các đợt của ván đang lưu (hoặc kỷ lục khi không có ván), không cho nhảy thẳng lên đợt cao.
- Khay đang chơi vẫn gồm tối đa ba khối, đặt theo thứ tự tùy ý. Dùng hết khay mới nhận nhóm kế tiếp.
- Hiển thị toàn bộ khối chưa cấp của đợt: tối đa hai khay/sáu khối, ghi rõ **TIẾP THEO** và **SAU NỮA**. Cả hai cùng màu, không còn chú giải xanh/vàng. Preview giữ đúng hình và hướng; chỉ kéo các khối trong khay chơi lớn.
- Mỗi khối đặt thành công tốn một lượt, không tính theo số ô trong khối. Thả sai không tốn lượt. Không có đếm ngược thời gian.
- Qua đợt: giữ mọi ô khối còn lại và màu của chúng, bỏ khay chưa dùng của đợt cũ, chuẩn bị mục tiêu/khay/lượt mới. Không sinh thêm bóng giữa một đợt.
- Classic và Daily không đổi. 20 màn Journey cũ giữ làm dữ liệu Daily và bài luyện, truy cập tại gear Home → **Bài luyện**. Sao Journey và các giao diện đã mở vẫn giữ; sao không bị thay bằng điểm Endless.
- Không có rewarded revive trong Endless Pop. Best score của Classic không bị điểm Endless ghi đè.

## Bộ sinh đề hoạt động thế nào?

`PopEndlessGenerator` làm việc trên bản sao của **bàn còn lại thật**, không xóa bàn để dễ sinh:

1. Chọn các hàng hoặc cột thành một cụm mục tiêu.
2. Chọn một chuỗi khối có thể đặt hợp lệ vào các khoảng còn trống, dùng các vị trí đó làm khoảng cần lấp.
3. Bổ sung khối quanh các khoảng đó để dựng bài toán; đánh dấu bóng trên các ô của cụm mục tiêu. Ô có sẵn không bị xóa hoặc đổi màu.
4. Chạy lại chuỗi đặt bằng đúng phép mô phỏng xóa hàng/cột của game. Chỉ nhận đề có một lời giải thật, khối được cấp đúng khay ba chiếc và hoàn thành trong số lượt cho phép.
5. So sánh một số lượng ứng viên hữu hạn, ưu tiên chuỗi giải dài hơn và hình đa dạng; chốt toàn bộ deck trước khi chơi. Xáo thứ tự trong cùng một khay không phá lời giải đã chứng minh.

Tăng khó theo số đợt: nhiều bước chuẩn bị/cụm mục tiêu hơn, ít lượt dư hơn. Có đợt nhẹ định kỳ. Độ khó có trần để vẫn hiển thị toàn bộ deck và tránh đề vô lý. Đây là tăng độ phức tạp của lời giải đã tìm được, **không phải thuật toán chứng minh lời giải ngắn nhất hoặc bằng chứng playtest rằng người chơi thấy vui**.

Nếu bàn quá chật hoặc không tìm được ứng viên trong ngân sách, fallback hoàn thành một hàng trên chính bàn đó, chia khoảng trống thành các khối thẳng. Nó vẫn được giải thử, không reset bàn và không chạy tìm kiếm vô hạn. Bản đầu cần playtest thêm tính đa dạng và độ khó trên người chơi thật.

## Save/resume

Mode `EndlessPop` được thêm cuối enum (không đổi số của các mode cũ). Save riêng: `blokkipop.run.v1.EndlessPop`, có backup riêng. Lưu bàn/màu/bóng, tổng điểm/lượt/bóng, số đợt, mốc bắt đầu đợt, khay dùng một phần, toàn bộ đề/deck và trạng thái random.

Tự flush sau mỗi giao dịch đặt khối hoàn chỉnh và sau chuyển đợt; lưu khi tạm dừng/rời app. Cả trạng thái vừa xóa hết bóng nhưng chưa sang đợt mới cũng được lưu. Resume trạng thái này chỉ chuyển một lần, không cộng lại thành tích. Tạm dừng và mọi input đặt khối đều bị chặn trong nhịp chuyển đợt. Chơi mới không xóa kỷ lục/giao diện/save của Classic hoặc Daily. Thua xóa save ván Endless đã kết thúc và giữ kỷ lục đợt hoàn thành/điểm/bóng.

Save được kiểm tra schema, revision, luật, mục tiêu, deck/khay, số lượt và trạng thái bàn. Save không tương thích không được trộn với đề mới. Đổi `Generator Revision` khi phát hành luật/deck không còn tương thích.

## Chỉnh trong Inspector

Chọn `Assets/_Project/Resources/Gameplay/PopGameplayConfiguration.asset` → **Endless**:

| Trường | Tác dụng |
| --- | --- |
| Replace Journey Home Button | Dùng nút chính cho Endless; tắt để trở lại luồng Journey cũ |
| Resume Run | Bật/tắt tiếp tục ván Endless |
| Candidate Attempts | Ngân sách ứng viên mỗi đợt; nhiều hơn không bảo đảm vui hơn |
| Minimum / Maximum Moves | Giới hạn số lượt/deck, tối đa 9 để preview không giấu khối |
| Starting / Minimum Slack | Lượt dư so với lời giải được tìm thấy |
| Waves Per Difficulty Step | Tốc độ tăng độ phức tạp |
| Maximum Target Lanes / Bubbles Per Lane | Giới hạn cụm hàng/cột và số bóng |
| Breathing Wave Interval | Khoảng cách giữa các đợt nhẹ; 0 tắt |
| Transition Seconds | Nhịp chuyển đợt, không phải đồng hồ tính thua |
| Fixed Seed | 0 = ván mới có seed mới; khác 0 để tái hiện QA |
| Title / Play / Resume / New Run / Description / Replace Warning | Các nhãn và hướng dẫn, không hardcode layout |

Nút **Preview / paint / replay Endless Pop generator** trên Inspector hoặc menu **Tools → Blokki Pop → Gameplay → Preview Endless Pop Generator** mở cửa sổ tô bàn còn lại, chọn seed/đợt, xem toàn bộ đề/deck và đi từng bước lời giải. Preview không thay đổi scene/save.

## UI thật trong prefab

Hai prefab `PopGameplayUI.prefab` và `PopJourneyFocusUI.prefab` có UI thật, không tạo object trong Play. Bản HUD v2 thay riêng dải Endless cũ: `Blokki Pop UI → Endless Pop Overlay → Endless Pop Queue → Header / Upcoming Trays`. Header chứa Đợt, Bóng còn, Lượt còn, điểm và Pause; Upcoming Trays chứa hai nhóm hình khối có nhãn. Không còn thông tin lặp ở HUD dưới. Xem hướng dẫn và prompt tạo sprite tại [Endless-Pop-UI.md](Endless-Pop-UI.md).

Overlay riêng để không thừa hưởng offset Safe Area/HUD cũ. Chỉnh vị trí trên **Header** và **Upcoming Trays**; camera đọc bounds thực của chúng để chừa vùng cho bàn và khay, không đổi RectTransform của chúng. Chọn Scene Preview = Hud và bật Preview Endless HUD để chỉnh trước Play; tắt checkbox này để chỉnh HUD Classic. Preview tắt trong các mode khác và khi rời Game.

Installer: **Tools → Blokki Pop → UI → Install Endless Pop Preview**. Chạy lại giữ queue/label/style đã chỉnh, không tạo trùng. Build guard chặn bản có entry Endless nhưng thiếu binding preview. Bản sao prefab trước nâng cấp ở `.utmp/endless-pop-20261016/backups/`.

## Kiểm chứng bản bàn giao

- `endless-final-verified.log`: **5.208 kiểm tra** đạt. Có 192 đợt sinh nối tiếp trên bàn còn lại, tái hiện seed và giải lại witness; chơi qua bảy đợt thật trong scene; exact resume cả khay dùng một phần và nhịp chuyển đợt; thắng ở lượt cuối; thua theo luật thật; xác nhận chơi mới; giữ Classic riêng; preview không lọt sang Home/Pause/Failure; Play/Stop tắt Domain Reload.
- `legacy-final-verified.log`: **558 kiểm tra hồi quy** đạt cho Classic, 20 bài Journey, Daily, tutorial, input và ba vòng Play/Stop.
- `home-regression.log`: **41 kiểm tra Home** đạt khi quay lại cấu hình Journey cũ.
- Script runtime biên dịch đạt với Editor, Android Development và Android Release. Các bước này không thay cho một lần đóng gói APK/chạy thử Android thật.

Log/ảnh nằm ở `.utmp/endless-pop-20261016/`; `endless-wave.png` là ảnh từ Unity chạy trong project QA ở 720×1600, không phải mockup. Profile QA riêng, không xóa save người chơi. Scene Home/Game chính và vị trí HUD gốc không bị ghi lại bởi installer; chỉ prefab được nâng cấp bổ sung. Chưa build/cài APK mới lên Vivo trong đợt này; cần playtest cân bằng/độ đa dạng và kiểm tra thiết bị trước khi phát hành.
