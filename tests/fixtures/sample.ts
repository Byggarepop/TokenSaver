// module-level note that should be stripped
/*
 * block describing User
 */

interface User {
    id: number;
    name: string;
}

type Box<T> = { value: T };

function greet(name: string): string {
    // inline note
    return `Hello, ${name}`;
}

class Repository<T> {
    private items: T[] = [];

    add(item: T): void {
        this.items.push(item);
    }

    find(predicate: (item: T) => boolean): T | undefined {
        return this.items.find(predicate);
    }
}

const u: User = { id: 1, name: "Ada" };
const box: Box<User> = { value: u };
greet(box.value.name);
