namespace WwmRedeemer;

/// <summary>Da ngon ngu Viet / Anh.</summary>
public static class L
{
    public static string Lang { get; set; } = "vi";
    public static event Action? Changed;

    public static void Set(string lang) { Lang = lang; Changed?.Invoke(); }

    private static readonly Dictionary<string, (string vi, string en)> T = new()
    {
        ["app_sub"]      = ("Tự động nhập mã đổi thưởng Where Winds Meet", "Auto-redeem Where Winds Meet exchange codes"),
        ["btn_refresh"]  = ("Làm mới mã", "Refresh codes"),
        ["btn_import"]   = ("Nhập từ file .txt", "Import .txt file"),
        ["btn_start"]    = ("Bắt đầu", "Start"),
        ["btn_stop"]     = ("Dừng (F12)", "Stop (F12)"),
        ["btn_calib"]    = ("Hiệu chỉnh nút", "Calibrate buttons"),
        ["btn_clear"]    = ("Xóa lịch sử đã nhập", "Clear redeemed history"),
        ["hdr_codes"]    = ("Danh sách mã", "Code list"),
        ["hdr_log"]      = ("Nhật ký", "Log"),
        ["hint"]         = ("Mở game tới Settings > Other (thấy dòng Exchange Code), rồi bấm Bắt đầu. Giữ F12 bất cứ lúc nào để dừng.",
                            "Open the game at Settings > Other (the Exchange Code row must be visible), then press Start. Hold F12 any time to stop."),
        ["count"]        = ("{0} mã · {1} chờ nhập · {2} đã xử lý", "{0} codes · {1} pending · {2} processed"),

        ["st_pending"]   = ("Chờ nhập", "Pending"),
        ["st_running"]   = ("Đang nhập…", "Entering…"),
        ["st_sent"]      = ("Thành công", "Success"),
        ["st_used"]      = ("Đã dùng rồi", "Already used"),
        ["st_expired"]   =("Hết hạn", "Expired"),
        ["st_invalid"]   = ("Không hợp lệ", "Invalid"),
        ["st_failed"]    = ("Không rõ", "Unknown"),
        ["st_done"]      = ("Đã nhập trước đó", "Already redeemed"),

        ["log_res_ok"]   = ("   → Thành công (hộp nhập đã tự đóng).", "   → Success (input box closed by itself)."),
        ["log_res_used"] = ("   → Mã đã dùng rồi, bỏ qua và đóng hộp nhập.", "   → Already used, skipped and closed the input box."),
        ["log_res_expired"] = ("   → Mã đã hết hạn, đóng hộp nhập.", "   → Code expired, closed the input box."),
        ["log_res_invalid"] = ("   → Mã không hợp lệ, đóng hộp nhập.", "   → Invalid code, closed the input box."),
        ["log_res_limit"] = ("   → Game báo giới hạn / thao tác quá nhanh. Dừng lại để tránh lỗi.", "   → Game reports a limit / too fast. Stopping to be safe."),
        ["log_res_unknown"] = ("   → Không xác định được kết quả, đóng hộp nhập.", "   → Result unclear, closed the input box."),
        ["log_closed_cancel"] = ("   Esc không đóng được hộp, đã bấm Cancel.", "   Esc did not close the box, clicked Cancel."),
        ["log_close_fail"] = ("Không đóng được hộp nhập mã. Dừng lại để tránh nhập sai.", "Could not close the code box. Stopping to avoid wrong input."),
        ["log_open_fail"] = ("Không mở được hộp Exchange Rewards. Dừng lại — thử bấm Hiệu chỉnh nút.", "Could not open the Exchange Rewards box. Stopping — try Calibrate buttons."),
        ["log_blind"]    = ("OCR không dùng được: chạy chế độ cơ bản (không phát hiện lỗi từng mã).", "OCR unavailable: running basic mode (per-code errors not detected)."),

        ["warn_title"]   = ("Lưu ý trước khi dùng", "Before you use this"),
        ["warn_text"]    = ("Ứng dụng này mô phỏng chuột và bàn phím để nhập mã trong game. Việc dùng macro/tự động hóa có thể vi phạm điều khoản của game và có rủi ro bị xử lý tài khoản. Bạn tự chịu trách nhiệm khi sử dụng.\n\nỨng dụng cần quyền Administrator (Windows sẽ hỏi mỗi lần mở) để gửi phím vào game. Ứng dụng không đọc hay sửa bộ nhớ/tệp của game, chỉ gọi api công khai codes.yar.gg để lấy danh sách mã.\n\nBạn đồng ý tiếp tục?",
                            "This app simulates mouse and keyboard input to enter codes in the game. Using macros/automation may violate the game's terms and could put your account at risk. You use it at your own responsibility.\n\nThe app needs Administrator rights (Windows asks each launch) to send input to the game. It does not read or modify game memory or files; it only calls the public codes.yar.gg API to get the code list.\n\nDo you agree to continue?"),

        ["warn_ok"]      = ("Tôi đồng ý, tiếp tục", "I agree, continue"),
        ["warn_exit"]    = ("Thoát", "Exit"),

        ["log_start"]   = ("Khởi động. Đang lấy danh sách mã…", "Started. Fetching code list…"),
        ["log_fetch_ok"] = ("Đã cập nhật {0} mã đang hoạt động từ codes.yar.gg.", "Updated {0} active codes from codes.yar.gg."),
        ["log_fetch_cache"] = ("Không kết nối được ({0}). Dùng danh sách đã lưu: {1} mã.", "Could not connect ({0}). Using saved list: {1} codes."),
        ["log_fetch_fail"] = ("Không lấy được mã và chưa có danh sách lưu: {0}", "Could not fetch codes and no saved list: {0}"),
        ["log_import"]   = ("Đã nhập {0} mã mới từ file ({1} mã trùng/không hợp lệ bị bỏ qua).", "Imported {0} new codes from file ({1} duplicate/invalid skipped)."),
        ["log_no_game"]  = ("Không thấy cửa sổ game (wwm.exe). Hãy mở game trước.", "Game window (wwm.exe) not found. Please open the game first."),
        ["log_no_codes"] = ("Không có mã nào cần nhập.", "No codes to enter."),
        ["log_foreground"] = ("Đưa game lên trước…", "Bringing game to front…"),
        ["log_detect"]   = ("Đang nhận diện các nút bằng OCR…", "Detecting buttons using OCR…"),
        ["log_detect_ok"] = ("Nhận diện được: {0}", "Detected: {0}"),
        ["log_detect_miss"] = ("Không nhận diện được: {0} → chuyển sang chọn tay bằng F9.", "Could not detect: {0} → switching to manual F9."),
        ["log_ocr_unavail"] = ("Windows OCR không dùng được (cần gói ngôn ngữ tiếng Anh). Chuyển sang chọn tay bằng F9.", "Windows OCR unavailable (English language pack needed). Switching to manual F9."),
        ["pt_button"]    = ("nút Exchange Code", "Exchange Code button"),
        ["pt_field"]     = ("ô nhập mã", "code input field"),
        ["pt_confirm"]   = ("nút Confirm", "Confirm button"),
        ["f9_prompt"]    = ("Đưa chuột lên {0} trong game rồi bấm F9.", "Move the mouse over the {0} in the game, then press F9."),
        ["f9_prompt_field"] = ("Mở hộp Exchange Rewards, đưa chuột lên {0} rồi bấm F9.", "Open the Exchange Rewards box, move the mouse over the {0}, then press F9."),
        ["f9_prompt_confirm"] = ("Dán/gõ tạm 1 ký tự để nút Confirm hiện, đưa chuột lên {0} rồi bấm F9.", "Paste/type one character so Confirm shows, move the mouse over the {0}, then press F9."),
        ["log_f9_saved"] = ("Đã ghi {0}.", "Saved {0}."),
        ["log_calib_done"] = ("Hiệu chỉnh xong và đã lưu.", "Calibration done and saved."),
        ["log_calib_cancel"] = ("Đã hủy hiệu chỉnh.", "Calibration cancelled."),
        ["log_running"]  = ("Bắt đầu nhập {0} mã. Giữ F12 để dừng.", "Entering {0} codes. Hold F12 to stop."),
        ["log_code"]     = ("[{0}/{1}] {2}", "[{0}/{1}] {2}"),
        ["log_game"]     = ("   Game: {0}", "   Game: {0}"),
        ["log_stopped"]  = ("Đã dừng bởi người dùng (F12).", "Stopped by user (F12)."),
        ["log_finished"] = ("Xong. Đã gửi {0} mã. Hãy kiểm tra thư trong game.", "Done. Sent {0} codes. Check your in-game mail."),
        ["log_game_lost"] = ("Mất cửa sổ game, dừng lại.", "Game window lost, stopping."),
        ["log_error"]    = ("Lỗi: {0}", "Error: {0}"),
        ["log_cleared"]  = ("Đã xóa lịch sử đã nhập.", "Redeemed history cleared."),
        ["log_saved_pos"] = ("Dùng vị trí nút đã lưu. Bấm \"Hiệu chỉnh nút\" nếu bị lệch.", "Using saved button positions. Press \"Calibrate buttons\" if they are off.")
    };

    public static string Get(string key) =>
        T.TryGetValue(key, out var v) ? (Lang == "vi" ? v.vi : v.en) : key;

    public static string F(string key, params object[] args) => string.Format(Get(key), args);
}
