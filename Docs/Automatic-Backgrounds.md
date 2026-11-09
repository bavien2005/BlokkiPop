# Nền đổi màu tự động theo điểm

Không dùng tranh phong cảnh, không có shop hay thao tác chọn theme. Giữ nền xanh gốc khi bắt đầu; trong Classic Endless và Endless Pop, đạt mốc điểm thì đổi sang một bảng màu nhẹ kèm hiệu ứng chuyển động phía sau bàn.

## Luồng mặc định

- Dưới 1.000 điểm: nền xanh đã có trong scene.
- 1.000, 2.000, 3.000… điểm **của ván đang chơi**: chọn ngẫu nhiên một bảng màu, không lặp ngay bảng màu vừa dùng.
- Ba bảng màu: xanh băng + tuyết rơi; tím dịu + sao lấp lánh; xanh ngọc + đom đóm.
- POP refresh trong 0,85 giây: vòng màu mới bung từ giữa ra, có viền sáng; bàn/khay zoom-nảy hai nhịp rồi về đúng trạng thái; hai bên bắn sao lên. Không dùng chớp trắng toàn màn hình hay rung ngẫu nhiên liên tục. HUD/menu được giữ ổn định.
- Pause: đóng băng chuyển màu và particle. Resume: tiếp tục.
- Home / đóng app khi ván chưa kết thúc: lưu bảng màu, mốc đã đạt và trạng thái random riêng cùng bản lưu Classic / Endless Pop. Continue giữ nguyên, không bốc lại và không phát lại hiệu ứng ăn mừng.
- Thua kết thúc ván / thắng: trở về nền gốc bằng thời gian không phụ thuộc timeScale. Nếu đang chờ quyết định hồi sinh, giữ nền của ván đó; hồi sinh không tính là bắt đầu ván mới.
- Chơi mới: điểm 0, nền gốc. Daily / bài luyện / tutorial không đổi nền theo điểm.

## Chỉnh trong Unity, không cần Play

Mở `Game`, mở `Main Camera > Automatic Score Backgrounds` trong Hierarchy.

- `Scene Preview`: 0 = nền gốc, 1 = tuyết, 2 = sao, 3 = đom đóm.
- `Animate Preview`: xem chuyển động particle ngay trong Editor. Đây chỉ là preview, không đổi điểm hoặc dữ liệu của người chơi.
- `Refresh Preview`: tua trực tiếp chuyển cảnh từ 0 đến 1. Nhấn `▶ Preview POP refresh` để chạy thử trọn hiệu ứng, không cần Play và không phải tự chơi đến 1.000 điểm. Để 1 sau khi chỉnh nếu muốn xem nền ở trạng thái ổn định.
- Hai object `Background Layer A/B` và bốn Particle System là object thật của prefab. Không sinh hierarchy mới lúc chạy.
- Nhấn `Select score milestones / scenes / particle settings` để chọn asset `Assets/_Project/Resources/Gameplay/PopAmbientConfiguration.asset`.

Trong asset:

| Thuộc tính | Ý nghĩa |
| --- | --- |
| First Score | Mốc đổi nền đầu tiên, mặc định 1000 |
| Score Interval | Khoảng cách các mốc sau, mặc định 1000 |
| Classic Endless / Endless Pop | Bật riêng cho từng chế độ |
| Refresh Duration | Thời gian POP refresh, mặc định 0,85 giây; code giữ tên `crossfadeSeconds` để tương thích asset cũ |
| Screen Bounce / Screen Lift | Độ zoom-nảy và nhấc màn chơi; 0 tắt |
| Bounce Count | Số nhịp nảy, mặc định 2 |
| Refresh Ring Width / Glow | Độ rộng và độ sáng vòng chuyển màu |
| Return To Default Seconds | Thời gian về nền gốc khi kết thúc |
| Reduced Motion | Tắt particle và chuyển động màu; đổi màu tức thời |
| Particle Density | Mật độ chung, 0 tắt particle |
| Transition Burst Count | Lượng bụi sao ở mốc; 0 tắt |
| Transition Burst Speed / Lifetime / Size | Tốc độ, thời gian tồn tại và kích thước sao bắn từ hai bên |
| Scenes > Top/Bottom Color | Hai màu của nền gradient nhẹ |
| Scenes > Glow Color/Strength | Ánh sáng màu dịch chuyển chậm; 0 tắt |
| Scenes > Effect | Snow, Stars hoặc Fireflies |
| Scenes > Particle… | Màu, độ trong, số lượng, tốc độ sinh, kích thước, tuổi thọ |
| Scenes > Vertical Speed / Horizontal Drift / Turbulence | Tốc độ, độ trôi ngang và nhiễu chuyển động |

Giữ `id` ổn định khi sửa màu để Continue vẫn nhận đúng bảng màu cũ. Có thể thêm bảng màu với ID riêng; mặc định hệ thống không lặp ngay bảng vừa chọn. Không sửa trực tiếp kích thước fit của Layer A/B để thay layout game; chúng tự phủ vùng camera, không đụng RectTransform của HUD, bàn hay khay.

## Cấu trúc code

- `PopAmbientConfiguration`: cấu hình Inspector, mốc điểm và bộ chọn màu ngẫu nhiên không lặp.
- `PopGameplaySession.Ambience`: trạng thái thẩm mỹ riêng của từng ván; snapshot được lưu cùng bản lưu đang chơi, không thay revision luật chơi.
- `GameFlowManager.AddCoin`: báo điểm mới cho session.
- `PopAmbientBackground`: đọc màu đã chọn; mở nền mới bằng mặt nạ vòng tròn và chạy Particle System đã đặt sẵn.
- `PopAmbientBackground.Refresh`: rebound có suy giảm, sao bắn hai bên và preview trong Inspector. Chỉ thay projection tạm thời trong callback render rồi khôi phục; không thay Transform, orthographic size hoặc tọa độ tính raycast. Tự bỏ phần nảy khi có thao tác kéo khối và khi Pause; Resume tiếp tục thời gian chuyển cảnh còn lại. Projection tự động cũng được trả lại, nên đổi tỉ lệ màn hình sau đó vẫn fit đúng.
- `AmbientColour.shader`: vẽ gradient bằng code, không hiển thị ảnh landscape. Sprite nền cũ chỉ được dùng làm hình chữ nhật hình học.
- `AmbientParticles.shader`: hạt tuyết, sao bốn cánh, quầng đom đóm bằng shader, không tải ảnh lớn.
- `ModernBackground`: giữ nguyên renderer nền mặc định; chỉ tạm ẩn particle cũ khi đang ở một bảng màu mới.

Random của màu có seed riêng. Không tiêu thụ random của sinh khối; không thay dữ liệu bàn, khay, độ khó, số lượt hoặc vị trí UI. Phần nảy mới là trình bày tạm thời khi đạt mốc, không phải chỉnh lại layout scene.

Menu cài đặt additive: `Tools > Blokki Pop > Backgrounds > Install Automatic Score Backgrounds`. Không chạy khi scene chưa lưu; cài lại không dựng lại prefab đang được chỉnh. Dự án chính đã được gắn rig, không cần chạy lại để chơi.

## Sửa liên quan

Daily thắng chỉ có một nút `Home` và một nút `Try Again`; nút dùng lại từ `Next` của Journey bị ẩn riêng ở Daily. Ô chọn theme cũ chuyển sang `BÀI LUYỆN` (dùng nội dung luyện đã có); dữ liệu mở khóa/palette cũ không bị xóa nhưng không còn điều khiển nền tự động.

Các ảnh phong cảnh thử ban đầu không được dùng trong Assets hoặc bản build. Bản nháp được chuyển vào `.utmp/ambient-backgrounds-20261017/rejected-landscape-prototype`, vẫn khôi phục được.

## Preview thật trong Unity

Các hình dưới được chụp từ game đang chạy trong dự án QA riêng, không phải ảnh concept. Điểm được tăng bằng bài kiểm tra để kiểm chứng mốc; thứ tự snow → stars → fireflies là seed tái hiện của bài kiểm tra, không phải thứ tự cố định trong game.

![Xanh băng và tuyết](Previews/ambient-snow.png)

![Tím dịu và sao lấp lánh](Previews/ambient-stars.png)

![Xanh ngọc và đom đóm](Previews/ambient-sunset.png)

## Kết quả kiểm tra (17/10/2026)

- Nền tự động: 1.299 kiểm tra, gồm pixel trên ảnh render thật, mốc 999/1000/2000, chọn ngẫu nhiên không lặp, Pause giữa crossfade, Continue chính xác ở cả hai chế độ, reduced motion, chỉnh particle trong Inspector cập nhật preview, reset khi thua, Daily chỉ một Home, hai lần Play với Domain Reload tắt.
- Gameplay cũ: 558 kiểm tra (bao gồm 20 màn luyện/Journey có lời giải, Daily, tutorial, bản lưu); chế độ theme cũ chỉ được bật trong bộ kiểm tra tương thích, không trong cấu hình sản phẩm.
- Classic UI: 399 kiểm tra, 5 tỉ lệ màn hình và notch.
- Endless Pop UI: 563 kiểm tra, 5 tỉ lệ màn hình và notch.
- 946 block Transform/RectTransform/Camera đã có được đối chiếu nguyên trạng; không thay đổi vị trí bàn, khay, HUD hoặc camera của scene người dùng. Home scene giữ nguyên sau lần người dùng lưu.
- C# compile sạch cho Editor, Android Development và Android Release. Đây không phải xác nhận APK đã được build/cài hoặc GPU của Vivo đã chạy phiên bản này.

Log chi tiết nằm trong `.utmp/ambient-backgrounds-20261017/`; ảnh preview nằm trong `Docs/Previews/`. Bộ test chỉ ghi dữ liệu vào dự án QA riêng (`BlokkiPopRegression`), không reset bản lưu/kỷ lục của người chơi ở dự án chính.

## Cập nhật POP refresh (18/10/2026)

Thay chuyển màu nhẹ bằng radial colour wave, vòng sáng, rebound render-only và 48 sao bắn hai bên. Cấu hình mặc định: 0,85 giây; Screen Bounce 0,09; Screen Lift 0,018; hai nhịp rebound. Lớp màu có overscan để nền xanh cũ không hở ở mép khi zoom-out. Không sửa layout scene/prefab hiện có.

Kiểm tra POP: 1.309 kiểm tra qua, có kiểm tra projection thực sự thay đổi khi render, được trả lại đúng trước raycast, camera còn tự cập nhật projection sau resize, kéo khối bỏ bounce, Pause/Continue/reduced motion và preview ngoài Play. Compile C# qua Editor/Android Development/Android Release. Chưa build/cài APK mới lên Vivo.

Preview chuyển động được mã hóa từ 33 frame chụp thật trong Unity (thu nhỏ để xem trong chat), không phải ảnh hoặc animation AI. Full-resolution frame và log nằm ở `.utmp/ambient-pop-20261018/`.

![POP refresh chụp trong game](Previews/ambient-pop-refresh.gif)
