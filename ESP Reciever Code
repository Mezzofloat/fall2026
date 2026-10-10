#include <Arduino.h>
#include <WiFi.h>
#include <esp_now.h>
#include <esp_wifi.h>
#include <esp_idf_version.h>

// ===================== SETTINGS =====================
const uint8_t ESPNOW_CHANNEL = 1;     // must match the wristband

bool PRINT_READABLE = false;          // false = CSV for Python (default), true = readable text
const int PRINT_EVERY_N = 1;          // readable mode only: print every Nth packet

// ===================== PACKETS =====================
typedef struct __attribute__((packed)) {
  uint8_t type;                       // 1=hello, 2=data, 3=bye
  float ax, ay, az, gx, gy, gz, batt;
  uint32_t t;
} DataPkt;

typedef struct __attribute__((packed)) {
  uint8_t cmd;                        // 1=calibrate, 2=disconnect, 3=motor scale, 4=connect
  float value;
} CmdPkt;

QueueHandle_t rxQueue;
uint8_t bandMac[6];
bool bandKnown = false;
volatile uint32_t badLenPkts = 0;
volatile uint32_t droppedPkts = 0;
uint32_t pktCount = 0;
uint32_t lastStatMs = 0;

// ===================== ESP-NOW =====================
void handleRecv(const uint8_t* mac, const uint8_t* d, int len) {
  if (len != sizeof(DataPkt)) { badLenPkts++; return; }
  if (!bandKnown) {
    memcpy(bandMac, mac, 6);
    esp_now_peer_info_t p = {};
    memcpy(p.peer_addr, mac, 6);
    p.channel = ESPNOW_CHANNEL;
    p.encrypt = false;
    if (esp_now_add_peer(&p) == ESP_OK) bandKnown = true;
  }
  DataPkt pkt;
  memcpy(&pkt, d, sizeof(pkt));
  if (xQueueSend(rxQueue, &pkt, 0) != pdTRUE) droppedPkts++;
}

#if ESP_IDF_VERSION >= ESP_IDF_VERSION_VAL(5, 0, 0)
void onRecv(const esp_now_recv_info_t* info, const uint8_t* d, int len) { handleRecv(info->src_addr, d, len); }
#else
void onRecv(const uint8_t* mac, const uint8_t* d, int len) { handleRecv(mac, d, len); }
#endif

void sendCmd(uint8_t cmd, float value) {
  if (!bandKnown) { Serial.println("ERR,band not seen yet"); return; }
  CmdPkt c = { cmd, value };
  esp_now_send(bandMac, (uint8_t*)&c, sizeof(c));
  if (PRINT_READABLE) Serial.printf(">> Command %u sent to band (value %.2f)\n", cmd, value);
  else                Serial.printf("ACK,%u,%.2f\n", cmd, value);
}

// ===================== PRINTING =====================
void printPacket(const DataPkt& p) {
  if (p.type == 1) {
    Serial.println(PRINT_READABLE ? "=== Band CONNECTED ===" : "EVT,CONNECTED");
    return;
  }
  if (p.type == 3) {
    Serial.println(PRINT_READABLE ? "=== Band DISCONNECTED ===" : "EVT,DISCONNECTED");
    return;
  }

  pktCount++;
  if (PRINT_READABLE) {
    if (pktCount % PRINT_EVERY_N != 0) return;
    Serial.printf("[%8.2fs] Accel(g): X=%7.3f Y=%7.3f Z=%7.3f | Gyro(dps): X=%8.2f Y=%8.2f Z=%8.2f | Batt: %.2fV | #%lu\n",
                  p.t / 1000.0f, p.ax, p.ay, p.az, p.gx, p.gy, p.gz, p.batt,
                  (unsigned long)pktCount);
  } else {
    // D,time_ms,ax,ay,az,gx,gy,gz,batt
    Serial.printf("D,%lu,%.4f,%.4f,%.4f,%.3f,%.3f,%.3f,%.2f\n",
                  (unsigned long)p.t, p.ax, p.ay, p.az, p.gx, p.gy, p.gz, p.batt);
  }
}

// ===================== SETUP =====================
void setup() {
  Serial.begin(115200);
  delay(500);
  WiFi.mode(WIFI_STA);
  WiFi.disconnect();
  esp_wifi_set_channel(ESPNOW_CHANNEL, WIFI_SECOND_CHAN_NONE);

  Serial.println("\n=== ESP-NOW Receiver ===");
  Serial.print("RECEIVER_MAC,");
  Serial.println(WiFi.macAddress());

  if (esp_now_init() != ESP_OK) {
    Serial.println("ERR,esp_now_init");
    while (true) delay(1000);
  }
  esp_now_register_recv_cb(onRecv);
  rxQueue = xQueueCreate(64, sizeof(DataPkt));

  Serial.println("READY");
}

// ===================== LOOP =====================
void loop() {
  // Band -> PC
  DataPkt p;
  while (xQueueReceive(rxQueue, &p, 0) == pdTRUE) {
    printPacket(p);
  }

  // Status line every 5 s (Python logs it; ignored by the data parser)
  if (millis() - lastStatMs > 5000) {
    lastStatMs = millis();
    if (PRINT_READABLE) {
      if (badLenPkts || droppedPkts)
        Serial.printf("!! bad-length packets: %lu, dropped: %lu\n",
                      (unsigned long)badLenPkts, (unsigned long)droppedPkts);
    } else {
      Serial.printf("STAT,%lu,%lu,%lu\n", (unsigned long)pktCount,
                    (unsigned long)badLenPkts, (unsigned long)droppedPkts);
    }
  }

  // PC -> band
  if (Serial.available()) {
    String line = Serial.readStringUntil('\n');
    line.trim();
    line.toUpperCase();
    if (line == "CAL")                  sendCmd(1, 0);
    else if (line == "DISC")            sendCmd(2, 0);
    else if (line == "CONN")            sendCmd(4, 0);
    else if (line.startsWith("SCALE,")) sendCmd(3, line.substring(6).toFloat());
    else if (line == "FMT,HUMAN")       { PRINT_READABLE = true;  Serial.println("Output: readable"); }
    else if (line == "FMT,CSV")         { PRINT_READABLE = false; Serial.println("Output: CSV"); }
  }
}
