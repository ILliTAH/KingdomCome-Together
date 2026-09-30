# คู่มือ Host World — สำหรับผู้ดูแลเครื่องเซิร์ฟเวอร์ (และ Codex บนเครื่องนั้น)

Fork นี้ต่อยอดจาก Kingdom Come: Together **0.18.2** เพิ่มสองอย่าง:

1. **ผู้เล่น Xbox Game Pass เข้าเล่นได้** (เกม Game Pass ไม่มี Modding Tools / debug API `:1403` / native plugin)
2. **Host World** — ให้**เครื่องเซิร์ฟเวอร์แยก (ไม่มีคนเล่น)** เป็น "โลกกลาง" กำหนดตำแหน่ง NPC ให้ผู้เล่นทุกคน แทนที่แต่ละเครื่องจะคิดเองแล้วแย่งกัน
   เครื่องผู้เล่นทุกเครื่อง (Steam และ Game Pass) เป็น guest เสมอ

เอกสารนี้เขียนให้ agent (Codex) ที่รันบน**เครื่องเซิร์ฟเวอร์**อ่านแล้วทำงานต่อได้ทันที
อ่านหัวข้อ "สถานะ: อะไรทดสอบแล้ว / ยังไม่ได้ทดสอบ" ก่อนลงมือทุกครั้ง

---

## 1. ภาพรวม

```
 เครื่องเซิร์ฟเวอร์ (Steam + KCD2 + Modding Tools)
 ├─ KcdMpServer.exe        relay, TCP 7778 (ตัวเดิมของ 0.18.2 ไม่ได้แก้)
 ├─ KingdomCome.exe        เกม Modding Tools = "โลกกลาง" (world host)
 └─ KcdMpClient.exe        agent ของโฮสต์ ใช้ชื่อขึ้นต้น "[HOST]"
        ▲                         ▲
        │ TCP 7778                │ TCP 7778
 เครื่องเพื่อน (Steam)        เครื่อง Game Pass
 launcher + agent ของ fork นี้  launcher + agent ของ fork นี้
 (debug API :1403 + plugin)    (agent คุยกับเกมผ่าน RemoteConsole :4600)
 = guest                       = guest
```

| เครื่อง | บทบาท | ใน launcher กด |
|---|---|---|
| เซิร์ฟเวอร์ | **host** — รายงาน NPC รอบตัว (40 ตัว ในระยะ 60 ม.) | HOST GAME → RUN AS WORLD HOST (หรือรัน `Start-WorldHost.bat` ในโฟลเดอร์ที่ติดตั้ง) |
| ผู้เล่น Steam | **guest** — แสดงผลตามโฮสต์ ไม่รายงาน NPC | JOIN SERVER → โหลดเซฟ → CONNECT |
| ผู้เล่น Game Pass | **guest** | JOIN SERVER |

ทุกเครื่องใช้ตัวติดตั้งเดียวกัน: **`KingdomCome-Coop-Setup-0.18.2.exe`** — มีครบในตัว ไม่ต้องลง KCDMP ตัวเดิมหรือม็อดอื่นก่อน
ติดตั้งที่ `%LOCALAPPDATA%\KCDMP-HostWorld` (ไม่ใช้สิทธิ์ admin) launcher หาเกมเองและวางม็อดลงเกมทุกครั้งที่กดเล่น
**ทุกเครื่องต้องใช้ release เดียวกัน** (ไฟล์ม็อด `kdcmp.pak` ต้องตรงกัน)
ผู้เล่น Steam ที่ยังใช้ KCDMP 0.18.2 ตัวเดิมก็เข้าร่วมได้ แต่จะไม่ได้กติกา host/guest ถ้าไม่ได้ใช้ม็อดของ fork นี้
agent ทุกตัวยังรายงานเวอร์ชัน `0.18.2` และ wire protocol ไม่เปลี่ยน จึงใช้ relay เดิมได้

### กติกาว่าใครเป็น host (อยู่ใน `kdcmp.lua` → `KCD2MP_WorldRole()`)

1. สั่งตรง ๆ: `mp_world_role host|guest` — **host เกิดได้ทางนี้ทางเดียว** เครื่องผู้เล่นไม่เป็น host เองไม่ว่ากรณีใด
   บนเครื่องเซิร์ฟเวอร์ agent ที่รันด้วย `--world-host` เป็นคนสั่ง และสั่งซ้ำทุกไม่กี่วินาที (`KCD2MP_AssertWorldHost`)
   เพราะการโหลดเซฟฆ่า timer ของม็อด และการเปิดเกมใหม่ทำให้ม็อดลืมบทบาท โดยไม่มีใครนั่งอยู่ที่เครื่องให้สั่งใหม่
2. ถ้าในเซสชันมีผู้เล่นที่ชื่อขึ้นต้นด้วย `[HOST]` **และผู้เล่นคนนั้นยังส่งตำแหน่งอยู่** → เครื่องอื่นทุกเครื่องเป็น guest
   และ**ไม่สร้างตัวละครแทน**ให้ผู้เล่นคนนั้น (ไม่มีใครเห็นตัวละครของโฮสต์)
   โฮสต์ที่เงียบเกิน 30 วินาที (`KCD2MP.worldHostSilenceS`) ถือว่าไม่อยู่ ชื่ออย่างเดียวไม่นับ: ต้องได้ตำแหน่งจากโฮสต์ก่อน
   (agent ของโฮสต์ส่งตำแหน่งอย่างน้อยทุก 2 วินาทีตามนาฬิกา แม้ยืนนิ่ง; โฮสต์ที่ออกแบบปกติถูกตัดทันที)
   เพราะ agent 0.18.2 ตัวเดิมเก็บชื่อของคนที่ออกไปแล้วและส่งซ้ำทุกไม่กี่วินาที
3. ไม่เข้าสองข้อบน (เครื่องโฮสต์ไม่ได้เปิด) → `peer`: ติดตามและจอง NPC แบบ 0.18.2 เดิม (5 ตัว / 30 ม. ไม่มีโลกกลาง)
   ต่างจากเดิมข้อเดียว: เมื่อข้อมูลเงียบ ถือ NPC ไว้ 8 วินาทีแทน 3

guest **ไม่ส่ง**สถานะ NPC ใด ๆ เลย (ไม่มี `npc_state` / `npc_claim` / drag claim) จึงแย่ง NPC จากโฮสต์ไม่ได้

---

## 2. สถานะ: อะไรทดสอบแล้ว / ยังไม่ได้ทดสอบ

งานทั้งหมดทำบนเครื่อง **Game Pass** ซึ่งรันเกม Steam / Modding Tools ไม่ได้

### ทดสอบกับเกมจริงแล้ว (ฝั่ง Game Pass, 2026-09-30)
- เกม Game Pass รันด้วย `-devmode`, RemoteConsole `:4600`, โหลดม็อดจาก `Documents\kingdomcome_mods\kdcmp`
- agent `--transport remoteconsole` ต่อ relay จริง เล่นกับผู้เล่น Steam 0.18.2 ได้: 0 ครั้งที่ค้าง, console ไม่หลุด
- ตัวละครแทนเพื่อนเป็น `NPC_NAI` (ไม่มี AI) เดินต่อเนื่อง, ท่าฟันแสดงผล (ผู้เล่นยืนยันด้วยตา)
- สภาพอากาศถูกตั้งกลับหลังโหลดเซฟ
- ตัวติดตั้ง exe และ launcher บนเครื่อง Game Pass: ติดตั้งแบบเงียบ, launcher หาเกมเอง, กด JOIN SERVER (relay ในเครื่อง)
  → สคริปต์วางม็อด เปิดเกม เกมโหลดเซฟเอง agent ขึ้น `Connected!`; ถอนการติดตั้งลบไฟล์และบรรทัดใน `user.cfg` ออก คงม็อดไว้
- เกมโหลดเซฟล่าสุดเองตอนเปิด ด้วย `wh_sys_AutoLoadLastSave = 1` ใน `user.cfg` ข้าง `KingdomCome.exe`:
  เริ่มโหลด 2 บรรทัด log หลังเมนูขึ้น (5 รอบที่กด Continue เอง ห่าง 19–35 บรรทัด)
  ใส่ค่าเดียวกันทาง command line (`+wh_sys_AutoLoadLastSave 1`) **ไม่ได้ผล**: เกมค้างที่เมนู ~100 วินาที
- `KCD2MP_AssertWorldHost`, วงรอบ follow และกติกา `[HOST]` ถูกเรียกในเกม Game Pass จริงผ่าน console แล้วทำงานตรงกับชุดเทสต์
  (timer เดินต่อเอง, สั่งซ้ำไม่เกิดวงรอบที่สอง) — เป็นการเรียกฟังก์ชัน ไม่ใช่การเป็นโฮสต์ให้ผู้เล่นจริง

### ทดสอบด้วยชุดเทสต์เท่านั้น (ยังไม่เคยรันกับเกมจริง)
- กติกา host / guest, การซ่อนผู้เล่น `[HOST]`, โฮสต์ติดตาม 40 ตัว / 60 ม.
- NPC puppet เคลื่อนที่ต่อเนื่อง, ถือไว้ 8 วินาที, หยุดขยับ NPC ที่กำลังคุยกับผู้เล่น
- ตำแหน่งที่โฮสต์ควรไปยืน (`KCD2MP_WorldHostFollowTarget`)
- โฮสต์ที่เงียบ 30 วินาทีไม่นับ; ชื่ออย่างเดียว (ยังไม่มีตำแหน่ง) หรือชื่อที่ถูกส่งซ้ำหลังโฮสต์ออก ไม่ทำให้เป็น guest
  (กติกานี้ถูกเรียกในเกม Game Pass จริงผ่าน console แล้วได้ผลตรงกัน)
- launcher: หาเกมทั้งสองแบบ, เลือกแพลตฟอร์ม, วางม็อดเฉพาะสองไฟล์, คำสั่งที่ส่งให้สคริปต์ — `dotnet test KcdMp.Launcher.Tests`
- agent `--world-host` ตั้งบทบาทและ follow ซ้ำหลังโหลดเซฟ / เปิดเกมใหม่
- สคริปต์ใน `package\` เฉพาะส่วนที่ไม่ต้องมีเกม (ทุกคำสั่งที่เรียกมีอยู่จริง, วางม็อด, วางเซฟข้าม, `user.cfg`, โฟลเดอร์ทำงาน) — `tools\Test-PackageScripts.ps1`

### ยังไม่เคยรันเลย — **ต้องทดสอบบนเครื่องเซิร์ฟเวอร์**
- `Start-WorldHost.ps1` ทั้งไฟล์
- ม็อดของ fork นี้บน build Modding Tools (ทั้งฝั่งโฮสต์และฝั่งผู้เล่น Steam)
- การให้ตัวละครของโฮสต์ย้ายตามผู้เล่น (`mp_world_host_follow`)
- โหลดของเกมเมื่อโฮสต์รายงาน NPC 40 ตัว
- การโหลดเซฟด้วย `wh_sys_LoadGame` จากสคริปต์, การย่อหน้าต่างเกม, `-MaxFps`, `-Window`
- agent `--world-host` กับเกมจริงผ่าน `:1403`
- launcher ฝั่ง Steam: วางม็อดลง Modding Tools ตอนกดเล่น, ปุ่ม RUN AS WORLD HOST
- native plugin (`KCDMP.dll`) และ injector ที่ build ใหม่จาก source ของ 0.18.2 ด้วย Visual Studio 2019 (ต้นทาง build ด้วยเครื่องมือของตัวเอง)

---

## 3. ติดตั้งและรันบนเครื่องเซิร์ฟเวอร์

ต้องมีก่อน: Steam, KCD2, **KCD2 Modding Tools** (รายการแยกใน Steam: app id `2429020`, โฟลเดอร์ `steamapps\common\KCD2Mod` —
เกมปกติ `KingdomComeDeliverance2` ใช้ไม่ได้) และเคยเปิด Modding Tools ผ่าน Steam หนึ่งครั้งให้ Workspace Setup
คัดลอกข้อมูลจนเสร็จ (ไม่เช่นนั้นเกมเด้ง `114 tables are not loaded`)
**Steam ต้องเปิดและล็อกอินอยู่** ด้วยบัญชีที่มีเกม: เกมถูกเปิดตรง ๆ (ไม่ผ่าน Steam) และถ้าต่อ Steam ไม่ได้จะค้างที่หน้าต่าง
`License not verified — No SteamApps` โดย `:1403` ไม่ขึ้น (หน้าต่างนี้เจอจริงในการรันครั้งแรกบนเครื่องเซิร์ฟเวอร์; ต้นทางบันทึกไว้ว่า
กรณี Steam ไม่ได้เปิด `kcd.log` มี `SteamApi_Init failed` — ยังไม่ได้ยืนยันกับ log ของเครื่องเซิร์ฟเวอร์จริง)
สคริปต์เปิด Steam ให้ถ้ายังไม่ได้เปิดในบัญชี Windows นี้ และถ้า `kcd.log` มีบรรทัดนั้น จะปิดเกมที่มันเปิดเองแล้วลองใหม่อีก 2 ครั้งก่อนบอกสาเหตุ
ถ้า log ใช้คำอื่น สคริปต์จะรอ 5 นาที ปิดเกม แล้วแจ้งข้อความที่เอ่ยถึงหน้าต่างนี้ — ส่วนนี้ทั้งหมด**ยังไม่เคยรันจริง**

1. รัน `KingdomCome-Coop-Setup-0.18.2.exe` (ติดตั้งแบบไม่ถาม: เพิ่ม `/VERYSILENT`) — ลงที่ `%LOCALAPPDATA%\KCDMP-HostWorld`
2. ปิดเกม แล้วเลือกทางใดทางหนึ่ง:
   - เปิด launcher → **HOST GAME** → **RUN AS WORLD HOST**
   - หรือรัน `Start-WorldHost.bat` ในโฟลเดอร์ที่ติดตั้ง (ใส่ตัวเลือกได้ เช่น `Start-WorldHost.bat -NoFollow`)

   ทั้งสองทางรันสคริปต์เดียวกัน (`Start-WorldHost.ps1`) หน้าต่าง console ที่เปิดขึ้นคือ console ของเซิร์ฟเวอร์
   - launcher เปิดมาแล้วขึ้นหน้า Settings พร้อมช่อง path ว่าง = หา Modding Tools ไม่เจอ ข้อความใต้ช่อง Steam บอกสาเหตุ
     (ไม่มี Steam / ยังไม่ได้ลง Modding Tools / ดาวน์โหลดไม่เสร็จ) มีปุ่ม GET THE MODDING TOOLS ON STEAM และ LOOK AGAIN
     launcher หา Steam จาก registry ของผู้ใช้ก่อน แล้วจึงของเครื่อง (กรณีล็อกอิน Windows คนละบัญชีกับที่ใช้ Steam)
   - ถ้ามี relay รันอยู่แล้วบนพอร์ตเดียวกัน สคริปต์จะใช้ตัวนั้น (`-NoRelay` เพื่อไม่เปิดเองเลย)
   - หา `KingdomCome.exe` ของ Modding Tools ไม่เจอ → ใส่ `-GameExe "<path>"`
3. **ไม่ต้องกดอะไรในเกม** สคริปต์วางเซฟข้าม (`save\autosave018.whs` ที่โปรเจกต์ต้นทางแจกกับ 0.18.2: ผ่านบทนำแล้ว)
   เป็น `Saved Games\kingdomcome2\saves\playline0\kcdmpskip.whs` **ก่อนเปิดเกม** (เกมอ่านรายการเซฟครั้งเดียวตอนเริ่ม)
   จากนั้นเปิดเกม ย่อหน้าต่าง แล้วสั่ง `wh_sys_LoadGame 0 kcdmpskip` ผ่าน `:1403` และรอจน `:1403` รายงานเวลาในเกม
   - ใช้เซฟอื่น: `-Save <ชื่อไฟล์ ไม่มี .whs> -Playline <0-4>` (ไฟล์ต้องอยู่ก่อนเปิดเกม)
   - โหลดเองด้วยมือ: `-NoAutoLoad`
4. เมื่อโลกโหลดแล้ว สคริปต์ฉีด native plugin (`KCDMP.dll` ที่อยู่ข้างสคริปต์) เข้าเกม เพื่อให้ความเสียหายและการตายของ NPC
   ที่ผู้เล่นรายงานมีผลในโลกของโฮสต์ด้วย (`-NoInject` = ไม่ฉีด; ฉีดไม่สำเร็จเป็นแค่คำเตือน)
5. สคริปต์รัน agent ชื่อ `[HOST] world` ด้วย `--world-host` — agent เป็นคนสั่งให้เกมเป็น host และให้ตัวละครตามผู้เล่น
   (`-NoFollow` = ไม่ตาม) แล้วสั่งซ้ำเองทุกไม่กี่วินาที หน้าต่างสคริปต์คือ "console ของเซิร์ฟเวอร์": ปิดหน้าต่างนี้ = agent หยุด

เปิดพอร์ต: TCP 7778 ขาเข้า (relay) เท่านั้น ห้ามเปิด `:1403` หรือ `:4600` ออกนอกเครื่อง

### รันแบบไม่มีหน้าจอเกม — ทำได้แค่ไหน

KCD2 **ไม่มีโปรแกรม dedicated server** แบบ Arma: โลกกลางคือตัวเกมเต็ม ๆ ที่ไม่มีคนนั่งเล่น
สิ่งที่ทำได้คือให้มันรันเองโดยไม่มีใครแตะและไม่เห็นหน้าต่าง:

| ต้องการ | วิธี | หลักฐาน |
|---|---|---|
| ไม่ต้องกดโหลดเซฟ | สคริปต์สั่ง `wh_sys_LoadGame` | โปรเจกต์ต้นทางใช้คำสั่งนี้กับ build Modding Tools เป็นประจำ (WO-73 ของต้นทาง); สคริปต์นี้ยังไม่เคยรัน |
| ไม่เห็นหน้าต่างเกม | สคริปต์ย่อหน้าต่าง (`-ShowWindow` = ไม่ย่อ) | ต้นทางรันเกมแบบย่อหน้าต่างตลอดการทดสอบ โลกเดินต่อที่ราว 25 fps |
| เกมไม่หยุดเมื่อไม่ได้อยู่หน้าสุด | สคริปต์สั่ง `wh_ui_PauseGameOnFocusLoss 0` | ต้นทางอ่านค่านี้ได้ 0 อยู่แล้วบน Modding Tools |
| ลดภาระเครื่อง | `-MaxFps 30` (ค่าเริ่มต้น), `-Window 640x360` (ไม่ได้เปิดไว้) | ยังไม่เคยลอง |
| ไม่มี GPU | **ทำไม่ได้** — ตัว render แบบ software ทำให้โลกเดินช้าลง 15–55 เท่า (ต้นทางวัด) | ต้องมี GPU |
| มี GPU แต่ไม่มีจอ | `r_HeadlessStartup=1` ใน `system.cfg` ของเกม (ใส่ทาง command line ไม่ทัน) | ต้นทางพบ; fork นี้ยังไม่เคยลอง |

ทรัพยากรที่ต้นทางวัดได้เมื่อมี GPU: CPU ราว 0.17 core, RAM ราว 8–9 GB

ถ้า `wh_sys_LoadGame` ไม่ทำงาน: สร้าง `user.cfg` ข้าง `system.cfg` ของเกม ใส่บรรทัด `wh_sys_AutoLoadLastSave = 1`
เกมจะโหลด**เซฟล่าสุด**เองตอนเปิด (ยืนยันแล้วบน Game Pass; บน Modding Tools ยังไม่ได้ลอง) แล้วรันสคริปต์ด้วย `-NoAutoLoad`

### เครื่องผู้เล่น

ทุกคนรัน `KingdomCome-Coop-Setup-0.18.2.exe` แล้วเปิด launcher → **ADD SERVER** (ที่อยู่เครื่องเซิร์ฟเวอร์) → **JOIN SERVER**

- ผู้เล่น Steam: launcher วางม็อดลง `<Modding Tools>\Mods\kdcmp\` และวางเซฟข้ามไว้ใน playline0 (เลือกจากเมนู Load) ตอนกด JOIN
  จากนั้นโหลดเซฟ แล้วกด **CONNECT** เมื่อเดินได้ (ฉีด plugin + เปิด agent) เหมือน launcher ต้นฉบับ
- ผู้เล่น Game Pass: กด JOIN แล้วไม่ต้องทำอะไรต่อ เกมโหลดเซฟล่าสุดเอง
  (ปิดได้ใน Settings: "Load my last save automatically" — ค่านี้เป็นบรรทัดใน `user.cfg` ข้างตัวเกม จึงมีผลกับการเปิดเกมทุกครั้งจนกว่าจะปิด
  ถอนการติดตั้งจะลบบรรทัดนี้ออกให้)
  เซฟข้ามบน Game Pass ต้องนำเข้าด้วย `tools\Import-GamePassSave.py` จาก repo (ต้องมี Python; สำรองเซฟเดิมให้ก่อน) เพราะเซฟ Game Pass ไม่ได้เป็นไฟล์ในโฟลเดอร์
- ชื่อที่ผู้เล่นอื่นเห็น ตั้งได้ใน Settings → Player Name; ชื่อที่ขึ้นต้นด้วย `[HOST]` ถูกตัดคำนั้นออก (สงวนไว้ให้เครื่องโฮสต์)

---

## 4. รายการตรวจสอบ (ทำตามลำดับ — ข้อไหนไม่ผ่านให้หยุดแก้ก่อน)

`kcd.log` ของ Modding Tools อยู่ในโฟลเดอร์เกม; ของ Game Pass อยู่ที่ `Documents\kcd.log`

| # | ตรวจอะไร | ผ่านเมื่อ |
|---|---|---|
| V1 | สคริปต์หาเกมและติดตั้งม็อด | พิมพ์ `Modding Tools game:` และ `Mod installed to ...\Mods\kdcmp` |
| V2 | เปิดเกมจาก exe ตรง ๆ ได้ | เกมขึ้นเมนู; ถ้าขึ้น `License not verified` = Steam ไม่ได้เปิด/ไม่ได้ล็อกอิน/บัญชีไม่มีเกม (สคริปต์ลองใหม่เองแล้วบอกสาเหตุ); ถ้ายังไม่ได้ ให้ลองเปิดผ่าน `steam://rungameid/2429020` |
| V3 | ม็อดโหลด | `kcd.log` มี `[KCD2-MP] === MOD INIT ===` และ `Commands OK` |
| V2b | เซฟโหลดเอง | `kcd.log` มี `Loading saved game '%USER%/saves/playline0/kcdmpskip.whs'` แล้วสคริปต์พิมพ์ `World loaded.` — ถ้าไม่โหลด: ไฟล์ต้องอยู่ก่อนเปิดเกม, ลอง `-Playline` ที่มีเซฟอยู่แล้ว, หรือใช้ `user.cfg` (หัวข้อ 3) |
| V2c | เกมเดินต่อขณะย่อหน้าต่าง | เวลาในเกมจาก `:1403` (`/api/rpg/Calendar?depth=1` → `GameTime`) เพิ่มขึ้นเรื่อย ๆ ขณะหน้าต่างย่ออยู่ |
| V3b | plugin | console ของสคริปต์พิมพ์ `Native plugin injected.` และ agent พิมพ์ `[combat] connected to KCDMP.dll` (อาจช้าได้ถึง ~10 วินาที) |
| V4 | บทบาท | ภายในไม่กี่วินาทีหลัง agent ต่อ relay: `kcd.log` มี `WORLD-ROLE host -> acting as host` และ `WORLD-HOST follow ON`; โหลดเซฟซ้ำในเกมแล้ว `WORLD-HOST follow ON` ต้องขึ้นอีกครั้งเอง |
| V5 | guest รู้จักโฮสต์ | `kcd.log` ของผู้เล่นมี `WORLD-HOST peer N '[HOST] world' is named as the world host: hidden` แล้วตามด้วย `WORLD-HOST peer N is reporting; this game is acting as guest` ภายในไม่กี่วินาที |
| V6 | โฮสต์รายงาน NPC | `kcd.log` ของโฮสต์มี `NPC-SYNC tracking <ชื่อ>` หลายสิบบรรทัดเมื่อมีผู้เล่นต่ออยู่ |
| V7 | โฮสต์ตามผู้เล่น | ผู้เล่นเดินห่างเกิน 12 ม. → `WORLD-HOST follow: moved to x,y,z` |
| V8 | guest นิ่ง | ผู้เล่นสองคนยืนที่เดียวกัน เห็น NPC ตำแหน่งเดียวกัน; นับ `NPC-SYNC release` ต่อนาที (ก่อนแก้วัดได้ 8.7) |
| V9 | โฮสต์หายแล้วผู้เล่นรู้ | ปิด agent ของโฮสต์ → `kcd.log` ของผู้เล่นมี `WORLD-HOST peer N left`; ถ้า agent ค้างโดยไม่ตัดการเชื่อมต่อ ผู้เล่นกลับเป็น `peer` เองใน ~30 วินาที |

วัด V8 จาก `kcd.log` ของ guest: จำนวนบรรทัด `NPC-SYNC puppet start` และ `NPC-SYNC release` หารด้วยเวลาเล่น

---

## 5. ความเสี่ยงและงานค้าง (เรียงตามความสำคัญ)

1. **บัญชี Steam** — เซิร์ฟเวอร์กับเครื่องส่วนตัวของเพื่อนรัน KCD2 Modding Tools พร้อมกันด้วยบัญชีเดียวกันไม่ได้
   (Steam จะเตะเครื่องหนึ่ง) ต้องใช้คนละบัญชีที่มีเกม หรือให้เครื่องหนึ่งอยู่ offline mode — **ยังไม่ได้ลอง**
2. **ตัวละครของโฮสต์ต้องไม่ตาย / ไม่ก่อเรื่อง** — ไม่มีใครเล่นมัน แต่ NPC มองเห็นและตีมันได้ ถ้าตาย เกมโหลดเซฟแล้วโลกย้อนกลับ
   ยังไม่มีโค้ดป้องกัน ต้องหาวิธีบน build จริง (เช่น ทำให้อมตะ, ให้ AI มองไม่เห็น)
3. **การย้ายตัวละครโฮสต์** ใช้ `player:SetWorldPos` ทุก 1 วินาทีเมื่อห่างเกิน 12 ม. — ยังไม่รู้ผลข้างเคียง
   (ตกพื้น, NPC ตกใจ, cutscene/เควสถูกกระตุ้น) ปรับได้ที่ `KCD2MP.worldHostFollow`
4. **เกมต้องไม่หยุดเมื่อหน้าต่างไม่โฟกัส / ขึ้นเมนู** — เมนูหยุด `Script.SetTimer` ทั้งหมด ม็อดจะเงียบ
   สคริปต์สั่ง `wh_ui_PauseGameOnFocusLoss 0` และย่อหน้าต่างให้ แต่ยังไม่เคยยืนยันบนเครื่องจริง (V2c)
5. **รายการเซฟถูกอ่านครั้งเดียวตอนเปิดเกม** — ไฟล์เซฟที่วางหลังเปิดเกมโหลดด้วย `wh_sys_LoadGame` ไม่ได้ (ต้นทางเจอสองครั้ง)
   สคริปต์จึงวางเซฟก่อนเปิดเกมเสมอ เครื่องที่ยังไม่เคยมีโฟลเดอร์ `saves\playline0` ยังไม่รู้ว่าเกมจะเห็นไฟล์หรือไม่:
   ถ้าไม่เห็น ให้เปิดเกมเริ่มเกมใหม่หนึ่งครั้งให้เกมสร้าง playline เอง แล้วรันสคริปต์อีกรอบ
6. **ค่าที่สคริปต์ตั้งอาจค้างอยู่ในโปรไฟล์เกม** — `sys_MaxFPS` (และ `r_*` ถ้าใช้ `-Window`) ถูกสั่งผ่าน console
   ถ้าเกมบันทึกค่าลงโปรไฟล์ เครื่องที่เจ้าของใช้เล่นเองด้วยจะติดค่านี้ไป ใช้ `-MaxFps 0` ถ้าไม่ต้องการ
6b. **ประสิทธิภาพ** — 40 ตัว × 4 ครั้ง/วินาที ถ้าโฮสต์กระตุกให้ลด `KCD2MP.npcSync.hostMaxTracked`
7. **ผู้เล่นอยู่ไกลโฮสต์เกิน 60 ม.** จะเห็นโลกของตัวเอง; ผู้เล่นแยกกันไกลเกิน 30 ม. โฮสต์จะอยู่กับคนที่ id ต่ำสุด
7b. **ผู้เล่นที่ใช้ KCDMP 0.18.2 ตัวเดิมและตั้งชื่อ Steam ขึ้นต้นด้วย `[HOST]`** จะถูกทุกเครื่องมองเป็นโฮสต์ (ตัวละครหาย NPC ไม่ถูกรายงาน)
   agent ของ fork นี้ตัดคำนั้นออกจากชื่อผู้เล่นให้ แต่ agent ตัวเดิมไม่ได้ตัด — อย่าใช้ชื่อแบบนั้น
8. **กิจกรรมของ NPC** (นั่ง ทำงาน คุย) ไม่ถูกส่ง — guest เห็นแค่ เดิน / วิ่ง / ยืน / ถืออาวุธ
9. **ความเสียหายและการตายของ NPC** ข้ามเครื่องต้องใช้ native plugin (`KCDMP.dll`) — โฮสต์และผู้เล่น Steam ฉีดได้ตามวิธีของ 0.18.2; Game Pass ไม่ได้
10. **เควส บทสนทนา ร้านค้า หีบ** ยังแยกของใครของมัน

---

## 6. ปุ่มปรับ (พิมพ์ใน console ของเกม หรือสั่งผ่าน `:1403`)

| คำสั่ง / ค่า | ความหมาย | ค่าเริ่มต้น |
|---|---|---|
| `mp_world_role host\|guest\|auto` | บทบาทของเกมนี้ | `auto` |
| `mp_world_host_follow on\|off` | ตัวละครโฮสต์ตามผู้เล่น | `off` (agent `--world-host` สั่ง `on`; `--no-world-follow` ไม่สั่ง) |
| `KCD2MP.worldHostSilenceS` | ผู้เล่นถือว่าโฮสต์หายเมื่อไม่ได้ตำแหน่งจากโฮสต์กี่วินาที | 30 |
| `KCD2MP.npcSync.hostMaxTracked` / `hostRadius` | จำนวน / ระยะ NPC ที่โฮสต์รายงาน | 40 / 60 ม. |
| `KCD2MP.npcSync.releaseS` | ถือ NPC ไว้กี่วินาทีเมื่อข้อมูลเงียบ (ทุกบทบาท; 0.18.2 เดิมคือ 3) | 8 |
| `KCD2MP.npcSync.puppetTickMs` | รอบอัปเดต NPC puppet | 33 |
| `KCD2MP.worldHostFollow.maxDrift` / `spread` | ระยะที่โฮสต์ยอมห่าง / ระยะที่ถือว่าผู้เล่นอยู่กลุ่มเดียวกัน | 12 / 30 ม. |
| `KCD2MP.ghostCloneLook` | ตัวละครแทนเพื่อนลอกหน้าตาจากตัวเรา (ทุกเครื่องที่ใช้ม็อดนี้ รวมผู้เล่น Steam) | `true` |
| `mp_npc_sync on\|off` | ปิด NPC sync ทั้งหมด | `on` |

ค่าในตาราง `KCD2MP.*` แก้ตอนรันได้ด้วย Lua เช่น `#KCD2MP.npcSync.hostMaxTracked = 25`

---

## 7. พัฒนาต่อ

```
git clone <repo นี้>
cd dotnet
dotnet test KcdMp.Client.Tests -c Release        # agent: RemoteConsole, log tail, world host (43 tests)
dotnet test KcdMp.Launcher.Tests -c Release      # launcher: หาเกม บอกว่าขาดอะไร วางม็อด คำสั่งที่ส่งให้สคริปต์ (36 tests)
dotnet test KcdMp.Farkle.Tests -c Release        # ของเดิม (59 tests)
pip install lupa
python tools\Test-GamePassLua.py                 # ม็อด: รัน kdcmp.lua ทั้งไฟล์บน Lua 5.1 โดยจำลองเกม
powershell -File tools\Test-PackageScripts.ps1   # สคริปต์ใน package\ ส่วนที่ไม่ต้องมีเกม
powershell -File tools\Build-And-Install-Mod.ps1 -NoInstall    # สร้าง kdcmp.pak ใหม่หลังแก้ Lua
powershell -File tools\Build-Installer.ps1                     # สร้าง release\KingdomCome-Coop-Setup-<VERSION>.exe
                                                               # (ต้องมี Inno Setup 6 และ Visual Studio C++; ดู README)
```

แก้ `kdcmp.lua` แล้ว**ต้องสร้าง pak ใหม่เสมอ** เกมอ่านจาก pak ไม่ได้อ่านไฟล์ `.lua`
และต้องปิดเกมก่อนวาง pak ทับ (เกมเปิดไฟล์ค้างไว้ และอ่านแค่ตอนเริ่ม)

ไฟล์ของ fork นี้:

| ไฟล์ | หน้าที่ |
|---|---|
| `dotnet/KcdMp.Client/RemoteConsole*.cs`, `ILuaCommandSink.cs` | ส่ง Lua เข้าเกม Game Pass ผ่าน RemoteConsole |
| `dotnet/KcdMp.Client/LogTailGameTransport.cs` | อ่านสถานะจาก `kcd.log`; ตรวจว่าเกมพร้อมโดยไม่ใช้ `:1403` |
| `kdcmp/Data/Scripts/Startup/kdcmp.lua` | ค้นคำว่า `Game Pass fork` และ `Host world` |
| `installer/KCDMP.iss` | ตัวติดตั้ง exe ของ fork นี้ (ทดสอบติดตั้ง/ถอนจริงบนเครื่อง Game Pass) |
| `KCDMP_launcher/Models/GameInstalls.cs`, `ModPackage.cs`, `ScriptLauncher.cs` | launcher: หาเกมสองแบบ วางม็อดตอนกดเล่น เรียกสคริปต์ข้างล่าง |
| `package/KcdmpCommon.ps1` | ส่วนที่สคริปต์ใช้ร่วมกัน |
| `package/Start-GamePass.ps1` | ผู้เล่น Game Pass — launcher เรียกเมื่อกด JOIN (ทดสอบกับเกมจริงแล้ว) |
| `package/Start-WorldHost.ps1` | เครื่องโฮสต์ — launcher เรียกเมื่อกด RUN AS WORLD HOST (ยังไม่เคยรัน) |
| `package/Uninstall-GameChanges.ps1` | ตัวถอนการติดตั้งเรียก: ลบบรรทัด auto-load และ (ถ้าเลือก) โฟลเดอร์ม็อด |
| `package/save/autosave018.whs` | เซฟข้ามของโปรเจกต์ต้นทาง (release 0.18.2) — ติดตั้งในชื่อ `kcdmpskip.whs` |
| `tools/Test-GamePassLua.py` | เทสต์ม็อดแบบ offline |
| `tools/Test-PackageScripts.ps1` | เทสต์สคริปต์ใน `package/` แบบ offline |
| `tools/Import-GamePassSave.py` | นำไฟล์ `.whs` เข้าเซฟของ Game Pass (ใช้กับเกมจริงแล้วหนึ่งเครื่อง) |
| `docs/GAMEPASS-FORK-CHANGELOG.md` | สิ่งที่เปลี่ยนจาก 0.18.2 พร้อมหลักฐานที่วัดได้ |

ข้อเท็จจริงเรื่อง RemoteConsole ที่วัดจากเกมจริง (อย่าเดาใหม่):
- เกมส่ง frame แล้วรอคำตอบหนึ่ง frame: `'1'` = ขอคำสั่ง (ตอบ `'5'` คำสั่ง หรือ `'0'` ว่าง), `'2'/'3'/'4'` = บรรทัด log **ต้องตอบ `'0'` เท่านั้น** ไม่เช่นนั้นเกมเลิกอ่าน connection นั้น
- frame ใหญ่เกิน 4095 bytes ไม่ถูกรัน
- `kcd.log` เขียนตัวขึ้นบรรทัดไว้**หน้า**บรรทัดถัดไป บรรทัดสุดท้ายจึงยังไม่จบจนกว่าจะมีบรรทัดใหม่

License: GPLv3 เหมือนต้นฉบับ (ดู `LICENSE`)
