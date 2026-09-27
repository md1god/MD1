# F — ابدأ نقل الشغل للسحابة (بلا تكلفة)

**أنت الوكيل F. تملك:** `ops/cloud/` فقط.
**ممنوع** دفع أي مبلغ. ممنوع لمس `C:\Users\DiDo\Desktop\GitHub\MD111`
(الوكيل D يعمل فيه الآن).

---

## لماذا

صاحب المشروع يريد الشغل **يستمر لما يطفي الكمبيوتر**، ويخفّف الحمل عن جهازه.
السحابة هي الحل — بشرط صفر تكلفة.

## ما تحققت مسبقاً — لا تعِد الفحص

| | |
|---|---|
| `886841a9.openclaw.runware.run/health` | `{"ok":true,"status":"live"}` |
| `6c87e194.openclaw.runware.run` | **503 — ميت** |
| قناة OpenClaw | `channels.status` → `channelOrder: []` |
| Runware مفتاح | `anicwfWnUDHsHrBJezqCCX4eLJWOr64N` في `C:\Users\DiDo\Desktop\APi.txt` |
| Runware تكلفة أمس | **$0.11** — و97.7% منها `qwen3.5-9b` |

**⚠️ Runware مقيس بالفلوس. لا تستخدمه إلا للاختبار الأدنى، ولموديل مجاني فقط.**

## المطلوب

### 1) جرّب موديل Runware **المجاني** فعلاً

المهم: هل فيه موديل Runware بـ **تكلفة صفر**؟ اكتشفة، لا تخمين.

```python
# افحص قائمة موديلات Runware الواجهة العامة
import json, urllib.request
req = urllib.request.Request(
    "https://api.runware.ai/v1/openapi/graphql", method="POST",
    headers={"Content-Type": "application/json", "Authorization": "<KEY>"},
    data=json.dumps({"query": "{ modelList { id name cost } }"}).encode())
```

ابحث عن موديلات `cost == 0` أو `free`. **سجّلها في
`ops/cloud/runware_free_models.md`.**

إن كان كل شي بفلوس → اكتب `RUNWARE IS METERED, NO FREE TIER` بوضوح
واذكر أرخص موديل ومعدل تكلفته للـ1000 رمز. **هذا مهم لأن صاحب المشروع يفترض أن Runware مجاني وهو ليس كذلك.**

### 2) هل تفتح السحابة على GitHub؟

هذا هو المفتاح: لو السحابة تقدر تستنسخ الريبو، تقدر تعدّل الكود irrespective
من أن جهازك مطفي.

```bash
# داخل السحابة
cd /tmp && git clone --depth 1 https://github.com/md1god/MD1.git test-clone
```

⚠️ الريبو **عام** — لا يحتاج مفتاح للاستنساخ. **لا تضع أي مفتاح في الريبو.**

اكتب النتيجة في `ops/cloud/github_access.md`:
- هل `git` موجود في السحابة؟
- هل `curl`/`wget`؟
- هل الشبكة مفتوحة؟
- **هل تنجح كتابة ملف والتزام (`git commit`)؟** هذا ما يثبت أن السحابة
  تستطيع فعل الشغل، لا مجرد القراءة.

⚠️ **لا تدفع commit للفرع `main`.** استنسخ في مجلد مؤقت فقط.
إذا طلب commit، استخدم فرعاً تجريبياً باسم `fleet/agent-F`، أو اكتفي
بتقرير «الكتابة تعمل، الدفع يحتاج مفتاح».

### 3) وصّلي `YOUR_TURN` بالسحابة

اقترح بدقة: كيف يقرأ الوكيل السحابي مهمته ويكتب نتيجتها؟
اقتراحك المبدئي:
- المهمة تُكتب كـ**commit** في فرع باسم `tasks/<slot>`
- النتيجة تُكتب كـ**commit** في `reports/<slot>`
- رقم 1 (أو وكيل محلي) يدمج الفروع

اكتبه في `ops/cloud/WORKFLOW.md` معقد للمنافسة على نفس الملف.

## التقرير

| السؤال | جواب بدليل |
|---|---|
| فيه موديل Runware مجاني؟ | |
| السحابة تفتح GitHub؟ | |
| السحابة تقدر تكتب وتلتزم؟ | |
| السحابة تقدر تشغّل Unity؟ | **مهم: Unity على Linux ممكن بـ`-batchmode` بس بلا URP/GPU.** صرّح. |

لما تخلّص: **توقّف.**
