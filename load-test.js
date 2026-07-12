// JavaScript source code
import http from 'k6/http';
import { check, sleep } from 'k6';
import { SharedArray } from 'k6/data';

// 1. Đọc và lấy ra danh sách TOÀN BỘ đường dẫn từ file swagger.json
const allPaths = new SharedArray('swaggerPaths', function () {
    const data = JSON.parse(open('./swagger.json'));
    return Object.keys(data.paths);
});

export const options = {
    vus: 10,           // 10 người dùng ảo đồng thời
    duration: '1m',    // Quét liên tục trong 1 phút
};

// ĐỔI LẠI ĐÚNG DOMAIN CỦA BẠN:
const BASE_URL = 'https://localhost:7122';

export default function () {
    // 2. Vòng lặp quét qua tất cả các API
    for (let path of allPaths) {

        // Mẹo xử lý: Biến các đường dẫn dạng /api/Movies/{id} thành /api/Movies/1
        // (Thay thế tất cả những gì nằm trong ngoặc nhọn {} thành số 1)
        let executablePath = path.replace(/{[^}]+}/g, '1');

        const fullUrl = `${BASE_URL}${executablePath}`;

        // 3. Gửi request GET tới API
        const res = http.get(fullUrl);

        // 4. Kiểm tra xem API có bị "sập" (văng lỗi 500) không
        check(res, {
            'Server không bị lỗi 500': (r) => r.status !== 500,
        });

        // Nghỉ 10ms giữa mỗi API để máy tính của bạn không bị treo
        sleep(0.01);
    }

    // Nghỉ 1 giây sau khi đã quét xong 1 vòng toàn bộ API rồi mới quét lại vòng mới
    sleep(1);
}