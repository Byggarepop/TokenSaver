"""Simulate the TokenReport.DetailedBlock output to verify the visual."""

def make_bar(ratio: float, width: int = 40) -> str:
    filled = max(0, min(width, round(ratio * width)))
    return "█" * filled + "░" * (width - filled)

def detailed_block(tool: str, before: int, after: int, notes: str | None = None):
    saved = max(0, before - after)
    pct = saved / before * 100 if before else 0
    ratio_after = after / before if before else 0
    out = []
    out.append(f"┌─ {tool}")
    out.append(f"│  Without tool:  {make_bar(1.0)} {before:,} tokens")
    out.append(f"│  With tool:     {make_bar(ratio_after)} {after:,} tokens")
    out.append(f"│  Saved:         {saved:,} tokens ({pct:.0f}%)")
    if notes:
        out.append(f"│  {notes}")
    out.append("└─")
    return "\n".join(out)

def cost_framing(before: int, after: int, price=3.0):
    cb = before / 1_000_000 * price
    ca = after / 1_000_000 * price
    saved = cb - ca
    return f"≈ ${cb:.4f} → ${ca:.4f} (saved ${saved:.4f} per call at current rates)"

def one_line(tool: str, before: int, after: int):
    saved = max(0, before - after)
    pct = saved / before * 100 if before else 0
    return f"[{tool}] Tokens without tool: {before:,}  →  with tool: {after:,}  ({pct:.0f}% saved)"

print("=" * 70)
print("  EXAMPLE 1: Roslyn Focused Emitter on a Blazor component")
print("=" * 70)
print()
print(detailed_block(
    "Focused Emitter",
    before=835,
    after=415,
    notes="Focus method: OnInitializedAsync. Other members: signatures only."
))
print()
print(cost_framing(835, 415))
print()
print()

print("=" * 70)
print("  EXAMPLE 2: Prompt Compressor on a verbose user prompt")
print("=" * 70)
print()
print(detailed_block(
    "Prompt Compressor",
    before=239,
    after=122,
    notes="5 filler phrases, 1 path, 1 code block stripped."
))
print()
print()

print("=" * 70)
print("  EXAMPLE 3: Combined — Compressor + Focused Emitter on the same prompt")
print("=" * 70)
print()
print(detailed_block(
    "Combined Pipeline",
    before=1074,        # 239 prompt + 835 attached file
    after=537,          # 122 compressed prompt + 415 focused file
    notes="User prompt: 239→122. Attached file: 835→415."
))
print()
print()

print("=" * 70)
print("  EXAMPLE 4: One-line variants (for log lines, MCP prefixes)")
print("=" * 70)
print()
print(one_line("Focused Emitter", 835, 415))
print(one_line("Prompt Compressor", 239, 122))
print(one_line("Lean Context MCP / FindSymbol", 4200, 380))
print()
print()

print("=" * 70)
print("  EXAMPLE 5: When the tool didn't help much (already-tight input)")
print("=" * 70)
print()
print(detailed_block(
    "Prompt Compressor",
    before=58,
    after=54,
    notes="Already lean — only whitespace normalized."
))
