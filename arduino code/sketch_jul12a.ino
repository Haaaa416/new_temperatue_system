int relayA0 = A0;  // 繼電器接在 A0 (常閉) 
int relayA1 = A1;  // 繼電器接在 A1 (常開)
int input = 0;     // 用於存儲輸入值
unsigned long previousMillis = 0;  // 記錄時間
const unsigned long interval = 10000;  // 5 分鐘 = 300000 毫秒 / 1秒 = 1000毫秒  / 10秒 = 10000毫秒
bool yellowMode = false;  // 記錄是否進入黃光模式

void setup() {
  // 設置 A0 和 A1 為輸出模式
  pinMode(relayA0, OUTPUT);
  pinMode(relayA1, OUTPUT);

  // 初始化繼電器狀態
  digitalWrite(relayA0, LOW);  // A0 初始為開啟（常閉）         - 紅光關閉
  digitalWrite(relayA1, HIGH);  // A1 初始為開啟（常開）        - 黃光開啟

  // 設置序列通訊速率
  Serial.begin(9600);
}

void loop() {
  // 檢查是否有序列資料可讀取
  if (Serial.available() > 0) {
    // 讀取輸入的值
    input = Serial.parseInt();

    // 根據輸入值控制繼電器
    if (input == 1) {
      // 輸入 1 時，關閉 A1，打開 A0 (關閉黃光，打開紅光)
      digitalWrite(relayA1, LOW);   // 關閉 A1
      digitalWrite(relayA0, HIGH);   // 打開 A0
      Serial.println("Relay A1 OFF, Relay A0 ON (Red Light)");

      // 記錄當前時間
      previousMillis = millis();
      yellowMode = false;  // 重置黃光模式
    }else if (input == 2) {
      // 輸入 2 時，關閉所有燈
      digitalWrite(relayA1, LOW);    // 關閉 A1
      digitalWrite(relayA0, LOW);    // 關閉 A0
      Serial.println("All Lights OFF");

      // 重置黃光模式和計時
      //yellowMode = false;
      //previousMillis = 0;
    }
  }

  // 檢查是否已過了 5 分鐘
   if (!yellowMode && millis() - previousMillis >= interval) {
     // 5 分鐘後，打開 A1，關閉 A0 (黃光)
     digitalWrite(relayA1, HIGH);    // 打開 A1
     digitalWrite(relayA0, LOW);    // 關閉 A0
     Serial.println("Relay A1 ON, Relay A0 OFF (Yellow Light)");
    
  //   // 設置為黃光模式
     yellowMode = true;
   }
}