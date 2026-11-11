/**
 * Presentation-only copy for AI availability. This file must not fetch,
 * probe, retry, or record progress.
 */
(function (global) {
    'use strict';

    const COPY = {
        localWriting: 'Đã đánh giá bằng bộ kiểm tra nội bộ.',
        internalChat: 'Trả lời từ dữ liệu trong hệ thống',
        selfPractice: 'Bạn vẫn có thể luyện và nghe lại. Bản thu này chưa được chấm tự động.',
        unassessed: 'Chưa chấm tự động',
        outage: 'Dịch vụ đánh giá đang gián đoạn.',
        unavailable: 'Tính năng AI tạm thời chưa sẵn sàng.',
        rate: 'Bạn đang gửi yêu cầu quá nhanh.',
        unknownChat: 'Chưa xác nhận được kết quả. Hãy tải lại lịch sử trước khi gửi lại.',
        access: 'Phiên đăng nhập đã hết hoặc bạn không có quyền.',
        notFound: 'Không tìm thấy nội dung hoặc bạn không có quyền xem.',
        mic: 'Mic bị từ chối hoặc không khả dụng — chưa lưu tiến độ.',
        nospeech: 'Chưa nhận được lời nói — chưa lưu tiến độ.',
        catalog: 'Chọn bài có sẵn'
    };

    function message(kind) {
        return Object.prototype.hasOwnProperty.call(COPY, kind) ? COPY[kind] : COPY.outage;
    }

    function classifySpeakingFailure(response, body) {
        const status = response && response.status ? response.status : 0;
        const redirected = !!(response && response.redirected);
        const code = body && (body.code || body.errorCode) ? String(body.code || body.errorCode) : '';
        if (redirected || status === 401 || status === 403) {
            if (!code || code === 'auth') {
                return { kind: 'access', message: message('access') };
            }
        }
        if (status === 404) {
            return { kind: 'access', message: (body && body.error) || message('notFound') };
        }
        if (code === 'no_speech' || (body && body.status === 'NoSpeech')) {
            return { kind: 'nospeech', message: (body && (body.message || body.error)) || message('nospeech') };
        }
        if (code === 'engine_auth' || code === 'invalid_configuration' || code === 'quota_exhausted') {
            return { kind: 'unavailable', message: message('unavailable') };
        }
        if (code === 'engine_busy') {
            const server = body && (body.error || body.message) ? String(body.error || body.message) : '';
            if (/hàng đợi/i.test(server)) {
                return { kind: 'busy', message: server };
            }
            return { kind: 'rate', message: message('rate') };
        }
        if (status === 429) {
            return { kind: 'rate', message: message('rate') };
        }
        if (code === 'circuit_open' || code === 'engine_unavailable' || code === 'engine_http_error' || code === 'engine_timeout' || status >= 500) {
            return { kind: 'outage', message: message('outage') };
        }
        return { kind: 'outage', message: (body && (body.error || body.message)) || message('outage') };
    }

    global.TctAiAvailability = {
        message: message,
        classifySpeakingFailure: classifySpeakingFailure
    };
})(window);
