# Reko decompiler extras

This repository contains tools, demos and other artifacts that are used in 
conjunction with the [Reko decompiler](https://github.com/uxmal/reko/).

## Batch
This tool runs through a directory, invoking Reko on binaries and collecting
all instructions that Reko's disassembler reports as unimplemented. The collected
instructions are then used to generate unit tests that can be used as a starting point for
implementing said unimplemented instructions.