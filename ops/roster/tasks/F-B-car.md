# B — تدقيق السيارة: لماذا "ما فيه سيارة"

**أنت الوكيل B. قراءة فقط. ممنوع تعديل أي ملف في `Assets/`.**
**تملك:** `ops/reports/car.md` فقط.

---

## لماذا هذه المهمة

المشهد `Assets/Scenes/Core/Core.unity` فيه GameObject اسمه `Kart` عليه
`SimpleCar` + BoxCollider + **54 ملف GUID واحد** في `m_Children`.

صاحب المشروع يقول **«لا توجد سيارة أصلاً»**.

إذاً إمّا الكارت غير مرئي، أو الموديل لم يُستورد، أو出资 مضاعفة.

## المطلوب — أرقام، لا تخمين

### أ) الموديل: من أين جاء الـ54 renderer؟

في `Core.unity`، الـ54 طفل تحت `Kart`. لكل طفل:
- ما اسم الـGameObject؟
- ما الـ`PrefabInstance` أو `m_Mesh` اللي يشير له؟
- **أهم:** هل يشير أي GUID إلى **prefab خارجي** (مجلد `Assets/`)؟

اكتب بايثون — PowerShell `Get-ChildItem -Recurse` **يفقد ملفات** في شجرة 12GB:

```python
import re, pathlib, os
core = pathlib.Path(r"D:\CC_GAME_1\Assets\Scenes\Core\Core.unity")
t = core.read_text(encoding="utf-8", errors="replace")
# اجمع كل GUIDs المشار إليها
guids = set(re.findall(r"guid: ([0-9a-f]{32})", t))
print("distinct guids in Core.unity:", len(guids))
```

ثم لكل GUID، ابحث في `D:\CC_GAME_1\Assets` عن ملف `.meta` يحمله (باستخدام `os.walk`).
سجّل: **كم GUID يشير لملف موجود، وكم لملف مفقود.**

### ب) الموديل موجود على القرص؟

ابحث بالاسم في `Assets/` عن:
`Vehicle_Car` · `Car_Hatchback` · `kart` · `mclaren` · `*Kart*`

اعطِ: المسار الكامل + الحجم + **هل فيه `.meta`؟**

### ج) الـPrefab: هل هو قابل للفتح؟

إذا الـ54 طفل يشيرون لـprefab واحد في `Assets/`، افتح ذلك الـprefab بـبايثون وعدّ:
كم `MeshRenderer`؟ كم `MeshFilter`؟ **هل `m_IsActive: 0` على أي منها؟**

### د)Layer Occlusion

هل `Kart` أو أي طفل على layer дела؟ شوف `m_Layer` في `Core.unity`
(haste layer 0 = Default). وهل الـCamera `cullingMask` يشمله؟

## التقرير: `ops/reports/car.md`

جدول واحد:

| البند | النتيجة | الدليل (file:line) |
|---|---|---|
| عدد Kids تحت Kart | | |
| GUIDs موجودة / مفقودة | | |
| MeshRenderer فعّال = 0 | | |
| الموديل على القرص | | |
| Layer / cullingMask | | |

**في آخر التقرير، جملة واحدة حاسمة:**

```
VERDICT: <السبب الدقيق لغياب السيارة من الشاشة>
```

إن كانت البيانات لا تكفي للقول → اكتب `VERDICT: INSUFFICIENT DATA` واذكر
ما ينقص بالضبط. **لا تخمّن.**

لما تخلّص: **توقّف.**
