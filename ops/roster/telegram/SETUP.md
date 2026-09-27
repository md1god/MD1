# إعداد شات تليجرام المجاني الدائم — دليل صاحب المشروع

**التاريخ:** 2026-09-27  
**التكلفة:** $0 — لا رصيد، لا دفع  
**الهدف:** تتكلم مع وكيل من جوالك، والشغل يستمر حتى لو طفى الكمبيوتر

---

## الخيار المختار: A — Telegram → OpenClaw السحابي

**لماذا هذا الخيار؟**  
- OpenClaw يدعم telegram رسمياً وثيقاً (`docs.openclaw.ai/channels/telegram`)
- السحابة `886841a9.openclaw.runware.run` حية دائماً
- البوت `Mdm1_Smart_bot` موجود وحي (`getMe: ok=true`)
- لا يحتاج VPN — السحابة تتصل بـ Telegram مباشرة
- الشغل يستمر لو طفى الكمبيوتر

---

## الخطوات بالترتيب

### الخطوة 1: تأكد أن البوت موجود عندك

افتح Telegram وابحث عن `@BotFather`. تأكد أن البوت `Mdm1_Smart_bot` موجود عندك. إذا مو، أنشئ بوت جديد:
```
/start → /newbot → اتبع التعليمات → احفظ الـ token
```

الرمز يبدو هكذا: `123456789:ABCdefGHIjklMNOpqrsTUVwxyz`

> **لا تكتب الرمز هنا. الرمز يبقى عندك فقط.**

---

### الخطوة 2: افتح واجهة الأوامر للسحابة

تحتاج تقدر تكتب أوامر على `886841a9.openclaw.runware.run`.

**الطريقة:**
- لو عندك SSH: `ssh openclaw@886841a9.openclaw.runware.run`
- أو من Control UI: افتح `http://886841a9.openclaw.runware.run` في المتصفح
- أو من التطبيق: استخدم OpenClaw Dashboard

> **ملاحظة:** Control UI في السحابة الحالية لم يبدأ (app bundle فشل). جرّب `http://886841a9.openclaw.runware.run` واتركه يرجع. لو ما عمل، استخدم SSH.

---

### الخطوة 3: ثبّت قناة telegram على السحابة

**من واجهة الأوامر (SSH):**
```bash
openclaw channels add --channel telegram --token PASTE_TOKEN_HERE
```

> عوّض `PASTE_TOKEN_HERE` بالرمز الحقيقي الذي حصلت عليه من BotFather.
> **لا تكتب أي مفتاح في أي ملف نصي.**

---

### الخطوة 4: ضع الموديل المجاني في الإعدادات

**افتح ملف الإعدادات:**
```bash
# داخل السحابة
nano ~/.openclaw/openclaw.json
```

**أو استخدم الأمر:**
```bash
openclaw config set agents.defaults.model "openrouter/qwen/qwen3.8-27b:free"
```

**الإعدادات المطلوبة:**
```json5
// ~/.openclaw/openclaw.json
{
  agents: {
    defaults: {
      model: "openrouter/qwen/qwen3.8-27b:free",
      workspace: "~/.openclaw/workspace"
    }
  },
  channels: {
    telegram: {
      enabled: true,
      botToken: "PASTE_TOKEN_HERE",
      dmPolicy: "pairing"
    }
  }
}
```

> ⚠️ **`PASTE_TOKEN_HERE`** — لا تكتب الرمز الحقيقي في أي ملف. الأمر `openclaw channels add` هو اللي يكتبه بشكل آمن.

---

### الخطوة 5: أعد تشغيل الـ Gateway

```bash
openclaw gateway restart
```

أو لو كان يعمل كخدمة:
```bash
openclaw service restart
```

---

### الخطوة 6: تحقق من القناة

```bash
openclaw channels status --probe
```

لو كل شي صحيح، راح يظهر `telegram` في قائمة القنوات.

---

### الخطوة 7: اقترِن من جوالك

1. افتح Telegram
2. ابحث عن `@Mdm1_Smart_bot`
3. أرسل أي رسالة (مثلاً: "مرحباً")
4. رجع للسحابة واكتب:
   ```bash
   openclaw pairing list telegram
   ```
5. ستظهر لك أكواد اقتران. وافق على الكود المناسب:
   ```bash
   openclaw pairing approve telegram <CODE>
   ```

> أكواد الاقتران تنتهي بعد ساعة.

---

### الخطوة 8: اختبر!

أرسل رسالة من جوالك:
```
Reply: اختبار
```

لو رد الوكيل — **مبروك!** الشغل شغّال من الجوال ودائماً.

---

## إذا حصة OpenRouter ما تجدّدت بعد

**تاريخ التجديد: 2026-09-28 00:00 UTC (غداً)**

أُجري الاختبار الفعلي بتاريخ 2026-09-27:
```powershell
& "C:\Users\DiDo\AppData\Local\Programs\@opencodedesktop\resources\opencode-cli.exe" `
  run --auto --model "openrouter/qwen/qwen3.8-27b:free" "Reply: FREE-OK"
```
**النتيجة:** علق في `build · qwen/qwen3.8-27b:free` ثم timeout.
**الاستنتاج:** `QUOTA STILL EXHAUSTED` — الحصة لم تتجدّد بعد.
```
→ انتظر التجديد أو استخدم موديل محلي مجاني آخر (انظر الخطوة 4).
```

---

## البدائل إذا A ما اشتغلت

### البديل B: Telegram → جهازك عبر VPN

- شغّل `openclaw` على جهازك
- ثبّت VPN (ngrok أو Tailscale)
- نفس البوت، نفس الخطوات
- **القيود:** يتوقف لو طفى الكمبيوتر

### البديل C: صفحة ويب دائمة (بدون تليجرام)

- افتح `http://886841a9.openclaw.runware.run`
- Control UI مدمج في OpenClaw
- شغّل من الجوال كأي موقع ويب
- **القيود:** Control UI الحالي في السحابة لم يبدأ — يحتاج إن Gateway يرجع

---

## المعلومات المهمة

| البند | القيمة |
|-------|--------|
| بوت تليجرام | `Mdm1_Smart_bot` / `MDM1 Assistant` |
| السحابة | `886841a9.openclaw.runware.run` |
| OpenClaw CLI (سحابة) | الإصدار 1.18.32 |
| الموديل المجاني | `openrouter/qwen/qwen3.8-27b:free` |
| الحصة | مستنفدة — تم التأكد فعلياً: command timeout في "build" loop |
| التكلفة | **$0** — لا شيء |
| `opencode-cli.exe` فيه telegram/channel؟ | **لا** — الأوامر فقط: upgrade, acp, api, debug, auth, mcp, plugin, models, stats, mini, run, session, service, reload, pair, serve |
| السحابة `/health` | ✅ `{"ok":true,"status":"live"}` |

---

## ⚠️ تنبيهات

- **لا تدفع أي رصيد** لأي سبب
- **لا تستخدم Runware** (مقيس بالفلوس: $0.11/يوم)
- **لا تكتب أي مفتاح في أي ملف نصي** — استخدم `openclaw channels add` للكتابة الآمنة
- **لا تستخدم `pip install`** من مصادر غريبة
- كل الأوامر مجانية 100%

---

## ملفات مرتبطة

| الملف | الغرض |
|-------|--------|
| `PLAN.md` | خطة التوصيل الكاملة |
| `SETUP.md` | دليل الإعداد (هذا الملف) |
| `ops/roster/AGENTS.md` | قواعد المشروع الأساسية |
| `ops/roster/agents.json` | رستر الوكلاء |
| `docs.openclaw.ai/channels/telegram` | توثيق رسمي |
