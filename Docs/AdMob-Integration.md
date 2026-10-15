# Blokki Pop — AdMob Mediation / Android

Cập nhật: 13/10/2026. Đây không phải chứng nhận pháp lý hay xác nhận đã có doanh thu.

## 1. Phạm vi và trạng thái

- Mediation: Google AdMob.
- Nguồn quảng cáo: AdMob Network và adapter Unity Ads. Unity Ads cần được thêm vào **bidding** trên dashboard mới tham gia phân phối quảng cáo.
- Format đầu tiên: **Rewarded** cho hồi sinh tối đa một lần mỗi lượt chơi.
- Không tự bật banner, interstitial hoặc app-open. Không cắt ngang lúc kéo khối; không tự mở video khi thua.
- SDK và luồng game đã có code. Ảnh dashboard của chủ game xác nhận đã tạo tài khoản, app, rewarded unit, nhóm mediation Android và mapping Unity Ads; không phải bắt đầu lại các bước này.
- Asset đã lưu ID AdMob thật để dùng sau, nhưng chế độ Development hiện dùng App ID và rewarded unit mẫu của Google. Simulation tắt, chế độ kiểm thử unit mediation thật tắt, release quảng cáo thật bị khóa.
- Privacy-policy URL và danh sách test-device trong asset còn trống. Ảnh dashboard chưa chứng minh test mode Unity Ads, xét duyệt app/account hoặc việc Unity Ads thực sự trả quảng cáo.

Các ID đã đối chiếu từ ảnh (không phải mật khẩu):

| Mục | Giá trị |
| --- | --- |
| AdMob Android App ID | `ca-app-pub-1158986668295280~1535263235` |
| AdMob Rewarded Unit ID — Revive_Android | `ca-app-pub-1158986668295280/3969854887` |
| Unity Android Game ID | `800394825` |
| Unity Rewarded Placement ID | `BP_Rewarded_Android` |

AdMob Network và Unity Ads đều hiện Active trong bidding; waterfall trống là bình thường với cấu hình này. Game ID/Placement ID ánh xạ trên dashboard, không phải gọi Unity Ads trực tiếp từ game.

Đừng hiểu “cài adapter” là “đấu giá đã chạy”. Code gửi một request tới AdMob; cấu hình bên máy chủ quyết định những mạng tham gia. Code game không so sánh eCPM giả hay tự chọn Unity Ads thắng.

## 2. Các phần đã cài

Package Manager tự tải từ scoped registry `https://package.openupm.com`, scope `com.google`:

| Thành phần | Package | Phiên bản |
| --- | --- | --- |
| Google Mobile Ads Unity | `com.google.ads.mobile` | 11.5.0 |
| Unity Ads mediation adapter | `com.google.ads.mobile.mediation.unity` | 3.21.1 |
| External Dependency Manager | `com.google.external-dependency-manager` | 1.2.187 |

Tên package adapter trên registry đã kiểm tra thực tế là `.unity`, dù một đoạn hướng dẫn hiện hành của Google viết `.unityads`. Không cài đồng thời SDK bằng UPM và một `.unitypackage` khác, vì dễ tạo assembly/dependency trùng.

Resolver khai báo native Android dependencies có GMA `25.4.0`, UMP `4.0.0`, Unity Ads `4.20.1`, adapter Android `4.20.1.0`. Manifest-merger report của APK kiểm thử đã xác nhận đúng bốn phiên bản này thực sự được merge. Gradle có thể chọn phiên bản khác sau khi nâng dependency; không suy ra phiên bản cuối chỉ từ XML. Giữ các Unity package đã pin khi kiểm thử; nâng SDK sau này phải kiểm tra lại adapter, manifest và build.

Android project hiện dùng Unity 6000.3.14f1, min API25, target API36, IL2CPP. Đã thêm Main/Properties/Settings Gradle templates từ đúng phiên bản Unity và bật chúng trong Player Settings. Resolver khai báo dependencies vào template để Gradle của Unity tải khi build, tránh bộ tải Gradle5.1.1 cũ không tương thích Java trên máy. Sau khi đổi SDK hoặc Unity, chạy Force Resolve và kiểm tra lại các template.

## 3. Luồng chạy và cách code được chia

### `Scripts/Ads/AdsService.cs`

Một service sống xuyên scene (`DontDestroyOnLoad`). Runtime tự tạo nó; không cần kéo thủ công vào Home/Game.

1. Chờ nhóm tuổi; không gọi `MobileAds.Initialize` hoặc request quảng cáo nếu Unknown/dưới18.
2. Với nhóm18+, chạy UMP `ConsentInformation.Update` mỗi lần khởi động, hiển thị form nếu cần, rồi kiểm tra `CanRequestAds`.
3. Đặt request configuration và lựa chọn privacy của mạng trước khi gọi `MobileAds.Initialize`, nhưng **để `PublisherFirstPartyIdEnabled = null` ở bước trước khởi tạo**.
4. Chờ callback khởi tạo mediation, áp dụng configuration lần hai với `PublisherFirstPartyIdEnabled = false`; chỉ cho phép `RewardedAd.Load` sau khi bước này thành công. API Android first-party ID yêu cầu SDK đã initialize; đặt `false` trước initialize gây exception trên thiết bị.
5. `IsRewardedReady` chỉ true khi có quảng cáo còn hạn, SDK/consent cho phép và không có thao tác đang mở.
6. `ShowRewarded` chỉ do nút người chơi bấm gọi. Callback earned đánh dấu thưởng; chỉ trả kết quả cho game sau close. Close mà không có earned thì không hồi sinh.
7. Ad một lần dùng xong được `Destroy`; service tải lượt tiếp theo. Retry có backoff, giới hạn số lần; cache hết hạn sau50phút; network/init/load có timeout.

Callback native được đưa về Unity main thread bằng synchronization context. Request giữ owner/scene; scene mới hoặc owner đã bị hủy không nhận thưởng của lượt cũ. Time scale/audio được khôi phục theo trạng thái service đã tạm giữ, không luôn ép mọi game về1. Có cửa sổ chờ tối đa2giây sau close cho earned đến muộn; nếu không có earned thì không tự đoán là đã xem đủ.

UMP form tương tác không bị timeout khi người chơi đang đọc vì SDK không có API an toàn để ép đóng form. Timeout phần mạng không phải hành động đồng ý thay người chơi. Watchdog quảng cáo chỉ xử lý callback close bị thiếu sau khi app thực sự quay về foreground; timeout không tự tạo thưởng.

### `Scripts/GameManager/GameFlowManager.cs`

Khi hết nước đi:

- Lưu high score trước, chặn thao tác, tạm dừng.
- Nếu chưa hồi sinh trong lượt, bàn có phương án cứu và quảng cáo đã sẵn sàng: mở `ReviveOfferPanel` **trước khi collapse**.
- Chọn xem: ghi nhận phiên lượt chơi/scene để lọc callback cũ.
- Kết quả earned: gọi `Blocks.TryReviveBoard`, đánh dấu `reviveUsed`, tiếp tục chính lượt hiện tại.
- Không có ad: đi thẳng Game Over như cũ. Không chờ mạng vô hạn. Bỏ qua quảng cáo cũng Game Over như cũ.
- Đóng sớm/lỗi: không hồi sinh; popup cho phép kết thúc hoặc thử lại nếu có ad mới.
- Restart/Home reset quyền hồi sinh cho lượt mới, nhưng không reset high score.

### `Scripts/Block/Board.cs`, `Blocks.cs`

Không xóa một vùng3×3 tùy tiện. Thuật toán thử mọi vị trí của mọi khối còn trên tray, đếm số ô đang chiếm trong footprint của khối đó, chọn phương án ít ô cần xóa nhất. Nếu hòa: ưu tiên slot trước, rồi hàng thấp/cột trái.

Chỉ xóa các ô cản trong footprint; ô ngoài vùng và lỗ của hình được giữ. Không thay khối đang cầm, không cộng điểm/lines/combo khi xóa cứu. Vì vậy luôn tạo được ít nhất một nước đi cho chính tray hiện tại.

Hồi sinh phải xảy ra trước animation bàn đổ; `Cell.Hide` không phải cơ chế dựng lại vị trí của cell đã rơi.

### `Scripts/UI/AdsPrivacyPanel.cs`, `ReviveOfferPanel.cs`

- UI được tạo khi chạy, có safe area, không phải thay serialized scene để nối ad SDK.
- Màn hỏi tuổi là ô số trống, không chọn sẵn18+; có Prefer not to say.
- Chỉ lưu nhóm tuổi + năm trong PlayerPrefs, không lưu tuổi chính xác/ngày sinh và không gửi giá trị nhập cho SDK.
- PRIVACY ở Home mở policy HTTPS/UMP privacy options khi cần. Xóa nhóm tuổi sau khi native SDK đã bắt đầu yêu cầu đóng hẳn và mở lại app; không giả vờ SDK có thể uninitialize.
- Nếu consent hoặc thiết lập trước khởi tạo lỗi, nhóm người chơi đủ điều kiện có thể bấm RETRY AD LOADING. Nếu native initialization đã bắt đầu nhưng không hoàn tất, yêu cầu đóng/mở lại app; không gọi initialize chồng lên phiên chưa rõ trạng thái.
- Bản Development hiển thị bước setup bị lỗi và phần đầu thông báo SDK thay vì chỉ ghi chung chung “Ad SDK unavailable”. Log có chi tiết đầy đủ để chẩn đoán.
- Trong chế độ Google demo, PRIVACY có TEST GOOGLE AD: mở quảng cáo native bằng đúng unit mẫu, báo kết quả earned/closed/failed, không hồi sinh bàn và không tạo doanh thu. Không có shortcut này trong production hoặc chế độ unit thật.
- Home có raw “tap anywhere” nên đã thêm guard riêng: bấm PRIVACY không bị hiểu thành bắt đầu game. Block input cũng bị chặn khi modal hoặc ad đang chờ.

Các singleton có `SubsystemRegistration` reset và cleanup để làm việc với Domain Reload tắt / Scene Reload bật.

## 4. Tuổi13+ và privacy — giới hạn cố ý

“Đối tượng mục tiêu13+” khác “mọi người13+ là người lớn”. Không tự biến target audience thành consent hay xếp hạng nội dung. Bản đầu dùng:

| Nhóm tự khai báo | Hành vi |
| --- | --- |
| Không cung cấp/không xác định | Chơi bình thường, không gọi init/request ads |
| Dưới13 | Chơi bình thường, không gọi init/request ads |
| 13–17 | Chơi bình thường, không gọi init/request ads |
| 18+ | UMP trước; chỉ request nếu consent/config cho phép |

Đây là lựa chọn giảm rủi ro, **không phải chính sách bắt mọi game13+ phải tắt ads13–17**. Google Play có yêu cầu riêng với ads dành cho trẻ em/người chưa xác định tuổi, bao gồm khả năng đóng sau5giây đối với nhiều format ngắt gameplay, kể cả rewarded. C# API rewarded không tự đảm bảo thời gian đóng creative của mọi mạng; cần kiểm tra provider/config/điện thoại trước khi mở monetization cho nhóm trẻ hơn. Tuổi18 cũng không thay thế đánh giá quy định từng thị trường.

Mặc định code chọn hướng không cá nhân hóa: `npa=1`, `rdp=1`, publisher personalization disabled, first-party ID disabled; Unity `privacy.consent=false` cho opt-out tương ứng. UMP/TCF thực tế vẫn cần cấu hình đúng và Unity Ads phải nằm trong danh sách partner. Không cá nhân hóa không có nghĩa khỏi cần consent, policy hay Data safety. Có thể giảm doanh thu; tối ưu chỉ sau khi privacy hoạt động đúng.

Giới hạn bằng chứng: chặn init/request ở đây là chặn các lời gọi từ game, không phải chứng nhận mọi thành phần native không chạy hoặc không gửi dữ liệu. Manifest vẫn có các provider tự đăng ký của SDK và quyền `AD_ID` do dependency merge vào. Cần kiểm tra hành vi native/privacy trên thiết bị và khai Data safety theo SDK thực tế; không khai “không thu thập dữ liệu” chỉ vì người chơi chưa xem video.

## 5. Mày cần làm gì ngoài code?

Không cần cài thêm SDK bằng tay. Cần mạng để Package Manager/Maven tải thư viện, Android Build Support + SDK/NDK/OpenJDK qua Unity Hub. Máy hiện đã có các module này; giấy phép Android SDK mới nếu được yêu cầu phải do mày xem/chấp nhận, không tự thay mặt mày.

### A. AdMob

1. Tạo/đăng nhập tài khoản của mày; tự làm xác minh, thanh toán, điều khoản. Không gửi mật khẩu/OTP cho dev.
2. Thêm app Android Blokki Pop, package production `com.vienstudio.blokkipop` (đối chiếu Play Console trước khi công bố).
3. Lấy **App ID** có dấu `~`, tạo ad unit **Rewarded** lấy **Ad Unit ID** có dấu `/`.
4. Tạo mediation group Rewarded/Android, gắn đúng unit, quốc gia target. Thêm Unity Ads trong bidding. AdMob Network và Unity Ads là hai nguồn, không phải hai mediator.

### B. Unity Monetization

1. Tạo project; chọn dùng mediation, partner Google AdMob.
2. Tạo Android rewarded bidding placement; lấy Android Game ID và Placement ID để mapping trên AdMob.
3. Hoàn tất quan hệ/điều khoản bidding theo dashboard, khai báo audience đúng thực tế, cấu hình mixed audience nếu applicable.
4. Bật test mode trên Unity Ads, đăng ký điện thoại test. Unity Ads waterfall đã ngừng tạo/sửa placement; dùng bidding.

Game ID/Placement ID thuộc mapping dashboard/adapter; game vẫn gọi một AdMob unit, không cần gọi Unity Ads trực tiếp để phát video.

### C. Privacy / store / website

1. Có privacy policy HTTPS công khai, mô tả thực tế SDK, quảng cáo, dữ liệu và lựa chọn privacy; không lấy một trang mẫu rồi coi là tự đạt luật.
2. Trong AdMob Privacy & messaging, cấu hình/publish messages cho thị trường cần hỗ trợ, thêm Unity Ads vào ad-partner lists (EU/US khi applicable).
3. Có developer website và `app-ads.txt` tại root website, với seller lines do chính dashboard AdMob/Unity cung cấp. Không đặt file này trong Unity Assets rồi nghĩ Google đã xác minh website.
4. Play Console: khai báo có quảng cáo, target audience đúng, trả lời questionnaire content rating và Data safety dựa trên SDK/cấu hình hiện tại. Đây là những việc khác nhau.
5. Theo dõi app verification/readiness, account approval; điền ID không đảm bảo fill hoặc tiền về ngay.

Thông tin có thể cung cấp để cấu hình tiếp: App ID, Rewarded Unit ID, Game ID/Placement ID, test-device hashed ID, privacy-policy URL. Đây không phải mật khẩu, nhưng vẫn nên gửi đúng nơi phù hợp; không đưa API key thanh toán/OTP vào asset.

## 6. Ba chế độ test/live — đừng nhầm

Mở **Blokki Pop > Ads > Select Configuration** để chọn `Assets/_Project/Resources/Ads/AdsConfiguration.asset`.

### Editor simulation

- Bật `Enable Development Reward Simulation` trong Inspector, Play, khai báo tuổi thực tế cho nhóm phù hợp.
- Để test QA, có thể dùng môi trường kiểm thử người trưởng thành; không hướng dẫn người dùng nhỏ tuổi khai gian.
- Popup có nút **TEST REVIVE — NO AD / NO REVENUE** riêng. Nút này kiểm tra game logic, không hiện SDK video, không impression, không doanh thu.
- Mặc định tắt; kết quả enum khác `Earned`; production không có đường trao thưởng simulation.

### Google demo trên điện thoại

- Để `Use Configured Mediation Test Unit In Development` tắt.
- Chọn **Prepare Google Demo Test App ID**, build Android với **Development Build**.
- Dùng ID Google mẫu. Có thể kiểm tra init/load/show/earned/close thực tế của SDK Google nhưng **không chứng minh Unity Ads bidding**.
- Simulation vẫn để tắt khi kiểm tra native ads.
- Cách kiểm tra nhanh: Home → PRIVACY → chờ dòng Google TEST ad ready → bấm TEST GOOGLE AD. Nút chỉ bật khi đã tải được ad. Xem hết rồi đóng: kết quả Earned; đóng trước điều kiện thưởng: không được tự suy ra Earned. Đây là nút kiểm tra SDK, không phải nút hồi sinh.
- Kiểm tra gameplay: chơi đến hết nước đi sau khi ad sẵn sàng → popup đề nghị hồi sinh → chủ động bấm xem → nhận earned callback và đóng ad → cứu bàn đúng một lần. Game không tự bật rewarded video chỉ vì thua; nếu ad chưa sẵn sàng, vẫn Game Over bình thường.

### Mediation QA trên điện thoại đã đăng ký test

- Điền App ID/Rewarded Unit ID thật, hashed test-device ID của **đúng điện thoại** vào `Test Device Ids`.
- Bật `Use Configured Mediation Test Unit In Development`; chọn **Prepare Configured Real App ID**; giữ Development Build.
- Bật test mode Unity Ads trên dashboard, dùng Ad Inspector single-source test để xác nhận adapter/network.
- Không chỉ điền một chuỗi test ID bất kỳ; code không thể chứng minh chuỗi đó thuộc điện thoại hiện tại. Phải kiểm tra nhãn test/log và dashboard. Tuyệt đối không bấm quảng cáo live của chính mình để thử.
- Home > PRIVACY có **RETRY AD LOADING** khi nhóm tuổi/cấu hình cho phép retry và **AD INSPECTOR (TEST)** trong chế độ mediation QA Development khi SDK khởi tạo xong. Nút Inspector không có trong production. Dùng hai nút này để kiểm tra status/network; chúng không tự trao thưởng.
- Development ghi cảnh báo adapter `NotReady` và tên adapter thực tế đã trả quảng cáo qua ResponseInfo. Không log credentials/mapping hay eCPM tự đặt. Có callback khởi tạo SDK không đồng nghĩa từng mạng đã hoạt động. [ResponseInfo chính thức](https://developers.google.com/admob/unity/response-info).

### Release thật

Sau khi thiết bị/dashboard/privacy/store đã kiểm tra:

1. `Use Test Ads` tắt.
2. ID thật và Privacy Policy URL hợp lệ.
3. `Live Ads Compliance Approved` bật **sau** kiểm tra, không dùng như cách né checklist.
4. Simulation tắt, chuẩn bị configured real App ID.
5. Tắt **Force test mode ON cho tất cả thiết bị** trên Unity Ads; giữ test chỉ cho thiết bị QA nếu provider hỗ trợ. Không vô tình phát hành mà mọi người vẫn nhận video test.
6. Build non-Development / AAB production, ký bằng keystore của chủ game.

Build guard sẽ chặn demo release, ID thiếu/nhầm App ID, config thiếu và API Android quá thấp. Nó chỉ kiểm tra cấu hình local; không đọc được approval, test-device thực tế, sự đồng ý từng partner hay tình trạng legal của mày.

## 7. Checklist thiết bị trước khi phát hành

- Cold launch: tuổi trống/nhóm lưu hợp lệ/nhóm hết năm; không init/request trước khi quyết định phù hợp.
- UMP: vùng yêu cầu consent, đồng ý/từ chối/chỉnh lại options; partner signals thực tế.
- Rewarded: xem đủ => hồi sinh1lần; close sớm => không; lỗi/no-fill/offline => không kẹt game.
- Pause/background/return; đổi scene hoặc restart trong callback; callback lặp/late => không thưởng nhầm lượt.
- Board, tray, score, high score, lines, combo được giữ; không cộng điểm vì xóa cứu.
- Scene reload/Play Stop nhiều lần với Domain Reload tắt; không singleton/listener trùng.
- Screen notch/ratio nhỏ, nút PRIVACY, keyboard tuổi, portrait/safearea, tắt âm thanh.
- Ad Inspector: Google source và Unity Ads single-source riêng, đúng adapter/version, đúng mapping, nhãn test.
- Không claim đấu giá hoạt động chỉ từ video Google mẫu hoặc Editor simulation.

## 8. Nguồn chính thức

- [GMA Unity setup](https://developers.google.com/admob/unity/quick-start)
- [Rewarded API](https://developers.google.com/admob/unity/rewarded)
- [UMP và privacy entry point](https://developers.google.com/admob/unity/privacy)
- [Unity Ads mediation, bidding, mapping, privacy, test mode](https://developers.google.com/admob/unity/mediation/unity)
- [Android MobileAds API — first-party ID phải gọi sau initialize](https://developers.google.com/admob/android/reference/com/google/android/gms/ads/MobileAds)
- [Google Play Families](https://support.google.com/googleplay/android-developer/answer/9893335?hl=en)
- [Target audience và age screen](https://support.google.com/googleplay/android-developer/answer/9867159?hl=en)
- [AdMob app-ads.txt / app verification](https://support.google.com/admob/answer/14538460?hl=en)

## 9. Bằng chứng kiểm thử local trước ngày 13/10 — bản sao riêng

Các thử nghiệm dùng bản sao `.utmp`, company/product riêng để không xóa high score của game chính. Editor callback mô phỏng chỉ là kiểm thử state machine; Android build chỉ là kiểm thử đóng gói, không phải chứng minh quảng cáo/network live.

Đã xác nhận:

- Runtime C# biên dịch qua Editor, Android Development và Android Release với SDK/adapter đã pin.
- `admob-runtime-scene-final.log`: **2.968 kiểm tra**, ba vòng Play/Stop, Domain Reload tắt / Scene Reload bật, exit0. Kiểm tra mọi shape trong catalog trên bốn loại occupancy, tối thiểu ô xóa so với brute-force độc lập, giữ tray/score/lines/combo, once-per-run, decline/failure/cancel/destroyed-owner/đổi scene trực tiếp, earned trước/sau close, pause/audio, tuổi12/13/17/18 và raw Home input guard. Không native ad hoặc impression.
- `sdk-resolve-templates.log`: Unity Android import/compile và resolver khai báo dependencies vào Gradle templates thành công, exit0. Đây chưa phải bằng chứng Maven đã tải hết hoặc APK build thành công.
- `highscore-scene-final.log`: **210 kiểm tra** bổ sung với code cuối, ba vòng Play/Stop, high score cuối870 trong clone, exit0.
- `build-guard-and-disk-final.log`: **13 kiểm tra** về default development, release gates, ID/SDK App ID mismatch, HTTPS policy, registered test-device entry, min/target API, exit0. Dùng ID giả đúng format chỉ để kiểm tra cấu hình local, không init SDK/request và không chứng minh account hợp lệ. Cùng tiến trình mới đọc được high score870 từ đĩa.
- `sdk-apk.log`: full Android build thành công, **0 lỗi, 7 cảnh báo**, APK ban đầu64.660.510byte. Native manifest có GMA25.4.0, UMP4.0.0, Unity Ads4.20.1 và adapter4.20.1.0; Google sample App ID, min API25, target API36.
- `sdk-apk-scripts-final.log`: cập nhật toàn bộ script cuối bằng `BuildScriptsOnly` sau full build, scene/asset và serialized configuration không đổi, **Succeeded, 0 lỗi, 2 cảnh báo**, exit0. IL2CPP output có xử lý đổi scene và adapter diagnostics mới. APK cuối **82.421.238byte (~82,4MB)**; `apksigner verify` exit0. Đây là Development APK, không phải tối ưu dung lượng/stripping của release. Không dùng scripts-only sau khi thay đổi scene/assets hoặc serialized layout; phải full build lại.
- Ảnh preview popup được kiểm tra; card có safearea và tự thu nhỏ cho màn hẹp.

Log và runner nằm trong `.utmp/domain-reload-validation-20261009` và `.utmp/admob-validation-20261010-01`, bị Git ignore có chủ ý. Không commit build/cache/log làm nặng repo.

### APK để thử trên điện thoại

File: `.utmp/admob-validation-20261010-01/BlokkiPop-AdMob-Test.apk`.

- ARM64, min API25, target API36, Development, ID Google demo. Simulation vẫn tắt.
- Package kiểm thử `com.vienstudio.blokkipop.admobvalidation`, company/product riêng; không ghi đè app production hoặc PlayerPrefs của nó. Tên app kiểm thử là `AdMobValidation20261010`.
- SHA256: `872798E33F60BA2BF8DB8E042082EC3D427519EAC45662D76F859CDCCAF32ED5`.
- Chưa cài/chạy trên điện thoại, chưa xem video native hoặc kiểm tra auction thật. Build + signature hợp lệ không thay thế kiểm thử thiết bị.
- Hai cảnh báo build cuối: `Active Input Handling = Both` đang có sẵn trong project, Unity cảnh báo Android không hỗ trợ chính thức; và iOS xcframework đi kèm SDK bị bỏ qua trên Android. Chưa tự đổi hệ thống input của game hoặc sửa package cache; phải kiểm tra touch/UI trên Android và chọn một input backend phù hợp trước release. Compiler cũng cảnh báo API icon/debug-symbol cũ ở tool Editor, không phải lỗi compile AdsService.

Full build lần hai (`sdk-apk-final.log`) đã chủ động dừng khi nó lặp xử lý shader TextMeshPro rất nặng. Kết quả cuối là build scripts-only ở trên; không coi log bị dừng là build pass. [Chế độ scripts-only của Unity](https://docs.unity.com/en-us/engine/6000.3/manual/building-and-publishing/build-scripts-only).

## 10. Sửa lỗi native Android ngày 13/10/2026

Log của bản game cài trên Vivo xác nhận nguyên nhân thực tế:

```text
MobileAds.initialize() must be called prior to enable/disable the publisher first-party ID.
```

SDK đã được đóng gói; lỗi xảy ra khi gọi first-party ID API trước initialize, khiến game chưa tới bước tải rewarded. Vì không có ad sẵn sàng nên khi thua game đi thẳng Game Over. Không thể sửa nguyên nhân này bằng việc chơi lại nhiều lần, đổi tuổi hoặc điền privacy-policy URL.

Sửa trong `Assets/_Project/Scripts/Ads/AdsService.cs`: factory configuration hai giai đoạn (nullable null trước initialize, false sau callback), chỉ mở cổng tải sau khi thiết lập thành công, retry an toàn và log callback native. `Assets/_Project/Scripts/UI/AdsPrivacyPanel.cs` bổ sung nút kiểm tra Google demo và thông tin lỗi dễ chẩn đoán. Không bật simulation hoặc quảng cáo kiếm tiền thật để che lỗi.

Sau một lần mở ad trả Failed, bổ sung `LastAdFailureReason` và thông báo cụ thể cho lỗi show/watchdog; màn PRIVACY giữ cả kết quả thử lẫn trạng thái tải hiện tại. Bản Android Development ghi tối đa 64 dòng chẩn đoán vào `Application.persistentDataPath/ads-development-diagnostics.log`, không ghi credentials, tuổi chính xác, consent strings hay publisher IDs. Phần ghi file không biên dịch vào Release. Callback native được đưa về Unity main thread nên log opened/earned có thể chỉ được xử lý sau khi native Activity đóng và Unity tiếp tục chạy; không kết luận ad chưa mở chỉ vì log này chưa xuất hiện khi Unity đang pause.

Bằng chứng mới:

- `.utmp/ads-native-fix-20261013/runtime-regression-final.log`: **3.022 kiểm tra**, ba vòng Play/Stop với Domain Reload tắt / Scene Reload bật. Có kiểm tra null/false đúng giai đoạn, giữ privacy/content rating, snapshot test-device list, shortcut native bị chặn trong Editor và toàn bộ regression gameplay/callback trước đó. Đây vẫn là kiểm tra callback mô phỏng, không phải bằng chứng video native.
- `.utmp/ads-native-fix-20261013/runtime-regression-verified.log`: **3.028 kiểm tra với code cuối**, ba vòng Play/Stop, hoàn tất thành công. Bổ sung background không timeout sai và lỗi unopened-show có diagnostic. Một lượt chạy trước phụ thuộc focus của batch Editor và thất bại khi Editor build chính kết thúc; fixture cuối đã đặt trạng thái foreground/background rõ ràng, không nới logic bảo vệ runtime để làm test pass.
- Biên dịch C# độc lập cho Editor, Android Development và Android Release: đều exit 0.
- Helper build kiểm tra **16 điều kiện configuration** trước khi đóng gói. Build cập nhật code dùng cache của full build chính đã hoàn tất ngày 13/10; không đổi scene, asset, SDK, package name hoặc layout field serialized. Chỉ dùng scripts-only trong điều kiện này; thay asset/scene/SDK phải full build lại.
- Build cuối: **Succeeded, 0 lỗi, 2 cảnh báo**, 386,90 giây. Hai cảnh báo là Active Input Handling Both có sẵn và iOS xcframework bị bỏ qua trên Android; không phải lỗi AdsService. APK 159.809.530 byte, ARM64/min API25/target API36, package `com.vienstudio.blokkipop`, Development/debug signature. Dung lượng này không phải bản Release đã tối ưu.
- `apksigner verify` qua; certificate SHA256 khớp APK game chính trước sửa. SHA256 APK cuối: `43F60311218B3D39729FABE4E7A497A319300655A7EC79074B8E90976D24263E`.
- Đã cài cập nhật bằng `adb install -r`, không uninstall/clear data. PlayerPrefs trước cập nhật và sau hai lượt ad vẫn **HighScore = 1562**, nhóm tuổi lưu 18+, năm 2026.

### Kết quả thật trên Vivo V2111

Đã xác nhận trên bản APK chính (không phải clone):

1. UMP cho phép request trong phiên kiểm tra; SDK initialize thành công; áp dụng first-party ID false sau initialize.
2. Rewarded được tải, adapter trả ad là `com.google.ads.mediation.admob.AdMobAdapter`, `CanShowAd=True`.
3. Hai lượt mở quảng cáo mẫu native qua TEST GOOGLE AD đều có callback opened, earned, closed, kết quả **Earned**. Sau mỗi lượt, ad cũ được bỏ và ad mới tải được.
4. Ảnh `native-ad-second.png` chụp màn Android thật có nhãn **Quảng cáo thử nghiệm**, **Đã cấp phần thưởng** và nút X. Chỉ đóng bằng X; không bấm Cài đặt/liên kết quảng cáo.
5. Điểm cao và nhóm tuổi không bị xóa khi cập nhật hay thử ad.

Bằng chứng nằm tại `.utmp/ads-native-fix-20261013/vivo-native-ads.log`, `native-ad-second.png`, `ad-view.png`, `build.result.json` và `runtime-regression-verified.log`. Helper Editor tự build đã được gỡ sau kiểm tra; không để lại cơ chế tự build trong project.

**Giới hạn:** đây là ad mẫu Google, không doanh thu, không chứng minh Unity Ads bidding/own-unit mediation. Nút TEST GOOGLE AD không hồi sinh bàn; board/once-per-run/cancel/close-early được regression bằng callback giả. Chưa dùng lượt Game Over trên Vivo để chứng minh trọn luồng native hồi sinh, chưa xác nhận close-early của creative native, consent theo mọi vùng hoặc tất cả nguồn mediation. Quảng cáo thật vẫn khóa; URL policy và danh sách test-device vẫn chưa điền.

### Mày làm gì ngay trên bản đã cài?

Không cần cài lại SDK hoặc đổi setting chỉ để thử Google demo:

1. Home → PRIVACY → chờ Google TEST ad ready → TEST GOOGLE AD → đóng bằng X khi hoàn tất. Earned ở đây là kết quả QA của SDK, không phải cứu bàn.
2. CLOSE để thoát PRIVACY → TAP TO START → chơi đến hết nước đi. Nếu ad đã sẵn sàng và chưa hồi sinh ván này, popup đề nghị hồi sinh xuất hiện trước Game Over.
3. Bấm nút xem quảng cáo trên popup hồi sinh. Chỉ callback earned và ad đã đóng mới được cứu bàn một lần; quảng cáo không tự bật chỉ vì thua. Chọn bỏ qua hoặc không có ad sẵn sàng vẫn đi đến Game Over.
4. Kiểm thử Unity Ads bidding là bước tiếp theo riêng: cần đăng ký đúng điện thoại test, xác nhận test mode của Unity Ads, chuyển sang configured mediation QA rồi kiểm tra từng nguồn qua Ad Inspector. Không bật quảng cáo live để tự thử và không coi demo Google là bằng chứng cả hai nguồn đã chạy.
