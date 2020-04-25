#!/bin/bash
for arch in \
	aarch64 alpha arc arm avr \
	bfin m68k msp430 \
	microblaze mips mips64 \
	hppa pdp11 \
	powerpc powerpc64 \
	riscv rl78 sparc \
	sh \
	i386 x86_64 \
	xtensa z80
do
	./build.sh ${arch}-unknown-elf
	if [ ! $? -eq 0 ]; then
		echo "Build for target ${arch}-unknown-elf failed"
		exit 1
	fi
done

for arch in s390 vax; do
	./build.sh ${arch}-unknown-linux
	if [ ! $? -eq 0 ]; then
		echo "Build for target ${arch}-unknown-elf failed"
		exit 1
	fi
done