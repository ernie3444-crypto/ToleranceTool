# Multi-Input (Derived / Compensated) Signals — Design Note

Status: **draft for review.** Nothing here is built yet. Open questions are marked
**`[Q1]`**…**`[Qn]`** and collected at the end.

---

## 1. Problem

Today every calculation assumes **one sensor → one raw channel → one scaling curve →
one engineering-unit value**. `ToleranceEngine.Calculate` takes a single
`SignalConfig` and either sums EU terms directly (Path A) or does the raw round-trip
through one curve (Path B).

Real datasheets also carry **derived parameters** whose value is computed from more
than one physical input:

- an RTD cold-junction-compensating a thermocouple (2 inputs)
- a temperature-/density-compensated level or flow (3–4 inputs)
- piecewise signals where the active input changes across the range (e.g. sensor A
  below a breakpoint, sensor B above it), and the two inputs have **different input
  tolerances**

For these we need the tolerance on the derived value to reflect each contributing
input's own tolerance, propagated through the combining formula.

---

## 2. The error-propagation math

For a derived value `y = f(x1 … xn)` with independent input errors `Δxi`:

```
worst case :  Δy = Σ |∂f/∂xi| · Δxi
RSS        :  Δy = sqrt( Σ ( ∂f/∂xi · Δxi )^2 )
```

The sensitivities `∂f/∂xi` are **constant** when `f` is linear in that input
(sums, weighted averages, additive bias/compensation over a modest span) and
**vary with the operating point** when it is not (square-root extraction, density
or ratio compensation).

Consequences:

- **Linear combine** — the derived EU value plus each input's nominal contribution
  weight is enough; we do not need live input readings.
- **Non-linear combine** — we need each input's operating point, because the
  sensitivity is evaluated there. The derived Expected alone cannot recover the
  individual input points (under-determined), so we need either the measured input
  values (from the input sheet) or nominal values (from the PMF `init val`).

The engine will compute `∂f/∂xi` **numerically** (central finite difference through
the transfer expression), so the same code path handles linear and non-linear `f`.

---

## 3. Data sources

| Source | Supplies | Notes |
|---|---|---|
| **PMF file** | *only three things*: which signals are analog I/O (`SigType` = AI/AO), the exact `Signal Name`, and each channel's `init val` (nominal/power-on value) | tab-delimited CSV, one row per channel; see §4 |
| **Signals master + signal-type registry** | each input channel's raw span, EU span, scale type, `signalType`/`moduleType` | the *existing* import pipeline — an input channel is resolved exactly like an output-sheet signal (Signal Name → alias ladder → Universal ID → `SignalConfig`) |
| **Input sheet** | the *measured* value of each input at each test step | a sibling of the output datasheet; user maps it; see §5 |
| **Output sheet** (today's datasheet) | the derived parameter's `Expected` and `Actual` | unchanged |
| **Derived-parameter map** | which inputs feed each derived parameter, the transfer function, and any breakpoints | location **`[Q2]`** — see §6 |
| **Tolerance library** | each input channel's *input* tolerance (via its `signalType` + `moduleType`), and the combine mode (RSS vs worst-case) | reuses the existing library |

The PMF only **flags the analog inputs and gives their nominal value**; the input
channel's ranges, scale, and tolerance come from the same libraries the output-sheet
signals use. The input sheet gives **measured values**; the derived-parameter map
gives **how they combine**. None is sufficient alone, and the input sheet is allowed
to be incomplete (see §7).

---

## 4. PMF importer

Tab-delimited CSV with a header row. One row per I/O channel.

### 4.1 Row filter

Keep rows where `SigType` is **`AI`** or **`AO`**. Drop `DI`, `DO`, `AOI`, and
everything else.

### 4.2 Columns used

**Only these three.** Every other PMF column is ignored — the input channel's ranges,
unit, and scale come from the signals master / signal-type registry (§3), not the PMF.

| PMF column | Use as |
|---|---|
| `SigType` | row filter (AI / AO) |
| `Signal Name` | the channel key — matches the input sheet exactly, and resolves to a `SignalConfig` through the existing alias ladder **`[Q1]`** |
| `init val` | the channel's nominal value — used as the fallback when the input sheet omits this signal (§7) and as the constant for a "known constant" input |

Explicitly **not read**: `Board`, `line`, `EU`, `U/P`, `V/C`, `Min/Max Engineering
Unit`, `min/max measured raw`, `terminal conf`, `customscale` / `ext volt src`,
`ext volt val`.

### 4.3 Output

The importer produces, keyed by `Signal Name`: `{ isAnalogIo: true, nominalValue }`.
Persisted alongside the resolved signal set (e.g.
`%APPDATA%\ToleranceTool\pmf-channels.xml`). It marks which signals are analog input
channels and supplies their nominal value; everything else about the channel is
looked up from the normal signal pipeline.

---

## 5. Input-sheet mapping

Structurally a **variant of `DatasheetMapping`**. It reuses:

- orientation (row-per-case / column-per-case) and the transpose path
- the **System ID** column mapping (user-selected) — its values are the signal
  names, matched to the PMF `Signal Name` and resolved through the alias ladder **`[Q1]`**
- the per-row unit column **`[Q5]`** (assumed identical semantics to the output sheet)
- the repeated-column-group logic for step columns

Differences from the output sheet:

| Aspect | Output sheet | Input sheet |
|---|---|---|
| Per-step columns | `Expected`, `Tolerance`, `Actual`, `P/F` quad | **one value per step** |
| Combo columns | — | optional **(value, value)** pair per step: the **first** value is used; the **second** is the counterpart representation (raw if the first is EU, or EU if the first is raw) |
| Value space | `Expected` is always EU | **per-sheet toggle: values are EU or raw** — user tells the tool |
| Tolerance / pass-fail | yes | none — it only supplies measured input readings |

Step alignment between the two sheets: **`[Q3]`** — assumed output step *N* ↔ input
step *N* by position; matching on step-number labels is the alternative.

The mapping is saved per input sheet, the same way output-sheet mappings are.

---

## 6. Derived-parameter definition

Per derived parameter we need:

```
derivedSignalType           # key, matched like today's signalType/moduleType
inputs:                     # ordered
  - role:      "process"    # a name used in the transfer expression
    signalName: "TT-101"    # -> SignalConfig (range/scale/tol) + PMF init val + input-sheet readings
  - role:      "coldjunction"
    signalName: "TT-101CJ"
transfer:    "process + (coldjunction - 0)"     # expression over the roles
combine:     RSS | WorstCase                     # default per §3
pieces:                     # optional, for piecewise signals
  - upTo:   50.0            # in derived EU
    transfer: "sensorA"
    inputs:   [sensorA]
  - transfer: "sensorB"
    inputs:   [sensorB]
```

- `transfer` is evaluated with each role bound to that input's **EU** value
  (raw → curve → EU per input, exactly like Path B today).
- `pieces` select by the derived `Expected`; the engine flags a warning when
  `Expected` is within roughly one band of a breakpoint, because the true
  uncertainty there straddles both regimes.
- **`[Q2]`** — where this definition lives: an `<Inputs>` / `<Transfer>` section in
  the tolerance library keyed by `derivedSignalType`, a separate file, or a naming
  convention. Assumed: **tolerance library**, as an optional extension of a
  `<Tolerance>` entry.

---

## 7. Missing-input policy

The input sheet will not always carry every input a derived parameter needs.
Resolution order per input, per step:

1. measured value from the input sheet (row matched by `Signal Name`, value at the
   step, interpreted per the sheet's EU/raw toggle and combo rule)
2. else the PMF `init val` for that channel (treated as a constant — zero
   contribution to `Δy`)
3. else the derived row is flagged **"inputs incomplete"** and skipped from
   Apply / Check, the same treatment as an unresolved row today

Default is to flag; `init val` covers the "known constant" inputs (cold-junction
reference, assumed density, …).

---

## 8. Calculation flow (derived parameter, step *N*)

```
output row → System ID → resolve → derived parameter
                                     │
                        derived-parameter map → inputs[] + transfer + pieces
                                     │
   for each input (by signalName):
     SignalConfig          → raw span, EU span, scale, signalType/moduleType
     PMF channel           → init val (nominal)
     input sheet @ step N   → measured raw (or EU); else init val; else FLAG
     input tolerance        → tolerance library (input's signalType/moduleType,
                              applied to its raw span)
     value ± input tol → scale curve → EU low / mid / high   (Path B per channel)
                                     │
   select piece by Expected (if pieces defined)
                                     │
   ∂(transfer)/∂(input_i) by finite difference, each input perturbed by its EU tol
   Δy = combine(  ∂f/∂xi · Δxi  , mode )        # RSS or worst-case
                                     │
   derived tolerance at step N  → rounded per the output sheet's precision policy
                                  × the sheet tolerance multiplier
```

Single-input signals stay on the current code path unchanged; a signal is "derived"
only when the derived-parameter map has an entry for it.

---

## 9. Reporting / UI

- The Datasheet Mapping review gains a way to point at the **input sheet** and the
  **PMF file** (file pickers + the input-sheet column mapping).
- The run report shows, per derived row, each input's contribution to the band so a
  reviewer can see which input dominates.
- The Tolerance Editor preview gains a "derived" mode that lets the engineer enter
  sample input values and see the propagated band, including the per-input split.

---

## 10. Suggested phasing

1. **PMF importer** → `{ Signal Name → (isAnalogIo, init val) }` (§4). Standalone,
   testable, no engine changes.
2. **Input-sheet mapping** (§5) as a `DatasheetMapping` variant + persistence.
3. **Derived-parameter map** schema (§6) + loader + validation.
4. **Engine**: generic finite-difference propagation through `transfer`, per-input
   Path B, RSS / worst-case combine. Linear + non-linear in one path.
5. **Piecewise** (`pieces`) + breakpoint proximity warning.
6. **UI**: input-sheet / PMF pickers, per-input contribution in the report, derived
   preview mode.

Steps 1–3 are pure import/config and can land behind the existing single-input
behaviour with zero regression risk.

---

## 11. Open questions

- **`[Q1]`** The input sheet's mapped identity column and the PMF `Signal Name` hold
  the same signal name (confirmed). Does the derived-parameter map (§6) reference its
  inputs by that `Signal Name`, or by Universal ID?
- **`[Q2]`** Where does the derived-parameter → {input list, transfer function,
  breakpoints} map live — tolerance library, a separate file, or a naming
  convention?
- **`[Q3]`** Step alignment: output step *N* ↔ input step *N* by position, or by
  matching step-number labels?
- **`[Q5]`** Is the input sheet's per-row unit column the same concept as the output
  sheet's per-row unit column?
- **`[Q6]`** For the combo **(value, value)** pairs — does the per-sheet "EU or raw"
  toggle describe the **first** value (the one used), with the second always being
  the other representation?
- **`[Q7]`** Transfer function vs weights: can the derived-parameter map ever give
  just linear contribution weights instead of a formula (which would let linear
  cases skip expression evaluation entirely)?
- **`[Q8]`** Is the input sheet always in the same workbook as the output sheet, or
  can it be a separate file? Same for the PMF.
- **`[Q9]`** Do input tolerances come purely from the existing tolerance library
  (keyed by each input's `signalType`/`moduleType`), or can the PMF/derived map
  override them per input?
- **`[Q10]`** RSS or worst-case by default, and is that a per-derived-signal choice
  or a global setting?

---

## 12. Sample files needed

A sanitized **PMF + input sheet + output sheet** triplet (even five rows each) to
confirm the column mappings in §4–§5 and the join in §8.
