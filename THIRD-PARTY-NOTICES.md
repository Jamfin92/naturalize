# Third-party notices

Naturalize is MIT-licensed (see [LICENSE](LICENSE)). It bundles and depends on the following.

## Bundled in this repository

**PT Serif** and **PT Sans** — SIL Open Font License 1.1.
`api/src/Naturalization.Api/Reports/Fonts/` (full text in `OFL.txt` in that directory).

These TTFs are compiled into the API assembly as embedded resources and embedded into every PDF the
API generates. The OFL permits both redistribution and embedding. They were chosen over
better-looking alternatives specifically because they still ship *static* faces — PdfSharp cannot
instance a variable-font weight axis, so a variable-only family renders bold text incorrectly.

The same two families are loaded on the web via `@fontsource/pt-serif` and `@fontsource/pt-sans`, so
screen and print share one font stack.

## Principal dependencies

| Package | Licence |
|---|---|
| PDFsharp / MigraDoc (`PDFsharp-MigraDoc`) | MIT |
| Microsoft.EntityFrameworkCore.Sqlite | MIT |
| Swashbuckle.AspNetCore | MIT |
| React, React DOM | MIT |
| Tailwind CSS | MIT |
| Radix UI primitives | MIT |
| shadcn/ui (vendored into `src/components/ui/`) | MIT |
| lucide-react | ISC |
| React Router | MIT |
| Vite | MIT |

Every dependency is under a permissive, OSI-approved licence. That is deliberate and is the reason
QuestPDF was rejected in favour of MigraDoc despite QuestPDF's better API — see the README section
["Why not QuestPDF"](README.md#why-not-questpdf).
