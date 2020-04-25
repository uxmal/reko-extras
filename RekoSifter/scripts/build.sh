#!/bin/bash
target="$1"
make distclean && \
find . -type f -iname "config.cache" -delete
./configure \
	--prefix=$PWD/out \
	--host=x86_64-w64-mingw32 \
	--target=${target} && \
make -j`nproc` all-opcodes && \
echo -ne "" | \
x86_64-w64-mingw32-gcc \
	-x c - \
	-shared -o out/opcodes-${target}.dll \
	-Lopcodes -Lbfd -Llibiberty -Lintl -Lzlib \
	-Wl,--whole-archive -lopcodes -lbfd -Wl,--no-whole-archive -liberty -lintl -lz &&
x86_64-w64-mingw32-strip -s out/opcodes-${target}.dll