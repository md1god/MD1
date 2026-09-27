# تقرير تدقيق السيارة (الوكيل B — قراءة فقط)

المشهد: `Assets/Scenes/Core/Core.unity` (52205 سطر، 2,061,091 بايت) — حجم/عدد سطور ببايثون `pathlib` (`Core.unity:1` رأس YAML).
الأداة: بايثون عبر `os.walk` وقراءة نص المشهد (ممنوع `Get-ChildItem` وحده).

## الجدول

| البند | النتيجة | الدليل (file:line) |
|---|---|---|
| عدد Kids تحت Kart | **51** (لا 54: 50 mesh + ‏1 Seat فارغ). أسماء `Object_4…Object_102` + `Seat` | `Assets/Scenes/Core/Core.unity:33357` (Kart)، `Core.unity:33392-33443` (قائمة `m_Children` الـ51)، `Core.unity:28229` (Seat)، `Core.unity:6843` (عينة `Object_4`) |
| GUIDs موجودة / مفقودة | **120 مميزاً: 114 موجودة على القرص، 6 "مفقودة" لا تمسّ الكارت**: 2 مدمجة في المحرك (`0000…e`، `0000…f`) + 4 أصول محذوفة خارج الكارت (سكربت على كائن الكاميرا + خامة `m_Shader` + سكربتان). مسح `os.walk` قرأ 7875 ملف `.meta`؛ مجلد `Packages/` فيه ملفان فقط (`manifest.json` + `packages-lock.json`) فلا `.meta` فيه أصلاً | `Core.unity:29,96` (المدمجان)، `Core.unity:7786` (سكربت الكاميرا `a79441…`)، `Core.unity:3398` (الشيدر `933532…`)، `Assets/_Game/Models/kart_mclaren.fbx.meta:2` (GUID الكارت موجود) |
| MeshRenderer فعّال | **50/50 فعّالة** (`m_Enabled: 1`)، 0 معطّلة؛ 50 `MeshFilter` كلها بـ`m_Mesh` صالح (50 `fileID` مميزة)؛ 0 أجسام `m_IsActive: 0` و0 `m_Enabled: 0` في الملف كله | عينة `Core.unity:6843` (`Object_4`)؛ عدّ ببايثون: 100 سطر يحمل GUID الكارت = 50 `m_Mesh` + 50 مادة (`- {fileID…`) |
| الموديل على القرص | **موجود**: `Assets/_Game/Models/kart_mclaren.fbx` (15,034,844 بايت) + `.meta` بنفس الـGUID؛ الـFBX يحوي 50 مصفوفة `Vertices` بإجمالي 151,870 رأساً — يطابق 50 `MeshFilter`؛ 50 خامة مميزة (`fileID` مختلف لكل Renderer) | `Assets/_Game/Models/kart_mclaren.fbx.meta:2` (`guid: 9ea009185059a7d49a46ab3ff6b2863d`)؛ فكّ ضغط مصفوفات الـFBX الثنائية ببايثون (`zlib`) |
| Layer / cullingMask | Kart ‏layer 0 نشط (`m_Layer: 0`، `m_IsActive: 1`)؛ كل الأطفال layer 0؛ الكاميرا الوحيدة `Main Camera` مفعّلة و`cullingMask = 4294967295` (كل الطبقات) وتنظر للكارت مباشرة (زاوية 4.8°، مسافة 12.7م) | `Core.unity:33356` (layer)، `Core.unity:33362` (نشط)، `Core.unity:7697` (Main Camera)، `Core.unity:7750` (culling bits)، `Core.unity:7771` (موضع الكاميرا 8,4,-5)، `Core.unity:33389` (موضع Kart ‏0,0,4) |

## تفاصيل مثبتة (أرقام لا تخمين)

- أ) الـ50 طفل أجسام مشهد عادية: كل `m_PrefabInstance` و`m_PrefabAsset` = `{fileID: 0}` — **لا يوجد Prefab خارجي إطلاقاً** (`Core.unity:6833-6836` عينة)، فلا شيء "مضاعَف/مفقود" من Prefab. كل الإشارات الـ100 تذهب لـGUID واحد `9ea009185059a7d49a46ab3ff6b2863d` وهو `kart_mclaren.fbx` (`Assets/_Game/Models/kart_mclaren.fbx.meta:2`).
- ب) الموديل على القرص: `Assets/_Game/Models/kart_mclaren.fbx` (15,034,844 بايت، مع `.meta`) + مجلد `kart_mclaren.fbm/` (صور PNG) + بدائل غير مستخدمة: `Vehicle_Car*.fbx` في `SimplePoly City` و`Car_Hatchback_*.fbx` في `ZRNAssets` و`2015_mclaren_650s_gt3.glb` (19MB) في `assets/3D` — كلها وُجدت بـ`os.walk` بالاسم.
- ج) لا Prefab ليُفتح (كل المراجع صفر). البديل: الـFBX يحوي 50 مصفوفة `Vertices` بعدد رؤوس إجمالي 151,870 — يطابق 50 `MeshFilter` مميزة `fileID` في المشهد.
- د) الحجم العالمي حقيقي لا صفري: حدود الـFBX 2.09×4.65×1.55 (من فكّ المصفوفات المضغوطة)، والاستيراد `useFileUnits:1` (`kart_mclaren.fbx.meta:50`) ثم مقياس الأطفال 100×: السيارة ≈ 2.1م × 1.6م × 4.6م في العالم — ليست صفرية ولا عملاقة. دوران الأطفال -90°X يطابق تحويل Z-up→Y-up للـFBX.
- الكاميرا: `Main Camera` عند (8,4,-5) (`Core.unity:7771`) واتجاهها نحو Kart بزاوية 4.8° فقط (محسوبة من الكواترنيون `Core.unity:7770`)، near 0.3 / far 1000 (`Core.unity:7740-7743`).
- ملاحظة جانبية (ليست سبب الاختفاء): `BoxCollider` على Kart بحجم (2.29, 5.09, 1.28) (`Core.unity:33446+`) يطابق محاور **الملف قبل الدوران** لا السيارة بعد دوران الأطفال -90°X — أي جدار فيزيائي خاطئ الاتجاه، لكنه لا يخفي الرسوميات.
- سكربت `SimpleCar` موجود: `Assets/_Game/Scripts/SimpleCar.cs` (الـGUID `1f38021226be5f74e84a3fd8de5ce374` في `Core.unity:33372` يطابق `SimpleCar.cs.meta`)، و`seat` يشير لمحوّل `Seat` (`Core.unity:28229`).

```
VERDICT: INSUFFICIENT DATA
```

سبب عدم الجزم: كل بيانات المشهد والقرص تقول إن السيارة **يجب أن تظهر** (موديل موجود، 50 Renderer فعّالة، طبقة مغطاة، كاميرا موجّهة عليها، حجم عالمي طبيعي ≈4.6م، وإحصاء الـGUID مكتمل: كل مراجع الكارت الـ100 موجودة على القرص). لا يوجد في الملفات النصية سبب مرئي للاختفاء. ما ينقص للحسم: فحص حي داخل المحرر: هل الـ50 مادة الفرعية للـFBX تُرسم فعلاً أم بخامة مكسورة/شفافة (لا يظهر في YAML)، وهل الـGame View يعرض مشهد `Core` أصلاً (Build Settings / مشهد آخر محمّل). ملاحظة: السكربت المفقود على كائن الكاميرا (`Core.unity:7786`، GUID `a79441f348de89743a2939f4d699eac1`) يستحق الفحص — لو كان يحرّك الكاميرا عند التشغيل فقد يفسّر اختفاء الكارت من الشاشة وقت اللعب.
