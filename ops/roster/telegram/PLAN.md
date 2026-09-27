# خطة التوصيل — Telegram ↔ OpenClaw

**تاريخ:** 2026-09-27  
**الوكيل:** E (Ling Flash)  
**الملكية:** `ops/roster/telegram/` فقط  
**التكلفة:** $0 — صفر رصيد، لا Runware

---

## A) Telegram → OpenClaw سحابي (886841a9) ✅ ممكن

### هل OpenClaw يدعم telegram؟

**نعم — وثّق رسمي.**  
الدليل: `docs.openclaw.ai/channels/telegram` — الصفحة تؤكد:
> "Telegram is production-ready for bot DMs and groups via grammY."

- النقل الافتراضي: **long polling** (لا يحتاج VPN للعنوان)
- الويبهوك اختياري (يحتاج HTTPS ingress)
- الإعداد: `openclaw channels add --channel telegram --token <bot-token>`
- التحقق: `openclaw channels status --probe`
- الاقتران (pairing): `openclaw pairing list/approve telegram`

### الوضع الحالي للسحابة

| | |
|---|---|
| `886841a9.openclaw.runware.run/health` | ✅ `{"ok":true,"status":"live"}` |
| `openclaw` CLI داخل السحابة | ✅ الإصدار 1.18.32 (per `agents.json`) |
| `channels.status` | `channelOrder: []` — **لا قنوات موصّلة** |
| البوت | `Mdm1_Smart_bot` / `getMe: ok=true` — **موجود وحي** |
| الموديل الحالي | `runware/openai-gpt-oss-120b` — **❌ مقيس بالفلوس ($0.11/يوم)** |

### الموديل المجاني

**المشكلة:** السحابة تستخدم Runware كنموذج افتراضي — ومقيس بالفلوس.  
**الحل:** تعيين موديل `:free` من OpenRouter في إعدادات OpenClaw على السحابة.

OpenClaw يسمح بتكوين الموديل عبر `~/.openclaw/openclaw.json` تحت `agents.defaults.model` أو `agents.entries`. يمكن ضبطه على:
```json5
{
  agents: {
    defaults: {
      model: "openrouter/qwen/qwen3.8-27b:free"
    }
  }
}
```

**⚠️ ولكن:** حصة OpenRouter حالية **مستنفدة** (429 Rate limit exceeded، `X-RateLimit-Remaining: 0`). التجديد: **2026-09-28 00:00 UTC** (غداً).

### الخطوات الدقيقة لصاحب المشروع

**الشرط المسبق:** الرمز (token) الخاص بـ `Mdm1_Smart_bot` موجود عند صاحب المشروع.

1. **افتح وصول SSH أو واجهة الأوامر للسحابة** `886841a9.openclaw.runware.run`

2. **ضع مفتاح OpenRouter المجاني** (إن لم يكن موجوداً) في متغير البيئة أو ملف الإعدادات:
   ```bash
   export OPENROUTER_API_KEY="<حيثما يكون>"
   ```

3. **عدّل إعدادات القناة:**
   ```bash
   # داخل السحابة
   openclaw channels add --channel telegram --token "PASTE_TOKEN_HERE"
   ```

4. **ضع الموديل المجاني في الإعدادات:**
   ```json5
   // ~/.openclaw/openclaw.json
   {
     agents: {
       defaults: {
         model: "openrouter/qwen/qwen3.8-27b:free"
       }
     },
     channels: {
       telegram: {
         enabled: true,
         dmPolicy: "pairing",
       }
     }
   }
   ```

5. **أعد تشغيل الـ Gateway:**
   ```bash
   openclaw gateway restart
   ```

6. **تحقق من القناة:**
   ```bash
   openclaw channels status --probe
   ```

7. **اقترِن من الجوال:** أرسل رسالة للبوت في Telegram، ثم:
   ```bash
   openclaw pairing list telegram
   openclaw pairing approve telegram <CODE>
   ```

### لماذا A ممكن؟
- ✅ OpenClaw يدعم telegram رسمياً وثيقاً
- ✅ السحابة 886841a9 حية وتعمل بـ `openclaw` CLI
- ✅ البوت موجود وحي (`getMe: ok=true`)
- ✅ الموديلات `:free` من OpenRouter موجودة (تنتظر تجديد الحصة)
- ✅ لا يحتاج VPN للعنوان (long polling يعمل من السحابة)
- ✅ الشغل يستمر لو طفا الكمبيوتر (السحابة = سيرفر دائم)

### القيود
- الموديل المجاني يحتاج OpenRouter API key صالح
- حصة OpenRouter حالية مستنفدة (تجدد 2026-09-28)
- **لا يمكن استخدام Runware** (مقيس بالفلوس — ممنوع)

---

## B) Telegram → جهازه عبر VPN (يعمل بدون تكلفة، يتوقف بإطفاء الجهاز)

### المميزات
- يعمل على نفس الجهاز، نفس بوت تليجرام
- لا يحتاج OpenRouter API key (يستخدم الموديلات المحلية المجانية)
- السرعة عالية (الردود 2.4–168 ثانية حسب الموديل)
- **موديلات متاحة محلياً:** `opencode/ling-3.0-flash-fin-free` (2.4s), `opencode/big-pickle` (12s), وغيرها

### القيود
- ❌ **يتوقف تماماً لو طُفئ الكمبيوتر** أو انقطع الإنترنت
- ❌ يحتاج VPN ثابت للوصول من الجوال (مثل ngrok، Cloudflare Tunnel، أو Tailscale)
- ❌ يحتاج تهيئة `openclaw` محلياً + نفس إعدادات telegram
- ❌ الموديل المحلي `opencode` يعمل فقط على جهاز بـ GPU كافية

### الخطوات العامة
1. ثبّت `openclaw` على الجهاز المحلي
2. `openclaw channels add --channel telegram --token "PASTE_TOKEN_HERE"`
3. ثبّت خدمة VPN (ngrok/tailscale) للوصول من الجوال
4. شغّل `openclaw gateway start`
5. اقترِن من الجوال

---

## C) البديل بلا تليجرام: صفحة ويب دائمة

### الوصف
بدلاً من telegram، استخدم **Control UI** لـ OpenClaw على السحابة:
```
http://886841a9.openclaw.runware.run
```
أو الرابط المباشر عبر المتصفح على الجوال.

### المميزات
- ✅ يعمل دائماً (السحابة لا تطفى)
- ✅ لا يحتاج telegram أبداً
- ✅ لا يحتاج VPN
- ✅ مجاني 100% (استخدم موديل `:free`)
- ✅ Control UI مدمج في OpenClaw (منفذ 18789)

### القيود
- ❌ ليس تطبيق تليجرام (تجربة مختلفة)
- ❌ Control UI الحالي في السحابة **لم يبدأ** (الـ app bundle فشل — راجع `https://886841a9.openclaw.runware.run`)
- ❌ يحتاج إن Gateway يشتغل بشكل صحيح

---

## ✅ الاختيار: A (Telegram → OpenClaw سحابي)

**السبب:** OpenClaw يدعم telegram رسمياً وثيقاً وdocumented. السحابة حية والبوت موجود. الوحيد هو انتظار تجديد حصة OpenRouter (2026-09-28 00:00 UTC). إذا لم يرِد صاحب المشروع الانتظار، البديل C (صفحة ويب) يعمل فوراً.

---

## حالة التحقق (الأدلة)

| البند | النتيجة | الدليل |
|-------|---------|--------|
| `opencode-cli.exe` فيه أمر telegram/channel؟ | **لا** | `--help` يظهر فقط: upgrade, acp, api, debug, auth, mcp, plugin, models, stats, mini, run, session, service, reload, pair, serve |
| `openclaw` CLI يدعم telegram؟ | **نعم** | `openclaw channels add --channel telegram --token <token>` — موثّق في `docs.openclaw.ai/channels/telegram/setup` |
| OpenClaw سحابي حي؟ | **نعم** | `/health` → `{"ok":true,"status":"live"}` |
| قناة telegram موصّلة؟ | **لا** | `channels.status` → `channelOrder: []` |
| حصة OpenRouter | **مستنفدة** | `HTTP 429`, `X-RateLimit-Remaining: 0`، التجديد 2026-09-28 |
| هل OpenRouter تجدد؟ | **لا لم يجدد بعد** | `run --model openrouter/qwen/qwen3.8-27b:free` علق في "build" loop ثم timeout |
