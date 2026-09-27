# A — أرِني اللعبة الحقيقية

**أنت الوكيل A. مهمتك الوحيدة: التقط صورة فعلية للعبة، ووصف ما تراه بصدق.**

**تملك:** `ops/shots/` فقط. ممنوع تعديل أي ملف آخر.

---

## المشكلة

صاحب المشروع يقول:

> «الكاميرا فتحت في البداية على البحر، شخصيات واقفة بجوار المراكب في الماء
> بدون حركة، ولا توجد سيارة أصلاً»

و还说: «ما أعرف شكل اللعبة الحقيقي، لأن ما ضبطت».

**ما أحد شغّل اللعبة فعلياً في هذه الجلسة.** كل الكلام السابق كان فحص ملفات
على القرص. الآن شغّلها.

## الخطوات

### 1) أغلق محرر يونيتي

```powershell
Get-Process Unity -ErrorAction SilentlyContinue | ForEach-Object {
  "killing Unity PID $($_.Id)"; Stop-Process -Id $_.Id -Force
}
```

المحرر يحمل قفل المشروع، وبدون إغلاقه يفشل أي `-batchmode`
(تأكدت: `HandleProjectAlreadyOpenInAnotherInstance`).

### 2) صوّر مشهد البداية

اكتب سكربت C# واحد في `ops/shots/Shoot.cs` (مجلدك، آمن)، ثم شغّله:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.0.84f1\Editor\Unity.exe" `
  -batchmode -quit -projectPath D:\CC_GAME_1 `
  -executeMethod Shoot.Run -logFile D:\CC_GAME_1\ops\shots\shoot.log
```

السكربت لازم:
1. يفتح `Assets/Scenes/Core/Core.unity`
2. يدخل Play Mode، ينتظر 5 ثواني
3. يلتقط صورة من **Game view** → `ops/shots/core_play.png`
4. يخرج من Play، ينتظر ثانية، يلتقط **Scene view** → `ops/shots/core_scene.png`
5. يطبع في اللوغ: عدد GameObjects، ومواقع كل GameObject باسم `Kart`
   (أو أي شيء فيه `Car`/`Kart`/`Vehicle`)

### 3) صوّر 3 مقاطع من العالم

افتح وكرّر لأسماء:
`Chunk_0_5_Sea` · `Chunk_5_5_City` · `Chunk_3_6_Forest`
(تأكد من الأسماء الحقيقية أولاً: `python -c "import os,glob;print(glob.glob(r'D:\CC_GAME_1\Assets\Scenes\World\*.unity')[:3])"`)

حفظ في `ops/shots/chunk_<name>.png`

### 4) أعد فتح المحرر

```powershell
Start-Process "C:\Program Files\Unity\Hub\Editor\6000.0.84f1\Editor\Unity.exe" `
  -ArgumentList "-projectPath","D:\CC_GAME_1"
```

## التقرير — هذا أهم جزء

في `ops/roster/handoff/`، اكتب بصراحة مطلقة:

| السؤال | أجب بنعم/لا + دليل |
|---|---|
| هل **`Kart`** موجود في المشهد فعلاً عند تشغيل اللعبة؟ | |
| هل له **MeshRenderer** ظاهر؟ كم واحداً؟ | |
| أين الكاميرا في أول إطار: إحداثياتها بالضبط؟ | |
| هل الكاميرا **تتبع** Kart أم واقفة؟ | |
| هل الشاشة **سوداء**؟ هل فيها ماء؟ هل فيها أرض؟ | |
| كم GameObject ظهر في اللقطة فعلياً؟ | |

**إذا ما قدرت تشغّل ولا مرة → اكتب `FAILED: <السبب>` بالضبط. لا تخمّن.
لا تكتب "يبدو أن". أرقام أو فشل صريح.**

أرفق: مسار الصور، وآخر 20 سطر من `ops/shots/shoot.log`.

لما تخلّص: **توقّف.**
