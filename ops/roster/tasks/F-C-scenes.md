# C — أصلح أسماء المشاهد المكسورة

**أنت الوكيل C. تملك ملفين بالضبط في `Assets/_Game/Scripts/`:**
`MainMenu.cs` · `ChunkSceneManager.cs`
**ممنوع** لمس أي ملف آخر — **`Core.unity` ممنوع منعاً باتاً** (الوكيل A يقرؤه الآن).

---

## العطل المؤكد

`MainMenu.cs:71` و `MainMenu.cs:77` يستدعيان:
```csharp
SceneManager.LoadScene("GameScene")
```

**`GameScene` غير موجود.** المشاهد الفعلية على القرص:
- `Assets/Scenes/Core/Core.unity` ← build index 0
- `Assets/Scenes/World/Chunk_X_Y_<Biome>.unity` ← 121 مقطع

وهذه ليست مخmansات: `ProjectSettings/EditorBuildSettings.asset` فيه 122 مدخلاً،
و 0 GUID معلّق (تحققت يدوياً).

**والأخطر:** `Assets/Scenes/Menu/MainMenu.unity` **غير موجود**، مع أن
`README.md` يدّعي أنه الفهرس 0. إما التز aching كاذب، أو المشهد حُذف.

## المطلوب

### 1) ابحث عن `MainMenu` — هل هو مربوط بأي مشهد؟

بايثون (لا PowerShell — الشجرة 12GB و`Get-ChildItem -Recurse` يفقد ملفات):

```python
import os, re, pathlib
root = pathlib.Path(r"D:\CC_GAME_1")
# 1) guid of MainMenu.cs
mm = (root/"Assets/_Game/Scripts/MainMenu.cs").read_text(encoding="utf-8")
```
اقرأ `.meta` 옆 `MainMenu.cs` → خذ الـGUID. ثم ابحث في **كل** `*.unity` و
`*.prefab` عن ذلك الـGUID.

- **إن وُجد** → أعطِ: الملف ورقم السطر.
- **إن لم يوجد** → الجواب: **`MainMenu` كود ميت، غير مستخدم في أي مشهد.**

### 2) كل نداءات SceneManager في المشروع

```python
import os, re
pat = re.compile(r"SceneManager\.(LoadScene\w*|SetActiveScene)\s*\(([^)]*)")
for dp, dn, fn in os.walk(r"D:\CC_GAME_1\Assets\_Game"):
    for f in fn:
        if f.endswith(".cs"):
            p = os.path.join(dp, f)
            for i, line in enumerate(open(p, encoding="utf-8", errors="replace"), 1):
                if "SceneManager." in line:
                    print(f"{p}:{i}: {line.strip()}")
```

لكل واحد اعمل جدول:

| file:line | اسم المشهد | موجود على القرص؟ |
|---|---|---|

### 3) الإصلاح — الأصغر الممكن

- إن كان `GameScene` هو المقصود → غيّره إلى **`"Core"`** (هذا اسم build
  index 0، و Unity يقبل الاسم بلا مسار).
- **لا تنشئ** مشهداً جديداً. **لا تنشئ** `Assets/Scenes/Menu/`.
- إن كان `MainMenu` غير مربوط بأي مشهد → لا تفعل شيئاً حيال ذلك، بلوثه بتقرير.

### 4) تحقّق صريح

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.0.84f1\Editor\Unity.exe" `
  -batchmode -quit -projectPath D:\CC_GAME_1 `
  -logFile D:\CC_GAME_1\ops\verify\compile_C.log
```

⚠️ **محرر يونيتي مفتوح الآن.** `-batchmode` **سيفشل** بقفل المشروع.
جرّب. إن فشل، اكتب `BATCHMODE BLOCKED BY EDITOR LOCK` بصراحة، وتحقق بـ
`-quit -batchmode` على مجلد **نسخة** إن أردت. **لا تقرّر أنها تعمل بلا دليل.**

شرط القبول: **0 `error CS`** في اللوغ، والنص الحرفي thereof.

## التقرير

في `ops/roster/handoff/`: كل سطر عدّلته بـ`file:line`، + جدول النداءات،
+ نص اللوغ. **لا تدّعِ نجاحاً لم تتحقق منه.**

لما تخلّص: **توقّف.**
