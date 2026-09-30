# Kingdom Come: Co-op (fork ของ Kingdom Come: Together 0.18.2)

ตัวติดตั้งเดียว ใช้ได้ทุกเครื่อง **ไม่ต้องลง KCDMP ตัวเดิมหรือม็อดอื่นก่อน** — ในนี้มีครบ: launcher, agent, relay, native plugin, ม็อดของเกม และเซฟข้ามบทนำ

เพิ่มจาก KCDMP 0.18.2 ตัวเดิม: **ผู้เล่น Xbox Game Pass เข้าเล่นได้** และ **Host World** (เครื่องเซิร์ฟเวอร์แยกที่ไม่มีคนเล่น เป็นโลกกลางกำหนดตำแหน่ง NPC ให้ผู้เล่นทุกคน)

## เล่น

1. เปิด **Kingdom Come Co-op** (ไอคอนบนเดสก์ท็อป) — launcher หาเกมในเครื่องเอง ทั้ง Xbox Game Pass และ Steam (Modding Tools)
2. กด **ADD SERVER** ใส่ที่อยู่เครื่องที่เปิดเซิร์ฟเวอร์ (เช่น `1.2.3.4` พอร์ต `7778`)
3. เลือกเซิร์ฟเวอร์ แล้วกด **JOIN SERVER**

**ทุกเครื่องในเซสชันต้องใช้ตัวติดตั้งจาก release เดียวกัน** ปิดเกมก่อนกด JOIN ทุกครั้ง

### ผู้เล่น Xbox Game Pass
- กด JOIN แล้วไม่ต้องทำอะไรต่อ: launcher วางม็อดลงเกม เปิดเกม **เกมโหลดเซฟล่าสุดเอง** แล้วต่อเซิร์ฟเวอร์
- หน้าต่าง console ที่เปิดขึ้นคือ agent — เปิดค้างไว้ตลอดที่เล่น ขึ้น `Game ready!` แล้ว `Connected!` คือใช้ได้
- ครั้งแรกมีหน้าต่าง UAC ให้กด **Yes** หนึ่งครั้ง — สร้างกฎ firewall บล็อกเครื่องอื่นไม่ให้เข้าพอร์ต 4600 ของเกม
- การโหลดเซฟเองทำงานด้วยบรรทัด `wh_sys_AutoLoadLastSave = 1` ใน `user.cfg` ข้างตัวเกม จึงมีผลกับการเปิดเกมทุกครั้ง
  ปิดได้ที่ Settings (F10) → "Load my last save automatically" (บรรทัดอื่นใน `user.cfg` ไม่ถูกแตะ) ถอนการติดตั้งก็ลบบรรทัดนี้ให้
- เกม Game Pass ไม่มีคำสั่งโหลดเซฟตามชื่อ จึงเลือกได้แค่ "เซฟล่าสุด"

### ผู้เล่น Steam
- ต้องมี KCD2 และ **KCD2 Modding Tools** (ฟรีใน Steam library) และเคยเปิด Modding Tools หนึ่งครั้งให้ตั้งค่าเสร็จ
- กด JOIN → launcher วางม็อดลง `<Modding Tools>\Mods\kdcmp\` และวาง**เซฟข้ามบทนำ**เป็น
  `Saved Games\kingdomcome2\saves\playline0\kcdmpskip.whs` (เลือกได้จากเมนู Load; เซฟของคุณเองไม่ถูกแตะ) แล้วเปิดเกม
- โหลดเซฟ พอเดินได้แล้วกด **CONNECT** ใน launcher — ขั้นตอนเดียวกับ launcher ต้นฉบับ

### เปิดเซิร์ฟเวอร์
- **เล่นไปด้วย**: HOST GAME → START GAME — เปิด relay บนเครื่องนี้ และบอกที่อยู่ที่ต้องส่งให้เพื่อน
- **เครื่องเซิร์ฟเวอร์ที่ไม่มีคนเล่น (โลกกลาง)**: HOST GAME → RUN AS WORLD HOST (เครื่อง Steam เท่านั้น)
  - เปิด relay, เปิดเกม, **โหลดเซฟข้ามเอง**, ย่อหน้าต่างเกม แล้วประกาศตัวเป็นโฮสต์ในชื่อ `[HOST] world` ไม่ต้องมีใครกดอะไร
  - หน้าต่าง console ที่เปิดขึ้นคือ console ของเซิร์ฟเวอร์ ปิด launcher ได้ แต่อย่าปิด console
  - KCD2 ไม่มีโปรแกรม dedicated server แบบ Arma: โฮสต์คือตัวเกมที่ย่อหน้าต่างไว้ **เครื่องต้องมี GPU**
  - รันตรง ๆ ก็ได้: `Start-WorldHost.bat` ในโฟลเดอร์นี้ ตัวเลือก: `-Save <ชื่อ> -Playline <0-4>`, `-NoAutoLoad`, `-ShowWindow`,
    `-MaxFps 30`, `-Window 640x360`, `-NoFollow`, `-NoInject`, `-NoRelay`
  - เปิดพอร์ต TCP 7778 ขาเข้า
  - **ส่วนนี้ยังไม่เคยรันกับเกมจริง** — อ่าน `HOST-WORLD-GUIDE.md` ก่อน มีรายการตรวจสอบและความเสี่ยงครบ

## สิ่งที่ใช้ได้ / ใช้ไม่ได้

| ทดสอบกับเกมจริงแล้ว (ฝั่ง Game Pass) | ทดสอบด้วยชุดเทสต์เท่านั้น / ยังไม่เคยรัน | ใช้ไม่ได้บน Game Pass |
|---|---|---|
| ตัวติดตั้ง + launcher: กด JOIN แล้วเกมเปิด โหลดเซฟเอง ต่อเซิร์ฟเวอร์ | ฝั่ง Steam ทั้งหมดของ launcher นี้ (วางม็อด, plugin ที่ build ใหม่) | ชุดเกราะ/อาวุธของเพื่อนไม่ sync |
| เห็นเพื่อนเดิน วิ่ง ฟัน (ตัวแทนไม่มี AI ไม่เดินเอง) | Host World: โฮสต์ / ผู้เล่น / ซ่อนตัวละครโฮสต์ | ความเสียหาย/การตายของ NPC ข้ามเครื่อง |
| ส่งตำแหน่ง เลือด stamina ของเรา | NPC ที่ sync มาเคลื่อนที่ต่อเนื่อง ถือไว้ 8 วินาที | ฟีเจอร์ที่ต้องใช้ native plugin |
| สภาพอากาศตามเซสชัน รวมถึงหลังโหลดเซฟ | เครื่องโฮสต์: โหลดเซฟเอง / ย่อหน้าต่าง / โฮสต์ย้ายตามผู้เล่น | โหลดเซฟตามชื่อ |

ข้อจำกัดของ Host World: ผู้เล่นต้องอยู่บริเวณเดียวกับโฮสต์ (ราว 60 ม.), กิจกรรมของ NPC (นั่ง ทำงาน) ไม่ถูกส่ง,
เควส บทสนทนา ร้านค้า หีบ ยังเป็นของใครของมัน

## ถ้ามีปัญหา

- **launcher บอกว่าหาเกมไม่เจอ** → หน้า Settings (F10) บอกว่าขาดอะไร:
  - *"Steam is here, but the KCD2 Modding tools are not installed"* = เครื่องมีแค่เกมปกติ ม็อดนี้รันบนเกมปกติของ Steam ไม่ได้
    กด **GET THE MODDING TOOLS ON STEAM** (ฟรี อยู่ใน Library → ตัวกรอง Tools) ลงเสร็จเปิดผ่าน Steam หนึ่งครั้ง แล้วกด **LOOK AGAIN**
  - *"Steam lists the KCD2 Modding tools, but their files are not on disk"* = ยังดาวน์โหลดไม่เสร็จ
  - หรือกด BROWSE ใส่ path เอง: Steam = `...\steamapps\common\KCD2Mod\Bin\Win64ReleaseSteamLTO_DLL\KingdomCome.exe`,
    Game Pass = `...\Kingdom Come- Deliverance II\Content\KingdomCome.exe`
- **เกมขึ้นหน้าต่าง `License not verified` / `No SteamApps`** (Steam) → ตอนเกมเปิด Steam ยังไม่ได้รันหรือยังไม่ได้ล็อกอิน
  เปิด Steam (ในบัญชี Windows เดียวกับที่รัน launcher) ล็อกอินด้วยบัญชีที่มีเกม รอจนขึ้น Library แล้วกดใหม่
  (World Host: สคริปต์พยายามเปิด Steam และลองใหม่ให้เอง — ส่วนนี้ยังไม่เคยรันจริง)
  ถ้า Steam เปิดและล็อกอินอยู่แล้ว สาเหตุที่พบบ่อยคือบัญชี Steam เดียวกันกำลังเล่นอยู่อีกเครื่อง — เครื่องเซิร์ฟเวอร์ควรใช้บัญชีของตัวเองที่มีเกม
- **`The game is already running without -devmode`** (Game Pass) → ปิดเกม แล้วกด JOIN ใหม่
- **`The game is running with a different version of the mod`** → ปิดเกม แล้วกด JOIN ใหม่ให้ม็อดถูกวางใหม่
- **ไม่ขึ้น `Game ready!`** → ต้องมีเซฟที่โหลดแล้วและยืนอยู่ในโลกเกม; ดู `Documents\kcd.log` (Game Pass) ว่ามี `=== MOD INIT ===`
- **Server is unreachable** → ตรวจที่อยู่/พอร์ต และว่าเครื่องเซิร์ฟเวอร์เปิด relay อยู่
- ข้อความ error ของ agent/สคริปต์อยู่ในหน้าต่าง console; ของ launcher อยู่ที่ `%APPDATA%\KCDMP_Launcher\app*.log`

## ความปลอดภัย

`-devmode` ทำให้เกม Game Pass เปิดพอร์ต 4600 ที่ไม่มีรหัสผ่าน กฎ firewall ข้างบนบล็อกเครื่องอื่นไว้
บนเครื่องเซิร์ฟเวอร์ เปิดออกนอกเครื่องเฉพาะ TCP 7778 — ห้ามเปิด `:1403` หรือ `:4600`

## ถอนการติดตั้ง

Settings ของ Windows → Apps → **Kingdom Come Co-op** → Uninstall
ตัวถอนลบบรรทัดโหลดเซฟเองออกจาก `user.cfg` ของเกม และถามก่อนว่าจะลบม็อดออกจากเกมด้วยไหม
ที่เหลือไว้: เซฟข้าม (`kcdmpskip.whs`) และกฎ firewall (บล็อกอย่างเดียว ลบได้ใน Windows Defender Firewall ชื่อ `KCDMP GamePass - block RemoteConsole 4600`)

Source และรายละเอียด: https://github.com/ILliTAH/KingdomCome-Together · License GPLv3 (`LICENSE.txt`)
