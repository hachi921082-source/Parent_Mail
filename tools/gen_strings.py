"""ui-strings.md の表から Strings.ja.cs を生成する（手で写さないための補助）。

使い方: python tools/gen_strings.py [出力先]
  既定の出力先: src/ParentMail.App/Resources/Strings.ja.cs
- ID（例 CP-01）の行だけを対象にし、SUPERSEDED の行は除く。
- 本文はそのまま定数にする。「A / B / C」のような複数文言の行は分割しない（使う側で扱う）。
- C# の識別子は ID の '-' を '_' にしたもの（CP_01）。
"""
import io, re, sys

SRC = 'docs/plans/ui-strings.md'
OUT = sys.argv[1] if len(sys.argv) > 1 else 'src/ParentMail.App/Resources/Strings.ja.cs'
ID_RE = re.compile(r'^[A-Z]{2,3}-\d{2}$')

rows, seen = [], set()
for line in io.open(SRC, encoding='utf-8'):
    if not line.startswith('|'):
        continue
    cells = [c.strip() for c in line.strip().strip('|').split('|')]
    if len(cells) < 4 or not ID_RE.match(cells[0]):
        continue
    if any(c == 'SUPERSEDED' for c in cells):
        continue
    # 通常表は 3 列目、ウィンドウ表（WD）も 3 列目が文言
    text = cells[2]
    if cells[0] in seen:
        raise SystemExit('duplicate ID: ' + cells[0])
    seen.add(cells[0])
    rows.append((cells[0], text))

def esc(s):
    bs = chr(92)
    return s.replace(bs, bs + bs).replace('"', bs + '"')

lines = ['// 自動生成: tools/gen_strings.py（docs/plans/ui-strings.md が正本。手で編集しない）',
         'namespace ParentMail.App.Resources;', '',
         'public static class Strings', '{']
for i, t in rows:
    lines.append('    /// <summary>%s</summary>' % esc(t).replace('<', '&lt;').replace('>', '&gt;'))
    lines.append('    public const string %s = "%s";' % (i.replace('-', '_'), esc(t)))
lines.append('}')
import os
os.makedirs(os.path.dirname(OUT) or '.', exist_ok=True)
io.open(OUT, 'w', encoding='utf-8-sig', newline='\r\n').write('\n'.join(lines) + '\n')
print('%d strings -> %s' % (len(rows), OUT))
