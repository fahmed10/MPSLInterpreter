# MPSL: Multi-Purpose Scripting Language

A dynamically typed, high-level programming language designed for use as a scripting language.

## Example Code
```cs
# Take a string and return it reversed
fn @rev_str str {
	0 -> var i
	"" -> var output
	while i < @len_of str {
		@ + str[(@len_of str) - i - 1] -> output
		@ + 1 -> i
	}
	output
}

# Calculate nth number in Fibonacci sequence
fn @fib n {
	if n = 0 | n = 1 {
		n -> break
	}
	(@fib n - 1) + (@fib n - 2)
}
```