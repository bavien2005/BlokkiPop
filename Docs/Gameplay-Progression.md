# Blokki Pop — gameplay và tiến trình v1

> Cập nhật 16/10/2026: đã thêm **Endless Pop** làm chế độ giải bóng liên tục ở nút chính Home. Journey 20 màn vẫn giữ làm bài luyện/Daily bank. Xem [luật, bộ sinh, save và UI của Endless Pop](Endless-Pop.md). Các phần Journey bên dưới mô tả bộ bài luyện cố định, không phải chế độ mới.

## Bản này có gì

Một scene `Game` phục vụ bốn chế độ. Không nhân bản 20 scene, không buộc người chơi xem quảng cáo để vượt Journey/Daily và không dùng điểm của các chế độ đó để ghi đè kỷ lục Classic.

| Chế độ | Luật và mục đích |
| --- | --- |
| Classic Endless | Luật đặt khối/xóa hàng hiện có; không giới hạn lượt. Có lưu/tiếp tục đúng bàn, điểm, màu, combo, khối còn lại và trạng thái sinh khối. Hồi sinh rewarded vẫn là lựa chọn tự nguyện, tối đa một lần/ván. |
| Pop Journey | 20 màn khởi đầu, mở lần lượt. Bàn ban đầu và bộ khối cố định; mục tiêu xóa hàng, bóng, combo hoặc xóa kép. Có giới hạn lượt, thắng/thua thật và 1–3 sao. Các màn đầu dạy luật, các màn cuối có chuỗi phụ thuộc dài hơn, tới lời giải bảy lượt. |
| Daily Pop | Một màn từ ngân hàng 15 đề mỗi ngày UTC, cùng bộ khối cho người dùng cùng phiên bản. Có lịch hoàn thành và danh hiệu mỗi bảy ngày thắng khác nhau. Chơi lại không cộng thêm một ngày thắng. |
| Learn to Play | Ba lượt kéo/thả thực tế: khối một ô → khối ba ô → hình vuông xóa hai hàng. Có chỉ vị trí, lời nhắc, bỏ qua và chơi lại. Người có kỷ lục cũ không bị ép học lại. |

Theme Sky có sẵn. Sunset mở ở 10 sao Journey; Night Glow ở 30 sao. Theme thay bảng màu khối, màu hiệu ứng, bóng mục tiêu và tint/ảnh nền gameplay. Màu các nút menu nay thuộc prefab/scene do người thiết kế chỉnh, không bị chọn theme ghi đè. Đây là phần thưởng trang trí, không phải tăng sức mạnh hay hàng hóa phải xem quảng cáo để mua.

**Golden Burst** là hiệu ứng pop mở riêng bằng thành tích: xóa ít nhất hai hàng/cột trong một lượt ở Classic/Journey/Daily. Vào **Themes & Rewards**, cuộn xuống **Pop Effects** và chọn hiệu ứng đã mở. Lựa chọn được lưu; hiệu ứng có hướng bắn tỏa, màu vàng, mật độ/tốc độ/thời gian hạt khác Classic Pop. Tutorial không cho thành tích lâu dài. Thông báo mở hiệu ứng không dừng ván Classic.

Daily là lịch **offline theo đồng hồ thiết bị**, không phải server chống gian lận hoặc bảng xếp hạng toàn cầu. 20 màn là bộ nội dung đầu tiên để chơi và mở rộng; không phải bằng chứng rằng độ khó/khả năng giữ chân đã được thử với người chơi thật.

## Chỉnh ở đâu trong Unity Editor

### UI thật trong Scene / Hierarchy (15/10/2026)

Không cần Play để thấy hoặc chỉnh UI mới. `Game.unity` dùng prefab `Assets/_Project/Prefabs/UI/PopGameplayUI.prefab`. Home đã được chuyển sang prefab riêng `PopJourneyFocusUI.prefab`, xem [hướng dẫn Home Journey Focus](Journey-Focus-Home.md); cây dưới đây mô tả bộ panel dùng chung trước khi tách Home:

```text
Blokki Pop UI  [Canvas, CanvasScaler, PopGameplayUI]
└─ Safe Area
   ├─ Home      → Title, Stats, Mode Buttons - Layout, Learn, Privacy
   ├─ Hud       → Title, Stats, Goals, Pause
   ├─ Tutorial  → Instruction, Skip
   ├─ Journey   → Stats, Level01…Level20, Previous, Page, Next, Back
   ├─ Level     → Description, Goals, Moves, Continue, Play, Back
   ├─ Daily     → Date, Goals, Status, Calendar/Day01…Day35, Stats, Play
   ├─ Rewards   → Scroll View/Items (Theme01…03, Effect01…02)
   ├─ Pause
   ├─ Victory   → Reward Star 1…3, Body, Rewards, Next, Retry, Home
   ├─ Failure
   ├─ ReplaceRun
   └─ InvalidSave
```

Các panel có một đối tượng `Card` bên trong để chỉnh nền và bố cục. Cách chỉnh:

1. Mở scene Home hoặc Game, **không bật Play**.
2. Chọn `Blokki Pop UI` ở Hierarchy. Trong component `Pop Gameplay UI`, chọn `Scene Preview`: ví dụ Journey, Daily, Rewards, Victory hoặc Tutorial.
3. Bấm **Show Selected Panel in Scene** nếu cần; **Frame Visible Panel in Scene** để đưa panel vào khung Scene. Đây là panel thật, không phải bản preview bị bỏ đi khi chạy.
   Với Journey, dùng **Journey Page Preview** ngay trên Inspector để chuyển giữa các nút Level01–10 và Level11–20 mà không bật Play.
4. Mở panel → `Card` → chọn chữ/nút/sao. Chỉnh **RectTransform** (anchor/pivot/vị trí/kích thước), **Image** (sprite/màu), **TextMeshPro** (font/cỡ chữ/màu/căn lề), **Button** (transition/màu disabled…).
5. Lưu scene bằng Ctrl+S. Muốn dùng cùng một thiết kế cho cả Home và Game, mở **prefab** và chỉnh ở Prefab Mode, hoặc Apply đúng override vào prefab. Chỉnh instance trong Game chỉ thay Game; Home chỉ thay Home. Không Apply All nếu có override không muốn dùng chung.

`Scene Preview` chỉ quyết định panel hiện khi sửa scene. Khi Play, Home luôn mở menu; Game mở HUD theo chế độ thật, không lấy popup Victory đang preview làm màn khởi đầu. Các panel khác vẫn có đầy đủ trong Hierarchy nhưng được tắt vì không thể hiện tất cả chồng lên nhau.

Code chỉ thay **nội dung dữ liệu, interactable, active state** và hoạt ảnh sao. Nó không tạo/xóa UI con, không ghi đè vị trí/cỡ/font/sprite/màu thiết kế. `Fit Device Safe Area` chỉ điều khiển **anchors của Safe Area** theo cutout, đồng bộ ở edit preview và Play; không xóa vị trí (`Anchored Position`), `Size Delta`, pivot hoặc bố cục con đã chỉnh. Tắt tùy chọn này nếu muốn tự điều khiển cả anchors (cần tự kiểm tra notch). Màu sao earned/empty, màu ô lịch completed/incomplete và nhịp reveal là dữ liệu trạng thái, chỉnh ở controller.

**Sửa lỗi HUD nhảy vị trí khi Play (15/10/2026):** code cũ có `safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero` chỉ khi chạy. Scene Game đã có chỉnh vị trí object cha Safe Area xuống `Y = -1493`; đoạn code đó xóa độ dịch của cha nên toàn bộ UI con nhảy lên, dù Inspector của `Hud` vẫn giữ nguyên số. Đã bỏ việc xóa offsets; cập nhật anchors ở cả preview và Play, có đánh dấu driven chỉ cho anchors. Không reset scene/prefab hoặc chuyển HUD về vị trí mặc định. Bộ kiểm thử cũ chỉ đối chiếu RectTransform của chữ con nên bỏ sót lỗi cha; kiểm thử mới đối chiếu cả hình chữ nhật thực tế trên màn hình của Hud và Pause qua Edit → Play → Stop, sau lưu/mở lại scene, cùng mô phỏng cutout/đổi chiều màn hình.

Để chuyển **riêng thanh thông tin + nút Pause**, chỉnh `Safe Area/Hud` hoặc `Hud/Card`. Chỉnh `Safe Area` sẽ dịch **tất cả** panel bên dưới, kể cả popup Pause/Journey/Victory; code nay giữ nguyên cả thay đổi đó, không âm thầm sửa lại.

Xác nhận riêng bản sửa này: **64 kiểm tra layout** đã qua (`PopUILayoutValidation.Run`, chỉ chạy trên dự án QA tách biệt); tái hiện được lỗi cũ bằng phép reset offsets rồi xác nhận HUD/Pause khớp tọa độ màn hình trước/sau Play trong ba trường hợp, kể cả parent đã kéo và HUD stretch. **558 kiểm tra gameplay** chạy lại đã qua; runtime C# Android Development/Release biên dịch thành công. Kết quả: `.utmp/scene-ui-20261015/layout-fix/layout-results.txt`, ảnh Play `layout-fix/hud-play-0.png`, log `layout-gameplay-regression.log`. Home/Game và prefab gốc không bị sửa trong bước này; chưa build/cài lại APK sau bản sửa layout.

`Mode Buttons - Layout` và `Play Buttons - Layout` dùng **VerticalLayoutGroup** đã lưu trong scene: chỉnh spacing/alignment ở component đó. LayoutGroup tự sắp các nút khi Continue ẩn/hiện; nếu muốn tự đặt từng nút bằng RectTransform thì bỏ/disable LayoutGroup rồi chỉnh thủ công. Đừng kéo vị trí một nút đang do LayoutGroup điều khiển rồi tưởng code reset nó.

`PopUIView → Elements` chứa các liên kết đã gán tới đối tượng thật. **Không đổi key/xóa binding** trừ khi đang chủ động thiết kế lại. Để tự giữ chữ viết trực tiếp trên TMP thay vì Text configuration, bỏ `Update Text` ở binding tương ứng. Giữ bật cho điểm/lượt/mục tiêu và ngày vì chúng cần cập nhật khi chơi.

Installer **Tools → Blokki Pop → UI → Install Authored UI (Preserve Existing)** chỉ tạo prefab/instance nếu chưa có; chạy lại không reset thiết kế đã sửa. Nó từ chối ghi đè scene đang có thay đổi chưa lưu. Scene Game có một canvas legacy rỗng, scale 0 từ trước; migration giữ nó ở `Legacy Runtime UI (preserved)` và tắt, không xóa. Scene gốc được sao lưu trong `.utmp/scene-ui-20261015/scene-backups`.

Kiểm tra Vivo phát hiện thêm một `Mode Goals HUD` tĩnh đã lưu ngoài prefab, gây chồng chữ khi vào Game. Đã giữ nguyên nhánh này tại chỗ, đổi tên `Legacy Mode Goals HUD (preserved)` và tắt; không xóa đối tượng hoặc thay bố cục authored HUD. Scene trước bước này còn trong `.utmp/scene-ui-20261015/scene-backups-before-legacy-hud`. Không bật lại hai nhánh legacy nếu không chủ động muốn khôi phục UI cũ.

Nếu thêm màn/theme/hiệu ứng mới vượt số object đã có, nhân bản object/row trong prefab và gán binding mới (`Level21`, `Theme04Title/Description/Select`…). Nếu đổi `Levels Per Page`, tự sắp vị trí các nút phù hợp trong prefab; runtime chỉ đổi nhóm được bật, không tự thiết kế lại layout. Build guard báo binding thiếu thay vì phát hành một menu rỗng.

Phần UI quảng cáo/privacy và GameOver Classic là hệ thống riêng; các thay đổi ở đây không tự thay consent hoặc bật quảng cáo thật.

### Intro Home và menu chọn chế độ (sửa 15/10/2026)

Bật `Blokki Pop UI` không được làm mất animation sáu khối màu. Lỗi cũ nằm ở `BlokkiPopIntroController.ShowModeMenuInsteadOfPrompt()`: khi menu xuất hiện, hàm này gọi `SetActive(false)` cho toàn bộ `animatedCells` và slogan. Coroutine vẫn chạy nhưng không còn khối màu nào được render. Đã bỏ hai thao tác ẩn đó; menu chỉ thay phần loading/`TAP TO START`, không can thiệp vào active state hoặc vị trí các khối/slogan. Khi đã dùng mode menu, loading hoàn tất cũng không bật lại nút Tap cũ phía sau menu.

Chọn `Blokki Pop UI` → component **Pop Gameplay UI** → **Home Intro → Wait For Home Intro Animation**:

- **Bật (mặc định):** logo và khối rơi xong mới hiện menu, tránh menu che mất animation nếu bố cục đang chồng lên mini-board.
- **Tắt:** menu hiện ngay; intro vẫn chạy. Cần tự sắp `Home/Card` và `Blokki Pop Intro/…/Brand and Mini Board` không đè lên nhau nếu muốn luôn thấy cả hai. Code không tự kéo/thu nhỏ thiết kế của mày.

Tốc độ rơi và khoảng cách giữa các lượt vẫn chỉnh tại `Blokki Pop Intro` → **Intro Animation → Cell Landing Duration / Cell Stagger Seconds**; thời gian logo ở **Logo Reveal Duration**. Bật/tắt `Blokki Pop UI` giữa intro không làm mất lịch hiện menu hoặc dừng coroutine của intro. Không đổi scene/prefab gốc để sửa lỗi này.

Xác nhận bản sửa intro: **109 kiểm tra riêng đã qua**, gồm UI bật từ đầu, UI tắt rồi bật sau intro, bật/tắt giữa animation với `Time.timeScale = 0`, tùy chọn hiện menu ngay/đợi animation, sáu khối đáp đúng vị trí và đủ opacity/scale, mở Journey rồi Back, và vào Game bằng scene đã preload. Chạy bốn chu kỳ Play/Stop với Domain Reload tắt; có ảnh render lúc khối đang rơi, không chỉ đối chiếu active state. **558 kiểm tra gameplay** chạy lại đã qua; runtime C# Editor/Android Development/Android Release biên dịch thành công. Bằng chứng: `.utmp/scene-ui-20261015/home-intro-fix/intro-results.txt`, `home-intro-visible-final.log`, `home-intro-gameplay-regression.log`; ảnh `home-intro-fix/home-cells-falling.png`. Chưa build/cài lại APK sau bản sửa này.

Mở **Tools → Blokki Pop → Gameplay → Select Configuration**.

Hoặc chọn file:

`Assets/_Project/Resources/Gameplay/PopGameplayConfiguration.asset`

Đây là asset thật, được nạp vào game qua Resources. Không cần sửa code để đổi những thông số được liệt kê dưới đây.

### Runs — chế độ và combo

- `Resume Classic / Journey / Daily`: bật/tắt lưu tiếp tục cho từng chế độ.
- `Disk Flush Interval Seconds`: chu kỳ ghi xuống đĩa; mặc định một giây. Rời màn, app vào nền hoặc thoát sẽ ghi ngay.
- `Classic Rewarded Revive`: cho phép hồi sinh tự nguyện của Classic. Không bật hồi sinh cho Journey/Daily/Tutorial.
- `Timed Combo`: mặc định **tắt**. Combo là các lượt liên tiếp có xóa hàng; lượt không xóa hàng làm đứt chuỗi. Người chơi có thể suy nghĩ mà không mất combo vì chờ quá 3,5 giây.
- `Timed Combo Window Seconds`: nếu bật timed combo, đây là thời gian tối đa giữa hai lượt xóa. Journey/Daily/Tutorial luôn dùng combo theo lượt để lời giải và mục tiêu nhất quán.
- `Fever Start Combo / Fever Visual Duration Seconds`: ngưỡng và thời lượng hiệu ứng Fever. Fever không tự tạo thêm mục tiêu/sao.
- `Prompt For Ad Age On Launch`: mặc định tắt để chơi trước. Nếu chưa có quyết định tuổi, game **không khởi tạo SDK quảng cáo**; người dùng có thể mở **AD PRIVACY** khi cần. Tắt câu hỏi lúc mở game không có nghĩa là mặc định mọi người 18+ hoặc bỏ consent.
- `Classic Fixed Seed`: để 0 khi chơi bình thường; đặt số khác 0 để tái tạo các bộ khối khi kiểm thử.

### Journey Levels — thiết kế màn

Mở phần tử của màn muốn sửa:

1. `ID`: danh tính lưu tiến trình. **Không đổi ID sau khi phát hành.** Đổi tên hiển thị bằng `Title`, không đổi ID.
2. `Title / Description`: tên và hướng dẫn hiện cho người chơi.
3. `Move Limit`: tổng lượt đặt khối thành công. Kéo sai/đặt không hợp lệ không tiêu lượt.
4. `Target Lines / Bubbles / Score / Combo / Multi Clear`: mục tiêu. Đặt 0 để không dùng mục tiêu đó. Mọi mục tiêu khác 0 phải đạt cùng một ván.
5. `Three Star Moves`: thắng trong số lượt này hoặc ít hơn → ba sao.
6. `Two Star Moves`: vượt mốc ba sao nhưng không vượt mốc này → hai sao. Bất kỳ chiến thắng hợp lệ nào còn lại → một sao.
7. `Starting Cells`: tọa độ x/y, Color ID và cờ Bubble của các ô ban đầu. Góc dưới trái là `(0,0)`, góc trên phải `(7,7)`.
8. `Piece Sequence`: chuỗi ID hình khối, phát ba khối/lần. Người chơi tự chọn thứ tự trong bộ ba; không có xoay khối và không có sinh ngẫu nhiên/adaptive trong màn được thiết kế.
9. `Verified Solution`: chuỗi hình khối + tọa độ gốc đặt. Đây là lời giải thật để kiểm tra nội dung, không chỉ là chú thích.

Ngay đầu Inspector có **Board preview / paint**:

- Chọn màn, Color ID; bấm ô để thêm/xóa khối ban đầu.
- Bật `Paint bubbles` rồi bấm ô đã có khối để bật/tắt bóng mục tiêu.
- Bấm **Show shape ID catalogue** để xem đúng hình ứng với từng ID.
- Có thể nhân bản màn làm điểm bắt đầu; sửa ID/tên, mục tiêu, bộ khối và lời giải sau đó.
- Bấm **Validate every level and tutorial**, hoặc **Tools → Blokki Pop → Gameplay → Validate All Levels**.

Bộ kiểm tra phát đúng bộ ba, kiểm tra từng vị trí đặt, xóa hàng/cột, đếm bóng ở giao điểm chỉ một lần và xác nhận lời giải thực sự đạt ba sao. Nó chặn build nếu màn không hợp lệ, ID bị trùng, đề Daily trỏ sai hoặc theme không có điều kiện mở khả thi.

Ví dụ: màn cần xóa hai hàng trong sáu lượt; đặt Three Star Moves = 2, Two Star Moves = 4. Người chơi thắng ở lượt 2 nhận ba sao, lượt 3–4 nhận hai sao, lượt 5–6 nhận một sao. Nếu đạt mục tiêu đúng lượt 6, game kiểm tra **thắng trước** và không phạt thua vì hết lượt.

### Authored Scoring và generator Classic

`Authored Scoring` là luật tính điểm của Journey/Daily/Tutorial: điểm mỗi ô đặt, điểm mỗi hàng, thưởng combo và hệ số tối đa. Bộ xác minh dùng cùng luật này.

Classic giữ các trường **Scoring** ở component `Board` trong scene Game. Generator Classic vẫn chỉnh ở component `Blocks`: các mốc điểm/độ khó, trọng số hình, số lần thử bộ ba, độ sâu mô phỏng, hỗ trợ khi nguy hiểm và rescue. Các tham số này không ảnh hưởng bộ khối cố định của Journey/Daily.

### Daily

- `Enabled`: hiện/ẩn chế độ.
- `Schedule Revision`: phiên bản lịch đề; tăng khi đổi ngân hàng/lịch sau phát hành.
- `Level IDs`: danh sách ID đề hợp lệ trong Journey.
- `Rotation Seed`: độ dịch cố định cho vòng đề; không dùng random mới mỗi lần mở.
- `Calendar Days To Show`: 7–35 ô lịch.
- `Celebration Every Wins`: số ngày thắng khác nhau để đạt thêm một danh hiệu; mặc định 7.

Mốc đổi ngày là **00:00 UTC**, tức **07:00 ở Việt Nam**. Một ván đang chơi giữ ngày lúc bắt đầu. Ván Daily hôm trước không được tiếp tục như đề hôm nay. Người dùng chỉnh đồng hồ có thể thay ngày offline; chưa có server xác nhận.

### Tutorial

Chỉnh bật học lần đầu, bỏ qua cho người có điểm cũ, cho phép Skip, hiện vòng chỉ ô, lời nhắc cho từng bước và dữ liệu `Lesson`.

Nếu thay bài học, cần thay lời nhắc và lời giải đồng bộ. Mỗi lượt được hướng dẫn yêu cầu đúng hình + vị trí; kéo sai trả khối về khay, không trừ lượt.

### Themes

Mỗi theme có ID, tên, mô tả, bảng màu, tint hoặc ảnh nền thay thế, màu nút, hiệu ứng và bóng.

`Required Journey Stars`, `Required Daily Wins`, `Required Classic Best` là các điều kiện mở. Mọi điều kiện khác 0 đều phải đạt; 0 nghĩa là không yêu cầu. Theme đầu danh sách phải luôn mở để người mới có giao diện hợp lệ.

### UI và Text

`UI` giữ **defaults để tạo prefab ban đầu**, không còn là lệnh ghi đè bố cục mỗi lần chơi. Sau khi prefab có sẵn, sửa font/cỡ/màu/vị trí/ảnh trực tiếp trên object thật như hướng dẫn phía trên. Defaults chỉ được dùng khi tạo một prefab chưa tồn tại; installer không reset prefab đã có. Vòng bóng/vòng chỉ ô, số màn mỗi trang và thời lượng thông báo reward vẫn là thông số gameplay được dùng trực tiếp. Nền bo góc mặc định nay là sprite UI built-in đã lưu trong prefab, không tạo texture mới lúc chạy.

`Pop Effects` chứa ID/tên/mô tả, số hàng/cột cần xóa cùng một lượt, best combo cần đạt, hướng tỏa hạt, hệ số mật độ/tốc độ/thời gian tồn tại, màu và mức pha màu. Hiệu ứng đầu danh sách phải luôn có sẵn. Thông số này chỉ thay hình thức, không thay điểm/mục tiêu/bộ khối.

`Block` có phần **Drag feel** trong Inspector để đổi khoảng nâng khối khi chạm và độ nhạy kéo. Khi app vào nền giữa lúc kéo, preview được hủy; khối chưa đặt không bị tính vào bàn lưu. Menu Settings cũ cũng pause và khôi phục đúng tốc độ chơi trước khi mở.

Ngày 14/10 đã chuyển phần kéo khối sang `BlockPointerInput`: một bộ quản lý gesture chung dùng Input System + Enhanced Touch, thay các callback `OnMouse`/`Input.mousePosition`. Theo dõi đúng touch ID, không cho ngón thứ hai giành khối, hủy khi mất focus/đưa app vào nền hoặc mở modal và bỏ qua UI tương tác. Chuột trong Editor vẫn kéo/thả được. Player Settings dùng **Input System Package (New)** thay cho Both. Enhanced Touch được bật/tắt cân bằng và reset singleton để tương thích Domain Reload off. Đây là cách polling touch được Unity hướng dẫn: [Touch support, Input System 1.19](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.19/manual/Touch.html).

`Text` chứa các nhãn, thông báo, mô tả luật và mẫu câu hiển thị. Mẫu có `{0}`, `{1}`… phải giữ đúng placeholder. Muốn dùng tiếng Việt cần font có đủ glyph tiếng Việt; nếu chỉ đổi nội dung mà font thiếu ký tự thì chữ có thể bị lỗi.

UI mới xử lý safe area; không thay đổi dữ liệu font/sprite/palette gốc chỉ để chọn theme. Game vẫn hướng tới màn hình dọc Android; không coi ảnh xem thử ngang trong Editor là cấu hình phát hành.

## Code hoạt động thế nào

- `PopGameplayConfiguration`: dữ liệu Inspector và lịch Daily.
- `PopLevelCatalog`: bộ 20 màn/bài học/theme khởi đầu; `PopLevelValidation` phát lại lời giải bằng mô phỏng thuần.
- `PopGameplaySession`: chủ sở hữu chế độ, lượt, mục tiêu, sao, mở màn/theme và chuyển scene.
- `PopSaveStore`: JSON có schema, kiểm tra cấu trúc, khóa riêng và bản backup.
- `PopGameplayUI`: controller scene-local, liên kết dữ liệu/hành động cho UI đã lưu; không còn bootstrap một canvas ẩn hoặc rebuild UI lúc chạy.
- `PopUIView`: các binding có tên tới RectTransform/TMP/Button/Graphic thật; cho phép tắt cập nhật text từng nhãn.
- `PopSceneUIAuthoring`: migration prefab/scene một lần, custom Inspector để preview panel và build guard kiểm tra binding.
- `PopStarGraphic`: hình sao UI dạng mesh, không phụ thuộc ảnh tải về.
- `Board.Progression`: import/export bàn, ô ban đầu, bóng mục tiêu và chỉ vị trí học.
- `Blocks.Progression`: bộ khối cố định, lưu khay, màu và RNG riêng của Classic.
- `GameFlowManager`: vẫn giữ điểm/HighScore và luồng hồi sinh; `GameWin` nay kết thúc màn thắng thực sự.

Một lượt hợp lệ đi theo thứ tự:

`Đặt khối → cộng điểm đặt → xóa hàng/cột & bóng → cộng điểm clear → ghi bộ đếm mục tiêu → tiêu thụ khối trong khay → kiểm tra thắng → kiểm tra hết lượt → phát bộ ba nếu cần → lưu trạng thái ổn định → kiểm tra không còn chỗ đặt.`

Không lưu nửa chừng trước khi khối đã được tiêu thụ. Không lưu các ô hover là ô đã đặt. Không tính hai lần bóng ở giao hàng/cột. Lượt sửa bàn do rewarded revive không tự cộng điểm, hàng, combo hay bóng.

Home đã có preload scene; chọn chế độ kích hoạt đúng preload đó, không xếp thêm một lần load sau một operation đang bị giữ ở `allowSceneActivation = false`.

## Lưu dữ liệu và nâng phiên bản

Giữ nguyên khóa cũ `HighScore`. Các khóa mới là:

- `blokkipop.progression.v1`: sao, ngày thắng, đã học, theme/hiệu ứng chọn, kỷ lục clear và combo dùng mở hiệu ứng. Các trường hiệu ứng được thêm tương thích với dữ liệu v1 cũ.
- `blokkipop.run.v1.Classic`, `.Journey`, `.Daily`: ván chưa kết thúc của từng chế độ.
- Mỗi khóa có `.backup` để hồi phục khi bản chính không đọc được.

Không dùng `PlayerPrefs.DeleteAll`. Thua/thắng chỉ xóa ván đang kết thúc; không xóa thành tích. Backup có thể là trạng thái trước lần ghi cuối, không phải bảo đảm cứu mọi hỏng hóc hoặc đồng bộ nhiều thiết bị.

Snapshot có revision và dấu vân tay luật/nội dung. Khi thay bố cục/bộ khối/luật đã phát hành, tăng `Content Revision`; bản lưu không tương thích sẽ bị từ chối và game mở bàn mới với thông báo. Sao/kỷ lục vẫn giữ theo ID. Đổi bộ hình/ID hoặc palette theo cách không tương thích cũng cần nâng revision.

Sinh khối Classic dùng RNG riêng, tránh việc random của hạt/âm thanh làm lệch bộ khối khi tiếp tục ván. Khi bật timed combo, lưu phần thời gian combo còn lại, không lưu thời điểm tuyệt đối của app.

## Kiểm thử và giới hạn

Các kiểm thử chạy trong checkout `.utmp/domain-reload-validation-20261009` với company name `BlokkiPopRegression`; runner từ chối chạy với preferences của game thật.

- Vòng gameplay cuối đã qua **517 kiểm tra**: thắng 20 màn bằng đặt khối trên scene thật, Daily chơi lại không nhân thành tích, tutorial kéo/đặt sai, khôi phục exact Classic và Journey đang dở, backup hỏng, thắng ở lượt cuối, thất bại vì hết lượt, khóa/mở theme/Golden Burst, áp ảnh nền theme thật, pause Settings và ba lần Play/Stop khi tắt Domain Reload. Lượt preview phát hiện sao kết quả thiếu `CanvasRenderer`; đã thêm component bắt buộc và kiểm tra cả renderer, kích thước sau reveal và mesh 11 đỉnh được gửi lên canvas. Log cuối: `.utmp/progression-validation-20261013/gameplay-preview-stars-final.log`.
- Luồng quảng cáo/hồi sinh cũ đã qua **3.031 kiểm tra hồi quy**, ba chu kỳ Play/Stop. Callback quảng cáo trong những test này là giả lập để kiểm chứng logic; không phải test mediation kiếm tiền thật.
- Sau đổi input backend, kiểm tra gameplay đạt **526 kiểm tra**, gồm gesture chuột nhận/đặt khối thật, Enhanced Touch nhận khối, ngón thứ hai không giành được gesture, canceled touch không làm thay đổi snapshot. Log: `.utmp/progression-validation-20261013/gameplay-new-input.log`. Kiểm tra quảng cáo/hồi sinh với backend mới vẫn đạt **3.031 kiểm tra**: `.utmp/gameplay-vivo-20261014/ads-new-input-regression.log`.
- C# đã biên dịch riêng cho Editor, Android Development và Android Release.
- Có một lỗi startup của `UnityEditor.Search` trong checkout kiểm thử, được nhận diện bằng stack và loại khỏi assertion runtime game; không bỏ qua lỗi của script gameplay.
- Sau tách TMP samples khỏi Resources nhưng giữ nguyên tài nguyên/GUID, lượt gameplay đạt **528 kiểm tra**: `.utmp/gameplay-vivo-20261014/gameplay-runtime-fonts-final.log`.
- Lượt APK bị hủy lúc 14:21 không phải kết quả cuối. **APK đầy đủ ngày 14/10 đã build thành công: 0 lỗi, 1 cảnh báo plugin iOS bị bỏ qua trên Android, 729 giây**, 160.151.749 byte. `.utmp/gameplay-vivo-20261014/build-result.json` xác nhận SHA256 `6B5FB66FAAFE237168FAD40520F2099232DD950B2595EE96EC2F1DD9E310D2E6`. APK đã được cài cập nhật lên Vivo V2111 lúc 20:31, giữ chữ ký/package/dữ liệu.
- Kiểm tra thật trên Vivo của bản 14/10: kéo tutorial sai không tiêu lượt, hoàn thành ba bước tutorial, thắng Journey 1 và 2 với popup sao, mở menu màn, đặt Classic/lưu vào nền/force-stop/mở lại/Continue khôi phục exact bàn-khay-score-counters; Google rewarded **test** mở, earned, đóng và tải lượt kế tiếp. 20 màn được thắng trong scene Unity bằng regression runner, chưa chơi tay hết 20 màn trên Vivo. Luồng native GameOver → xem ad → hồi sinh bàn chưa được chứng nhận end-to-end bằng thao tác thật trong lượt này; logic có test synthetic riêng.
- APK 14/10 có UI sinh lúc chạy. Nó **không phải chứng nhận cho thay đổi authored UI ngày 15/10**. Kết quả test/build của UI mới phải được ghi riêng sau khi chạy xong.
- **Authored UI 15/10: 550 kiểm tra PASS**, log `.utmp/scene-ui-20261015/gameplay-authored-ui-verified.log`: đủ 12 panel trong scene trước Play; preview popup thật; ghi/lưu override RectTransform, TMP font/cỡ/color, tắt text binding và giữ chữ designer; chuyển scene/ba vòng Domain Reload off vẫn giữ; đổi trang Journey dùng object có sẵn; mở các modal lặp lại không tăng/giảm số UI objects; installer chạy lại không reset override. Test phát hiện và đã sửa lifecycle `ExecuteAlways` khi Unity khôi phục scene edit lúc Stop.
- Hồi quy quảng cáo/hồi sinh sau đổi UI **3.031 kiểm tra PASS**, `.utmp/scene-ui-20261015/ads-authored-ui-regression.log`. Vẫn là callback giả lập để kiểm tra logic, không khẳng định Unity Ads bidding hoặc native revive end-to-end.
- Sau thêm chuyển trang Journey trong Inspector, chặn binding bị xóa/đứt liên kết và xử lý HUD tĩnh trùng, lượt cuối đạt **558 kiểm tra PASS**: `.utmp/scene-ui-20261015/gameplay-authored-ui-hud-final.log`. Mỗi vòng Play chỉ có một nhãn mô tả Classic hiện ra; preview Journey trang 2 dùng đúng các object Level11–20 có sẵn.
- **APK authored UI cuối ngày 15/10 build thành công: 0 lỗi, 1 cảnh báo plugin iOS bị bỏ qua trên Android**, 43,32 giây cho lượt incremental sửa scene, 160.154.700 byte. File `.utmp/scene-ui-20261015/BlokkiPop-AuthoredUI-Vivo.apk`; SHA256 `83B49F10255993380858F51B66CC110FF70D50F362CE77E3F6EB6C1D1B627758`. Chữ ký debug giữ nguyên. Một lượt trước đó bị Cancelled, rồi đã build thành công; không lấy log Cancelled cũ làm trạng thái cuối.
- Lượt cài authored UI dùng `adb install -r`. Đã đối chiếu ngay sau update và sau Continue: snapshot Classic **266 điểm / 12 lượt / 3 hàng** giữ exact bàn, khay, màu, RNG, combo và counters; HighScore 1562, 6 sao/2 màn Journey, tutorialComplete, theme Sky/effect Classic và quyết định tuổi đã lưu không đổi. Đây là dữ liệu thật trước update, không thay bằng profile QA.
- Hồi quy cuối với scene đã sửa HUD vẫn **3.031 PASS**: `.utmp/scene-ui-20261015/ads-final-hud-regression.log`. Native APK cuối đã qua Home, Classic Continue/HUD sạch, Pause/Home, chọn Journey/mục tiêu, kéo một ô thắng lại Journey 1 và ba sao, Daily/lịch UTC, cuộn Rewards tới Golden Burst, quay về Home và AD PRIVACY. Replay không nhân sao/màn hoàn thành và không ghi đè HighScore Classic.
- Native Google **TEST** ad đã mở dưới `com.google.android.gms.ads.AdActivity`, hiển thị nhãn thử nghiệm và cấp thưởng; log xác nhận opened → earned callback → closed(earned=True) → next ad ready. Đây là nút QA trong Privacy, không phải chứng nhận end-to-end Classic chết → xem ads → sửa bàn hồi sinh hoặc live Unity bidding. Chi tiết: `.utmp/scene-ui-20261015/ads-native-final.log`, `vivo-final-verification.json`.
- Ảnh Vivo cuối trong `.utmp/scene-ui-20261015`: `handoff-home-final.png`, `classic-final-clean-hud.png`, `level-goals-final.png`, `journey-one-final-ready.png`, `victory-final.png`, `daily-final.png`, `rewards-final.png`, `rewards-effects-final.png`, `privacy-final.png`, `google-test-ad-final-open.png`. Helper auto-build/migration tạm đã được gỡ, không còn request build đang chờ. Công cụ Inspector/migration thủ công vẫn giữ trong project.

Cảnh báo của build lõi đã được phân loại: phần lớn do ảnh icon bị nén (đã đổi import icon sang Uncompressed), plugin iOS của SDK bị bỏ qua trên Android, icon Legacy/Round bị deprecate và vài thông báo compiler TMP/clang. Cảnh báo Both trước đây được xử lý bằng cách **chuyển cả luồng kéo sang backend mới rồi mới đổi setting**, không chỉ giấu cảnh báo hoặc đổi setting khi code vẫn dùng legacy Input. Phần chứng nhận trên Vivo và APK cuối được ghi riêng sau khi thực sự hoàn thành.

Đã sửa import ảnh intro thành Sprite (vẫn dùng được qua Texture2D/RawImage ở Home), để các tham chiếu Sprite của nền Sunset/Night Glow thực sự có tài nguyên. Không sửa ảnh gốc hoặc reset các thay đổi scene của người dùng.

Ảnh preview chạy thật trong checkout kiểm thử được lưu dưới `.utmp`: `progression-home-preview.png`, `progression-journey-menu-preview.png`, `progression-level-goals-preview.png`, `progression-daily-preview.png`, `progression-themes-preview.png`, `progression-tutorial-preview.png` và `progression-win-preview.png`. Sao/điểm trên các ảnh là dữ liệu QA của profile riêng, không phải tiến trình người dùng. Ảnh native và log build 14/10 nằm trong `.utmp/gameplay-vivo-20261014`.

Những kiểm tra này không thay cho playtest cảm giác kéo/thả, độ khó, thời lượng phiên chơi và trải nghiệm trên nhiều điện thoại. Chưa có cloud save, leaderboard online, mua hàng, battle pass, mạng xã hội hoặc server chống chỉnh đồng hồ.

## Chưa bật kiếm tiền thật / chưa tự phát hành

Bản này giữ Google demo ads, gate consent/tuổi và cổng kiểm tra release. Không tự bật live ads chỉ vì gameplay đã xong. Xem `Docs/AdMob-Integration.md` cho SDK, tài khoản mediation, test device, privacy policy, app-ads.txt và các bước cấu hình ngoài project.

Icon và splash riêng hiện có của project được giữ nguyên. Không thay chữ ký, package ID hoặc xóa dữ liệu điện thoại trong lúc cập nhật test.

Trước phát hành vẫn cần: playtest người thật và cân độ khó, kiểm tra thêm tỷ lệ màn hình/thiết bị, privacy policy và khai báo store đúng thực tế, app/ads approval, cấu hình mediation-test rồi live phù hợp, release signing/AAB và thử bản release. Đây là bản gameplay v1 có tiến trình chơi, không phải tuyên bố đã đủ mọi điều kiện phát hành thương mại.
