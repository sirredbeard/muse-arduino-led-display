/*
 * VentunoQLedMatrix - MCU framebuffer sink for the VENTUNO Q 8x13 LED matrix.
 *
 * Deploy this sketch to the STM32H5 MCU with Arduino App Lab. It listens on
 * the SoC->MCU USB serial link for the line protocol documented in
 * protocol/PROTOCOL.md and renders frames with the official Arduino_LED_Matrix
 * library (https://docs.arduino.cc/tutorials/ventuno-q/led-matrix/).
 *
 * All rendering (font, text effects, animation) happens in the .NET service;
 * this sketch only unpacks frames and calls matrix.draw().
 */

#include <Arduino_LED_Matrix.h>

// ---------------------------------------------------------------------------
// RGB LEDs: user-addressable, active-low (LOW = on).
// Pin map from the ABX00181 datasheet, section 5 (UI & Indicators).
// ---------------------------------------------------------------------------
static const uint8_t RGB_PINS[4][3] = {
  { LED1_R, LED1_G, LED1_B },
  { LED2_R, LED2_G, LED2_B },
  { LED3_R, LED3_G, LED3_B },
  { LED4_R, LED4_G, LED4_B }
};

Arduino_LED_Matrix matrix;

// Framebuffer: 104 bytes, row-major 8 rows x 13 columns, brightness 0-7.
// Matches the uint8_t[104] layout from the Arduino LED matrix guide.
uint8_t fb[104];

constexpr size_t LINE_BUF_LEN = 260;
char line[LINE_BUF_LEN];
size_t lineLen = 0;

static uint8_t hexVal(char c) {
  if (c >= '0' && c <= '9') return (uint8_t)(c - '0');
  if (c >= 'a' && c <= 'f') return (uint8_t)(c - 'a' + 10);
  if (c >= 'A' && c <= 'F') return (uint8_t)(c - 'A' + 10);
  return 0xFF;
}

void clearFb() {
  memset(fb, 0, sizeof(fb));
  for (uint8_t i = 0; i < 4; i++) {
    digitalWrite(RGB_PINS[i][0], HIGH);
    digitalWrite(RGB_PINS[i][1], HIGH);
    digitalWrite(RGB_PINS[i][2], HIGH);
  }
  matrix.draw(fb);
}

// F1 <26 hex chars>: 13 packed bytes, bit (y*13+x) = pixel (x, y).
void handleF1(const char* hex) {
  uint8_t packed[13];
  for (uint8_t i = 0; i < 13; i++) {
    uint8_t hi = hexVal(hex[i * 2]);
    uint8_t lo = hexVal(hex[i * 2 + 1]);
    if (hi == 0xFF || lo == 0xFF) return; // malformed: ignore
    packed[i] = (uint8_t)((hi << 4) | lo);
  }
  for (uint8_t row = 0; row < 8; row++) {
    for (uint8_t col = 0; col < 13; col++) {
      uint16_t bit = (uint16_t)row * 13 + col;
      fb[row * 13 + col] = (packed[bit / 8] & (1u << (bit % 8))) ? 7 : 0;
    }
  }
  matrix.draw(fb);
}

// F3 <104 hex chars>: one hex digit per pixel, row-major brightness 0-7.
void handleF3(const char* hex) {
  for (uint16_t i = 0; i < 104; i++) {
    uint8_t v = hexVal(hex[i]);
    if (v == 0xFF || v > 7) return; // malformed: ignore
    fb[i] = v;
  }
  matrix.draw(fb);
}

// RGB <i> <r> <g> <b>: channels 0-255, >127 counts as on (active-low).
void handleRgb(const char* args) {
  int i, r, g, b;
  if (sscanf(args, "%d %d %d %d", &i, &r, &g, &b) != 4) return;
  if (i < 0 || i > 3) return;
  digitalWrite(RGB_PINS[i][0], r > 127 ? LOW : HIGH);
  digitalWrite(RGB_PINS[i][1], g > 127 ? LOW : HIGH);
  digitalWrite(RGB_PINS[i][2], b > 127 ? LOW : HIGH);
}

void handleLine() {
  line[lineLen] = '\0';
  if (lineLen >= 4 && strncmp(line, "PING", 4) == 0) {
    SerialUSB.println("PONG 1");
  } else if (lineLen >= 2 && strncmp(line, "F1", 2) == 0 && lineLen >= 29) {
    handleF1(line + 3);
  } else if (lineLen >= 2 && strncmp(line, "F3", 2) == 0 && lineLen >= 107) {
    handleF3(line + 3);
  } else if (lineLen >= 3 && strncmp(line, "RGB", 3) == 0) {
    handleRgb(line + 4);
  } else if (lineLen >= 5 && strncmp(line, "CLEAR", 5) == 0) {
    clearFb();
  }
  // Unknown or short lines are ignored.
  lineLen = 0;
}

void setup() {
  SerialUSB.begin(115200);

  for (uint8_t i = 0; i < 4; i++) {
    pinMode(RGB_PINS[i][0], OUTPUT);
    pinMode(RGB_PINS[i][1], OUTPUT);
    pinMode(RGB_PINS[i][2], OUTPUT);
  }

  matrix.begin();
  matrix.setGrayscaleBits(3); // 8 brightness levels per LED
  clearFb();
}

void loop() {
  while (SerialUSB.available() > 0) {
    char c = (char)SerialUSB.read();
    if (c == '\n') {
      handleLine();
    } else if (c != '\r' && lineLen < LINE_BUF_LEN - 1) {
      line[lineLen++] = c;
    } else if (c != '\r') {
      lineLen = 0; // overflow: drop the line
    }
  }
}
