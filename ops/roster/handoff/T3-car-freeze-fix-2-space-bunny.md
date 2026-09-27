# T3 — إصلاح تجميد السيارة — رقم 2 / Space Bunny

- **الوكيل:** 2-space-bunny · `opencode/space-bunny-free` · $0
- **التاريخ:** 2026-09-27
- **الملكية المنفَّذة:** `Assets/_Game/Scripts/SimpleCar.cs` — ملف واحد فقط
- **لم يُلمس:** `Assets/Scenes/` · `ProjectSettings/` · أي سكربت آخر · لا Rigidbody · لا سكربت جديد · لم يُنسخ `KartFollowCamera.cs`

---

## 1. السطور التي عدّلتها — كل سطر

النسخة قبل: `ops/roster/handoff/SimpleCar.cs.beforeT3` (مدّعي به رقم 1)

| # | `file:line` | قبل | بعد |
|---|---|---|---|
| 1 | `Assets/_Game/Scripts/SimpleCar.cs:28-29` | `if (Keyboard.current == null \|\| !player) return;`<br>`var kb = Keyboard.current;` | `var kb = Keyboard.current;`<br>`if (kb == null) return;` |
| 2 | `Assets/_Game/Scripts/SimpleCar.cs:31-32` | `if (kb.eKey.wasPressedThisFrame)` | `// E للركوب/النزول — يتجاهل الضغطة بأمان لو ما في لاعب`<br>`if (player && kb.eKey.wasPressedThisFrame)` |
| 3 | `Assets/_Game/Scripts/SimpleCar.cs:39-42` | `if (!driving && !driveDirect) return;`<br>`driving = driving \|\| driveDirect;` | `// ما في لاعب = قيادة مباشرة على أي حال بدون ركوب`<br>`bool direct = driveDirect \|\| player == null;`<br>`if (!driving && !direct) return;`<br>`driving = driving \|\| direct;` |
| 4 | `Assets/_Game/Scripts/SimpleCar.cs:52` | `if (!driveDirect) player.position = seat.position;` | `if (!driveDirect && player) player.position = seat.position;` |
| 5 | `Assets/_Game/Scripts/SimpleCar.cs:59` | — (سطر جديد) | `if (!player) return; // لا لاعب = لا ركوب ولا نزول` |

`Compare-Object` بين `.beforeT3` والملف الحالي: **7 سطور مضافة، 5 محذوفة، 0 سطر آخر متغيّر.**

**لم أغيّر** `speed` / `turn` / `useDist` / `driveDirect` (سطر 9-12 و 17 كما هي) — المشهد مالك رقم 1.
القيم في المشهد نفسها مؤكَّدة بـ`Assets/Scenes/Core/Core.unity`: `driveDirect: 1` موجود.

---

## 2. لماذا هذا يزيل التجميد — تتبّع منطقي

`Core.unity` لا يحتوي وسم `Player` إطلاقاً — الدليل: عدّ `m_TagString` في المشهد = `Untagged` × 67 و `MainCamera` × 1، **وصفر `Player`**. ∴ `Start()` السطر 21-22 لا يُسند `player` ويبقيه `null`.

| الوضع | قبل | بعد |
|---|---|---|
| `Update()` إطار عادي | `!player` = true → **return في كل إطار** → السيارة مجمّدة دائماً | `kb != null` فيمر → `direct = driveDirect \|\| player == null` = **true** → ينزل للـ`Rotate`/`Translate` |
| `driveDirect = 0` حتى لو غيّره رقم 1 | لا يزال مجمّداً | `player == null` وحده يكفي للقيادة |
| ضغط `E` بلا لاعب | `Vector3.Distance(player.position, ...)` → **NullReferenceException** | `player &&` تمنع الدخول للفرع أصلاً → الضغطة تُتجاهل بأمان |
| `SetDrive(true/false)` بلا لاعب | `player.SetParent(...)` → **NullReferenceException** | `if (!player) return;` قبل `SetParent` |

لمسارات الحركة `SimpleCar.cs:49-50` (`Rotate`/`Translate`) — لا تحتاج Rigidbody، كما نصّت المهمة.

---

## 3. التحقق — كمبايل حقيقي

### 3.1 ⛔ الفحص المطلوب (`Unity -batchmode`) — **لم يُنفَّذ، недо_reason قفل المشروع**

شغّلته بالضبط كما ورد في T3:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.0.84f1\Editor\Unity.exe" `
  -batchmode -quit -projectPath D:\CC_GAME_1 -logFile D:\CC_GAME_1\ops\verify\compile_t3.log
```

**لم يبدأ.** الدليل من `ops/verify/compile_t3.log:22-29`:

```
It looks like another Unity instance is running with this project open.

Multiple Unity instances cannot open the same project.

Project: D:/CC_GAME_1
Fatal Error! It looks like another Unity instance is running with this project open.
```

ويؤكده آخر 30 سطر من نفس اللوغ — `HandleProjectAlreadyOpenInAnotherInstance` أي أن يونيتي **لم يصل إلى مرحلة الكمبايل أصلاً**:

```
0x00007FFA57D98B65 (Unity) EditorMonoConsole::LogToConsoleImplementation
0x00007FFA57E96C56 (Unity) HandleProjectAlreadyOpenInAnotherInstance
0x00007FFA57E98A68 (Unity) Application::InitializeProject
0x00007FFA57E9848D923 (Unity) UnityMain
========== END OF STACKTRACE ===========
A crash has been intercepted by the crash handler.
```

> ⚠️ **`Select-String 'error CS'` على هذا اللوغ = 0 — وهذا لا يعني نجاح.**
> الصفر هنا لأن الكمبايل **ما حصل**، لا لأنه نجح. لا أستخدمه كدليل قبول.

**سببه:** يونيتي PID 11540 مفتوح على نفس المشروع منذ `9/27/2026 5:12:04 PM`
(`Unity.exe -projectPath D:\CC_GAME_1`)، والقفل `Temp/UnityLockfile` موجود من `5:12:05 PM`.
وآخر حالة له: `Checking for Broken ProBuilder References (busy for 35:14)`.

**لم أقتله** — جلسة المحرر ملك صاحب المشروع وخطر فقدان عمل غير معدوم.
**هذا يحتاج رقم 1:** يُغلق المحرر ثم يrepeated نفس الأمر. Expected: نفس نتيجة 3.2.

### 3.2 ✅ بديل حقيقي — كومبايلر يونيتي نفسه على نفس المصادر

استخدمت **Roslyn الخاص بيونيتي** على نفس `-define` ونفس الـreferences/namespaces
المستخرجَين من `Assembly-CSharp.csproj` الذي تولّده يونيتي — أي نفس كمبايل
`Assembly-CSharp` بدقّة، بلا تحريف.

```powershell
& "C:\Program Files\dotnet\dotnet.exe" `
  "C:\Program Files\Unity\Hub\Editor\6000.0.84f1\Editor\Data\DotNetSdkRoslyn\csc.dll" `
  "@...\t3_asm_cs.rsp"
```

**المخرج الحقيقي — `Assembly-CSharp` (17 سكربت، منها `SimpleCar.cs`):**

```
Microsoft (R) Visual C# Compiler version 4.3.1-3.22526.13 (41a5af9d)
Copyright (C) Microsoft Corporation. All rights reserved.

EXITCODE=0
```

**صفر `error CS` · صفر `warning` · صفر سطر diagnostics.**

ثم **`Assembly-CSharp-Editor`** (20 سكربت، مُربوط بالـdll الطازج الذي بُني في 3.2):

```
Microsoft (R) Visual C# Compiler version 4.3.1-3.22526.13 (41a5af9d)
Copyright (C) Microsoft Corporation. All rights reserved.

EXITCODE=0
```

**صفر `error CS` · صفر `warning`.**

**تغطية المشروع كاملة:** الـ17 كلها في `Assembly-CSharp`، والـ18 في
`Assets/_Game/Scripts/Editor/SceneScreenshotTaker.cs` (مجلد Editor) → `Assembly-CSharp-Editor`.
ف compiling الاثنين معاً = **المشروع كله 0 خطأ CS**.

**دليل أن التغيّر دخل فعلاً في الـbinary:**
`t3_asm_cs.dll` = 80896 بايت @ `7:42:40 PM` · مقابل `Library/ScriptAssemblies/Assembly-CSharp.dll`
القديم = 80384 بايت @ `5:07:15 PM`. الاختلاف + الربط الناجح للـeditor على الـdll الجديد
يعني أن `SimpleCar.cs` المُعدَّل مُترجَم فعلاً.

### 3.3 ✅ سلامة UTF-8

```
UTF-8 STRICT DECODE: OK (file is valid UTF-8)
BOM?: 75 73 69  (لا يوجد BOM — كما كان)
```

.codepoints لسطر 31-as-new: `U+0644 U+0644 U+0631 U+0643 U+0628` = «للركوب» سليمة.
الملف UTF-8 سليم — المشوّش في الكونسول cp1256 فقط، كما نبّهت T3.

---

## 4. هل السيارة تتحرك الآن؟

**UNVERIFIED: لم أشغّل playmode** — المحرر مشغول/معلّق على فحص ProBuilder منذ 35 دقيقة،
و`-batchmode` لا يصل للكمبايل بسبب القفل. **لا أدّعي أنها تتحرك.**

ما أستطيع إثباته بالكمبايل فقط: **0 `error CS` في المشروع كله**، و**مسارات الانهيار `NullReferenceException` مُزالة** (§2).
الحركة نفسها تحتاج ضغط arrows/WASD في playmode — وهذا معيار رقم 1 في المهمة [4].

---

## 5. ما يحتاجه رقم 1

1. **يُغلق** `Unity.exe` PID 11540 (معلّق على ProBuilder).
2. يُشغّل أمر T3 كما هو → يجمع `0 error CS` من يونيتي نفسه.
3. يدخل playmode على `Core.unity` → `W`/السهم الأمامي → تأكيد أن `Kart` تتحرك و`FollowCam` يتبعها.

⚠️ إن بقي `driveDirect` بلا لاعب سؤالاً: الإصلاح يعمل حتى لو صُفّر `driveDirect`،
لأن `player == null` وحده يشغّل القيادة المباشرة (`SimpleCar.cs:40`). متى يُضاف كائن
بوسم `Player`، يعود السلوك الأصلي للركوب/النزول بـ`E` طبيعياً.

---

## 6. Warning outside my box — for agent 1

After finishing I listed every file under `Assets/` and `ProjectSettings/`
modified after 7:00 PM. Three files changed and **none of them are mine**:

| File | Time | Mine? |
|---|---|---|
| `Assets/_Game/Scripts/MainMenu.cs` | 7:35:45 PM | **No** |
| `ProjectSettings/Packages/com.unity.ai.assistant/Settings.json` | 7:32:08 PM | **No** — likely Unity PID 11540 during the ProBuilder pass |
| `ProjectSettings/Packages/com.unity.probuilder/Settings.json` | 7:32:34 PM | **No** — likely Unity PID 11540 during the ProBuilder pass |

The only file I edited anywhere in this project is
`Assets/_Game/Scripts/SimpleCar.cs`.

`ops/roster/handoff/` contains `fleet-A..F-20260927-192856.md`, which means other
agents were running at the same time. So the 7:35 PM change to `MainMenu.cs`
most likely came from one of them. Note that task `[2.1]` in `YOUR_TURN` is a
read-only diagnosis of `MainMenu.cs` and explicitly says "do not modify" —
so whoever wrote to that file at 7:35 PM was outside that instruction.
**Agent 1 should check it.**
