# MPSL: Multi-Purpose Scripting Language

A dynamically typed, high-level programming language designed for use as a scripting language.

## Example Code
```r
fn @fib n {
    if n <= 1 => n
    else => @fib n - 1! + @fib n - 2
}

fn @get_ordinal num {
    match @mod num, 100 {
        @ = 11 | @ = 12 | @ = 13 => "th"
        else => match @mod num, 10 {
            @ = 1 => "st"
            @ = 2 => "nd"
            @ = 3 => "rd"
            else => "th"
        }
    } -> var suffix

    @"{num}{suffix}"
}

# Calculate 20 numbers in the Fibonacci sequence
each i : @range_to 20 {
    @print @"{@get_ordinal i + 1}: {@fib i}"
}
```

## Running Standalone

Clone this repository and build the source code. Run the executable from the command line and pass in the path to the MPSL file you would like to run.