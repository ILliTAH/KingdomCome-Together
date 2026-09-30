<p align="center">
  <img src="docs/branding/kcd2-mp-logo.png" alt="KCD2 Multiplayer" width="220">
</p>

<h1 align="center">Kingdom Come: Together — Host World fork</h1>
<p align="center"><em>ม็อด co-op ไม่เป็นทางการสำหรับ Kingdom Come: Deliverance II — เพิ่มผู้เล่น Xbox Game Pass และ "โลกกลาง"</em></p>

<p align="center">
  <a href="https://github.com/ILliTAH/KingdomCome-Together/releases"><img alt="release" src="https://img.shields.io/github/v/release/ILliTAH/KingdomCome-Together?include_prereleases&label=release&style=flat-square&color=b8860b"></a>
  <img alt="base" src="https://img.shields.io/badge/base-KCDMP%200.18.2-555555?style=flat-square">
  <a href="LICENSE"><img alt="License: GPLv3" src="https://img.shields.io/badge/license-GPLv3-2c3e50?style=flat-square"></a>
  <img alt="Platform" src="https://img.shields.io/badge/platform-Windows-555555?style=flat-square">
</p>

**English summary.** A fork of [Kingdom Come: Together](https://github.com/DeepFriedDepp/KingdomCome-Together) at tag `0.18.2`. It lets **Xbox Game Pass** players join a stock 0.18.2 session, and adds **Host World**: a dedicated host machine that nobody plays on decides where the NPCs are, and every player's game displays it. The Game Pass path is tested on a live game; the Steam and dedicated-host paths have never been run. Details: [docs/HOST-WORLD-GUIDE.md](docs/HOST-WORLD-GUIDE.md) (Thai) and [docs/GAMEPASS-FORK-CHANGELOG.md](docs/GAMEPASS-FORK-CHANGELOG.md) (English).

> **ไม่เกี่ยวข้องกับ Warhorse Studios** — Kingdom Come: Deliverance เป็นเครื่องหมายการค้าของ Warhorse Studios
> โปรเจกต์นี้เป็นงานแฟนเมดที่ไม่แสวงกำไร และยังเป็นรุ่นทดลอง ควรสำรองเซฟก่อนเล่น

---

## fork นี้เพิ่มอะไรจาก KCDMP 0.18.2

| | ตัวเดิม 0.18.2 | fork นี้ |
|---|---|---|
| ผู้เล่น **Xbox Game Pass** | เล่นไม่ได้ (ต้องใช้ Steam + Modding Tools) | **เล่นได้** — agent คุยกับเกมผ่าน RemoteConsole แทน debug API |
| ตำแหน่ง NPC | แต่ละเครื่องคิดเอง ยืมกันได้ 5 ตัวในระยะ 30 ม. แล้วแย่งกัน | **Host World** — เครื่องโฮสต์แยก (ไม่มีคนเล่น) เป็นโลกกลาง รายงาน 40 ตัวในระยะ 60 ม. เครื่องผู้เล่นทุกเครื่องแสดงผลตาม |
| เครื่องโฮสต์ | ไม่มี | มีตัวเปิด (relay + เกม + agent) สำหรับเครื่องเซิร์ฟเวอร์ — **ยังไม่เคยรันกับเกมจริง** |
| ตัวแทนของเพื่อน (ฝั่ง Game Pass) | — | หุ่นเชิดไม่มี AI ลอกหน้าตาจากตัวเรา เดินต่อเนื่อง ฟันให้เห็น |
| ตัวติดตั้ง | `KCDMP-Setup` (Steam เท่านั้น) | `Setup.bat` ตัวเดียว ใช้ได้ทั้ง Game Pass / Steam / เครื่องโฮสต์ |

agent ยังรายงานเวอร์ชัน `0.18.2` และ wire protocol ไม่เปลี่ยน จึงใช้ relay ของ 0.18.2 ตัวเดิมได้ และเล่นร่วมกับผู้เล่น Steam ที่ใช้ KCDMP 0.18.2 ได้

## ติดตั้งและเล่น

1. ดาวน์โหลด **`KCDMP-0.18.2-HostWorld.zip`** จาก [หน้า Releases](https://github.com/ILliTAH/KingdomCome-Together/releases) แล้วแตกไฟล์
2. ปิดเกม แล้วดับเบิลคลิก **`Setup.bat`** — มันบอกว่าเจอเกมแบบไหนในเครื่อง แล้วให้เลือก

| เลือก | ใช้กับ | ทำอะไร |
|---|---|---|
| **1** Play on Xbox Game Pass | ผู้เล่น Game Pass | ติดตั้งม็อด เปิดเกมด้วย `-devmode` แล้วต่อ relay ให้ (ครั้งแรกถามที่อยู่ relay) |
| **2** Install the mod for Steam | ผู้เล่น Steam ที่ลง [KCDMP 0.18.2](https://github.com/DeepFriedDepp/KingdomCome-Together/releases/tag/0.18.2) ไว้แล้ว | วางไฟล์ม็อดลง Modding Tools จากนั้นเล่นผ่าน KCDMP Launcher ตามเดิม |
| **3** Run this machine as WORLD HOST | เครื่องเซิร์ฟเวอร์ที่มี Steam + KCD2 + Modding Tools | เปิด relay + เกม + agent ให้เครื่องนี้เป็นโลกกลาง |

**ทุกเครื่องในเซสชันต้องใช้ zip จาก release เดียวกัน** รายละเอียดแต่ละโหมดอยู่ใน `README-TH.md` ภายใน zip

```
 เครื่องเซิร์ฟเวอร์ (Steam)  ── relay :7778 + เกม = โลกกลาง  ("[HOST] world")
        ▲                         ▲
 ผู้เล่น Steam (guest)        ผู้เล่น Game Pass (guest)
```

**โฮสต์คือเครื่องเซิร์ฟเวอร์แยก ไม่มีใครเล่นบนเครื่องนั้น** ผู้เล่นทุกคน (Steam และ Game Pass) เป็น guest
เครื่องผู้เล่นไม่เป็นโฮสต์เองไม่ว่ากรณีใด — ถ้าเครื่องโฮสต์ไม่ได้เปิดอยู่ ม็อดทำงานแบบ 0.18.2 เดิม (ไม่มีโลกกลาง)

## สถานะ: อะไรทดสอบแล้ว

| ส่วน | สถานะ |
|---|---|
| ผู้เล่น Game Pass เล่นกับผู้เล่น Steam 0.18.2 ผ่าน relay จริง | **ทดสอบกับเกมจริงแล้ว** (2026-09-30) |
| ตัวแทนเพื่อนไม่มี AI / เดินต่อเนื่อง / ท่าฟัน / อากาศหลังโหลดเซฟ | **ทดสอบกับเกมจริงแล้ว** |
| Host World: กติกา host–guest, โฮสต์รายงาน 40 ตัว, NPC เคลื่อนที่ต่อเนื่อง, ซ่อนตัวละครโฮสต์ | ผ่านชุดเทสต์ — **ยังไม่เคยรันกับเกมจริง** |
| ม็อดของ fork นี้บนเกม Steam / Modding Tools | **ยังไม่เคยรัน** |
| ตัวเปิดเครื่องโฮสต์ (`Start-WorldHost`) และการให้โฮสต์ตามผู้เล่น | **ยังไม่เคยรัน** |

งานทั้งหมดทำบนเครื่อง Game Pass ซึ่งรันเกม Steam ไม่ได้ — ฝั่ง Steam และเครื่องโฮสต์ต้องตรวจตาม
[รายการใน docs/HOST-WORLD-GUIDE.md](docs/HOST-WORLD-GUIDE.md) ก่อนใช้จริง

## ฟีเจอร์ต่อแพลตฟอร์ม

| | ผู้เล่น Steam | ผู้เล่น Game Pass |
|---|---|---|
| เห็นผู้เล่นอื่น เดิน วิ่ง ป้ายชื่อ | ✅ | ✅ |
| ส่งตำแหน่ง เลือด stamina ของเรา | ✅ | ✅ |
| ท่าฟันของผู้เล่นอื่น | ✅ (native) | ✅ (คลิปฟันจริง) |
| สภาพอากาศและเวลาตามเซสชัน | ✅ | ✅ |
| NPC ตามโลกของเครื่องโฮสต์ (ตำแหน่ง เดิน/วิ่ง ถืออาวุธ) | ✅ | ✅ |
| คุยเสียงตามระยะ, Farkle | ✅ | ยังไม่ได้ทดสอบ |
| ชุดเกราะ/อาวุธของผู้เล่นอื่นตรงกับของจริง | ✅ | ❌ (ตัวแทนลอกหน้าตาจากตัวเราแทน) |
| ความเสียหาย/การตายข้ามเครื่อง | ✅ | ❌ |
| ไอคอนผู้เล่นบนแผนที่ | ❌ | ❌ |

✅ = รองรับ ไม่ได้แปลว่าทดสอบแล้วทุกข้อ — ดูตาราง "สถานะ" ข้างบนว่าข้อไหนทดสอบกับเกมจริงแล้ว
คอลัมน์ Steam คือความสามารถของ 0.18.2 ตัวเดิม ดู [README ของ 0.18.2](https://github.com/DeepFriedDepp/KingdomCome-Together/blob/0.18.2/README.md)
สำหรับรายละเอียด วิธีเล่น Farkle และข้อจำกัดของตัวเดิม
Game Pass ทำแถว ❌ ไม่ได้เพราะไม่มี debug API และ native plugin ของ build Modding Tools

## ข้อจำกัดของ Host World

- ผู้เล่นต้องอยู่บริเวณเดียวกับโฮสต์ (ราว 60 ม.) — เกมของโฮสต์คำนวณ NPC ละเอียดเฉพาะรอบตัวมัน ไกลกว่านั้นแต่ละคนเห็นโลกของตัวเอง
- กิจกรรมของ NPC (นั่ง ทำงาน คุย) ไม่ถูกส่ง — guest เห็นแค่ เดิน / วิ่ง / ยืน / ถืออาวุธ
- เควส บทสนทนา ร้านค้า หีบ ยังเป็นของใครของมัน
- เครื่องโฮสต์กับเครื่องส่วนตัวใช้บัญชี Steam เดียวกันเปิดเกมพร้อมกันไม่ได้
- ตัวละครของโฮสต์ไม่มีใครเล่น แต่ยังถูก NPC ตีได้ — ยังไม่มีโค้ดป้องกัน

ความเสี่ยงและงานค้างทั้งหมดเรียงตามความสำคัญอยู่ใน [docs/HOST-WORLD-GUIDE.md §5](docs/HOST-WORLD-GUIDE.md)

## ความปลอดภัย

- เกม Game Pass ต้องเปิดด้วย `-devmode` ซึ่งเปิดพอร์ต **4600** ที่ไม่มีรหัสผ่าน — ตัวติดตั้งสร้างกฎ firewall บล็อกเครื่องอื่นให้ (ขอสิทธิ์ admin ครั้งเดียว)
- บนเครื่องเซิร์ฟเวอร์ เปิดออกนอกเครื่องเฉพาะ **TCP 7778** (relay) — ห้ามเปิด `:1403` หรือ `:4600`
- ที่อยู่ relay เก็บใน `relay.txt` บนเครื่องของแต่ละคน ไม่อยู่ใน repo และไม่อยู่ใน zip

## สำหรับนักพัฒนา

ต้องมี [.NET 8 SDK](https://dotnet.microsoft.com/download) ขึ้นไป และ Python 3 + `pip install lupa`

```powershell
cd dotnet
dotnet test KcdMp.Client.Tests -c Release      # agent: RemoteConsole, log tail (25 tests)
dotnet test KcdMp.Farkle.Tests -c Release      # ของเดิม (59 tests)
cd ..
python tools\Test-GamePassLua.py               # ม็อด: รัน kdcmp.lua ทั้งไฟล์บน Lua 5.1 โดยจำลองเกม
powershell -File tools\Build-And-Install-Mod.ps1 -NoInstall   # สร้าง kdcmp.pak ใหม่หลังแก้ Lua
powershell -File tools\Build-HostWorldRelease.ps1             # สร้าง release\KCDMP-<VERSION>-HostWorld.zip
```

| ที่ | มีอะไร |
|---|---|
| `dotnet/KcdMp.Client/` | agent — ของ fork นี้: `RemoteConsole*.cs`, `ILuaCommandSink.cs`, การแก้ใน `LogTailGameTransport.cs` และ `GameBridge.cs` |
| `dotnet/KcdMp.Client.Tests/` | เทสต์ของ agent รวม RemoteConsole จำลองที่บังคับกติกาเดียวกับเกมจริง |
| `kdcmp/` | ม็อด (Lua + `kdcmp.pak`) — ค้นคำว่า `Game Pass fork` และ `Host world` ใน `kdcmp.lua` |
| `package/` | `Setup`, `Start-GamePass`, `Start-WorldHost` ที่อยู่ใน zip |
| `tools/Test-GamePassLua.py` | เทสต์ม็อดแบบ offline |
| `dotnet/KcdMp.Server/`, `KcdMp.Protocol/`, `native/`, `KCDMP_launcher/`, `installer/` | ของ 0.18.2 ไม่ได้แก้ |

เอกสาร:
- [docs/HOST-WORLD-GUIDE.md](docs/HOST-WORLD-GUIDE.md) — ติดตั้ง รัน รายการตรวจสอบ ความเสี่ยง ปุ่มปรับ (สำหรับผู้ดูแลเครื่องเซิร์ฟเวอร์)
- [docs/GAMEPASS-FORK-CHANGELOG.md](docs/GAMEPASS-FORK-CHANGELOG.md) — ทุกอย่างที่เปลี่ยนจาก 0.18.2 พร้อมตัวเลขที่วัดได้
- [docs/superpowers/specs/2026-09-30-host-world-npc-sync-design.md](docs/superpowers/specs/2026-09-30-host-world-npc-sync-design.md) — แบบของ Host World
- [docs/LAUNCHING.md](docs/LAUNCHING.md), [docs/NETWORKING.md](docs/NETWORKING.md) — ของ 0.18.2 (ฝั่ง Steam และการตั้ง relay)

## License และที่มา

[GNU General Public License v3.0](LICENSE) เหมือนต้นทาง

สายของโปรเจกต์: [`marczukmichal/kcd2-multiplayer`](https://github.com/marczukmichal/kcd2-multiplayer) (แนวคิดและตัวแรก)
→ [`DeepFriedDepp/KingdomCome-Together`](https://github.com/DeepFriedDepp/KingdomCome-Together) (พัฒนาต่อโดยได้รับอนุญาตจากผู้พัฒนาเดิม)
→ fork นี้ แยกจาก tag `0.18.2` เมื่อ 2026-09-30 เครดิตของตัวม็อดทั้งหมดเป็นของสองโปรเจกต์ข้างต้น
fork นี้เพิ่มเฉพาะส่วน Game Pass และ Host World ตามที่ระบุไว้ใน changelog
