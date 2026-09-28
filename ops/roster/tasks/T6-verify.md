# TASK — Final verification of today's fixes (Agent #5 Longcat)

## السياق
المدقق النهائي. اليوم ثبتنا بالقياس في محرر حي:
- `D:\CC_GAME_1\ops\shots\real\verify.txt` — أول إطار: الكاميرا تنظر للسيارة (CENTER-RAY -> Kart 6.6m)
- نفس الملف: `W-DRIVE MOVED 160.380 m` → السيارة تقود والكاميرا تتبع (camera after drive pos z=154.03)
- `Assets/Editor/CCVerify.cs` — أداة القياس (مفتاح الحالة CCVerify2)
- `Assets/_Game/Scripts/FollowCam.cs` — إصلاح ذاتي (يبحث عن "Kart" إن كان الهدف فارغاً)، سطور 12-16
- `Assets/Scenes/Core/Core.unity` — الكاميرا تنظر للأسفل 16.7° نحو السيارة (m_LocalRotation x=0.145220)

## المطلوب (داخل ملكيتك فقط: ops/verify/)
1. اقرأ `ops/shots/real/verify.txt` وتحقق بالأرقام:
   أ) هل أول إطار يُصيب السيارة في المركز؟ (سطر CENTER-RAY)
   ب) هل القيادة تتحرك؟ كم متراً؟
2. شغّل فحص البناء القابل للتشغيل: `python D:\CC_GAME_1\ops\roster\verify_build_scenes.py`
   (اقرأ أولاً ما يفعله، وأبلغ نتيجته الحرفية. إن رفض التشغيل بسبب ملكية — اكتب UNVERIFIED)
3. اكتب `ops/verify/verify-today.md`: جدول البنود + النتائج الحقيقية + الحكم النهائي PASS/FAIL.

## القواعد
- لا تلمس ملفاً خارج `ops/verify/`
- كل رقم له مصدر (file:line أو مخرجات أمر)
- لا تخمّن. لو ما استطعت التحقق → `UNVERIFIED:` وتوقّف.
- التقرير في `ops/roster/handoff/` باسمك.