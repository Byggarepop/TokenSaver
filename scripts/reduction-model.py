"""
Estimate the token reduction on different realistic file shapes.

The model: a focused emission keeps:
- The focus method body (full)
- Roughly 30% of the type's other members (those it touches), as signatures
- Usings, namespace, type declaration

It drops everything else: unrelated methods, doc comments on dropped members,
unused fields, etc.
"""

scenarios = [
    # (description, total_lines, focus_method_lines, n_methods_in_class, comment_density)
    ("Small Blazor component (the AuiGrid test)",     90, 25, 6, 0.20),
    ("Medium service class",                          200, 30, 12, 0.25),
    ("Legacy OrderService (typical pain case)",       500, 40, 30, 0.30),
    ("WinForms-era Form ported to Blazor",            900, 35, 50, 0.35),
    ("Generated Astro table accessor",                1200, 20, 80, 0.15),
]

print(f"{'Scenario':<48}  {'Orig':>6}  {'Focus':>6}  {'Saved':>6}")
print("-" * 80)

for desc, total, focus_body, n_methods, comment_density in scenarios:
    # Focused output:
    # - Header overhead: 15 lines (usings + namespace + class decl + braces)
    # - Focus method: focus_body lines
    # - Referenced members as signatures: ~30% of n_methods, 1 line each
    # - Referenced fields/props: ~5 lines
    referenced_methods = max(1, int(n_methods * 0.3))
    focused_lines = 15 + focus_body + referenced_methods + 5

    # Convert lines to chars (rough: 50 chars/line average for code,
    # higher for files with more comments)
    chars_per_line = 45 + (comment_density * 40)
    orig_chars = int(total * chars_per_line)
    focused_chars = int(focused_lines * 45)  # focused output has no comments

    orig_tokens = orig_chars // 4
    focused_tokens = focused_chars // 4
    pct = (1 - focused_tokens / orig_tokens) * 100

    print(f"{desc:<48}  {orig_tokens:>6}  {focused_tokens:>6}  {pct:>5.0f}%")

print()
print("These are estimates; actual results depend on how interconnected the")
print("focus method is with the rest of the class.")
