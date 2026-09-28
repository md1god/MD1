# 🌍 CC GAME — The Gatekeeper's Heir

> **المشروع:** `D:\DiDo111_CC-Game`
> **المحرك:** Unity 6000.0.84f1 · URP
> **آخر تحديث:** 2026-09-27
> **الحالة:** العالم مبني ومُوزَّع — جاهز للاختبار

---

## 📖 القصة

**نظام 7 فصول** — وريث هرم الهرم **يوسف** مع **روز**، في 7 بوابات نجمية.

| # | الفصل | المكان | المشاهد |
|---|-------|--------|---------|
| ١ | **الهبوط** — آدم يهبط كذكر عنكبوت | الجيزة | `Chunk_3_9`, `Chunk_2_9` |
| ٢ | **البئر** — يسقطان في بئر تحت الهرم | الجيزة | `Chunk_2_9`, `Chunk_1_8` |
| ٣ | **السرداب** — 7 أبواب في غرفة دائرية | تحت الأرض | `Chunk_1_8`, `Chunk_0_8` |
| ٤ | **الفحم** — أول حضارة | الغابة | `Chunk_1_1`, `Chunk_1_2` |
| ٥ | **البرونز** — مدينة نيون | المدينة | `Chunk_5_5`, `Chunk_5_4` |
| ٦ | **الديناصور** — وادي التريكس | الوادي | `Chunk_2_1`, `Chunk_2_2` |
| ٧ | **الرومان** — أطلال في الصحراء | الصحراء | `Chunk_2_8`, `Chunk_2_7` |
| ٨ | **الآلة** — مدينة بلا|population | المدينة | `Chunk_5_6`, `Chunk_4_6` |
| ٩ | **النووي** — مدينة محطمة، لوح مذاكرة | المدينة | `Chunk_4_5`, `Chunk_4_4` |
| ١٠ | **المريخ** — قاعدة فضائية + طائرة مشرية + جنود | الفضاء | `Chunk_0_8`, `Chunk_0_9` |

**الخاتمة:** البوابة الثامنة (الفضاء) تُفتح. "الأرض ليست من Kurt" — قالها آدم لروز.

📄 **التفصيل الكامل:** `ops/world-rebuild/STORY.md`

---

## 🗺️ العالم

```
العالم: 11,000 × 11,000 وحدة  (شبكة 11×11 = 121 مقطع)
مقاس المقطع: 1,000 × 1,000 وحدة
chunkSize في المانيفست: 1000
```

| Biome | الموقع | الأصول |
|-------|--------|--------|
| **Sea** | الإطار الخارجي | قوارب + صخور |
| **Forest** | Z = −20000…−5000 | Pine, Conifer, Cypress + صخور |
| **City** | Z = −5000…+10000 | 138 SimplePoly + شوارع + إنارة |
| **Desert** | Z = +10000…+20000 | كثبان + صخور |
| **Alien** | X = +15000…+20000 | SciFi Kit + ميكات + جنود |

**المدينة فيها شوارع حقيقية:** `Road Lane_01` + `Road Intersection_01` + `Road Sidewalk` + `Props_Street Light` (كل 120 م)

**لا يوجد مرجع معطوب** — 0 Broken PPtr في كل المشاهد.

---

## 🚗 السيارة

```
المقاس: 2.09 × 1.55 × 4.64 متر   ← مكلارين 650S حقيقي
اللون : برتقالي Papaya (50 خامة)
الفيزياء: Rigidbody + 4 WheelColliders + تعليق + تعادم هوائي + Anti-roll
```

**المفاتيح:** `WASD` أو الأسهم = قيادة · `Space` = فرامل يد · `E` = ركوب/نزول

---

## 🎬 المشاهد

```
Assets/Scenes/
├── Menu/MainMenu.unity      ← [0] البداية — زر PLAY
├── Core/Core.unity          ← [1] McLaren (5 كائنات فقط)
└── World/Chunk_X_Y_*.unity  ← [2..122] 121 مقطع
```

**Build Settings = 123 مشهد** (القائمة أولاً، ثم السيارة، ثم العالم)

---

## ✅ ما تم إنجازه

| # | المهمة | النتيجة |
|---|--------|---------|
| 1 | تقسيم `SampleScene` (371MB) | ✅ **121 مشهد** + Core |
| 2 | تصحيح المقياس | ✅ 50,000 → **11,000** (كثافة ×25) |
| 3 | حفظ كل الأصول | ✅ **124,237** prefab — لا شيء ضائع |
| 4 | الكاميرا + الإضاءة | ✅ داخل كل مشهد (لون حسب الـbiome) |
| 5 | إصلاح 31,869 ضوء | ✅ 15 prefab `_nolight` |
| 6 | تنظيف Core | ✅ 10 كائنات فوضى → `_parked/` |
| 7 | السيارة | ✅ فيزياء + لون + 4.64 م |
| 8 | نسج السيارة | ✅ 47 نسيجة نُقلت |
| 9 | تخفيف التكرار | ✅ 1,000 → ~56 لكل مقطع |
| 10 | أصول Biome | ✅ كل مقطعbiome صحيح |
| 11 | شوارع المدينة | ✅ شبكة متصلة 1,112 قطعة |
| 12 | Build Settings | ✅ 123 مشهد، Menu أولاً |
| 13 | فحص المراجع | ✅ **0 مرجع معطوب** |
| 14 | مجلد نظيف | ✅ `D:\CC_GAME_1` |

---

## 🔄 ما سيُنفّذ

| # | المهمة | الأولوية |
|---|--------|----------|
| 1 | **أصول القصة** (تريكس، طائرة، جنود، شخصيات) | 🔴 جارٍ |
| 2 | **ماء OptiWater** بدل Plane رمادي | 🔴 |
| 3 | **مشهد الانترو** (فيديو 76MB + موسيقى) | 🔴 |
| 4 | **نتيجة حقيقية عند التشغيل** (السيارة تتحرك) | 🔴 |
| 5 | أنيميشن الشخصيات | 🟡 |
| 6 | AI للسيارات والمراكب والسفن | 🟡 |
| 7 | نسيج الأرض (بدل Plane) | 🟡 |
| 8 | غرفة البوابات السبعة | 🟡 |
| 9 | دمج `Unity111` (40 سكربت) | 🟢 |

---

## 🛠️ الأدوات

كل أدواتي في **`ops/world-rebuild/tools/`**:

| الأداة | الوظيفة |
|--------|---------|
| `rebuild-world-121.ps1` | يولّد 121 مشهداً من المصدر (بمقياس 0.2) |
| `SceneFillerEditor.FillAll` | يوزّع أصول الـbiome + الشوارع (121 دفعة) |
| `StoryAssets.Run` | يضع أصول القصة في مكامنها |
| `FinalizeGame.Run` | ينظّف Core + سيارة + Build Settings |
| `Look.Capture` | يصوّر أي مشهد للتحقق |
| `SceneValidator.Run` | يفحص كل المشاهد |

**⚠️ تحذير مهم:** `rebuild-world-121.ps1` **يعيد كتابة `Core.unity` من المصدر** — بعد تشغيله شغّل `FinalizeGame.Run` دائماً.

---

## 🚀 التشغيل

```
افتح:  D:\DiDo111_CC-Game
مشهد البداية:  Assets\Scenes\Menu\MainMenu.unity
اضغط Play
```

**أو مباشرة:** `Core\Core.unity` → Play (تتجاوز القائمة)

**المفاتيح:** WASD · Space (فرامل يد) · E (ركوب)

---

## 📁 الملفات المهمة

| الملف | المحتوى |
|-------|---------|
| `ops/world-rebuild/README.md` | تقرير العمل التفصيلي |
| `ops/world-rebuild/STORY.md` | القصة كاملة (10 فصول) |
| `ops/world-rebuild/reports/` | تقارير الفحص |
| `Assets/_Game/Resources/WorldManifest.txt` | مانيفست 121 مقطع |
| `.opencode/reports/look/` | لقطات التحقق |

---

## 🗑️ محذوف (لا يُستخدم)

| | |
|---|---|
| `SampleScene.unity` (371MB) | استُخرج منه كل شيء |
| `_Game/World/Chunks/` | 9 مشاهد مكسورة |
| `_Game/World/Streams/` | 11 ملف ميتة |
| `Core/_parked/` | فوضى الوكيل السابق |
| `Library` / `Temp` / `Logs` | تُبنى تلقائياً |

---

## 🆕 تحديث 2026-09-27 — متحكم الأركيد + فحص الكمبايل

### 🤖 الرستر النهائي — 5 وكلاء، 4 يعملون، صفر تكلفة

اختُبر **13** موديلاً على هذا الجهاز. **4** فقط يردّون مجاناً.
الاسم في القائمة لا يعني أنه يعمل — والدليل في `ops/roster/probe_results.json`.

| # | الاسم | الموديل | التكلفة | الاختبار | الدور |
|---|-------|---------|---------|---------|-------|
| **1** | Big Pickle | `opencode/big-pickle` | $0 | ✅ 8.6s | **المدير — القرار النهائي** |
| **2** | Space Bunny | `opencode/space-bunny-free` | $0 | ✅ 9.7s | البرمجة — سكربتات اللعبة |
| **3** | Muse Spark | `opencode-go/muse-spark-1.3-contributor` | $0 | 🔒 موقوف | المدقق |
| **4** | Ling Flash | `opencode/ling-3.0-flash-fin-free` | $0 | ✅ 5.4s | **موزّع الأصول** |
| **5** | Longcat | `opencode/longcat-2.5-preview-free` | $0 | ✅ 13.5s | المدقّق النهائي — بناء واختبار |

**رقم 3 موقوف — والسبب ليس في الكود:**
```
Upstream: This Go model trains on request data.
          Allow paid endpoints that train on request data in [Privacy settings]
```
فُحص كل مفاتيح `https://opencode.ai/config.json` — **لا يوجد مفتاح Privacy**.
التبديل في واجهة تطبيق OpenCode فقط. جُرّب `muse-spark-1.2` و`1.3` على
`opencode` و`opencode-go` — الأربعة موقوفة. **رقم 3 محجوز، لا يُعطى لغيره.**

**حظر الموديلات المدفوعة — مُفعَّل برمجياً.** 17 موديلاً في القائمة السوداء
و`dispatch.py` يرفضها. الاختبار:
```
$ python dispatch.py 3-test --probe
REFUSED: model 'opencode-go/muse-spark-1.3-contributor' is on the banned list.
```

**فصل الملكية يمنع التداخل:** `ops/distribution/` لرقم 4 · `ops/verify/` لرقم 5 ·
`Assets/_Game/Scripts/` لرقم 2 · `ProjectSettings/` و`Assets/Scenes/` لرقم 1.
و`DISPATCH.lock` يمنع تشغيل وكيلين معاً.

**ملف العقد:** `YOUR_TURN` — مهمة كل وكيل مكتوبة فيه، ومخرجاته تُكتب فيه.

### 📋 النطاق — شيء واحد فقط

```
توزيع الأصول → عالم مفتوح → سيارة أتحرك بها → نختبر → نشاهد
```
أي اقتراح يخرج عن هذا المسار يُكتب في `IDEA.md` ويُرفض.

### ما صُنع

| # | العمل | التفصيل |
|---|-------|---------|
| 1 | فحص وإصلاح مجلد تجريبي قديم | `C:\Users\DiDo\Desktop\New Unity Project` — كان فيه `PlayerKart.cs` مكسور (خطأ كمبايلر `speedFactor` + منطق `trackLayer` يمنع الحركة + قيم `ForceMode` خاطئة). أُصلح كله وتحوّل لـ `linearVelocity/linearDamping` بدون تحذيرات |
| 2 | نقل المفيد فقط إلى `D:\CC_GAME_1` | `Assets/_Game/Scripts/PlayerKart.cs` — متحكم أركيد (درفت/بوست) لا مثيل له هنا (`CarPhysics` واقعية، و`SimpleCar` تحريك Transform). أُضيف له قراءة Input مزدوجة (legacy + لوحة مفاتيح Input System) لأن المشروع يعمل بنظام Input الجديد فقط (`activeInputHandler: 2`) |
| 3 | ما لم يُنقل عن قصد | `MainSceneBuilder.cs` و`KartFollowCamera.cs` — مكرران لما هو موجود وأفضل (`PhysicsSetup` + `CarTextureLinker` + `FollowCam`) |
| 4 | فحص كمبايل كامل (batchmode) | `Tundra build success` + `return code 0` — **صفر `error CS`** على يونيتي `6000.0.84f1` |
| 5 | فتح المحرر | `D:\CC_GAME_1` يعمل الآن في محرر يونيتي، جاهز لـ Play من `Core/Core.unity` |

### استخدام PlayerKart (بديل أركيد لـ CarPhysics)

> ⚠️ واحد فقط على الـ `Kart` — لا تجمع `PlayerKart` و`CarPhysics` معاً.

```
1. أوقف Play
2. عطّل CarPhysics على الـ Kart
3. أضف PlayerKart (من Assets/_Game/Scripts)
4. Play
```

**المفاتيح:** `WASD`/أسهم = قيادة · `Shift`/`Space` = درفت (اشحن ثم أفلت للبوست) · الرجوع تلقائي بالفرامل ثمcliff

---

## 🆕 تحديث 2026-09-27 (٢) — إصلاح الـBuild + نظام الوكلاء

> **إضافة فقط.** لم يُحذف من هذا الملف أي سطر سابق.

### 🔴 إصلاح عطل التشغيل — السبب الجذري

المشكلة التي جعلت اللعبة "غير مضبوطة" عند التشغيل كانت هنا بالضبط، وليست في الأصول:

```
قبل:  build entries 12   |  resolved 1   |  DANGLING 11   |  world in build 0/121
بعد:  build entries 122  |  resolved 122 |  DANGLING 0    |  world in build 121/121
```

**ما كان يحدث:** `ProjectSettings/EditorBuildSettings.asset` كان يشير إلى 11 ملف
`Scene_01_Sea_South.unity` … `Scene_11_Sea_North.unity` — **ملفات محذوفة**. الـGUIDs
موجودة، الملفات لا. وفي مكانها على القرص 121 مشهد `Chunk_X_Y_<Biome>.unity` لم
يكن أيٌّ منها في الـbuild.

**النتيجة:** `ChunkSceneManager.cs:26` يستدعي
`SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive)`
وهذا **لا يجد المشهد إلا إذا كان في الـbuild**. صفر مقطع ⇒ العالم ما ينزل أبداً
⇒ شاشة فارغة مهما كان الموجود على القرص.

**الإصلاح:** `ops/roster/fix_build_settings.py` يعيد بناء القائمة من المشاهد
الموجودة فعلياً، ويحفظ GUIDs من ملفات `.meta` نفسها، ويضع `Chunk_0_5` في
الفهرس 1 لأنه المقطع الذي `WorldManager.cs:36` يحمّله عند البدء.

**الدليل:**
```powershell
python ops\roster\verify_build_scenes.py     # exit 0
```
**النسخة الاحتياطية:** `ops/roster/EditorBuildSettings.asset.bak`

### 🤖 نظام الوكلاء

| # | الوكيل | الموديل | التكلفة | الدور |
|---|--------|---------|---------|-------|
| **1** | Big Pickle (المدير) | `opencode/big-pickle` | $0 | القرار النهائي · بنية المشروع |
| **2** | Space Bunny | `opencode/space-bunny-free` | $0 | البرمجة · السكربتات |
| **3** | Muse Spark | `opencode-go/muse-spark-1.3-contributor` | $0 | التدقيق · المراجعة |

**حظر الموديلات المدفوعة:** 11 موديل في قائمة سوداء داخل
`ops/roster/agents.json`، و`dispatch.py` يرفضها برمجياً حتى لو أُضيفت بالخطأ
(تم اختبار الرفض: `REFUSED: agent 'SNEAK' has costPerRun=5`).

**منع التداخل:** تقسيم ملكية صارم للملفات + `DISPATCH.lock` يمنع تشغيل وكيلين
على نفس الملف في نفس الوقت — وهو ما يكسر أزواج `.cs`/`.meta` في يونيتي.

**الملفات المحورية الجديدة:**

| الملف | الغرض |
|-------|-------|
| `AGENTS.md` | قوانين الفريق + الأدوار + تقسيم الملكية |
| `YOUR_TURN` | مهمة كل وكيل — العقد بيني وبينهم |
| `IDEA.md` | صندوق أفكار مشترك، رقم 1 يقرّر |
| `PLAN.md` | خطة العمل المرحلية |

**مشغّل الوكلاء:**
```powershell
cd D:\CC_GAME_1\ops\roster
python dispatch.py --list
python dispatch.py space-bunny tasks/T2.md --timeout 900
```

### ⚠️ بند مزيّف يجب تصحيحه

البند «Build Settings ✅ 123 مشهد، Menu أولاً» في أعلى هذا الملف **غير صحيح** —
كان 12 مدخلاً منها 11 معلّقة، و`Assets/Scenes/Menu/MainMenu.unity` **غير موجود**.
تم إصلاح الـbuild فعلياً إلى 122 مدخلاً. مصحح `MainMenu.cs:71` الذي ينادي
`SceneManager.LoadScene("GameScene")` — وهو اسم غير موجود — ما زال مفتوحاً
(المهمة 1.1 في `PLAN.md`). السطر الأصلي لم يُحذف؛ التصحيح Quirks موثّق هنا.

### 🧪 اختبار الوكيل رقم 3 (probe) — 2026-09-27

❌ فشل: `dispatch.py muse-spark --probe` لم يطبع `PROBE-OK` (28.2s، بدون timeout، وبدون stdout).
السبب عائق خصوصية وليس كوداً: الموديل يتدرّب على بيانات الطلبات ويحتاج تفعيل endpoints مدفوعة في Privacy settings.
الدليل: `ops/roster/handoff/muse-spark-probe-20260927-174612.md`. النتيجة مسجّلة أيضاً في مربّع رقم 3 داخل `YOUR_TURN`. بانتظار قرار رقم 1 قبل أي مهمة جديدة.



## ✅ اختبار القبول: السيارة تتحرك — 2026-09-28

**الحالة السابقة:** الشاشة الفاضية عند Play (سببان، كلاهما اكتُشف وأُصلح).

### السبب 1 — المجلد لا يترجم
Assets/Editor/CCDriveTest.cs: ثلاثة أخطاء CS
- CS0200 ButtonControl.isPressed تُقرأ فقط (سطران) → الحقن عبر InputSystem.QueueStateEvent + KeyboardState.Set(Key.W, ...) بدل الكتابة في الخاصية.
- CS0103 speed غير معرّف → sc_speed().
- النتيجة: المشروع كله كان يتعطل عن الترجمة → Play mode لا يدخل أبداً → شاشة فاضية.

### السبب 2 — انهيار GPU عند الإقلاع (الجهاز الثقيل)
- d3d11: failed to create swap chain [0x887a0005] = DXGI_ERROR_DEVICE_REMOVED على Intel HD 4600.
- انهيار ثانٍ في ScriptableRenderContext.Cull (exit 0xC000041D).
- **السبب الجذري:** ops/guard/memguard.py بحد 3.6GB على الجوب كله (Unity + AssetImportWorker + مشغّلات الشادر كلهم أولاد يرثون الحد) — GPU يتشارك الرام فسقف الذاكرة جوّعه. **إطلاق Unity بدون قيد الحارس يعمل** (الـpagefile على D: هو الوقاية الحقيقية وهو موجود أصلاً).

### الكاميرا
- مؤكد من الملف: FollowCam موصولة بالسيارة (Core.unity:7718 target = الـKart). "الكاميرا الفاضية" كان وهم الـPlay الذي لا يعمل.
- حسّنت نقطة البداية: الكاميرا صارت (0,3,-3) تنظر للسيارة (كانت (8,4,-5) تحدق في مبنى أحمر) — Core.unity:7655-7656.

### الدليل الحقيقي (من داخل الـEditor الحي)
ops/shots/real/drive.txt:
- kart BEFORE pos=(0.000, 0.000, 4.000) → AFTER pos=(0.000, 0.000, 153.285)
- *** MOVED 149.285 m in ~210 frames *** => THE CAR DRIVES
- FollowCam تبعت السيارة: (0,3,-3) → (0,3,143.42)
- CAPTURE 05_playmode_after_driving.png 267992b colours=510 mean=(133,174,146) HAS CONTENT
- تحقق مستقل: PNG 1280×720، 146 لوناً، سماء 25% + غابة + أرض (ليست سماة مسطحة).
- نسخة على السطح: C:\Users\DiDo\Desktop\CC_SHOTS\4_playmode_driving.png


---

## 2026-09-28 — شكوى اللاعب (سماء فقط + W لا يعمل) أُصلحت وأُثبتت بالقياس

- السبب: محرر اللاعب القديم كان يحمل المشهد في ذاكرته قبل تصحيح اتجاه الكاميرا
  (كانت الكاميرا تنظر لأعلى -16.7° بسبب خطأ إشارة مكتوب سابقا) + دخول Play أثناء استيراد
  غير مستقر (سجل داخلي: MOVED 0.000 m في جلسة 16:08) + ضرورة النقر داخل نافذة Game
  قبل الضغط على W (سلوك Unity القياسي).
- الإصلاحات (كلها على القرص، المحرر الحالي حمّلها):
  * Core.unity: كاميرا تنظر للأسفل 16.7° نحو السيارة (m_LocalRotation x=0.145220,
    نسخة: Core.unity.bak-cam3)
  * FollowCam.cs:12-16: شفاء ذاتي — إن كان الهدف فارغاً يبحث عن GameObject "Kart"
  * CCVerify.cs: أداة قياس (أول إطار + قيادة) — مفتاح الجلسة CCVerify2
  * CCDriveTest.cs: معطّل نهائياً (انتهى دوره)
- القياس الحقيقي (verify.txt):
  * أول إطار: CENTER-RAY -> Kart dist=6.6 (السيارة بمركز الشاشة) + كاميرا تنظر لأسفل
  * W: kart BEFORE (0,0,4.0) -> AFTER (0,0,164.4) = MOVED 160.380 m => W يقود
  * الكاميرا تتبع أثناء القيادة: z 3 -> 154.03
- لقطات مثبتة: 06/07/08_verify_*.png (كلها HAS CONTENT) على القرص وفي
  C:\Users\DiDo\Desktop\CC_SHOTS\ (5_playmode_verified_driving.png)
- الوكلاء: #2 Space Bunny أصلح FollowCam (ترجمة Roslyn exit 0)،
  #4 Ling Flash أكد توزيع الأصول 121/121 (0 ناقص)، #5 Longcat يراجع نهائياً.
- الجهاز: رام حرة ~4.2GB بعد الإغلاقات، صفحة ملف D: مُثبّتة 24GB (حارس التجمد)،
  الترخيص أُصلح بإعادة تشغيل عميل التراخيص (كان الإقلاع عالقاً في "Opening project").
