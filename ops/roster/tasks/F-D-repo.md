# D — نضّف الريبو وأدفع

**أنت الوكيل D. تملك مجلد واحد فقط:**
`C:\Users\DiDo\Desktop\GitHub\MD111`
**ممنوع** لمس `D:\CC_GAME_1` **قراءة فقط** للمصدر. ممنوع لمس يونيتي.

---

## الهدف

الريبو `https://github.com/md1god/MD1` هو ريبو اللعبة. فيه شغل اليوم
(`D:\CC_GAME_1`) غير مرفوع. ادفعه، **بدون الأصول الثقيلة**.

## ما تحققت مسبقاً (لا تعِد الفحص)

- المصادقة شغالة: `git ls-remote origin` ← `exit 0`
- الريبو: 15 كوميت، 71.2 MB، الفرع `main`
- **مستحيل** رفع الـ`Assets/` الـ12GB من `D:\CC_GAME_1`
  (صاحب المشروع: الأصول على HuggingFace `Dido599999/MD111`، ما نرفعها)
- في `D:\CC_GAME_1` ما يستحق الرفع (نُسخة بـPython، لا PowerShell):

| المسار | العدد | ملاحظة |
|---|---|---|
| `Assets/_Game/Scripts/*.cs` | 18 | كود اللعبة ✅ |
| `Assets/Scenes/**/*.unity` | 123 | **314 MB** — مشاهد YAML نصية |
| `Assets/Scenes/**/*.meta` | 181 | **لازم** — بدونها GUID تنكسر |
| `ProjectSettings/*` | 25 | فيه إصلاح الـbuild ✅ |
| `ops/**` | 70 | الرستر والمهام والتحقق |

## الخطوات

### 1) نسخ احتياطي قبل أي حذف

```powershell
cd C:\Users\DiDo\Desktop\GitHub\MD111
git status
git diff > C:\Users\DiDo\Desktop\GitHub\MD111_backup_before_D.diff
```

**ممنوع** `git reset --hard`، `git clean -fd`، ولا أي حذف لمحتوى
`.git/`. إن احتجت تحذف ملفاً من المتتبَع: `git rm` فقط.

### 2) انسخ من `D:\CC_GAME_1`

```powershell
$src = "D:\CC_GAME_1"; $dst = "C:\Users\DiDo\Desktop\GitHub\MD111"
foreach ($p in @("Assets\_Game","Assets\Scenes","ProjectSettings","ops")) {
  robocopy "$src\$p" "$dst\$p" /E /XD Library Temp Logs obj /NFL /NDL /NJH /NJS /NP
}
```

انسخ كذلك ملفات coordination: `AGENTS.md` · `YOUR_TURN` · `IDEA.md` ·
`PLAN.md` · `README.md`

⚠️ `Assets/Scenes` فيه 314 MB. **إذا تجاوز الوقت 20 دقيقة، أوقف النسخ واكتب
`PARTIAL: n من 123 مشهد` — لا تماطل.**

### 3) `.gitignore` — امنع ما لا يجب

أضف (لا تحذف الموجود):
```
# Unity generated
/[Ll]ibrary/
/[Tt]emp/
/[Oo]bj/
/[Bb]uilds/
/[Ll]ogs/
/[Uu]serSettings/

# 12GB of binary art stays on HuggingFace (Dido599999/MD111), never in git
*.glb
*.blend
*.psd
*.exr
*.hdr
```

**لا** تضف `*.fbx` / `*.png` — يوجد 72 و106 منها في الريبو أصلاً ومتوافقة
مع `.gitattributes` الخاص بـLFS. لو حذفتها تكسر مشروعاً قائماً.

### 4) تحقّق قبل الدفع

```powershell
git status --short | Measure-Object        # كم ملف تغيّر؟
git diff --stat ProjectSettings/          # هل إصلاح الـbuild راح؟
Select-String -Path ProjectSettings\EditorBuildSettings.asset -Pattern "^\s*-\s*path:" | Measure-Object
```

**شرط القبول:** عدد مدخلات الـbuild = **122**، وعدد `Chunk_*.unity` في
git = **121**.

### 5) ادفع

```powershell
git add -A
git commit -m "Sync CC_GAME_1: game scripts, 121 world chunks, 123 scenes, ProjectSettings build fix"
git push origin main
```

## التقرير

| البند | القيمة |
|---|---|
| عدد الملفات المضافة | |
| عدد `.unity` في git | |
| عدد `Chunk_*.unity` | |
| مدخلات الـbuild بعد النسخ | |
| `git push` | نجح؟ **نعم/لا + آخر 5 أسطر** |

إن فشل الدفع → انسخ الخطأ الحرفي كاملاً. **لا تعِد المحاولة بلا نهاية،**
ولا تدّعِ نجحت.

لما تخلّص: **توقّف.**
