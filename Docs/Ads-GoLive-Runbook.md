# Blokki Pop — quảng cáo thật: việc cần làm để phát hành

Chính sách privacy đang public tại:

`https://blokki-pop-vien-privacy.berrydog4.chatgpt.site`

Nút **PRIVACY POLICY** trong game dùng URL này. Game hiện vẫn để quảng cáo thật **tắt có chủ đích**: test/native mediation phải xong trước khi bật.

## Phần đã hoàn thành trong project

- Có một luồng rewarded ads duy nhất, chỉ do người chơi tự bấm xem để hồi sinh tối đa một lần trong lượt.
- Có hỏi nhóm tuổi, UMP consent/privacy options, timeout/retry và không trao revive nếu native SDK không trả earned callback.
- Đã có AdMob App ID và rewarded unit ID thật trong asset, nhưng Development đang dùng Google demo ads an toàn.
- Có release build guard: chặn build release nếu còn test ads, chưa có privacy URL, App ID sai hoặc live compliance chưa được xác nhận.
- Privacy policy đã công khai và được lưu trong `AdsConfiguration.asset`.

## Phần chủ tài khoản phải làm

Không gửi mật khẩu, mã OTP, file keystore hay dữ liệu thanh toán cho bất kỳ ai.

1. **Xác nhận hỗ trợ**: kiểm tra `nguyenbavien05@gmail.com` nhận/gửi mail được. Đây là email public trong store listing và privacy policy.
2. **AdMob**: trong app Android có package `com.vienstudio.blokkipop`, xác nhận App ID và rewarded ad unit đang Active. Giữ mapping Unity Ads mediation/bidding theo dashboard hiện có.
3. **Privacy & Messaging**: publish thông báo/consent cần thiết cho các quốc gia mày định phát hành; kiểm tra Unity Ads có trong partner list nếu dashboard yêu cầu.
4. **Điện thoại QA**: bật Developer options + USB debugging, cắm một Android thật. Trong AdMob/Ad Inspector, đăng ký chính điện thoại đó là test device; bật Unity Ads test mode theo dashboard. Không tự bấm quảng cáo live để thử.
5. **Play Console**: tạo app với đúng package ID, thêm URL privacy trên đây, điền Ads declaration, Data safety, target audience/content rating và contact email theo sự thật của bản cuối.
6. **Ký phát hành**: tự tạo/giữ keystore và mật khẩu, hoặc thiết lập Play App Signing. Keystore không được mất và không được commit vào Git.

## Những gì cần gửi lại cho người làm kỹ thuật

Chỉ gửi các thông tin không phải bí mật sau:

- Xác nhận đã hoàn thành sáu mục phía trên (nếu một mục chưa xong, nói mục nào).
- Mã **hashed test-device ID** của điện thoại QA hoặc để người làm kỹ thuật đọc nó từ log/Ad Inspector khi điện thoại đã kết nối.
- Danh sách quốc gia dự kiến phát hành nếu không phát hành toàn cầu.
- Xác nhận email hỗ trợ ở trên là email mày muốn công khai lâu dài.

Không gửi password, OTP, recovery code, private keystore hoặc thông tin thanh toán.

## Quy trình test bắt buộc trước live

1. Kỹ thuật viên điền test-device ID, bật `Use Configured Mediation Test Unit In Development`, rồi chuẩn bị App ID thật cho **Development Build**.
2. Cài bản Development lên đúng máy test. Trên Home chọn 18+ (để test), hoàn thành consent nếu UMP yêu cầu, rồi kiểm tra trạng thái `Mediation QA ready`/test label.
3. Mở Ad Inspector để xác nhận adapter và source trả ad. Xem rewarded test ad đến hết, đóng ad, kiểm tra callback earned và ad lượt sau tải lại.
4. Chơi đến Game Over: chọn revive, xem rewarded test ad, kiểm tra board revive đúng một lần. Đóng sớm/lỗi/network mất phải **không** revive và game vẫn kết thúc/chơi bình thường.
5. Lặp lại với Internet tắt, chọn dưới 18 và chọn `Prefer not to say`: game vẫn chơi được, không request ad.
6. Ghi lại kết quả + model máy/Android version. Chỉ pass toàn bộ mới chuyển sang live.

## Chuyển sang bản release

Sau khi test pass và chủ tài khoản xác nhận dashboard/privacy declarations đúng, kỹ thuật viên sẽ:

1. Tắt `Use Test Ads` và bật xác nhận compliance trong `AdsConfiguration`.
2. Bật App ID thật trong Google Mobile Ads settings, chạy `Validate Android Release Settings`.
3. Build một **signed Android App Bundle (.aab)** bằng keystore của chủ tài khoản.
4. Upload trước lên Play internal/closed testing; kiểm tra lại privacy link, startup, rewarded ads và revive bằng đúng build release.
5. Tăng `versionCode` cho mọi bản cập nhật sau đó và rollout dần trước khi public production.

Chỉ đổi hai cờ live mà bỏ qua test/privacy/dashboard không làm quảng cáo “xong”; build guard và checklist này tồn tại để chặn chính sai lầm đó.
