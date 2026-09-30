# คู่มือ Host World — สำหรับผู้ดูแลเครื่องเซิร์ฟเวอร์ (และ Codex บนเครื่องนั้น)

Fork นี้ต่อยอดจาก Kingdom Come: Together **0.18.2** เพิ่มสองอย่าง:

1. **ผู้เล่น Xbox Game Pass เข้าเล่นได้** (เกม Game Pass ไม่มี Modding Tools / debug API `:1403` / native plugin)
2. **Host World** — ให้เกมเครื่องเดียวเป็น "โลกกลาง" กำหนดตำแหน่ง NPC ให้ทุกคน แทนที่แต่ละเครื่องจะคิดเองแล้วแย่งกัน

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
 KCDMP 0.18.2 ตัวเดิม          ชุด GamePass ของ fork นี้
 + ไฟล์ม็อดของ fork นี้        (agent คุยกับเกมผ่าน RemoteConsole :4600)
 = guest                       = guest
```

| เครื่อง | บทบาท | ต้องติดตั้ง |
|---|---|---|
| เซิร์ฟเวอร์ | **host** — รายงาน NPC รอบตัว (40 ตัว ในระยะ 60 ม.) | `Setup.bat` → 3 (หรือ `Start-WorldHost.bat`) |
| ผู้เล่น Steam | **guest** — แสดงผลตามโฮสต์ ไม่รายงาน NPC | KCDMP 0.18.2 ตามปกติ แล้ว `Setup.bat` → 2 |
| ผู้เล่น Game Pass | **guest** | `Setup.bat` → 1 (หรือ `Start-GamePass.bat`) |

ทุกเครื่องใช้ zip เดียวกัน: **`KCDMP-0.18.2-HostWorld.zip`** (มี `Setup.bat` ให้เลือกโหมด)
**ทุกเครื่องต้องใช้ไฟล์ม็อด (`kdcmp.pak`) ชุดเดียวกันจาก release เดียวกัน**
agent ทุกตัวยังรายงานเวอร์ชัน `0.18.2` และ wire protocol ไม่เปลี่ยน จึงใช้ relay เดิมได้

### กติกาว่าใครเป็น host (อยู่ใน `kdcmp.lua` → `KCD2MP_WorldRole()`)

1. สั่งตรง ๆ: `mp_world_role host|guest` (ตัวเปิดเซิร์ฟเวอร์สั่ง `host` ให้เอง)
2. ถ้าในเซสชันมีผู้เล่นที่ชื่อขึ้นต้นด้วย `[HOST]` → เครื่องอื่นทุกเครื่องเป็น guest
   และ**ไม่สร้างตัวละครแทน**ให้ผู้เล่นคนนั้น (ไม่มีใครเห็นตัวละครของโฮสต์)
3. ไม่เข้าสองข้อบน → ดูจาก build: Modding Tools = host, retail / Game Pass = guest

guest **ไม่ส่ง**สถานะ NPC ใด ๆ เลย (ไม่มี `npc_state` / `npc_claim` / drag claim) จึงแย่ง NPC จากโฮสต์ไม่ได้

---

## 2. สถานะ: อะไรทดสอบแล้ว / ยังไม่ได้ทดสอบ

งานทั้งหมดทำบนเครื่อง **Game Pass** ซึ่งรันเกม Steam / Modding Tools ไม่ได้

### ทดสอบกับเกมจริงแล้ว (ฝั่ง Game Pass, 2026-09-30)
- เกม Game Pass รันด้วย `-devmode`, RemoteConsole `:4600`, โหลดม็อดจาก `Documents\kingdomcome_mods\kdcmp`
- agent `--transport remoteconsole` ต่อ relay จริง เล่นกับผู้เล่น Steam 0.18.2 ได้: 0 ครั้งที่ค้าง, console ไม่หลุด
- ตัวละครแทนเพื่อนเป็น `NPC_NAI` (ไม่มี AI) เดินต่อเนื่อง, ท่าฟันแสดงผล (ผู้เล่นยืนยันด้วยตา)
- สภาพอากาศถูกตั้งกลับหลังโหลดเซฟ

### ทดสอบด้วยชุดเทสต์เท่านั้น (ยังไม่เคยรันกับเกมจริง)
- กติกา host / guest, การซ่อนผู้เล่น `[HOST]`, โฮสต์ติดตาม 40 ตัว / 60 ม.
- NPC puppet เคลื่อนที่ต่อเนื่อง, ถือไว้ 8 วินาที, หยุดขยับ NPC ที่กำลังคุยกับผู้เล่น
- ตำแหน่งที่โฮสต์ควรไปยืน (`KCD2MP_WorldHostFollowTarget`)

### ยังไม่เคยรันเลย — **ต้องทดสอบบนเครื่องเซิร์ฟเวอร์**
- `Start-WorldHost.ps1` ทั้งไฟล์
- ม็อดของ fork นี้บน build Modding Tools (ทั้งฝั่งโฮสต์และฝั่งผู้เล่น Steam)
- การให้ตัวละครของโฮสต์ย้ายตามผู้เล่น (`mp_world_host_follow`)
- โหลดของเกมเมื่อโฮสต์รายงาน NPC 40 ตัว

---

## 3. ติดตั้งและรันบนเครื่องเซิร์ฟเวอร์

ต้องมีก่อน: Steam, KCD2, **KCD2 Modding Tools** และเคยเปิด Modding Tools ผ่าน Steam หนึ่งครั้งให้ Workspace Setup
คัดลอกข้อมูลจนเสร็จ (ไม่เช่นนั้นเกมเด้ง `114 tables are not loaded`)

1. แตก `KCDMP-0.18.2-HostWorld.zip`
2. ปิดเกม แล้วรัน `Setup.bat` เลือก 3 (หรือรัน `Start-WorldHost.bat` ตรง ๆ)
   - ถ้ามี relay รันอยู่แล้วบนพอร์ตเดียวกัน สคริปต์จะใช้ตัวนั้น (`-NoRelay` เพื่อไม่เปิดเองเลย)
   - หา `KingdomCome.exe` ของ Modding Tools ไม่เจอ → ใส่ `-GameExe "<path>"`
3. โหลดเซฟในเกม (สคริปต์รอจน `:1403` รายงานเวลาในเกม)
4. สคริปต์สั่ง `KCD2MP_SetWorldRole("host")` และ `KCD2MP_SetWorldHostFollow("on")` แล้วรัน agent ชื่อ `[HOST] world`

เปิดพอร์ต: TCP 7778 ขาเข้า (relay) เท่านั้น ห้ามเปิด `:1403` หรือ `:4600` ออกนอกเครื่อง

ผู้เล่น Steam: ปิดเกม → `Setup.bat` เลือก 2 (วางม็อดลง `<Modding Tools>\Mods\kdcmp\`) → เล่นผ่าน KCDMP Launcher ตามเดิม
ผู้เล่น Game Pass: `Setup.bat` เลือก 1 (ถามที่อยู่ relay ครั้งแรก) ครั้งต่อไปใช้ `Start-GamePass.bat`

---

## 4. รายการตรวจสอบ (ทำตามลำดับ — ข้อไหนไม่ผ่านให้หยุดแก้ก่อน)

`kcd.log` ของ Modding Tools อยู่ในโฟลเดอร์เกม; ของ Game Pass อยู่ที่ `Documents\kcd.log`

| # | ตรวจอะไร | ผ่านเมื่อ |
|---|---|---|
| V1 | สคริปต์หาเกมและติดตั้งม็อด | พิมพ์ `Modding Tools game:` และ `Mod installed to ...\Mods\kdcmp` |
| V2 | เปิดเกมจาก exe ตรง ๆ ได้ | เกมขึ้นเมนู; ถ้า Steam ไม่ยอม ให้เปลี่ยนเป็น `steam://rungameid/2429020` |
| V3 | ม็อดโหลด | `kcd.log` มี `[KCD2-MP] === MOD INIT ===` และ `Commands OK` |
| V4 | บทบาท | `kcd.log` มี `WORLD-ROLE host -> acting as host` |
| V5 | guest รู้จักโฮสต์ | `kcd.log` ของผู้เล่นมี `WORLD-HOST peer N '[HOST] world' is the world host: hidden; this game is acting as guest` |
| V6 | โฮสต์รายงาน NPC | `kcd.log` ของโฮสต์มี `NPC-SYNC tracking <ชื่อ>` หลายสิบบรรทัดเมื่อมีผู้เล่นต่ออยู่ |
| V7 | โฮสต์ตามผู้เล่น | ผู้เล่นเดินห่างเกิน 12 ม. → `WORLD-HOST follow: moved to x,y,z` |
| V8 | guest นิ่ง | ผู้เล่นสองคนยืนที่เดียวกัน เห็น NPC ตำแหน่งเดียวกัน; นับ `NPC-SYNC release` ต่อนาที (ก่อนแก้วัดได้ 8.7) |

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
5. **โหลดเซฟเองตอนเปิด** — มี cvar `wh_sys_AutoLoadLastSave` ในเกม ยังไม่ได้ลอง
6. **ประสิทธิภาพ** — 40 ตัว × 4 ครั้ง/วินาที ถ้าโฮสต์กระตุกให้ลด `KCD2MP.npcSync.hostMaxTracked`
7. **ผู้เล่นอยู่ไกลโฮสต์เกิน 60 ม.** จะเห็นโลกของตัวเอง; ผู้เล่นแยกกันไกลเกิน 30 ม. โฮสต์จะอยู่กับคนที่ id ต่ำสุด
8. **กิจกรรมของ NPC** (นั่ง ทำงาน คุย) ไม่ถูกส่ง — guest เห็นแค่ เดิน / วิ่ง / ยืน / ถืออาวุธ
9. **ความเสียหายและการตายของ NPC** ข้ามเครื่องต้องใช้ native plugin (`KCDMP.dll`) — โฮสต์และผู้เล่น Steam ฉีดได้ตามวิธีของ 0.18.2; Game Pass ไม่ได้
10. **เควส บทสนทนา ร้านค้า หีบ** ยังแยกของใครของมัน

---

## 6. ปุ่มปรับ (พิมพ์ใน console ของเกม หรือสั่งผ่าน `:1403`)

| คำสั่ง / ค่า | ความหมาย | ค่าเริ่มต้น |
|---|---|---|
| `mp_world_role host\|guest\|auto` | บทบาทของเกมนี้ | `auto` |
| `mp_world_host_follow on\|off` | ตัวละครโฮสต์ตามผู้เล่น | `off` (ตัวเปิดเซิร์ฟเวอร์สั่ง `on`) |
| `KCD2MP.npcSync.hostMaxTracked` / `hostRadius` | จำนวน / ระยะ NPC ที่โฮสต์รายงาน | 40 / 60 ม. |
| `KCD2MP.npcSync.releaseS` | guest ถือ NPC ไว้กี่วินาทีเมื่อข้อมูลเงียบ | 8 |
| `KCD2MP.npcSync.puppetTickMs` | รอบอัปเดต NPC puppet | 33 |
| `KCD2MP.worldHostFollow.maxDrift` / `spread` | ระยะที่โฮสต์ยอมห่าง / ระยะที่ถือว่าผู้เล่นอยู่กลุ่มเดียวกัน | 12 / 30 ม. |
| `KCD2MP.ghostCloneLook` | (guest) ตัวละครแทนเพื่อนลอกหน้าตาจากตัวเรา | `true` |
| `mp_npc_sync on\|off` | ปิด NPC sync ทั้งหมด | `on` |

ค่าในตาราง `KCD2MP.*` แก้ตอนรันได้ด้วย Lua เช่น `#KCD2MP.npcSync.hostMaxTracked = 25`

---

## 7. พัฒนาต่อ

```
git clone <repo นี้>
cd dotnet
dotnet test KcdMp.Client.Tests -c Release        # agent: RemoteConsole, log tail (25 tests)
dotnet test KcdMp.Farkle.Tests -c Release        # ของเดิม (59 tests)
pip install lupa
python tools\Test-GamePassLua.py                 # ม็อด: รัน kdcmp.lua ทั้งไฟล์บน Lua 5.1 โดยจำลองเกม
powershell -File tools\Build-And-Install-Mod.ps1 -NoInstall    # สร้าง kdcmp.pak ใหม่หลังแก้ Lua
powershell -File tools\Build-HostWorldRelease.ps1              # สร้าง release\KCDMP-<VERSION>-HostWorld.zip
```

แก้ `kdcmp.lua` แล้ว**ต้องสร้าง pak ใหม่เสมอ** เกมอ่านจาก pak ไม่ได้อ่านไฟล์ `.lua`
และต้องปิดเกมก่อนวาง pak ทับ (เกมเปิดไฟล์ค้างไว้ และอ่านแค่ตอนเริ่ม)

ไฟล์ของ fork นี้:

| ไฟล์ | หน้าที่ |
|---|---|
| `dotnet/KcdMp.Client/RemoteConsole*.cs`, `ILuaCommandSink.cs` | ส่ง Lua เข้าเกม Game Pass ผ่าน RemoteConsole |
| `dotnet/KcdMp.Client/LogTailGameTransport.cs` | อ่านสถานะจาก `kcd.log`; ตรวจว่าเกมพร้อมโดยไม่ใช้ `:1403` |
| `kdcmp/Data/Scripts/Startup/kdcmp.lua` | ค้นคำว่า `Game Pass fork` และ `Host world` |
| `package/Setup.ps1`, `KcdmpCommon.ps1` | ตัวติดตั้งตัวเดียว: หาเกมในเครื่อง วางม็อด เลือกโหมด |
| `package/Start-GamePass.ps1` | ผู้เล่น Game Pass (ทดสอบกับเกมจริงแล้ว) |
| `package/Start-WorldHost.ps1` | เครื่องโฮสต์ (ยังไม่เคยรัน) |
| `tools/Test-GamePassLua.py` | เทสต์ม็อดแบบ offline |
| `docs/GAMEPASS-FORK-CHANGELOG.md` | สิ่งที่เปลี่ยนจาก 0.18.2 พร้อมหลักฐานที่วัดได้ |

ข้อเท็จจริงเรื่อง RemoteConsole ที่วัดจากเกมจริง (อย่าเดาใหม่):
- เกมส่ง frame แล้วรอคำตอบหนึ่ง frame: `'1'` = ขอคำสั่ง (ตอบ `'5'` คำสั่ง หรือ `'0'` ว่าง), `'2'/'3'/'4'` = บรรทัด log **ต้องตอบ `'0'` เท่านั้น** ไม่เช่นนั้นเกมเลิกอ่าน connection นั้น
- frame ใหญ่เกิน 4095 bytes ไม่ถูกรัน
- `kcd.log` เขียนตัวขึ้นบรรทัดไว้**หน้า**บรรทัดถัดไป บรรทัดสุดท้ายจึงยังไม่จบจนกว่าจะมีบรรทัดใหม่

License: GPLv3 เหมือนต้นฉบับ (ดู `LICENSE`)
