# E — شات تليجرام المجاني الدائم

**أنت الوكيل E. تملك:** `ops/roster/telegram/` فقط.
**ممنوع** لمس أي مكان آخر. ممنوع دفع أي مبلغ.

---

## المطلوب

صاحب المشروع يريد يتكلم مع وكيل من **جواله**، ويستمر الشغل **حتى لو طفا
الكمبيوتر**.والقرار:: **نموذج مجاني 100%**، لا Runware (مقيس بالفلوس:
$0.11 اليوم، 97.7% منه `qwen3.5-9b`).

## ما تحققت مسبقاً — لا تعِد الفحص

### بوت تليجرام حي
```
bot username : Mdm1_Smart_bot
first name   : MDM1 Assistant
getMe        : ok=true
```

### السحابة
| | |
|---|---|
| `https://886841a9.openclaw.runware.run` | حي، `/health` → `{"ok":true,"status":"live"}` |
| `https://6c87e194.openclaw.runware.run` | **ميت — 503** |

### القناة الآن
`channels.status` → `channelOrder: []` — **ما فيه أي قناة موصّلة**.

## الموديلات المجانية المتاحة

**على جهاز صاحب المشروع (6 مجانية مؤكدة باختبار `17×23=391`):**
```
opencode/space-bunny-free             168s   <- 3 أيام متبقية!
opencode/muse-spark-1.3-contributor-free  24s
opencode/big-pickle                    12s
opencode/longcat-2.5-preview-free      4.4s
opencode/mimo-v2.6-flash-free          3.1s
opencode/ling-3.0-flash-fin-free       2.4s   <- الأسرع
```

**OpenRouter — 17 موديل `:free` حقيقي:**
```
openrouter/qwen/qwen3.8-27b:free
openrouter/nvidia/nemotron-3-ultra-550b-a55b:free
openrouter/liquid/lfm-2.5-2.6b:free
openrouter/poolside/laguna-xs-2.1:free
openrouter/cohere/north-mini-code:free
openrouter/thinkingmachines/inkling:free
... (17 إجمالاً)
```

### ⚠️ حالة حصة OpenRouter الآن
المفتاح **صالح** (`is_free_tier: true`) لكن:
```
HTTP 429  Rate limit exceeded: free-models-per-day
X-RateLimit-Remaining: 0
X-RateLimit-Reset: 1790553600000   ->  2026-09-28 00:00 UTC
```
وعرض المزوّد: `Add 10 credits to unlock 1000 free model requests per day`

**🚫 ممنوع إضافة أي رصيد. صفر تكلفة. مطلوب. إن ظهر هكذا → تجاهل ورفض.**

## المطلوب بالترتيب

### 1) وثّق خطة التوصيل — `ops/roster/telegram/PLAN.md`

اكتب **دقيقاً**، بدون تنفيذ مُكلف:

```
A) Telegram → OpenClaw سحابي (886841a9)
   - الموديل: أي واحد :free من القائمة أعلاه
   - قناة telegram: هل OpenClaw يدعمها أصلاً؟ أثبت من التوثيق أو من
     channels.status. لا تخمّن.
   - الخطوات الدقيقة اللي ينفّذها صاحب المشروع (رمز البوت موجود عنده)

B) Telegram → جهازه عبر VPN  (يعمل بلا تكلفة، لكن يتوقف بإطفاء الجهاز)
   - openclaw محلي + نفس بوت تليجرام
   - المميزات والقيود

C) البديل بلا تليجرام: صفحة ويب دائمة
   - نفس السحابة، رابط يفتح من الجوال
```

**اختار A إن كان ممكناً، واذكر لماذا إن لم يكن.**

### 2) أثبت ما يمكن إثباته مجاناً

- هل `opencode-cli.exe` فيه أمر `telegram` أو `channel`؟ `--help` وتوثّجه.
- جرّب **نموذج `:free` واحد فقط** من OpenRouter للتحقق أن الحصة تجددت:
  ```powershell
  & "C:\Users\DiDo\AppData\Local\Programs\@opencodedesktop\resources\opencode-cli.exe" `
    run --auto --model "openrouter/qwen/qwen3.8-27b:free" "Reply: FREE-OK"
  ```
  إن رجع 429 → اكتب `QUOTA STILL EXHAUSTED` وتوقّف. **لا تدفع.**

### 3) جهّز ملف الربط — `ops/roster/telegram/SETUP.md`

خطوات مرقّمة، بالعربية، **كل خطوة قابلة للتنفيذ بيد صاحب المشروع**، وبها
مكان putting الرمز مكتوباً `PASTE_TOKEN_HERE` — **لا تكتب أي مفتاح في الملف.**

## التقرير

| البند | النتيجة |
|---|---|
| هل OpenClaw يدعم telegram؟ | نعم/لا + الدليل |
| مجاني 100% أم يحتاج رصيد؟ | |
| كم خطوة يحتاج صاحب المشروع؟ | |
| هل حصة OpenRouter تجددت؟ | |

**ممنوع:** أي `add credits`، أي مفتاح مدفوع، أي `pip install` من مصادر غريبة.

لما تخلّص: **توقّف.**
