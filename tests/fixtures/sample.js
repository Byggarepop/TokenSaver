// Top-level line comment that should be stripped.
/*
 * Block comment describing the module.
 * Spans multiple lines.
 */

const greeting = "hello // not-a-comment /* nor this */ world";
const path = 'C:\\Users\\test'; // trailing comment after a string
const template = `line one
line two with ${greeting} interpolated`;

function add(a, b) {
    // inline note
    return a + b; /* trailing block */
}

class Counter {
    constructor() {
        this.n = 0;
    }

    /** doc comment */
    increment() {
        this.n += 1;
        return this.n;
    }
}

const c = new Counter();
add(c.increment(), 41);
