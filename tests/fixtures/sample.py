# Module-level comment to be stripped.



def add(a, b):
    # inline note about x
    return a + b  # end-of-line note


class Counter:
    """Docstring with a # inside that must survive."""

    def __init__(self):
        self.n = 0

    def increment(self):
        self.n += 1
        return self.n


message = "this # is not a comment"
multi = """triple-quoted
spanning lines
with # inside still preserved"""

c = Counter()
add(c.increment(), 41)
