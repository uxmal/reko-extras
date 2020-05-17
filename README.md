# Reko decompiler extras

This repository contains tools, demos and other artifacts that are used in 
conjunction with the [Reko decompiler](https://github.com/uxmal/reko/).

## RekoSifter
This tool was inspired by https://github.com/xoreaxeaxeax/sandsifter, which uses a clever algorithm to enumerate the vast x86 instruction space. RekoSifter can be used with any one of the architectures supported by Reko, and is useful to discover unimplemented or illegal instructions. RekoSifter optionally compares its output with other disassemblers (currently objdump and LLVM).

## Batch
This tool runs through a directory, invoking Reko on binaries and collecting all instructions that Reko's disassembler reports as unimplemented. The collected instructions are then used to generate unit tests that can be used as a starting point for implementing said unimplemented instructions.
