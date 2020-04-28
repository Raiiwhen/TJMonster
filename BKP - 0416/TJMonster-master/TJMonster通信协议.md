# TJMonster通信协议

### 通用标志

0xff 错误标志

---

### SYNC帧

上位机：**0x08** 0x0a 0x0d

下位机：

| sync帧 | 上行内容 | 描述                  |
| ------ | -------- | --------------------- |
| 0      | 帧头#1   | **固定** 0xb1         |
| 1      | 帧头#2   | **固定** 0xb1         |
| 2      | RTC yy   | uint8_t               |
| 3      | RTC mm   | uint8_t               |
| 4      | RTC dd   | uint8_t               |
| 5      | RTC hh   | uint8_t               |
| 6      | RTC mm   | uint8_t               |
| 7      | IMU      | **IMU ID** 0x01~0x04  |
| 8      | NAND #1  | **NAND ID** 0x01~0x08 |
| 9      | NAND #2  | **NAND 剩余容量** 8位 |
| 10     | SD #1    | **SD TPYE** 0x01~0x04 |
| 11     | SD #2    | **SD 剩余容量** 8位   |
| 12     | W25 #1   | **W25 ID** 0x01~0x08  |
| 13     | W25 #2   | **W25 剩余容量** 8位  |
| 14     | 电池电压 | 5.5V按8位细分         |
| 15     | 帧尾     | 0xd1                  |

---

### DATA Stream帧

上位机：0x09 0x0a 0x0d

下位机：128bytes 的data（即uint8_t mst_datastream[128]），无起止。

---

