# تقرير الوكيل C — إصلاح أسماء المشاهد

**التاريخ:** 2026-09-27
**الوكيل:** C
**الملكية:** `Assets/_Game/Scripts/MainMenu.cs` · `Assets/_Game/Scripts/ChunkSceneManager.cs`

---

## 1) هل `MainMenu` مربوط بأي مشهد؟

**الجواب: لا.**

GUID الخاص بـ `MainMenu.cs`: `a4034645b0d6b364bacfe4261f660c52`

البحث في كل `*.unity` تحت `Assets/Scenes/`:

```
=== Searching for MainMenu GUID: a4034645b0d6b364bacfe4261f660c52 ===
  NOT FOUND in any .unity file under Assets/Scenes/
```

**الاستنتاج:** `MainMenu` كود ميت، غير مستخدم في أي مشهد. `Assets/Scenes/Menu/MainMenu.unity` **غير موجود** على القرص.

---

## 2) كل نداءات SceneManager في الملفين المملوكين

| file:line | اسم المشهد | موجود على القرص؟ |
|---|---|---|
| `MainMenu.cs:71` | `"Core"` | ✅ نعم — `Assets/Scenes/Core/Core.unity` (build index 0) |
| `MainMenu.cs:77` | `"Core"` | ✅ نعم — `Assets/Scenes/Core/Core.unity` (build index 0) |
| `ChunkSceneManager.cs:26` | `path` (ديناميكي من `coordToPath`) | ✅ يعتمد على `ChunkData.scenePath` |
| `ChunkSceneManager.cs:45` | `path` (ديناميكي) | ✅ يعتمد على `ChunkData.scenePath` |
| `ChunkSceneManager.cs:72` | `path` (ديناميكي) | ✅ يعتمد على `ChunkData.scenePath` |

---

## 3) الإصلاح

**لا إصلاح مطلوب.**

المهمة وصفت أن `MainMenu.cs:71` و `:77` يستدعيان `SceneManager.LoadScene("GameScene")`. **هذا غير صحيح.** الملف الحالي يحتوي بالفعل على:

```csharp
// MainMenu.cs:71
SceneManager.LoadScene("Core"); // Main gameplay scene

// MainMenu.cs:77
SceneManager.LoadScene("Core");
```

الملف يشير بالفعل إلى `"Core"` — وهو اسم المشهد الصحيح (build index 0). **لم أعدّل أي سطر.**

---

## 4) التحقق

### Unity batchmode compile

**BATCHMODE BLOCKED BY EDITOR LOCK**

```
HandleProjectAlreadyOpenInAnotherInstance
```

محرر يونيتي مفتوح حالياً. `-batchmode` فشل بقفل المشروع. لم يتم إنتاج لوغ تجميع صالح.

### فحص يدوي للمشاهد

```
Core.unity exists: True
MainMenu.unity exists: False
EditorBuildSettings.asset: 376 lines
Scene entries: 122
  Assets/Scenes/Core/Core.unity          ← build index 0
  Assets/Scenes/World/Chunk_0_5_Sea.unity
  Assets/Scenes/World/Chunk_0_0_Sea.unity
  ...
```

---

## 5) ملخص

| البند | الحالة |
|---|---|
| `MainMenu` مربوط بمشهد؟ | ❌ لا — كود ميت |
| `GameScene` موجود؟ | ❌ لا — لكن لا أحد يستدعيه |
| `Core` موجود؟ | ✅ نعم — build index 0 |
| `MainMenu.cs:71` يشير إلى | ✅ `"Core"` (صحيح) |
| `MainMenu.cs:77` يشير إلى | ✅ `"Core"` (صحيح) |
| تعديلات قمت بها | **لا شيء** — الملف سليم أصلاً |
| تجميع Unity | ⚠️ BATCHMODE BLOCKED BY EDITOR LOCK |

---

## 6) توصية

`MainMenu.cs` كود ميت غير مربوط بأي مشهد. إن كان المطلوب إنشاء قائمة رئيسية فعلية، هذا يتطلب مشهداً جديداً — **خارج نطاق هذه المهمة**. يُوثّق هنا فقط.

**الوكيل C توقّف.**
