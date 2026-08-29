# Tolerance Tool — User Guide

The Tolerance Tool is an Excel add-in that standardizes tolerance calculations on
engineering test datasheets. You map a datasheet's columns once, point the tool at
your signal configuration, and it fills the Tolerance column for every test case —
round-tripping each expected value through the signal's scaling curve so the band is
correct even for non-linear signals.

---

## 1. Install

1. Build in Release (`dotnet build ToleranceTool.sln -c Release`) or use a supplied
   `ToleranceTool64-packed.xll`.
2. Run the installer (no admin rights needed):

   ```powershell
   powershell -ExecutionPolicy Bypass -File dist\Install-ToleranceTool.ps1
   ```

   It copies the add-in to `%LOCALAPPDATA%\ToleranceTool\bin`, seeds starter
   configuration into `%APPDATA%\ToleranceTool` (existing files are kept), and
   registers the add-in with Excel.
3. Restart Excel. A **Tolerance Tool** ribbon tab appears.

To remove it: `dist\Uninstall-ToleranceTool.ps1` (your configuration is left in place).

Requirements: 64-bit Excel 2016 or later / Microsoft 365, .NET Framework 4.8 (ships
with Windows 10 1903+ and Windows 11). The Access source additionally needs the
Microsoft Access Database Engine redistributable.

---

## 2. Shared configuration (set up once per team)

These libraries live in `%APPDATA%\ToleranceTool\` and are edited from the ribbon's
**Setup** group. Starter versions are installed for you.

| Library | File | Editor | Holds |
|---|---|---|---|
| Signal types | `signal-types.xml` | Signal Types | Named signal type → raw range (e.g. `4-20mA` → 4–20 mA) |
| Scale types | `scale-types.xml` | Scale Types | Named curve → forward + inverse expression |
| Tolerances | `tolerances.xml` | Tolerance Editor | `signalType + moduleType` → one or more band terms |
| Alias tables | `alias-tables.xml` | Alias Tables | System ID → signal rules, used during resolution |

### Scale types

Each curve is two expressions over a normalized variable `x` in `[0, 1]`:

- **Forward** maps the EU fraction to the raw fraction.
- **Inverse** is its reciprocal.

A curve must satisfy `Forward(0) = 0`, `Forward(1) = 1` and be monotonic; the editor
checks this numerically and plots both directions. Functions available include
`Pow`, `Sqrt`, `Log10`, `Abs`, `Max`, `Min` (NCalc syntax — use `Pow(a, b)`, not
`a ^ b`).

### Tolerances

A band is the **sum** of typed terms. Each term declares the space it lives in:

| Term | Attributes | Meaning |
|---|---|---|
| `Percent` | `value` (fraction), `basis` = `rawSpan` (default) \| `euSpan` \| `reading` | `±0.3%` → `value="0.003"`. `rawSpan` for 4–20 mA is 0.3% × 16 mA, applied in raw units. |
| `AbsoluteEu` | `value`, `unit`, `unitSystem` = `English` \| `SI` | A fixed magnitude already in EU. Scaled by the EU-span ratio if the row's unit system differs. |
| `AbsoluteRaw` | `value`, `unit` | A fixed raw offset (counts, mA). Forces the raw round-trip. |
| `Expression` | `space` = `raw` \| `eu`, body | Escape hatch. Variables: `expected`, `rawExpected`, `rawLow`, `rawHigh`, `rawSpan`, `euLow`, `euHigh`, `euSpan`. |

When every term is EU-space the tool skips the scale round-trip (the **fast path**).
The Tolerance Editor shows a live preview: pick a sample signal and an expected
value and see each term resolved, the ± band, and which path was used.

---

## 3. Import your signal configuration

**Setup → Signal Configuration.**

1. **Add file…** one or more CSV / XLSX files (or **Add Access…** for a database
   query). Each file is a *source*.
2. Mark exactly one source as the **master** — the one that links Sensor Name to
   Universal ID. Its row count sets how many signals exist.
3. For each source, set the **Universal ID column** and map the fields you have to
   their columns (a column letter, or a 1-based number). Column-oriented / key-value
   sheets are supported — set the source's orientation and give row numbers as
   locators.
4. **Build preview.** Every signal appears with a **Complete** flag (green when every
   required field resolved). Incomplete rows list the exact field and source that
   failed.
5. **Save signal set…** — writes `last-signal-set.xml` into `%APPDATA%\ToleranceTool`,
   which the datasheet pane reads.

Sources are joined with a left join from the master on Universal ID. Raw ranges you
do not import are filled from the signal-type registry.

---

## 4. Map a datasheet and run

Make the datasheet the **active worksheet**, then **Setup → Datasheet Mapping**.

1. **Orientation** — row-per-case (the usual layout) or column-per-case.
2. **Label row / column** — where the headers are (1-based).
3. Bind each parameter to a header: **System ID**, **Expected**, **Tolerance** are
   required; **Actual** and **Pass/Fail** are optional. Header matching is
   case-insensitive and trimmed, and the header text is stored so a moved column is
   re-found.
4. **Default unit system** for the sheet (English or SI). If you map a per-row unit
   column it overrides the default for rows where it is non-blank.
5. **Precision** — how the written value is rounded:
   - *Match Expected* — count the significant digits shown in that row's Expected
     cell and round the tolerance to the same count (recommended).
   - *Significant figures* / *Decimal places* — a fixed count for every row.
   - Rounding mode: half-to-even (default) or half-up.
6. **Resolution review** — the grid shows every data row's System ID, how it
   resolved, and the signal it landed on. The resolution ladder is:

   1. a per-sheet **override** you set in this grid,
   2. **exact** match of the System ID to a Sensor Name,
   3. **alias tables**, in priority order,
   4. **auto-match** — exactly one Sensor Name occurs as a whole token of the
      System ID.

   Rows that do not resolve (pink) or match more than one signal are never guessed.
   Correct them with the **Override → Universal ID** dropdown.
7. **Save mapping** — stored per sheet.

Then:

| Button | Effect |
|---|---|
| **Apply Tolerances** | Clears the tool's own comments, calculates every mapped row, and writes the Tolerance column. Rows that cannot be calculated (no signal, no tolerance, curve undefined) are left untouched and listed. Extrapolated rows are flagged. |
| **Check Tolerances** | Calculates the same values but writes nothing. Where the existing Tolerance cell differs beyond a small relative tolerance, adds a `[ToleranceTool]` comment: expected vs. found, the signal used, the band applied. |
| **Pass / Fail** | Fills the Pass/Fail column from `|Actual − Expected| ≤ Tolerance`, using the values already in the sheet. |
| **Clear Tool Comments** | Removes only comments that start with `[ToleranceTool]`. |

---

## 5. How the calculation works

For a tolerance whose terms are all in EU, the band is applied symmetrically about
the expected value — no conversion.

Otherwise the tool round-trips:

1. `expected` → EU fraction → **Forward** curve → raw fraction → `rawExpected`.
2. Sum the raw-space terms into a raw band; apply it: `rawExpected ± rawBand`.
3. Each raw edge → **Inverse** curve → EU fraction → EU value; add any EU-space
   terms outside the round-trip.
4. Because a non-linear curve makes a symmetric raw band asymmetric in EU, the
   written tolerance is the **larger** of the two EU-side deviations.

If the band runs past the sensor range, linear curves extrapolate cleanly; if the
inverse of a `SquareRoot` / `Logarithmic` curve goes non-finite there, the row is
flagged and its Tolerance cell is left untouched rather than writing a bad number.

---

## 6. Where things are stored

| What | Where |
|---|---|
| The add-in | `%LOCALAPPDATA%\ToleranceTool\bin\ToleranceTool64-packed.xll` |
| Shared libraries | `%APPDATA%\ToleranceTool\*.xml` |
| Last imported signal set | `%APPDATA%\ToleranceTool\last-signal-set.xml` |
| Per-sheet datasheet mapping | in the workbook, mirrored to `%APPDATA%\ToleranceTool\sheets\<sheet>.xml` |

---

## 7. Sample data

`dist/samples/` contains `signals-master.csv` + `signals-ranges.csv` (import these
as two sources, master first) and `sample-datasheet.csv` (paste into a sheet, map
**Signal Name → System ID**, **Expected**, **Tolerance**, **Actual**, **Pass/Fail**).
They line up with the starter tolerance and signal-type libraries.
