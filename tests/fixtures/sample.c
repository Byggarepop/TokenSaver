/* top-level block comment */
#include <stdio.h>
#include <stdlib.h>

// line comment about MAX
#define MAX 100
#define GREET(name) printf("Hello, %s\n", name)

/* A struct with a closing brace } inside a string below */
typedef struct {
    int id;
    char label[32];
} Item;

/*
 * multi-line block comment
 * explaining add()
 */
int add(int a, int b) {
    // inline note
    return a + b;
}

int main(void) {
    int result = add(3, 4);
    /* another comment */
    char *fmt = "result = %d }"; /* } in string must not end a block */
    printf(fmt, result);
    return 0;
}
