// top-level line comment
#include <iostream>
#include <string>
#include <vector>

/* block comment describing the class */
#define VERSION "1.0"

class Calculator {
public:
    // constructor comment
    Calculator() : _offset(0) {}

    /* add two values */
    int add(int a, int b) const {
        // inline note about addition
        return a + b + _offset;
    }

    std::string describe() const {
        /* a } inside a string literal below — must not corrupt extraction */
        return "Calculator v" + std::string(VERSION) + " offset=} end";
    }

private:
    int _offset;
};

/*
 * multi-line block comment
 * for main
 */
int main() {
    Calculator calc;
    std::cout << calc.add(1, 2) << std::endl; // trailing comment
    return 0;
}
